<purpose>
Execute all plans for a feature using wave-based parallel execution. Orchestrator stays lean — delegates plan execution to gfd-executor subagents. After all plans complete, spawns gfd-verifier to check against FEATURE.md acceptance criteria.
</purpose>

<core_principle>
Orchestrator coordinates, not executes. Each subagent loads its plan and runs tasks. Orchestrator: discover plans → group waves → spawn executors → handle checkpoints → collect results → verify acceptance criteria.
</core_principle>

<required_reading>
@$HOME/.claude/get-features-done/references/ui-brand.md
</required_reading>

<process>

<step name="initialize" priority="first">
Parse slug from $ARGUMENTS (first positional argument).

**If no slug provided:**

```
╔══════════════════════════════════════════════════════════════╗
║  ERROR                                                       ║
╚══════════════════════════════════════════════════════════════╝

No feature slug provided.

**To fix:** /gfd:execute-feature <slug>
```

Exit.

Load all context in one call:

```bash
INIT=$($HOME/.claude/get-features-done/bin/gfd-tools init execute-feature "${SLUG}")
```

Extract values from key=value output:
- `executor_model` (grep "^executor_model=" | cut -d= -f2-)
- `verifier_model` (grep "^verifier_model=" | cut -d= -f2-)
- `parallelization` (grep "^parallelization=" | cut -d= -f2-)
- `feature_found` (grep "^feature_found=" | cut -d= -f2-)
- `feature_dir` (grep "^feature_dir=" | cut -d= -f2-)
- `feature_slug` (grep "^feature_slug=" | cut -d= -f2-)
- `feature_name` (grep "^feature_name=" | cut -d= -f2-)
- `feature_status` (grep "^feature_status=" | cut -d= -f2-)
- `plan_count` (grep "^plan_count=" | cut -d= -f2-)
- `incomplete_count` (grep "^incomplete_count=" | cut -d= -f2-)
- Each `plan=` line is a plan filename (repeated, one per plan); each `incomplete_plan=` line is an incomplete plan filename

Read feature file separately:
```bash
cat "docs/features/${SLUG}/FEATURE.md"
```

**If `project_exists` is false:** Error — run `/gfd:new-project` first.

**If `feature_found` is false:** Error — feature directory not found.

**If `plan_count` is 0:**

```
╔══════════════════════════════════════════════════════════════╗
║  ERROR                                                       ║
╚══════════════════════════════════════════════════════════════╝

No plans found for feature: [SLUG]

**To fix:** Run /gfd:plan-feature [SLUG] first.
```

Exit.

**If `feature_status` is `done`:**

Use AskUserQuestion:
- header: "Already Done"
- question: "Feature [SLUG] is marked done. Re-execute it?"
- options:
  - "Yes — re-execute" — Run plans again (e.g., after changes)
  - "No — show progress" — Show me what was already done

If "No": Exit and suggest `/gfd:status`.
</step>

<step name="update_status">
Update FEATURE.md status to "in-progress":

```bash
$HOME/.claude/get-features-done/bin/gfd-tools feature-update-status "${SLUG}" "in-progress"
```
</step>

<step name="discover_and_group_plans">
Load plan inventory with wave grouping:

```bash
PLAN_INDEX=$($HOME/.claude/get-features-done/bin/gfd-tools feature-plan-index "${SLUG}")
```

Extract from key=value output:
- Summary keys: `slug=`, `plan_count=`, `complete_count=`
- Per-plan keys (one group per plan): `plan_id=`, `plan_file=`, `plan_type=`, `plan_wave=`, `plan_status=` (`complete` or `pending`), `plan_autonomous=` (`true` or `false`)
- Per-wave summary keys: `wave_id=`, `wave_plan_count=`, `wave_complete_count=`

**Filtering:** Skip plans where `plan_status=complete` (already complete). If all filtered: report "All plans already complete" and proceed to verification.

Report the execution plan:

```
## Execution Plan

**[feature_name]** — [total_plans] plans across [wave_count] waves

| Wave | Plans | What it builds |
|------|-------|----------------|
| 1 | 01, 02 | {from plan objectives, 3-8 words} |
| 2 | 03     | ... |
```
</step>

<step name="execute_waves">
Execute each wave in sequence. Within a wave: parallel if `parallelization.enabled` is true (from config), sequential if false.

**For each wave:**

