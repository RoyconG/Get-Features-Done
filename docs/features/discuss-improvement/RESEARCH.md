# Feature: Discuss Improvement — Research

**Researched:** 2026-02-22
**Domain:** Claude Code workflow modification — slash command argument parsing, file reading, and FEATURE.md mutation
**Confidence:** HIGH

## Summary

This feature modifies a single workflow file (`get-features-done/workflows/discuss-feature.md`) and its corresponding command definition (`commands/gfd/discuss-feature.md`). There are no new libraries, no new services, and no new data formats involved. The change is purely additive: insert a context-gathering step between the existing Step 4 (banner display) and Step 5 (gray area analysis), then thread the captured context through to Step 5 and Step 9 (FEATURE.md write).

The two input modes (free-text prompt vs file path argument) must be handled at Step 1 (argument parsing), not at the context-gathering step itself. The file path case requires reading the file with the Read tool before Step 5 begins. The free-text case uses AskUserQuestion with a "Skip" option so the user is never blocked.

The only persistence requirement is writing the context to `## Notes` under a `### Source Context` heading in FEATURE.md. This happens in the already-existing Step 9 rewrite using the Write tool. No new files, no new gfd-tools commands, and no schema changes are required.

**Primary recommendation:** Modify `discuss-feature.md` (workflow) and `discuss-feature.md` (command) only. Insert one new step (Step 4.5) for context gathering, propagate `SOURCE_CONTEXT` to Steps 5 and 9.

---

## User Constraints (from FEATURE.md)

### Locked Decisions

- **Input method:** Free text prompt (skippable) OR file path as second argument. File path skips the prompt entirely.
- **Placement:** After status transition and banner display (current Step 4), before gray area analysis (current Step 5).
- **Context usage:** Feeds into gray area analysis (Step 5) to tailor discussion topics — not used anywhere else in the conversation.
- **Persistence:** Saved to FEATURE.md Notes section under a `### Source Context` heading.
- **Claude's discretion:** Whether to save raw text or summarize based on length. Do not add a rule; let the workflow instruction say "at Claude's discretion."

### Out of Scope

- No changes to gfd-tools.cjs / GfdTools (the CLI binary).
- No changes to other workflows (plan-feature, research-feature, execute-feature, etc.).
- No new AskUserQuestion for context type or format.
- No structured parsing of the context — treat it as opaque text regardless of source.

---

## Architecture Patterns

### Pattern: Workflow Step Insertion

All GFD workflows follow a numbered step list in a `<process>` block. The correct way to add behavior is to insert a numbered step between existing steps. Do not modify existing step logic to do double duty.

The new step lives at **Step 4.5** (or renumber: insert as Step 5, shift existing Steps 5–12 to Steps 6–13). Renumbering is acceptable — the steps are documentation, not code that references step numbers programmatically.

**Step sequence after change:**

```
Step 1. Parse and Validate Slug          (existing — ADD second argument parsing here)
Step 2. Run Init                         (existing — unchanged)
Step 3. Validate Status                  (existing — unchanged)
Step 4. Transition to Discussing         (existing — unchanged, banner displayed here)
Step 5. [NEW] Gather Source Context      (new step — prompt or read file)
Step 6. Analyze Feature (was Step 5)     (existing — USE context here)
Step 7. Present Gray Areas (was Step 6)  (existing — unchanged)
Step 8. Discuss Selected Areas (was Step 7) (existing — unchanged)
Step 9. Synthesize and Confirm (was Step 8) (existing — unchanged)
Step 10. Update FEATURE.md (was Step 9)  (existing — WRITE context here)
Step 11. Transition to Discussed (was Step 10) (existing — unchanged)
Step 12. Commit (was Step 11)            (existing — unchanged)
Step 13. Done (was Step 12)              (existing — unchanged)
```

### Pattern: $ARGUMENTS Parsing

All workflows extract the first positional argument as the slug:

```
Extract the feature slug from $ARGUMENTS (first positional argument).
```

To add a second argument, extend Step 1 with:

```
Extract the optional file path from $ARGUMENTS (second positional argument, if present).
Set FILE_PATH to the second argument, or empty string if not provided.
```

This is the only place FILE_PATH is set. No external tooling parses $ARGUMENTS — Claude reads the instruction and interprets $ARGUMENTS directly. There is no argv parser to update.

**Confidence:** HIGH — verified by reading all 5 existing workflows that follow this exact pattern.

### Pattern: Conditional AskUserQuestion

The free-text prompt must be skippable. The correct pattern is an AskUserQuestion with a "Skip" option (or equivalent label such as "No additional context"). When the user selects Skip, SOURCE_CONTEXT remains empty and the workflow proceeds.

