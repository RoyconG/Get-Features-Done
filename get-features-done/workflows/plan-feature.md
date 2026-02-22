<purpose>
Create executable plan files (NN-PLAN.md) for a feature with integrated research and verification. Default flow: Research (if enabled and needed) → Plan → Check → Done. Orchestrates gfd-researcher, gfd-planner, and gfd-plan-checker agents with a revision loop (max 3 iterations).
</purpose>

<required_reading>
Read all files referenced by the invoking prompt's execution_context before starting.

@$HOME/.claude/get-features-done/references/ui-brand.md
</required_reading>

<process>

## 1. Initialize

Parse slug from $ARGUMENTS (first positional argument).

**If no slug provided:**

```
╔══════════════════════════════════════════════════════════════╗
║  ERROR                                                       ║
╚══════════════════════════════════════════════════════════════╝

No feature slug provided.

**To fix:** /gfd:plan-feature <slug>
```

Exit.

Load all context in one call:

```bash
INIT=$($HOME/.claude/get-features-done/bin/gfd-tools init plan-feature "${SLUG}")
```

Extract from key=value output:
- `researcher_model`, `planner_model`, `checker_model` (grep "^key=" | cut -d= -f2-)
- `research_enabled`, `plan_checker_enabled`, `feature_found`, `project_exists` (grep "^key=" | cut -d= -f2-)
- `feature_dir`, `feature_slug`, `feature_name`, `feature_status`, `feature_priority` (grep "^key=" | cut -d= -f2-)
- `has_research`, `has_plans`, `plan_count` (grep "^key=" | cut -d= -f2-)

Read feature file separately:
```bash
cat "docs/features/${SLUG}/FEATURE.md"
```

**If `project_exists` is false:** Error — run `/gfd:new-project` first.

**If `feature_found` is false:**

```
╔══════════════════════════════════════════════════════════════╗
║  ERROR                                                       ║
╚══════════════════════════════════════════════════════════════╝

Feature not found: [SLUG]

**To fix:** Run /gfd:new-feature [SLUG] to create it first.
         Or run /gfd:status to see all features.
```

Exit.

## 2. Validate Feature Status

Check `feature_status` from init JSON.

**Valid statuses for planning:** `researched`, `planning` (for re-entry if interrupted)

**If status is `new`, `discussing`, or `discussed`:**
Show error: "Feature [SLUG] needs research before planning. Run /gfd:discuss-feature [SLUG] then /gfd:research-feature [SLUG] first."
Exit.

**If status is `in-progress`:**

Use AskUserQuestion:
- header: "Already Active"
- question: "Feature [SLUG] is in-progress. Re-plan it?"
- options:
  - "Add more plans" — Create additional plans alongside existing ones
  - "Replan from scratch" — Delete existing plans and start over
  - "Cancel" — Keep current plans, don't change anything

If "Cancel": Exit.
If "Replan from scratch": Confirm deletion, then continue.

**If status is `done`:**

```
╔══════════════════════════════════════════════════════════════╗
║  ERROR                                                       ║
╚══════════════════════════════════════════════════════════════╝

Feature [SLUG] is already done.

**To fix:** Run /gfd:status to see remaining features.
```

Exit.

## 3. Check Existing Plans

Use `has_plans` and `plan_count` from init JSON.

**If plans already exist:**

List current plans:

```bash
ls "${feature_dir}"/*-PLAN.md 2>/dev/null
```

Use AskUserQuestion:
- header: "Plans Exist"
- question: "Feature [SLUG] already has [plan_count] plan(s). What do you want to do?"
- options:
  - "Add more plans" — Create additional plans
  - "View existing plans" — Show me what's there before deciding
  - "Replan from scratch" — Delete existing plans and create new ones

If "View existing plans": Display plan filenames and objectives, then re-ask.
If "Replan from scratch": Confirm and delete existing PLAN.md files.

## 4. Update Feature Status to Planning

Update FEATURE.md status field from `researched` → `planning`:

```bash
$HOME/.claude/get-features-done/bin/gfd-tools feature-update-status "${SLUG}" "planning"
```

## 5. Load Codebase Context

Check for existing codebase maps:

```bash
CODEBASE_DOCS=""
if [ -d "docs/features/codebase" ]; then
  CODEBASE_DOCS=$(ls docs/features/codebase/*.md 2>/dev/null | head -5)
fi
```

