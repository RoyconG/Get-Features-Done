---
feature: discuss-improvement
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - get-features-done/workflows/discuss-feature.md
  - commands/gfd/discuss-feature.md
autonomous: true
acceptance_criteria:
  - "When no file path argument is provided, a skippable free text prompt appears after the DISCUSSING banner asking for additional context"
  - "When a file path is provided as a second argument to /gfd:discuss-feature, the file is read and the free text prompt is skipped"
  - "Provided context is used to inform gray area analysis (step 5), producing more relevant discussion topics"
  - "Provided context is saved to the Notes section of FEATURE.md under a Source Context heading (raw for short text, summarized for long text — Claude's discretion)"

must_haves:
  truths:
    - "When FILE_PATH is provided, the file is read and SOURCE_CONTEXT is set from its contents; no free-text prompt appears"
    - "When FILE_PATH is absent, the user is offered a skippable prompt; selecting Skip leaves SOURCE_CONTEXT empty"
    - "When SOURCE_CONTEXT is non-empty, Step 6 analysis incorporates it to surface domain-specific gray areas and skip pre-answered ones"
    - "When SOURCE_CONTEXT is non-empty, FEATURE.md Notes includes a ### Source Context heading with raw or summarized text"
    - "When SOURCE_CONTEXT is empty, FEATURE.md Notes does NOT include a ### Source Context heading"
  artifacts:
    - path: "get-features-done/workflows/discuss-feature.md"
      provides: "Updated workflow with Step 5 inserted, Steps renumbered, Step 1 extended, Steps 6 and 10 enhanced, success_criteria updated"
      contains: "## 5. Gather Source Context"
    - path: "commands/gfd/discuss-feature.md"
      provides: "Updated command definition with second argument documented"
      contains: "argument-hint: <feature-slug> [context-file]"
  key_links:
    - from: "Step 1 (workflow)"
      to: "Step 5 (workflow)"
      via: "FILE_PATH variable set in Step 1, read in Step 5"
      pattern: "FILE_PATH"
    - from: "Step 5 (workflow)"
      to: "Step 6 (workflow)"
      via: "SOURCE_CONTEXT variable set in Step 5, referenced in Step 6"
      pattern: "SOURCE_CONTEXT"
    - from: "Step 5 (workflow)"
      to: "Step 10 (workflow)"
      via: "SOURCE_CONTEXT variable persisted to FEATURE.md Notes"
      pattern: "Source Context"
---

<objective>
Enhance the discuss-feature workflow to accept additional context before gray area analysis.

Purpose: Users can provide a ticket, spec, or design doc (via file path argument or free-text paste) that feeds into gray area analysis, producing more targeted discussion topics. Context is persisted to FEATURE.md.

Output:
- Modified `get-features-done/workflows/discuss-feature.md` with new Step 5, renumbered steps, enhanced analysis and FEATURE.md write instructions
- Modified `commands/gfd/discuss-feature.md` with updated argument-hint
</objective>

<context>
@docs/features/discuss-improvement/FEATURE.md
@docs/features/discuss-improvement/RESEARCH.md
@docs/features/PROJECT.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Insert context-gathering step and wire SOURCE_CONTEXT through the workflow</name>
  <files>get-features-done/workflows/discuss-feature.md</files>
  <action>
Make the following targeted edits to `get-features-done/workflows/discuss-feature.md`:

**1. Extend Step 1 (argument parsing) — add FILE_PATH extraction after the slug extraction line:**

In `## 1. Parse and Validate Slug`, after the line:
```
Extract the feature slug from $ARGUMENTS (first positional argument).
```
Add:
```
Extract the optional context file path from $ARGUMENTS (second positional argument, if present).
Set FILE_PATH to the second argument value, or empty string if not provided.
```

**2. Renumber existing Steps 5–12 → Steps 6–13:**

Rename each heading:
- `## 5. Analyze Feature` → `## 6. Analyze Feature`
- `## 6. Present Gray Areas` → `## 7. Present Gray Areas`
- `## 7. Discuss Selected Areas` → `## 8. Discuss Selected Areas`
- `## 8. Synthesize and Confirm` → `## 9. Synthesize and Confirm`
- `## 9. Update FEATURE.md` → `## 10. Update FEATURE.md`
- `## 10. Transition to Discussed` → `## 11. Transition to Discussed`
- `## 11. Commit` → `## 12. Commit`
- `## 12. Done` → `## 13. Done`

**3. Insert new Step 5 — Gather Source Context — between the banner step (Step 4) and the newly renumbered Step 6:**

Insert the entire new step after `## 4. Transition to Discussing`:

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

**4. Enhance Step 6 (Analyze Feature) — add SOURCE_CONTEXT incorporation:**