1. **Describe what's being built (BEFORE spawning):**

   Read each plan's `<objective>`. Extract what's being built and why.

   ```
   ---
   ## Wave {N}

   **{Plan ID}: {Plan Name}**
   {2-3 sentences: what this builds, technical approach, why it matters}

   Spawning {count} executor(s)...
   ---
   ```

   - Bad: "Executing authentication plan"
   - Good: "JWT-based session management — creates token generation, validation middleware, and refresh token rotation. Required before protected routes can enforce authentication."

2. **Display banner:**

   ```
   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    GFD ► EXECUTING WAVE {N}
   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   ```

3. **Spawn gfd-executor agents:**

   Pass paths only — executors read files themselves with their fresh context.
   This keeps orchestrator context lean.

   ```
   Task(
     subagent_type="gfd-executor",
     model="{executor_model}",
     run_in_background=true,  (only if parallel wave and multiple plans)
     prompt="
       <objective>
       Execute plan {plan_id} for feature {SLUG} ({feature_name}).
       Commit each task atomically. Create SUMMARY.md. Record decisions in FEATURE.md.
       </objective>

       <execution_context>
       @$HOME/.claude/get-features-done/templates/summary.md
       </execution_context>

       <files_to_read>
       Read these files at execution start using the Read tool:
       - Feature: {feature_dir}/FEATURE.md
       - Plan: {feature_dir}/{plan_file}
       - Config: docs/features/config.json
       </files_to_read>

       <acceptance_criteria>
       The feature acceptance criteria (from FEATURE.md) for reference:
       {acceptance_criteria}
       </acceptance_criteria>

       <success_criteria>
       - [ ] All tasks in the plan executed
       - [ ] Each task committed individually with descriptive message
       - [ ] SUMMARY.md created in plan directory (or alongside plan)
       - [ ] Decisions/blockers added to FEATURE.md
       - [ ] Return ## PLAN COMPLETE with summary
       </success_criteria>
     "
   )
   ```

4. **Wait for all agents in wave to complete.**

5. **Report completion — spot-check claims first:**

   For each plan:
   - Verify SUMMARY.md exists alongside or in `{feature_dir}/{plan_id}-SUMMARY.md`
   - Check `git log --oneline --grep="{SLUG}"` returns commits since execution started
   - Check for `## Self-Check: FAILED` marker in SUMMARY.md

   If ANY spot-check fails: report which plan failed → ask "Retry plan?" or "Continue with remaining waves?"

   If pass:
   ```
   ---
   ## Wave {N} Complete

   **{Plan ID}: {Plan Name}**
   {What was built — from SUMMARY.md one-liner}
   {Notable deviations, if any}

   {If more waves: what this enables for next wave}
   ---
   ```

6. **Handle failures:**

   **Known Claude Code bug (classifyHandoffIfNeeded):** If an agent reports "failed" with error containing `classifyHandoffIfNeeded is not defined`, this is a Claude Code runtime bug — not a GFD issue. Run the same spot-checks. If spot-checks PASS → treat as **successful**. If spot-checks FAIL → treat as real failure.

   For real failures: report which plan failed → ask "Continue?" or "Stop?" → if continue, dependent plans may also fail. If stop, partial completion report.

7. **Handle checkpoint plans:**

   Plans with `autonomous: false` require user interaction.

   When executor returns a checkpoint:

   ```
   ╔══════════════════════════════════════════════════════════════╗
   ║  CHECKPOINT: {Type}                                          ║
   ╚══════════════════════════════════════════════════════════════╝

   **Plan:** {plan_id} {Plan Name}
   **Progress:** {N}/{M} tasks complete

   {Checkpoint details from agent return}

   ──────────────────────────────────────────────────────────────
   → {ACTION PROMPT}
   ──────────────────────────────────────────────────────────────
   ```

   Wait for user response, then spawn continuation agent with user's response as context.

8. **Proceed to next wave.**
</step>

<step name="aggregate_results">
After all waves complete:

```markdown
## [feature_name] Execution Complete

**Waves:** {N} | **Plans:** {M}/{total} complete

| Wave | Plans | Status |
|------|-------|--------|
| 1 | plan-01, plan-02 | ✓ Complete |
| CP | plan-03 | ✓ Verified |
| 2 | plan-04 | ✓ Complete |

### Plan Details
1. **01**: [one-liner from SUMMARY.md]
2. **02**: [one-liner from SUMMARY.md]

### Issues Encountered
[Aggregate from SUMMARYs, or "None"]
```
</step>

<step name="verify_feature_goal">
**If `verifier_enabled` is false (from config):** Skip to update_feature_status.

Verify the feature achieved its acceptance criteria, not just completed tasks.

**Display banner:**
```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► VERIFYING
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

◆ Spawning verifier...
```