If codebase docs exist, note relevant ones for the planner based on feature type. Pass codebase context to agents.

## 6. Handle Research

**Skip if:** `--skip-research` flag present, or `research_enabled` is false (from init) without `--research` override.

**If `has_research` is true AND no `--research` flag:** Use existing RESEARCH.md, skip to Step 7.

**If RESEARCH.md missing OR `--research` flag:**

Display banner:
```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► RESEARCHING [SLUG]
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

◆ Spawning researcher...
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
**Description:** [from FEATURE.md description section]
**Priority:** [priority]
**Depends on:** [depends_on]

**Acceptance Criteria:**
[acceptance criteria from FEATURE.md]
</feature_context>

<project_context>
**Codebase docs:** [codebase context if available]
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
- **`## RESEARCH BLOCKED`:** Display blocker, offer: 1) Provide context, 2) Skip research, 3) Abort.

## 7. Load File Contents

Load feature and research content separately (`@` syntax doesn't work across Task() boundaries):

```bash
FEATURE_CONTENT=$(cat "docs/features/${SLUG}/FEATURE.md" 2>/dev/null)

# Load research if it was just written
RESEARCH_CONTENT=""
if [ -f "${feature_dir}/RESEARCH.md" ]; then
  RESEARCH_CONTENT=$(cat "${feature_dir}/RESEARCH.md")
fi
```

## 8. Spawn gfd-planner Agent

**Display banner:**
```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► PLANNING [SLUG]
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

◆ Spawning planner...
```

Planner prompt:

```markdown
<planning_context>
**Feature Slug:** [SLUG]
**Feature Name:** [feature_name]
**Priority:** [priority]
**Depends on:** [depends_on]

**Feature Definition:**
[feature_content — full FEATURE.md]

**Acceptance Criteria (MUST be achievable after all plans complete):**
[acceptance criteria extracted from feature_content]

**Research:** [research_content — or "No research available"]

**Codebase context:** [codebase_docs — or "No codebase map available"]
</planning_context>

<output_location>
Write all PLAN.md files to: [feature_dir]/
File naming: 01-PLAN.md, 02-PLAN.md, etc.

Each plan needs:
- Frontmatter: wave, depends_on, files_modified, autonomous
- Objective section
- Tasks in XML format
- Verification criteria
- must_haves (derived from acceptance criteria)
</output_location>

<downstream_consumer>
Plans are consumed by /gfd:execute-feature.

Plans need:
- Frontmatter (wave, depends_on, files_modified, autonomous)
- Tasks in XML format with clear, actionable steps
- Verification criteria derived from acceptance criteria
- must_haves: observable outcomes that map back to FEATURE.md acceptance criteria
</downstream_consumer>

<quality_gate>
- [ ] PLAN.md files created in feature directory
- [ ] All acceptance criteria from FEATURE.md addressed across plans
- [ ] Each plan has valid frontmatter with wave assignment
- [ ] Tasks are specific and actionable
- [ ] Dependencies correctly identified between plans
- [ ] Waves assigned for parallel execution where possible
- [ ] must_haves derived from acceptance criteria
</quality_gate>

Return ## PLANNING COMPLETE with plan count and wave structure.
```

```
Task(
  prompt="First, read $HOME/.claude/agents/gfd-planner.md for your role and instructions.\n\n" + planner_prompt,
  subagent_type="general-purpose",
  model="{planner_model}",
  description="Plan feature [SLUG]"
)
```

## 9. Handle Planner Return

- **`## PLANNING COMPLETE`:** Display plan count and wave structure. If `--skip-verify` or `plan_checker_enabled` is false (from init): skip to Step 12. Otherwise: Step 10.
- **`## CHECKPOINT REACHED`:** Present to user, get response, spawn continuation.
- **`## PLANNING INCONCLUSIVE`:** Show attempts, offer: Add context / Retry / Manual planning.

## 10. Spawn gfd-plan-checker Agent

**Display banner:**
```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► VERIFYING PLANS
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

◆ Spawning plan checker...
```

```bash
PLANS_CONTENT=$(cat "${feature_dir}"/*-PLAN.md 2>/dev/null)
```

Checker prompt:

```markdown
<verification_context>
**Feature:** [SLUG] — [feature_name]

**Acceptance criteria (ALL must be achievable after plans complete):**
[acceptance criteria from FEATURE.md]

**Plans to verify:**
[plans_content]
</verification_context>

<expected_output>
- ## VERIFICATION PASSED — all acceptance criteria covered, plans are sound
- ## ISSUES FOUND — structured issue list with specific problems
</expected_output>
```

```
Task(
  prompt=checker_prompt,
  subagent_type="gfd-plan-checker",
  model="{checker_model}",
  description="Verify plans for [SLUG]"
)
```

## 11. Handle Checker Return

- **`## VERIFICATION PASSED`:** Display confirmation, proceed to Step 12.
- **`## ISSUES FOUND`:** Display issues, check iteration count, proceed to revision.

**Revision Loop (Max 3 Iterations):**

Track `iteration_count` (starts at 1 after initial plan + check).

**If iteration_count < 3:**

Display: `Sending back to planner for revision... (iteration {N}/3)`

Revision prompt:

```markdown
<revision_context>
**Feature:** [SLUG]
**Mode:** revision

**Existing plans:** [plans_content]
**Checker issues:** [structured issues from checker]

**Acceptance criteria:** [acceptance criteria from FEATURE.md]
</revision_context>

<instructions>
Make targeted updates to address checker issues.
Do NOT replan from scratch unless issues are fundamental.
Return ## PLANNING COMPLETE with what changed.
</instructions>
```

```
Task(
  prompt="First, read $HOME/.claude/agents/gfd-planner.md for your role and instructions.\n\n" + revision_prompt,
  subagent_type="general-purpose",
  model="{planner_model}",
  description="Revise plans for [SLUG]"
)
```

After planner returns → spawn checker again (Step 10), increment iteration_count.

**If iteration_count >= 3:**

Display: `Max iterations reached. {N} issues remain:` + issue list

Offer:
1) Force proceed — accept plans with known issues
2) Provide guidance and retry — I'll give specific direction
3) Abandon — don't create plans yet