In `## 6. Analyze Feature`, after the existing line:
```
Analyze the feature description to identify gray areas worth discussing.
```
Add before the `**Read the feature description...` line:
```
**If SOURCE_CONTEXT is not empty:** Also analyze SOURCE_CONTEXT for domain-specific constraints, pre-specified requirements, and terminology from the source material. Use this to:
- Resolve gray areas that SOURCE_CONTEXT already answers (skip those in Step 7)
- Surface context-specific gray areas not apparent from the feature description alone

```

**5. Enhance Step 10 (Update FEATURE.md) — add Source Context heading to Notes write instruction:**

In `## 10. Update FEATURE.md`, replace the `## Notes` bullet point:
```
- `## Notes` populated with:
  - Implementation decisions from discussion
  - Claude's discretion areas
  - Deferred ideas (if any) clearly marked
  - Any specific references or "I want it like X" moments
```
With:
```
- `## Notes` populated with:
  - **Source Context** (if SOURCE_CONTEXT is non-empty): Under `### Source Context` heading. Write raw text if short (roughly under 500 words). Summarize at Claude's discretion if longer. Omit this heading entirely if SOURCE_CONTEXT is empty.
  - Implementation decisions from discussion
  - Claude's discretion areas
  - Deferred ideas (if any) clearly marked
  - Any specific references or "I want it like X" moments
```

**6. Update `<success_criteria>` — add two new checklist items:**

In the `<success_criteria>` block, after:
```
- [ ] Status transitioned to "discussing" before conversation starts
```
Add:
```
- [ ] Source context gathered (file read from FILE_PATH, or free-text prompt offered and either filled or skipped)
- [ ] SOURCE_CONTEXT written to FEATURE.md Notes under ### Source Context heading when non-empty (omitted when empty)
```
  </action>
  <verify>
Run the following checks:

```bash
grep -n "FILE_PATH" get-features-done/workflows/discuss-feature.md
grep -n "SOURCE_CONTEXT" get-features-done/workflows/discuss-feature.md
grep -n "## 5. Gather Source Context" get-features-done/workflows/discuss-feature.md
grep -n "## 6. Analyze Feature" get-features-done/workflows/discuss-feature.md
grep -n "## 13. Done" get-features-done/workflows/discuss-feature.md
grep -n "Source Context" get-features-done/workflows/discuss-feature.md
```

All six patterns must have matches. Confirm step numbering runs 1–13 with no gaps.
  </verify>
  <done>
- Step 1 sets FILE_PATH from second argument
- New `## 5. Gather Source Context` exists with all three branches (file path, free-text prompt, empty fallback)
- Step 6 references SOURCE_CONTEXT for enhanced analysis
- Step 10 Notes bullet includes conditional `### Source Context` heading with summarization discretion note
- Steps are numbered 1–13 (no gaps, no duplicates)
- success_criteria includes two new Source Context checklist items
  </done>
</task>

<task type="auto">
  <name>Task 2: Update command definition argument-hint</name>
  <files>commands/gfd/discuss-feature.md</files>
  <action>
In `commands/gfd/discuss-feature.md`, change the `argument-hint` frontmatter field from:
```
argument-hint: <feature-slug>
```
To:
```
argument-hint: <feature-slug> [context-file]
```

No other changes to this file.
  </action>
  <verify>
```bash
grep "argument-hint" commands/gfd/discuss-feature.md
```
Output must be: `argument-hint: <feature-slug> [context-file]`
  </verify>
  <done>
The command definition `argument-hint` field reflects that a second optional `[context-file]` argument is accepted.
  </done>
</task>

</tasks>

<verification>
After both tasks complete:

1. Workflow file has `FILE_PATH` set in Step 1 and read in Step 5
2. New Step 5 handles three branches: file path provided, free-text prompt, and empty fallback
3. Step 6 conditionally incorporates SOURCE_CONTEXT into gray area analysis
4. Step 10 conditionally writes `### Source Context` to FEATURE.md Notes
5. All steps renumbered 1–13 with no gaps
6. `<success_criteria>` includes Source Context items
7. Command definition argument-hint updated

Manual spot-check: read through Steps 1, 5, 6, 10, and 13 in the modified workflow to confirm narrative coherence.
</verification>

<success_criteria>
- FILE_PATH parsed from second positional argument in Step 1
- Step 5 present: reads file if FILE_PATH set, offers skippable prompt otherwise
- Step 6 incorporates SOURCE_CONTEXT when non-empty
- Step 10 conditionally writes `### Source Context` to FEATURE.md Notes
- Steps numbered 1–13 sequentially
- `commands/gfd/discuss-feature.md` argument-hint shows `<feature-slug> [context-file]`
- All 4 FEATURE.md acceptance criteria addressed
</success_criteria>

<output>
After completion, create `docs/features/discuss-improvement/01-SUMMARY.md` with:
- What was changed (both files, specific edits)
- Key variable names introduced: FILE_PATH, SOURCE_CONTEXT
- Step numbering change: old Steps 5–12 became Steps 6–13; new Step 5 inserted
- Any implementation notes or decisions made during execution
</output>