```
Task(
  prompt="
  <objective>
  Verify feature [SLUG] ([feature_name]) achieves its acceptance criteria.
  </objective>

  <feature_dir>[feature_dir]</feature_dir>

  <files_to_read>
  Read at start:
  - Feature definition: [feature_dir]/FEATURE.md (contains acceptance criteria)
  - All plan summaries: [feature_dir]/*-SUMMARY.md
  </files_to_read>

  <verification_task>
  For each acceptance criterion in FEATURE.md:
  1. Verify the criterion is demonstrably met in the codebase
  2. Check plan summaries confirm the relevant tasks were done
  3. Run any automated checks if applicable (tests, linting)
  4. Note any criteria that require human verification

  Write VERIFICATION.md to [feature_dir]/ with:
  - Status: passed | gaps_found | human_needed
  - Per-criterion results
  - Evidence for each passing criterion
  - Specific gaps for each failing criterion
  - Human verification items (if any)

  Return ## VERIFICATION PASSED or ## GAPS FOUND or ## HUMAN NEEDED
  </verification_task>
  ",
  subagent_type="gfd-verifier",
  model="{verifier_model}",
  description="Verify feature [SLUG]"
)
```

**Handle verifier return:**

| Status | Action |
|--------|--------|
| `## VERIFICATION PASSED` | → update_feature_status (mark done) |
| `## HUMAN NEEDED` | Present human-check items, get approval or issue description |
| `## GAPS FOUND` | Present gap summary, offer `/gfd:plan-feature [SLUG]` for gap closure |

**If HUMAN NEEDED:**
```
╔══════════════════════════════════════════════════════════════╗
║  CHECKPOINT: Verification Required                           ║
╚══════════════════════════════════════════════════════════════╝

All automated checks passed. {N} items need human testing:

{From VERIFICATION.md human_verification section}

──────────────────────────────────────────────────────────────
→ Type "approved" or describe issues
──────────────────────────────────────────────────────────────
```

If "approved": proceed to update_feature_status.
If issues described: treat as gaps_found.

**If GAPS FOUND:**
```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► VERIFICATION — GAPS FOUND
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

**Score:** {N}/{M} acceptance criteria verified
**Report:** {feature_dir}/VERIFICATION.md

### What's Missing
{Gap summaries from VERIFICATION.md}

───────────────────────────────────────────────────────────────

## ▶ Next Up

`/gfd:plan-feature [SLUG]` — create gap-closure plans

───────────────────────────────────────────────────────────────
```

Do not mark feature as done if gaps exist. Exit.
</step>

<step name="update_feature_status">
Mark feature as done:

```bash
# Update FEATURE.md status
$HOME/.claude/get-features-done/bin/gfd-tools feature-update-status "${SLUG}" "done"

# Check off acceptance criteria (if all verified)
# Update the Last updated footer
```

Update FEATURE.md acceptance criteria checkboxes to checked if all verified.
</step>

<step name="commit_planning_docs">
```bash
git add "${feature_dir}/FEATURE.md" "${feature_dir}/VERIFICATION.md" && git diff --cached --quiet || git commit -m "docs(${SLUG}): complete feature execution"
```
</step>

<step name="token_usage_reporting">
After all plans have executed (and verifier has run, if enabled), append token usage rows to FEATURE.md.

1. Resolve models for the agents that ran:
   ```bash
   gfd-tools resolve-model gfd-executor
   gfd-tools resolve-model gfd-verifier
   ```
   Extract each model: `grep "^model=" | cut -d= -f2-`

2. Get today's date: `date +%Y-%m-%d`

3. Read the current FEATURE.md content (`docs/features/<slug>/FEATURE.md`).

4. Check if `## Token Usage` section exists:
   - If yes: append rows to the existing table.
   - If no: create the section with header.

5. Rows to append (one per agent role that ran):
   ```
   | execute | <YYYY-MM-DD> | gfd-executor | <executor-model> | — | — | — |
   ```
   If verifier ran (check workflow config — verifier is enabled unless explicitly disabled):
   ```
   | execute | <YYYY-MM-DD> | gfd-verifier | <verifier-model> | — | — | — |
   ```
   Note: Interactive workflow runs use `—` for token columns because exact token counts are not available from the Task tool return value. For headless runs, the C# commands write actual token counts.

6. Table format when creating new section:
   ```markdown
   ## Token Usage

   | Workflow | Date | Agent Role | Model | Input | Output | Cache Read |
   |----------|------|------------|-------|-------|--------|------------|
   | execute | <YYYY-MM-DD> | gfd-executor | <model> | — | — | — |
   | execute | <YYYY-MM-DD> | gfd-verifier | <model> | — | — | — |
   ```

