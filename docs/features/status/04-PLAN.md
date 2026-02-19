---
feature: status
plan: 04
type: execute
wave: 2
depends_on: ["01"]
files_modified:
  - commands/gfd/research-feature.md
  - get-features-done/workflows/research-feature.md
autonomous: true
acceptance_criteria:
  - "/gfd:research-feature exists — investigates implementation approach, transitions `discussed` → `researching` → `researched`"
must_haves:
  truths:
    - "/gfd:research-feature exists as a command file"
    - "research-feature transitions status discussed → researching on start"
    - "research-feature spawns gfd-researcher subagent"
    - "research-feature transitions status researching → researched on completion"
    - "research-feature writes RESEARCH.md to the feature directory"
    - "research-feature errors with a clear message if feature is not in 'discussed' status"
  artifacts:
    - path: "commands/gfd/research-feature.md"
      provides: "Thin command file for the research-feature command"
      contains: "name: gfd:research-feature"
    - path: "get-features-done/workflows/research-feature.md"
      provides: "Research workflow that spawns gfd-researcher and manages status transitions"
      contains: "feature-update-status"
  key_links:
    - from: "commands/gfd/research-feature.md"
      to: "get-features-done/workflows/research-feature.md"
      via: "@reference in execution_context"
      pattern: "@.*workflows/research-feature\\.md"
    - from: "research-feature workflow"
      to: "gfd-researcher agent"
      via: "Task() spawn"
      pattern: "gfd-researcher"
    - from: "research-feature workflow"
      to: "RESEARCH.md"
      via: "researcher agent output"
      pattern: "RESEARCH\\.md"
---

<objective>
Create the `/gfd:research-feature` command and workflow that manages the `discussed → researching → researched` lifecycle arc.

Purpose: After scope is defined via `/gfd:discuss-feature`, the research step investigates implementation approach. This mirrors the research step currently embedded in `plan-feature` but as a standalone command. When `plan-feature` later runs for a feature in `researched` status, it skips research because RESEARCH.md already exists (the existing `has_research` check handles this).

Output: `commands/gfd/research-feature.md` and `get-features-done/workflows/research-feature.md`.
</objective>

<execution_context>
@/home/conroy/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/status/FEATURE.md
@docs/features/status/RESEARCH.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create the research-feature command file</name>
  <files>commands/gfd/research-feature.md</files>
  <action>
Create /var/home/conroy/Projects/GFD/commands/gfd/research-feature.md following the GFD command pattern:

```markdown
---
name: gfd:research-feature
description: Research feature implementation approach
argument-hint: <feature-slug>
allowed-tools: Read, Write, Bash, Grep, Glob
---

<objective>Investigate implementation approach for a feature. Transitions status from `discussed` → `researching` → `researched` and produces RESEARCH.md.</objective>

<execution_context>
@/home/conroy/.claude/get-features-done/workflows/research-feature.md
@/home/conroy/.claude/get-features-done/references/ui-brand.md
</execution_context>

<process>Execute the research-feature workflow.</process>
```
  </action>
  <verify>
```bash
cat /var/home/conroy/Projects/GFD/commands/gfd/research-feature.md
```
File exists, contains `name: gfd:research-feature`, and `@` reference to the research-feature workflow.
  </verify>
  <done>
commands/gfd/research-feature.md exists with correct frontmatter and execution_context reference
  </done>
</task>

<task type="auto">
  <name>Task 2: Create the research-feature workflow</name>
  <files>get-features-done/workflows/research-feature.md</files>
  <action>
Create /var/home/conroy/Projects/GFD/get-features-done/workflows/research-feature.md. This workflow mirrors the research step currently in plan-feature.md (step 6) but as a standalone command. Reuse `init plan-feature` (as documented in RESEARCH.md — it already provides all needed context including researcher_model and feature content).

Write the following content:

```markdown
<purpose>
Investigate implementation approach for a feature. Spawns gfd-researcher to produce RESEARCH.md. Transitions status: discussed → researching → researched.
</purpose>

<required_reading>
Read all files referenced by the invoking prompt's execution_context before starting.

@/home/conroy/.claude/get-features-done/references/ui-brand.md
</required_reading>

<process>

## 1. Parse and Validate Slug

Extract the feature slug from $ARGUMENTS (first positional argument).

**If no slug provided:**

```
No feature slug provided.

**To fix:** /gfd:research-feature <slug>
```

Exit.

## 2. Run Init

```bash
INIT_RAW=$(node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs init plan-feature "${SLUG}" --include feature,state,requirements)
if [[ "$INIT_RAW" == @file:* ]]; then
  INIT_FILE="${INIT_RAW#@file:}"
  INIT=$(cat "$INIT_FILE")
  rm -f "$INIT_FILE"
else
  INIT="$INIT_RAW"
fi
```

Parse JSON for: `feature_found`, `feature_dir`, `feature_name`, `feature_status`, `feature_content`, `researcher_model`, `requirements_content`, `has_research`.

**If `feature_found` is false:**

```
Feature not found: [SLUG]

**To fix:** Run /gfd:new-feature [SLUG] to create it first.
         Then run /gfd:discuss-feature [SLUG] to define scope.
```

Exit.

## 3. Validate Status

Check `feature_status` from init JSON.

**If status is `discussed` or `researching`:** Proceed (researching allows re-entry after interruption).

**If status is `new` or `discussing`:**

```
Feature [SLUG] is not ready for research (status: [STATUS]).

Run /gfd:discuss-feature [SLUG] first to define scope and acceptance criteria.
```

Exit.

**If status is `researched`:**

**If `has_research` is true:** Use AskUserQuestion:
- header: "Already Researched"
- question: "Feature [SLUG] already has a RESEARCH.md. Re-research it?"
- options:
  - "Yes — re-research" — Overwrite existing RESEARCH.md
  - "Cancel" — Keep current research

If "Cancel": Exit.

**If status is `planning`, `planned`, `in-progress`, or `done`:**

```
Feature [SLUG] is already past the research phase (status: [STATUS]).

Research is only needed before planning. If you want to re-research, run /gfd:plan-feature [SLUG] --research.
```

Exit.

## 4. Transition to Researching

```bash
node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs feature-update-status "${SLUG}" "researching"
```

**Display stage banner:**

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► RESEARCHING [SLUG]
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

◆ Spawning researcher...
```

## 5. Load Codebase Context

```bash
CODEBASE_DOCS=""
if [ -d "docs/features/codebase" ]; then
  CODEBASE_DOCS=$(ls docs/features/codebase/*.md 2>/dev/null | head -5)
fi
```

## 6. Spawn gfd-researcher

Extract content from init JSON:

```bash
FEATURE_CONTENT=$(echo "$INIT" | jq -r '.feature_content // empty')
REQUIREMENTS_CONTENT=$(echo "$INIT" | jq -r '.requirements_content // empty')
RESEARCHER_MODEL=$(echo "$INIT" | jq -r '.researcher_model // "sonnet"')
```

Research prompt:

```markdown
<objective>
Research how to implement feature: [feature_name] ([SLUG])
Answer: "What do I need to know to PLAN this feature well?"
</objective>

<feature_context>
**Slug:** [SLUG]
**Name:** [feature_name]

**Feature Definition:**
[feature_content — full FEATURE.md]
</feature_context>

<project_context>
**Requirements:** [requirements_content]
**Codebase docs:** [codebase_docs — or "No codebase map available"]
</project_context>

<downstream_consumer>
Your RESEARCH.md feeds into the gfd-planner. Be prescriptive:
- Specific approaches with rationale
- What NOT to do and why
- Patterns to follow from existing codebase
- Dependencies and integration points
- Potential gotchas or edge cases
</downstream_consumer>

<output>
Write to: [feature_dir]/RESEARCH.md
Return ## RESEARCH COMPLETE with brief summary when done.
</output>
```

