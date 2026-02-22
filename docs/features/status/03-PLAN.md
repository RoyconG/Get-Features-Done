---
feature: status
plan: 03
type: execute
wave: 2
depends_on: ["01"]
files_modified:
  - commands/gfd/discuss-feature.md
  - get-features-done/workflows/discuss-feature.md
autonomous: true
acceptance_criteria:
  - "/gfd:discuss-feature exists — deep conversation to refine scope, transitions `new` → `discussing` → `discussed`"
must_haves:
  truths:
    - "/gfd:discuss-feature exists as a command file"
    - "discuss-feature transitions status new → discussing on start"
    - "discuss-feature transitions status discussing → discussed on completion"
    - "discuss-feature collects expanded description, acceptance criteria (3-5), priority, dependencies, and notes"
    - "discuss-feature writes all collected content back to FEATURE.md"
    - "discuss-feature errors clearly if feature is not in 'new' status"
  artifacts:
    - path: "commands/gfd/discuss-feature.md"
      provides: "Thin command file for the discuss-feature command"
      contains: "name: gfd:discuss-feature"
    - path: "get-features-done/workflows/discuss-feature.md"
      provides: "Full discussion workflow with status transitions and FEATURE.md update"
      contains: "feature-update-status"
  key_links:
    - from: "commands/gfd/discuss-feature.md"
      to: "get-features-done/workflows/discuss-feature.md"
      via: "@reference in execution_context"
      pattern: "@.*workflows/discuss-feature\\.md"
    - from: "discuss-feature workflow"
      to: "FEATURE.md"
      via: "Write tool with full updated content"
      pattern: "feature-update-status.*discussing"
---

<objective>
Create the `/gfd:discuss-feature` command and workflow that manages the `new → discussing → discussed` lifecycle arc.

Purpose: After `/gfd:new-feature` creates a minimal feature stub, `/gfd:discuss-feature` runs a structured conversation to expand scope into a full feature definition. This separates "registering" a feature from "defining" it. The workflow transitions status and updates FEATURE.md with all gathered content.

Output: `commands/gfd/discuss-feature.md` and `get-features-done/workflows/discuss-feature.md`.
</objective>

<execution_context>
@$HOME/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/status/FEATURE.md
@docs/features/status/RESEARCH.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create the discuss-feature command file</name>
  <files>commands/gfd/discuss-feature.md</files>
  <action>
Create ./commands/gfd/discuss-feature.md following the GFD command pattern:

```markdown
---
name: gfd:discuss-feature
description: Refine feature scope through conversation
argument-hint: <feature-slug>
allowed-tools: Read, Write, Edit, Bash, Grep, Glob, AskUserQuestion
---

<objective>Deep conversation to refine feature scope. Transitions status from `new` → `discussing` → `discussed` and populates FEATURE.md with acceptance criteria, description, priority, dependencies, and notes.</objective>

<execution_context>
@$HOME/.claude/get-features-done/workflows/discuss-feature.md
@$HOME/.claude/get-features-done/references/ui-brand.md
@$HOME/.claude/get-features-done/references/questioning.md
</execution_context>

<process>Execute the discuss-feature workflow.</process>
```
  </action>
  <verify>
```bash
cat ./commands/gfd/discuss-feature.md
```
File exists, contains `name: gfd:discuss-feature`, and has `@` reference to the discuss-feature workflow in execution_context.
  </verify>
  <done>
commands/gfd/discuss-feature.md exists with correct frontmatter and execution_context reference
  </done>
</task>

<task type="auto">
  <name>Task 2: Create the discuss-feature workflow</name>
  <files>get-features-done/workflows/discuss-feature.md</files>
  <action>
Create ./get-features-done/workflows/discuss-feature.md with the full workflow. The workflow handles: slug validation, init, status guard, status transition to discussing, conversation, FEATURE.md update, status transition to discussed, commit.

Write the following content:

```markdown
<purpose>
Deepen a feature definition through structured conversation. Starts from a minimal FEATURE.md (created by new-feature) and populates it with acceptance criteria, expanded description, priority, dependencies, and notes. Transitions status: new → discussing → discussed.
</purpose>

<required_reading>
Read all files referenced by the invoking prompt's execution_context before starting.

@$HOME/.claude/get-features-done/references/ui-brand.md
@$HOME/.claude/get-features-done/references/questioning.md
</required_reading>

<process>

## 1. Parse and Validate Slug

Extract the feature slug from $ARGUMENTS (first positional argument).

**If no slug provided:**

```
No feature slug provided.

**To fix:** /gfd:discuss-feature <slug>
```

Exit.

## 2. Run Init

```bash
INIT_RAW=$(node $HOME/.claude/get-features-done/bin/gfd-tools.cjs init plan-feature "${SLUG}" --include feature,state)
if [[ "$INIT_RAW" == @file:* ]]; then
  INIT_FILE="${INIT_RAW#@file:}"
  INIT=$(cat "$INIT_FILE")
  rm -f "$INIT_FILE"
else
  INIT="$INIT_RAW"
fi
```

Parse JSON for: `feature_found`, `feature_dir`, `feature_name`, `feature_status`, `feature_content`.

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
- header: "Already Discussed"
- question: "Feature [SLUG] is already discussed. Re-discuss it?"
- options:
  - "Yes — re-discuss" — Run conversation again, overwrite FEATURE.md
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
node $HOME/.claude/get-features-done/bin/gfd-tools.cjs feature-update-status "${SLUG}" "discussing"
```

**Display stage banner:**

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► DISCUSSING [SLUG]
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

Show the current one-liner from FEATURE.md as context.

## 5. Conversation

Use a conversational approach — follow threads, don't interrogate. Show the one-liner from the existing FEATURE.md as context.

**Question 1 — Expanded Description:**

Ask freeform: "Tell me more about [SLUG]. What problem does it solve and why does it matter?"

Wait for response.

**Question 2 — Acceptance Criteria:**

Use AskUserQuestion:
- header: "Done Looks Like"
- question: "How will you know [SLUG] is complete? What can a user do when it works?"
- options:
  - "Walk me through the user experience" — Describe it conversationally
  - "I have specific criteria" — I'll list them
  - "Let me think about this" — I'll describe my goals and you help me derive criteria

Follow up until you have 3-5 concrete, observable behaviors. Each should be independently verifiable.

**Question 3 — Priority:**

Use AskUserQuestion:
- header: "Priority"
- question: "How important is [SLUG] relative to other work?"
- options:
  - "Critical — blocking other work"
  - "High — important for current goals"
  - "Medium — standard priority"
  - "Low — nice to have"

**Question 4 — Dependencies:**

Use AskUserQuestion:
- header: "Dependencies"
- question: "Does [SLUG] depend on other features being done first?"
- options:
  - "No dependencies" — This can be worked on independently
  - "Yes, has dependencies" — I'll name the feature slugs it needs
  - "Not sure yet" — Leave this empty for now

If "Yes, has dependencies": ask for a comma-separated list of feature slugs.

**Question 5 — Notes / Constraints:**

Ask freeform: "Any design decisions, constraints, or context I should capture? (Optional — press enter to skip)"

**Summarize and confirm:**

Present what you captured:

```
## Feature: [SLUG]

**Description:** [2-3 sentence summary from conversation]

**Acceptance Criteria:**
- [ ] [Criterion 1]
- [ ] [Criterion 2]
- [ ] [Criterion 3]

**Priority:** [high/medium/low/critical]
**Depends on:** [slugs or "none"]
**Notes:** [constraints or "none"]
```

Use AskUserQuestion:
- header: "Confirm"
- question: "Does this capture [SLUG] correctly?"
- options:
  - "Looks good — save it" — Proceed
  - "Adjust description" — Let me refine the description
  - "Adjust acceptance criteria" — Let me refine the criteria
  - "Adjust something else" — Let me specify what to change