## 12. Update FEATURE.md Status to Planned

Update FEATURE.md status field from `planning` → `planned` and populate the Tasks section with links to plan files:

```bash
# Update status
$HOME/.claude/get-features-done/bin/gfd-tools feature-update-status "${SLUG}" "planned"
```

Update the Tasks section in FEATURE.md to list the created plans:

```markdown
## Tasks

- [01-PLAN.md](01-PLAN.md) — [plan objective]
- [02-PLAN.md](02-PLAN.md) — [plan objective]
```

## 13. Commit

```bash
git add "${feature_dir}/FEATURE.md" && git add ${feature_dir}/*-PLAN.md 2>/dev/null; git add "${feature_dir}/RESEARCH.md" 2>/dev/null; git diff --cached --quiet || git commit -m "docs(${SLUG}): create plan"
```

(RESEARCH.md will be a no-op if research was skipped.)

## 14. Present Final Status

Output directly as markdown:

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► [SLUG] PLANNED ✓
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

**[feature_name]** — [N] plan(s) in [M] wave(s)

| Wave | Plans | What it builds |
|------|-------|----------------|
| 1    | 01, 02 | [objectives] |
| 2    | 03     | [objective]  |

Research: {Completed | Used existing | Skipped}
Verification: {Passed | Passed with override | Skipped}
```

```
───────────────────────────────────────────────────────────────

## ▶ Next Up

**Execute [SLUG]** — run all [N] plans

`/gfd:execute-feature [SLUG]`

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
- `cat docs/features/[SLUG]/*-PLAN.md` — review plans
- `/gfd:plan-feature [SLUG] --research` — re-research first

───────────────────────────────────────────────────────────────
```

</process>

<success_criteria>

- [ ] Feature slug validated and exists
- [ ] Feature status allows planning (not done)
- [ ] Existing plans handled (add/replan/cancel)
- [ ] FEATURE.md status updated to "planning" before spawning agents
- [ ] Research completed (unless --skip-research or exists)
- [ ] gfd-researcher spawned with feature acceptance criteria
- [ ] gfd-planner spawned with feature + research context
- [ ] Plans created and cover all acceptance criteria
- [ ] gfd-plan-checker spawned (unless skipped)
- [ ] Verification passed OR user override OR max iterations with user decision
- [ ] FEATURE.md status updated to "planned" — **committed**
- [ ] Tasks section populated with plan references — **committed**
- [ ] User knows next step is `/gfd:execute-feature [SLUG]`

</success_criteria>
