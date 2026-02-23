<purpose>
Investigate implementation approach for a feature. Spawns gfd-researcher to produce RESEARCH.md. Transitions status: discussed → researching → researched.
</purpose>

<required_reading>
Read all files referenced by the invoking prompt's execution_context before starting.

@$HOME/.claude/get-features-done/references/ui-brand.md
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
INIT=$($HOME/.claude/get-features-done/bin/gfd-tools init plan-feature "${SLUG}")
```

Extract from key=value output: `feature_found`, `feature_dir`, `feature_name`, `feature_status`, `researcher_model`, `has_research` (grep "^key=" | cut -d= -f2-).

Read feature file separately:
```bash
cat "docs/features/${SLUG}/FEATURE.md"
```

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
$HOME/.claude/get-features-done/bin/gfd-tools feature-update-status "${SLUG}" "researching"
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

Extract content from init output:

```bash
RESEARCHER_MODEL=$(echo "$INIT" | grep "^researcher_model=" | cut -d= -f2-)
FEATURE_CONTENT=$(cat "docs/features/${SLUG}/FEATURE.md")
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
  prompt="First, read $HOME/.claude/agents/gfd-researcher.md for your role and instructions.\n\n" + research_prompt,
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
$HOME/.claude/get-features-done/bin/gfd-tools feature-update-status "${SLUG}" "researched"
```

## 8. Commit

```bash
git add "docs/features/${SLUG}/FEATURE.md" "docs/features/${SLUG}/RESEARCH.md" && git diff --cached --quiet || git commit -m "docs(${SLUG}): research feature"
```

## 9. Token Usage Reporting

After research is complete and committed, append a token usage row to FEATURE.md:

1. Determine the model used for the researcher agent:
   ```bash
   gfd-tools resolve-model gfd-researcher
   ```
   Extract: `grep "^model=" | cut -d= -f2-`

2. Get today's date: `date +%Y-%m-%d`

3. Read the current FEATURE.md content (`docs/features/<slug>/FEATURE.md`).

4. Check if a `## Token Usage` section exists in FEATURE.md:
   - **If it exists:** append a new row to the table. Find the last row of the table and insert after it (before any next `##` section or end of file).
   - **If it does not exist:** append the full section at the end of the file.

5. Row format:
   ```
   | research | <YYYY-MM-DD> | gfd-researcher | <model> | — | — | — |
   ```
   Note: Interactive workflow runs use `—` for token columns because exact token counts are not available from the Task tool return value. For headless auto-research runs, the C# AutoResearchCommand writes actual token counts.

6. New section format (when creating for the first time):
   ```markdown
   ## Token Usage

   | Workflow | Date | Agent Role | Model | Input | Output | Cache Read |
   |----------|------|------------|-------|-------|--------|------------|
   | research | <YYYY-MM-DD> | gfd-researcher | <model> | — | — | — |
   ```

7. Use the Edit tool (preferred) or Write tool to update FEATURE.md with the new row.

8. Commit the FEATURE.md update:
   ```bash
   git add docs/features/<slug>/FEATURE.md
   git commit -m "docs(<slug>): add research token usage"
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
- [ ] Status guard: only proceeds for discussed/researching (with confirm for researched)
- [ ] Status transitioned to "researching" before spawning researcher
- [ ] gfd-researcher spawned with feature content
- [ ] RESEARCH.md written to feature directory by researcher
- [ ] Status transitioned to "researched" after researcher completes
- [ ] Committed
- [ ] User knows next step is /gfd:plan-feature [SLUG]

</success_criteria>