Loop until "Looks good — save it" selected.

## 6. Update FEATURE.md

Read the current FEATURE.md content. Rewrite it using the Write tool with:
- `status: discussing` in frontmatter (already set — leave it, will update after)
- `priority:` updated to user's selection
- `depends_on:` updated to user's list (or empty array)
- `## Description` updated with expanded 2-3 sentence description
- `## Acceptance Criteria` populated with 3-5 criteria in `- [ ] [criterion]` format
- `## Notes` populated with any design decisions from conversation

Preserve existing frontmatter fields (`name`, `slug`, `owner`, `assignees`, `created`).

Keep the `## Tasks` section as-is (populated during planning).

## 7. Transition to Discussed

```bash
node $HOME/.claude/get-features-done/bin/gfd-tools.cjs feature-update-status "${SLUG}" "discussed"
```

## 8. Update STATE.md

Update `docs/features/STATE.md`:
- Last activity: today's date — "Discussed feature [SLUG]"

## 9. Commit

```bash
node $HOME/.claude/get-features-done/bin/gfd-tools.cjs commit "docs(${SLUG}): discuss feature scope" --files docs/features/${SLUG}/FEATURE.md docs/features/STATE.md
```

## 10. Done

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► [SLUG] DISCUSSED ✓
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Feature: [SLUG]
Status: discussed
Acceptance criteria: [N] defined
```

Present next step:

```
───────────────────────────────────────────────────────────────

## ▶ Next Up

**Research [SLUG]** — investigate implementation approach

`/gfd:research-feature [SLUG]`

<sub>`/clear` first → fresh context window</sub>

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
- [ ] 5 questions asked: description, acceptance criteria, priority, dependencies, notes
- [ ] Confirmation loop until user approves
- [ ] FEATURE.md rewritten with all gathered content
- [ ] Status transitioned to "discussed" after FEATURE.md update
- [ ] STATE.md updated
- [ ] Committed
- [ ] User knows next step is /gfd:research-feature [SLUG]

</success_criteria>
```

Note: When writing the file, all the triple-backtick code blocks within the markdown content must use actual triple backtick characters. The bash block in step 2 must be a properly-formatted fenced code block.
  </action>
  <verify>
```bash
# File exists
ls ./get-features-done/workflows/discuss-feature.md

# Contains status transitions
grep "feature-update-status.*discussing" ./get-features-done/workflows/discuss-feature.md
grep "feature-update-status.*discussed" ./get-features-done/workflows/discuss-feature.md

# Contains FEATURE.md update step
grep -i "write.*FEATURE\|Update FEATURE" ./get-features-done/workflows/discuss-feature.md
```
All three greps should return matches.
  </verify>
  <done>
get-features-done/workflows/discuss-feature.md exists with:
- Status transition to "discussing" on start
- Status transition to "discussed" on completion
- 5-question conversation structure
- FEATURE.md rewrite step
- Commit step
- Next step pointing to /gfd:research-feature
  </done>
</task>

</tasks>

<verification>
```bash
# Both files exist
ls ./commands/gfd/discuss-feature.md
ls ./get-features-done/workflows/discuss-feature.md

# Command points to workflow
grep "workflows/discuss-feature" ./commands/gfd/discuss-feature.md

# Workflow has both status transitions
grep "discussing\|discussed" ./get-features-done/workflows/discuss-feature.md | head -5
```
</verification>

<success_criteria>
- commands/gfd/discuss-feature.md exists and references the workflow
- get-features-done/workflows/discuss-feature.md exists with full workflow
- Workflow transitions new → discussing → discussed
- Workflow updates FEATURE.md with all collected content
- Workflow errors with clear message for wrong-status entry
- Workflow commits and routes to /gfd:research-feature
</success_criteria>

<output>
After completion, create `docs/features/status/03-SUMMARY.md` with what was built and any deviations.
</output>