7. Update FEATURE.md using Edit or Write tool.

8. Commit:
   ```bash
   git add docs/features/<slug>/FEATURE.md
   git commit -m "docs(<slug>): add execute token usage"
   ```
</step>

<step name="offer_next">
**Display stage banner:**

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► FEATURE [SLUG] COMPLETE ✓
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

Check remaining features by scanning `docs/features/` for FEATURE.md files with status not "done":

```bash
REMAINING=$($HOME/.claude/get-features-done/bin/gfd-tools list-features)
```

Extract from key=value output: each feature appears as a group of repeated keys — `feature_slug=`, `feature_name=`, `feature_status=`, `feature_owner=`, `feature_priority=` — one group per feature. Filter out any feature where `feature_status` is `done`.

**Route based on remaining features:**

**If features are in-progress:**

```
───────────────────────────────────────────────────────────────

## ▶ Next Up

**Continue [next-slug]** — feature in progress

`/gfd:execute-feature [next-slug]`

<sub>`/clear` first → fresh context window</sub>
```

**If features are planned (ready to execute):**

```
───────────────────────────────────────────────────────────────

## ▶ Next Up

**Execute [next-slug]** — plans ready

`/gfd:execute-feature [next-slug]`

<sub>`/clear` first → fresh context window</sub>
```

**If features are researched (ready for planning):**

```
───────────────────────────────────────────────────────────────

## ▶ Next Up

**Plan [next-slug]** — feature needs planning

`/gfd:plan-feature [next-slug]`

<sub>`/clear` first → fresh context window</sub>
```

**After displaying the primary Next Up command, render the active features status table:**

Reuse the `REMAINING` data from the `list-features --status not-done` call above (do NOT call list-features again). The current feature ([SLUG]) has just moved to "done" so it won't appear. If there are 2+ remaining features, render:

```
| Feature Name | Status | Next Step |
|--------------|--------|-----------|
| **[routed-to-feature-name]** | [status] | [next command] |
| [other-name] | [status] | [next command] |
```

- The feature routed to in Next Up is listed first and **bolded**
- Other remaining features in default sort order
- Next Step uses the same status→command mapping as /gfd:status
- Skip this table if only 1 or 0 remaining features

**If all features are done:**

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► ALL FEATURES COMPLETE ✓
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

All {N} features done. Project complete!

───────────────────────────────────────────────────────────────

## ▶ Next Up

**Review project status**

`/gfd:status`

───────────────────────────────────────────────────────────────

**Also available:**
- `/gfd:new-feature <slug>` — add more features
```

**Always append:**

```
───────────────────────────────────────────────────────────────

**Also available:**
- `/gfd:status` — see all features and overall status

───────────────────────────────────────────────────────────────
```
</step>

</process>

<context_efficiency>
Orchestrator: ~10-15% context. Subagents: fresh context each. No polling (Task blocks). No context bleed.
</context_efficiency>

<failure_handling>
- **classifyHandoffIfNeeded false failure:** Claude Code bug — spot-check SUMMARY.md and commits. If pass, treat as success.
- **Agent fails mid-plan:** Missing SUMMARY.md → report, ask user how to proceed.
- **Dependency chain breaks:** Wave 1 fails → Wave 2 dependents likely fail → user chooses attempt or skip.
- **All agents in wave fail:** Systemic issue → stop, report for investigation.
- **Checkpoint unresolvable:** "Skip this plan?" or "Abort execution?" → record partial progress.
</failure_handling>

<resumption>
Re-run `/gfd:execute-feature [SLUG]` → discover_and_group_plans finds completed SUMMARYs → skips them → resumes from first incomplete plan → continues wave execution.
</resumption>

<success_criteria>

- [ ] Feature slug validated and exists
- [ ] Plans discovered and grouped into waves
- [ ] FEATURE.md status updated to "in-progress" before execution
- [ ] Each wave described before spawning (not just "executing plan X")
- [ ] Executor agents spawned with feature acceptance criteria as context
- [ ] Wave completion spot-checked (SUMMARY.md exists, commits present)
- [ ] gfd-verifier spawned after all plans complete (unless disabled)
- [ ] Verification passed OR gaps handled with clear next step
- [ ] FEATURE.md status updated to "done" (only if verification passes) — **committed**
- [ ] User routed to next feature or congratulated if all done

</success_criteria>
