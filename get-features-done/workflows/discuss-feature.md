<purpose>
Extract implementation decisions that downstream agents need. Analyze the feature to identify gray areas, let the user choose what to discuss, then deep-dive each selected area until satisfied.

You are a thinking partner, not an interviewer. The user is the visionary — you are the builder. Your job is to capture decisions that will guide research and planning, not to figure out implementation yourself.

Transitions status: new → discussing → discussed.
</purpose>

<downstream_awareness>
**FEATURE.md feeds into:**

1. **gfd-researcher** — Reads FEATURE.md to know WHAT to research
   - "User wants System.CommandLine" → researcher investigates that library
   - "JSON output only" → researcher looks into serialization patterns

2. **gfd-planner** — Reads FEATURE.md to know WHAT decisions are locked
   - "Only port active commands" → planner scopes task list accordingly
   - "Claude's Discretion: error format" → planner can decide approach

**Your job:** Capture decisions clearly enough that downstream agents can act on them without asking the user again.

**Not your job:** Figure out HOW to implement. That's what research and planning do with the decisions you capture.
</downstream_awareness>

<philosophy>
**User = founder/visionary. Claude = builder.**

The user knows:
- How they imagine it working
- What it should look/feel like
- What's essential vs nice-to-have
- Specific behaviors or references they have in mind

The user doesn't know (and shouldn't be asked):
- Codebase patterns (researcher reads the code)
- Technical risks (researcher identifies these)
- Implementation approach (planner figures this out)
- Success metrics (inferred from the work)

Ask about vision and implementation choices. Capture decisions for downstream agents.
</philosophy>

<scope_guardrail>
**CRITICAL: No scope creep.**

The feature boundary comes from FEATURE.md and is FIXED. Discussion clarifies HOW to implement what's scoped, never WHETHER to add new capabilities.

**Allowed (clarifying ambiguity):**
- "How should the output be formatted?" (behavior choice)
- "What happens on error?" (within the feature)
- "Should it support both modes?" (variant of existing scope)

**Not allowed (scope creep):**
- "Should we also add monitoring?" (new capability)
- "What about a web UI for this?" (new capability)
- "Maybe include caching?" (new capability)

**The heuristic:** Does this clarify how we implement what's already in the feature, or does it add a new capability that could be its own feature?

**When user suggests scope creep:**
```
"[Capability X] would be a new feature — that's its own `/gfd:new-feature`.
Want me to note it for later?

For now, let's focus on [feature domain]."
```

Capture the idea in a "Deferred Ideas" section of Notes. Don't lose it, don't act on it.
</scope_guardrail>

<required_reading>
Read all files referenced by the invoking prompt's execution_context before starting.

@$HOME/.claude/get-features-done/references/ui-brand.md
@$HOME/.claude/get-features-done/references/questioning.md
</required_reading>

<process>

## 1. Parse and Validate Slug

Extract the feature slug from $ARGUMENTS (first positional argument).

Extract the optional context file path from $ARGUMENTS (second positional argument, if present).
Set FILE_PATH to the second argument value, or empty string if not provided.

**If no slug provided:**

```
No feature slug provided.

**To fix:** /gfd:discuss-feature <slug>
```

Exit.

## 2. Run Init

```bash
INIT=$($HOME/.claude/get-features-done/bin/gfd-tools init plan-feature "${SLUG}")
```

Extract from key=value output: `feature_found`, `feature_dir`, `feature_name`, `feature_status` (grep "^key=" | cut -d= -f2-).

Read feature file separately:
```bash
cat "docs/features/${SLUG}/FEATURE.md"
```

**If `feature_found` is false:**

```
Feature not found: [SLUG]

**To fix:** Run /gfd:new-feature [SLUG] to create it first.
```

Exit.

## 3. Validate Status

Check `feature_status` from init JSON.

**If status is `new` or `discussing`:** Proceed (discussing allows re-entry after interruption).

**If status is `discussed`:**

Use AskUserQuestion:
- header: "Re-discuss?"
- question: "Feature [SLUG] is already discussed. Re-discuss it?"
- options:
  - "Yes — re-discuss" — Run conversation again, update FEATURE.md
  - "Cancel" — Keep current definition

If "Cancel": Exit.

**If status is `researching`, `researched`, `planning`, `planned`, `in-progress`, or `done`:**

```
Feature [SLUG] is already past the discussion phase (status: [STATUS]).

The discuss-feature command is for features in 'new' status.
```

Exit.

## 4. Transition to Discussing

```bash
$HOME/.claude/get-features-done/bin/gfd-tools feature-update-status "${SLUG}" "discussing"
```

**Display stage banner:**

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► DISCUSSING [SLUG]
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

Show the current one-liner from FEATURE.md as context.

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

## 6. Analyze Feature

Analyze the feature description to identify gray areas worth discussing.