Existing precedent for skip-style questions:

```
Use AskUserQuestion:
- header: "Re-discuss?"
- question: "Feature [SLUG] is already discussed. Re-discuss it?"
- options:
  - "Yes — re-discuss"
  - "Cancel"
```

The new step follows the same shape — present a question, handle each option, continue. The key distinction: if FILE_PATH was provided, skip AskUserQuestion entirely and go straight to reading the file.

**Confidence:** HIGH — AskUserQuestion is the standard for all user decisions in workflows.

### Pattern: File Reading in Workflows

Workflows read files using the Read tool or via Bash `cat`. The existing workflow already reads FEATURE.md via Bash:

```bash
cat "docs/features/${SLUG}/FEATURE.md"
```

For reading a user-supplied file path, use the Read tool directly (not Bash cat) — it handles absolute and relative paths, handles errors gracefully, and avoids shell injection risk from unvalidated user input.

**Approach for file read step:**

```
Read the file at FILE_PATH using the Read tool.
If the file does not exist or cannot be read:
  Display an error and fall back to the free-text prompt.
Set SOURCE_CONTEXT to the file contents.
```

**Confidence:** HIGH — Read tool is already in allowed-tools for discuss-feature.md command.

### Pattern: Context Threading

GFD workflows pass context through implicit state (named variables established in one step, referenced in later steps). The workflow file is a prompt, not code — variable names like `SOURCE_CONTEXT`, `SLUG`, `FILE_PATH` are natural-language references.

The correct approach:

- Step 1: Set `FILE_PATH` from second argument (or empty)
- Step 5 (new): Set `SOURCE_CONTEXT` from file read or free-text input (or empty if skipped)
- Step 6 (was 5): Reference `SOURCE_CONTEXT` in the analysis instruction
- Step 10 (was 9): Write `SOURCE_CONTEXT` to FEATURE.md Notes section

No data structure is needed. These are prose instructions to Claude.

**Confidence:** HIGH — this is how every existing GFD workflow threads slug, status, config values, etc.

---

## Implementation Detail: The New Step (Step 5)

The new step must handle three branches:

**Branch A — File path provided:**
```
If FILE_PATH is set:
  Read the file at FILE_PATH using the Read tool.
  If read succeeds: set SOURCE_CONTEXT to the file contents.
  If read fails: display warning, fall back to Branch B (free-text prompt).
  Skip the AskUserQuestion prompt.
```

**Branch B — No file path, prompt user:**
```
If FILE_PATH is not set:
  Use AskUserQuestion:
  - header: "Context"
  - question: "Any additional context for this feature? (tickets, specs, design docs)"
  - options:
    - "Skip — start without context"
    - "I'll paste context below" (triggers follow-up free-text)

  If "Skip": SOURCE_CONTEXT = empty. Proceed.
  If "I'll paste context below": Ask as plain inline question (NOT AskUserQuestion)
    for free text input, then set SOURCE_CONTEXT to the response.
```

**Why two-step for free text:** AskUserQuestion does not natively accept free-form multi-line text as the primary input. The standard pattern for collecting free text in GFD workflows is to ask inline (as Claude would in conversation), not via AskUserQuestion. The AskUserQuestion is used to present the choice; the actual text collection is a follow-up inline ask. This matches how `new-project.md` collects the project description: `Ask inline (freeform, NOT AskUserQuestion): "What do you want to build?"`

**Branch C — Context is empty:**
```
If SOURCE_CONTEXT is empty (user skipped or file was empty):
  Proceed directly to Step 6. No mention of context in Step 6 instructions.
```

---

## Implementation Detail: Gray Area Analysis Enhancement (Step 6)

The current Step 5 instruction:

> Analyze the feature description to identify gray areas worth discussing.

The enhanced Step 6 instruction adds:

> If SOURCE_CONTEXT is not empty, incorporate it into your analysis. Use the context to identify domain-specific gray areas, terminology from the source material, and constraints or requirements that were pre-specified. The context may resolve some gray areas before discussion (skip those) or surface new ones specific to the source material.

**What NOT to do:** Do not ask the user to categorize, interpret, or confirm the context. Claude reads it and uses it — no user interaction at this step for context processing.

**Confidence:** HIGH — This is a pure workflow instruction change with no tooling dependency.

---

## Implementation Detail: FEATURE.md Notes Persistence (Step 10)

The current Step 9 instruction writes to `## Notes` with implementation decisions, Claude's discretion areas, and deferred ideas.

Add a `### Source Context` heading to the write instruction:

```
## Notes

### Source Context

[If SOURCE_CONTEXT is not empty: Write raw text if short (under ~500 words).
Summarize at Claude's discretion if long. If SOURCE_CONTEXT is empty: omit this heading entirely.]

### Implementation Decisions
...
```

**Critical:** Only include `### Source Context` if SOURCE_CONTEXT is non-empty. An empty heading would create clutter in FEATURE.md. The workflow instruction must say "omit this heading if no context was provided."

**Confidence:** HIGH — Step 9 already rewrites the entire FEATURE.md using Write tool. The heading addition is purely additive to the write instruction.

---

## Command Definition Change

The command definition at `commands/gfd/discuss-feature.md` needs one update: the `argument-hint` field must reflect the optional second argument.

**Current:**
```yaml
argument-hint: <feature-slug>
```

**Updated:**
```yaml
argument-hint: <feature-slug> [context-file]
```

No other changes to the command definition. The allowed-tools already includes Read (for reading context files).

**Confidence:** HIGH — verified by reading the existing command definition.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead |
|---------|-------------|-------------|
| File existence check | Shell `test -f` or stat | Read tool — it returns an error on missing files |
| Argument count detection | Custom $ARGUMENTS parser | Natural language in workflow: "second positional argument, if present" |
| Text length measurement | Word/character count logic | Claude's judgment per the locked decision |
| Context type detection | File extension sniffing | Treat all context as opaque text regardless of source |

---

## Common Pitfalls

### Pitfall 1: AskUserQuestion for Free-Text Collection

**What goes wrong:** Using AskUserQuestion with an "Other" option and expecting the user to type multi-line context there. AskUserQuestion is for choices, not document-length text input.

**How to avoid:** Use AskUserQuestion only to offer the choice ("paste context" vs "skip"). Follow up with an inline conversational ask for the actual text.

**Warning signs:** If the workflow instruction says `AskUserQuestion` with an expectation of receiving a ticket body, spec, or document as the answer — that's wrong.

### Pitfall 2: FILE_PATH Validation via Bash

**What goes wrong:** Using `bash -c "test -f '$FILE_PATH'"` for file existence check introduces shell injection risk if FILE_PATH contains special characters (a real risk for user-supplied paths).

**How to avoid:** Use the Read tool directly. It handles missing files with a clear error message. Catch the error in the workflow branch and fall back gracefully.

### Pitfall 3: Forcing Context Into Every Subsequent Step

**What goes wrong:** Mentioning SOURCE_CONTEXT in Steps 7, 8, 9 (discussion loop, synthesis) where it isn't needed. This bloats instructions and can cause Claude to over-reference the external document during conversation.

**How to avoid:** SOURCE_CONTEXT is used in exactly two places: Step 6 (analysis) and Step 10 (FEATURE.md write). It is background context for analysis, not a running reference during the discussion conversation.

### Pitfall 4: Empty Source Context Heading in FEATURE.md

**What goes wrong:** Workflow always writes `### Source Context` with empty body when no context was provided. Results in orphaned headings in FEATURE.md.

**How to avoid:** The write instruction must explicitly state: "Include `### Source Context` heading only if SOURCE_CONTEXT is non-empty. Omit it entirely if no context was provided."

### Pitfall 5: Renaming Without Updating success_criteria

**What goes wrong:** Steps are renumbered in `<process>` but the `<success_criteria>` section at the bottom still refers to old step labels like "Step 5" or "gray areas".

**How to avoid:** After inserting the new step, review the `<success_criteria>` block. Add a new criterion for the context step:
- `[ ] Source context gathered (file read or free-text prompt offered)`
- `[ ] SOURCE_CONTEXT written to FEATURE.md Notes under Source Context heading (or omitted if empty)`

---

## Files to Modify

| File | Change |
|------|--------|
| `get-features-done/workflows/discuss-feature.md` | Insert Step 5, modify Step 6 (was 5), modify Step 10 (was 9), renumber Steps 5–12, update `<success_criteria>` |
| `commands/gfd/discuss-feature.md` | Update `argument-hint` to `<feature-slug> [context-file]` |

No other files need modification.

---

## Code Examples

### Step 1 extension (argument parsing)

```markdown
## 1. Parse and Validate Slug

Extract the feature slug from $ARGUMENTS (first positional argument).
Extract the optional context file path from $ARGUMENTS (second positional argument, if present).
Set FILE_PATH to the second argument value, or empty string if not provided.

**If no slug provided:** [existing error block]
```

### New Step 5 (context gathering)