```
Task(
  prompt="First, read /home/conroy/.claude/agents/gfd-researcher.md for your role and instructions.\n\n" + research_prompt,
  subagent_type="general-purpose",
  model="{researcher_model}",
  description="Research feature [SLUG]"
)
```

**Handle researcher return:**

- **`## RESEARCH COMPLETE`:** Display confirmation, continue to Step 7.
- **`## RESEARCH BLOCKED`:** Display blocker, offer: 1) Provide context and retry, 2) Abort.

## 7. Transition to Researched

```bash
node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs feature-update-status "${SLUG}" "researched"
```

## 8. Update STATE.md

Update `docs/features/STATE.md`:
- Last activity: today's date — "Researched feature [SLUG]"

## 9. Commit

```bash
node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs commit "docs(${SLUG}): research feature" --files docs/features/${SLUG}/FEATURE.md docs/features/${SLUG}/RESEARCH.md docs/features/STATE.md
```

## 10. Done

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► [SLUG] RESEARCHED ✓
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Feature: [SLUG]
Status: researched
Research: docs/features/[SLUG]/RESEARCH.md
```

Present next step:

```
───────────────────────────────────────────────────────────────

## ▶ Next Up

**Plan [SLUG]** — create implementation plans

`/gfd:plan-feature [SLUG]`

<sub>`/clear` first → fresh context window</sub>

───────────────────────────────────────────────────────────────

**Also available:**
- `/gfd:status` — see all features and their status

───────────────────────────────────────────────────────────────
```

</process>

<success_criteria>

- [ ] Slug validated and feature found
- [ ] Status guard: only proceeds for discussed/researching (with confirm for researched)
- [ ] Status transitioned to "researching" before spawning researcher
- [ ] gfd-researcher spawned with feature content and requirements
- [ ] RESEARCH.md written to feature directory by researcher
- [ ] Status transitioned to "researched" after researcher completes
- [ ] STATE.md updated
- [ ] Committed
- [ ] User knows next step is /gfd:plan-feature [SLUG]

</success_criteria>
```

Note: All triple-backtick code blocks in the written content must use actual triple backtick characters.
  </action>
  <verify>
```bash
# File exists
ls /var/home/conroy/Projects/GFD/get-features-done/workflows/research-feature.md

# Contains both status transitions
grep "feature-update-status.*researching" /var/home/conroy/Projects/GFD/get-features-done/workflows/research-feature.md
grep "feature-update-status.*researched" /var/home/conroy/Projects/GFD/get-features-done/workflows/research-feature.md

# References gfd-researcher
grep "gfd-researcher" /var/home/conroy/Projects/GFD/get-features-done/workflows/research-feature.md
```
All three greps should return matches.
  </verify>
  <done>
get-features-done/workflows/research-feature.md exists with:
- Status transition to "researching" on start
- gfd-researcher spawn step
- Status transition to "researched" on completion
- Commit step
- Next step pointing to /gfd:plan-feature
  </done>
</task>

</tasks>

<verification>
```bash
# Both files exist
ls /var/home/conroy/Projects/GFD/commands/gfd/research-feature.md
ls /var/home/conroy/Projects/GFD/get-features-done/workflows/research-feature.md

# Command points to workflow
grep "workflows/research-feature" /var/home/conroy/Projects/GFD/commands/gfd/research-feature.md

# Workflow has both status transitions
grep "researching\|researched" /var/home/conroy/Projects/GFD/get-features-done/workflows/research-feature.md | head -5
```
</verification>

<success_criteria>
- commands/gfd/research-feature.md exists and references the workflow
- get-features-done/workflows/research-feature.md exists with full workflow
- Workflow transitions discussed → researching → researched
- Workflow spawns gfd-researcher subagent
- Workflow writes RESEARCH.md via researcher
- Workflow errors with clear message for wrong-status entry
- Workflow commits and routes to /gfd:plan-feature
</success_criteria>

<output>
After completion, create `docs/features/status/04-SUMMARY.md` with what was built and any deviations.
</output>