**If SOURCE_CONTEXT is not empty:** Also analyze SOURCE_CONTEXT for domain-specific constraints, pre-specified requirements, and terminology from the source material. Use this to:
- Resolve gray areas that SOURCE_CONTEXT already answers (skip those in Step 7)
- Surface context-specific gray areas not apparent from the feature description alone



**Read the feature description from FEATURE.md and determine:**

1. **Domain boundary** — What capability is this feature delivering? State it clearly.

2. **Gray areas** — For each relevant category, identify 1-2 specific ambiguities that would change implementation.

3. **Skip assessment** — If no meaningful gray areas exist (pure infrastructure, clear-cut implementation), the feature may not need discussion.

**Generate phase-specific gray areas — not generic categories.** Examples:

```
Feature: "C# Rewrite"
→ Command parity, Project structure, Build/publish approach, Error handling contract

Feature: "User authentication"
→ Session handling, Error responses, Multi-device policy, Recovery flow

Feature: "CLI for database backups"
→ Output format, Flag design, Progress reporting, Error recovery

Feature: "API documentation"
→ Structure/navigation, Code examples depth, Versioning approach, Interactive elements
```

**The key question:** What decisions would change the outcome that the user should weigh in on?

**Claude handles these (don't ask):**
- Technical implementation details
- Architecture patterns
- Performance optimization

## 7. Present Gray Areas

Present the domain boundary and gray areas to user.

**First, state the boundary:**
```
Feature: [SLUG] — [Name]
Domain: [What this feature delivers — from your analysis]

We'll clarify HOW to implement this.
(New capabilities belong in other features.)
```

**Then use AskUserQuestion (multiSelect: true):**
- header: "Discuss"
- question: "Which areas do you want to discuss for [feature name]?"
- options: Generate 3-4 feature-specific gray areas, each formatted as:
  - "[Specific area]" (label) — concrete, not generic
  - [1-2 questions this covers] (description)

**Do NOT include a "skip" or "you decide" option.** User ran this command to discuss — give them real choices.

**Examples by domain:**

For "C# Rewrite" (tooling rewrite):
```
☐ Command parity — Which commands are active? Port all or subset?
☐ Project structure — Single project or split? Where does it live?
☐ Build approach — Published binary or dotnet run? CI integration?
☐ Error contract — Same JSON error format or improve it?
```

For "Post Feed" (visual feature):
```
☐ Layout style — Cards vs list vs timeline? Information density?
☐ Loading behavior — Infinite scroll or pagination? Pull to refresh?
☐ Content ordering — Chronological, algorithmic, or user choice?
☐ Post metadata — What info per post? Timestamps, reactions, author?
```

Continue to discuss_areas with selected areas.

## 8. Discuss Selected Areas

For each selected area, conduct a focused discussion loop.

**Philosophy: 4 questions, then check.**

Ask up to 4 questions per area before offering to continue or move on. Each answer often reveals the next question.

**For each area:**

1. **Announce the area:**
   ```
   Let's talk about [Area].
   ```

2. **Ask up to 4 questions using AskUserQuestion:**
   - header: "[Area]" (max 12 chars — abbreviate if needed)
   - question: Specific decision for this area
   - options: 2-3 concrete choices (AskUserQuestion adds "Other" automatically)
   - Include "You decide" as an option when reasonable — captures Claude discretion

3. **After 4 questions (or when area is covered), check:**
   - header: "[Area]" (max 12 chars)
   - question: "More questions about [area], or move to next?"
   - options: "More questions" / "Next area"

   If "More questions" → ask up to 4 more, then check again
   If "Next area" → proceed to next selected area
   If "Other" (free text) → interpret intent: continuation phrases ("chat more", "keep going", "yes", "more") map to "More questions"; advancement phrases ("done", "move on", "next", "skip") map to "Next area". If ambiguous, ask.

4. **After all areas complete, gather remaining metadata:**

   Use AskUserQuestion:
   - header: "Priority"
   - question: "How important is [SLUG] relative to other work?"
   - options:
     - "Critical — blocking other work"
     - "High — important for current goals"
     - "Medium — standard priority"
     - "Low — nice to have"

   Use AskUserQuestion:
   - header: "Dependencies"
   - question: "Does [SLUG] depend on other features being done first?"
   - options:
     - "No dependencies" — This can be worked on independently
     - "Yes, has dependencies" — I'll name the feature slugs it needs
     - "Not sure yet" — Leave this empty for now

   If "Yes, has dependencies": ask for a comma-separated list of feature slugs.

**Question design:**
- Options should be concrete, not abstract ("Cards" not "Option A")
- Each answer should inform the next question
- If user picks "Other", receive their input, reflect it back, confirm

**Scope creep handling:**
If user mentions something outside the feature domain:
```
"[Capability] sounds like a new feature — that belongs in its own `/gfd:new-feature`.
I'll note it as a deferred idea.

Back to [current area]: [return to current question]"
```

Track deferred ideas internally.

## 9. Synthesize and Confirm

Derive acceptance criteria from the discussion (3-5 concrete, observable behaviors). Each should be independently verifiable, written from user/system perspective.

Present what you captured:

```
## Feature: [SLUG]

**Description:** [2-3 sentence summary synthesized from discussion]

**Acceptance Criteria:**
- [ ] [Criterion derived from discussion]
- [ ] [Criterion derived from discussion]
- [ ] [Criterion derived from discussion]

**Implementation Decisions:**
- [Category]: [Decision captured]
- [Category]: [Decision captured]
- Claude's discretion: [Areas where user said "you decide"]

**Priority:** [critical/high/medium/low]
**Depends on:** [slugs or "none"]
**Deferred ideas:** [ideas that came up but belong in other features, or "none"]
```

Use AskUserQuestion:
- header: "Confirm"
- question: "Does this capture [SLUG] correctly?"
- options:
  - "Looks good — save it" — Proceed
  - "Adjust something" — Let me specify what to change
  - "Revisit an area" — Go back to a specific discussion area

Loop until "Looks good — save it" selected.

## 10. Update FEATURE.md

Read the current FEATURE.md content. Rewrite it using the Write tool with:
- `status: discussing` in frontmatter (already set — leave it, will update after)
- `priority:` updated to user's selection
- `depends_on:` updated to user's list (or empty array)
- `## Description` updated with expanded 2-3 sentence description
- `## Acceptance Criteria` populated with 3-5 criteria in `- [ ] [criterion]` format
- `## Notes` populated with:
  - **Source Context** (if SOURCE_CONTEXT is non-empty): Under `### Source Context` heading. Write raw text if short (roughly under 500 words). Summarize at Claude's discretion if longer. Omit this heading entirely if SOURCE_CONTEXT is empty.
  - Implementation decisions from discussion
  - Claude's discretion areas
  - Deferred ideas (if any) clearly marked
  - Any specific references or "I want it like X" moments

Preserve existing frontmatter fields (`name`, `slug`, `owner`, `assignees`, `created`).

Keep the `## Tasks` section as-is (populated during planning).
Keep the `## Decisions` and `## Blockers` sections as-is.

## 11. Transition to Discussed

```bash
$HOME/.claude/get-features-done/bin/gfd-tools feature-update-status "${SLUG}" "discussed"
```

## 12. Commit

```bash
git add "docs/features/${SLUG}/FEATURE.md" && git diff --cached --quiet || git commit -m "docs(${SLUG}): discuss feature scope"
```

## 13. Done

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► [SLUG] DISCUSSED ✓
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Feature: [SLUG]
Status: discussed
Acceptance criteria: [N] defined
Decisions captured: [N]
```

Present next step:

```
───────────────────────────────────────────────────────────────

## ▶ Next Up

**Research [SLUG]** — investigate implementation approach

`/gfd:research-feature [SLUG]`

<sub>`/clear` first → fresh context window</sub>
```

**After displaying the primary Next Up command, render the active features status table:**

Call `list-features` to get all features. Filter out done features. If there are 2+ active features, render:

```
| Feature Name | Status | Next Step |
|--------------|--------|-----------|
| **[current-slug-name]** | [status] | [next command] |
| [other-name] | [status] | [next command] |
```

- Current feature (SLUG from this workflow) listed first and **bolded**
- Other active features in default sort order
- Next Step uses the same status→command mapping as /gfd:status
- Skip this table if only 1 or 0 active features remain

```
───────────────────────────────────────────────────────────────

**Also available:**
- `/gfd:status` — see all features and their status

───────────────────────────────────────────────────────────────
```

</process>

<success_criteria>

- [ ] Slug validated and feature found
- [ ] Status guard: only proceeds for new/discussing (with confirm for discussed)
- [ ] Status transitioned to "discussing" before conversation starts
- [ ] Source context gathered (file read from FILE_PATH, or free-text prompt offered and either filled or skipped)
- [ ] SOURCE_CONTEXT written to FEATURE.md Notes under ### Source Context heading when non-empty (omitted when empty)
- [ ] Gray areas identified through intelligent analysis (not generic questions)
- [ ] User selected which areas to discuss
- [ ] Each selected area explored until user satisfied (4 questions then check)
- [ ] Scope creep redirected to deferred ideas
- [ ] Acceptance criteria derived from discussion (not asked as a separate form question)
- [ ] Priority and dependencies gathered
- [ ] Confirmation loop until user approves
- [ ] FEATURE.md rewritten with all gathered content and decisions
- [ ] Status transitioned to "discussed" after FEATURE.md update
- [ ] Committed
- [ ] User knows next step is /gfd:research-feature [SLUG]

</success_criteria>