```markdown
## 5. Gather Source Context

**If FILE_PATH is set:**

Read the file at FILE_PATH using the Read tool.

If the file is read successfully: set SOURCE_CONTEXT to the file contents. Proceed to Step 6.

If the file cannot be read (does not exist, permission denied, etc.): display a warning:
```
⚠ Could not read context file: [FILE_PATH]
```
Then fall through to the free-text prompt below.

**If FILE_PATH is not set (or file read failed):**

Use AskUserQuestion:
- header: "Context"
- question: "Do you have additional context to share? (ticket, spec, design doc, requirements)"
- options:
  - "Skip — discuss without context"
  - "Yes — I'll paste it now"

If "Skip — discuss without context": set SOURCE_CONTEXT to empty string. Proceed to Step 6.

If "Yes — I'll paste it now": ask inline (NOT AskUserQuestion):
  "Paste your context below (ticket body, spec excerpt, requirements doc, etc.):"
  Set SOURCE_CONTEXT to whatever the user provides. Proceed to Step 6.

**If SOURCE_CONTEXT is empty:** Proceed to Step 6 without any context reference.
```

### Step 6 enhancement (analysis with context)

```markdown
## 6. Analyze Feature

Analyze the feature description to identify gray areas worth discussing.

**If SOURCE_CONTEXT is not empty:** Also analyze SOURCE_CONTEXT for domain-specific constraints, pre-specified requirements, or terminology from the source material. Use this to:
- Resolve gray areas that SOURCE_CONTEXT already answers (skip those in Step 7)
- Surface context-specific gray areas not apparent from the feature description alone

[... rest of existing Step 5 instruction unchanged ...]
```

### Step 10 Notes section write instruction

```markdown
- `## Notes` populated with:
  - **Source Context** (if SOURCE_CONTEXT is non-empty): Under `### Source Context` heading.
    Write raw text if short (roughly under 500 words). Summarize at Claude's discretion if longer.
    Omit this heading entirely if SOURCE_CONTEXT is empty.
  - Implementation decisions from discussion
  - Claude's discretion areas
  - Deferred ideas (if any) clearly marked
  - Any specific references or "I want it like X" moments
```

---

## Open Questions

1. **Free-text input length limit**
   - What we know: Claude receives user input as conversation text; no hard limit enforced by GFD tooling.
   - What's unclear: If a user pastes a very large document (10,000+ words) inline, it may consume significant context window before discussion even begins.
   - Recommendation: Workflow instruction can note "very long context will be summarized before use" — this gives Claude permission to compress if needed. No hard limit rule required.

2. **Re-discuss behavior with existing Source Context**
   - What we know: Re-discuss re-runs the workflow from Step 3.
   - What's unclear: If a context file was previously saved to FEATURE.md Notes, should re-discuss offer to reuse it?
   - Recommendation: Out of scope for this feature. Re-discuss clears and rebuilds Notes the same way it does today. The Source Context heading will simply be replaced or omitted based on whatever context (if any) the user provides in the re-discussion run.

---

## Sources

### Primary (HIGH confidence)

- `/var/home/conroy/Projects/GFD/get-features-done/workflows/discuss-feature.md` — Full workflow, all 12 steps examined
- `/var/home/conroy/Projects/GFD/commands/gfd/discuss-feature.md` — Command definition, argument-hint field
- `/var/home/conroy/Projects/GFD/docs/features/codebase/ARCHITECTURE.md` — Workflow layer description
- `/var/home/conroy/Projects/GFD/docs/features/codebase/CONVENTIONS.md` — Naming and coding patterns
- `/var/home/conroy/Projects/GFD/get-features-done/templates/feature.md` — FEATURE.md Notes section schema
- `/var/home/conroy/Projects/GFD/get-features-done/references/ui-brand.md` — AskUserQuestion usage patterns
- `/var/home/conroy/Projects/GFD/get-features-done/workflows/new-project.md` — Free-text collection pattern (inline ask vs AskUserQuestion)

### Secondary (MEDIUM confidence)

- Other workflow files (plan-feature, execute-feature, research-feature, new-feature) — cross-verified $ARGUMENTS parsing pattern

---

## Metadata

**Confidence breakdown:**
- Files to modify: HIGH — directly read the source files
- Step insertion approach: HIGH — pattern matches all existing workflow modifications
- AskUserQuestion for free-text: HIGH — verified against new-project.md pattern
- File path parsing: HIGH — $ARGUMENTS pattern used consistently across 5 workflows
- FEATURE.md Notes write: HIGH — Step 9 already rewrites entire file; heading addition is additive

**Research date:** 2026-02-22
**Valid until:** 2026-03-22 (stable — GFD workflow format changes rarely)
