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
