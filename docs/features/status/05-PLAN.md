---
feature: status
plan: 05
type: execute
wave: 3
depends_on: ["01", "02", "03", "04"]
files_modified:
  - get-features-done/workflows/new-feature.md
  - get-features-done/workflows/plan-feature.md
  - get-features-done/workflows/execute-feature.md
  - get-features-done/templates/feature.md
autonomous: true
acceptance_criteria:
  - "/gfd:new-feature simplified to slug + one-liner, sets status to `new`"
  - "/gfd:plan-feature updated to transition `researched` → `planning` → `planned`"
  - "/gfd:execute-feature updated to transition `planned` → `in-progress` → `done`"
must_haves:
  truths:
    - "new-feature workflow asks only for slug and one-liner description, sets status to 'new'"
    - "plan-feature accepts 'researched' as valid entry status"
    - "plan-feature sed pattern updates 'researched' → 'planning' (not 'backlog' → 'planning')"
    - "execute-feature sed pattern updates 'planned' → 'in-progress' (backlog reference removed)"
    - "feature.md template uses 'status: new' as default"
    - "All /gfd:progress references in updated workflows replaced with /gfd:status"
  artifacts:
    - path: "get-features-done/workflows/new-feature.md"
      provides: "Simplified new-feature workflow — slug + one-liner only"
      contains: "status: new"
    - path: "get-features-done/workflows/plan-feature.md"
      provides: "Updated plan-feature — accepts researched, updates sed patterns"
      contains: "researched"
    - path: "get-features-done/workflows/execute-feature.md"
      provides: "Updated execute-feature — no backlog references"
      contains: "status: in-progress"
    - path: "get-features-done/templates/feature.md"
      provides: "Updated feature template — status: new as default"
      contains: "status: new"
  key_links:
    - from: "new-feature workflow"
      to: "FEATURE.md"
      via: "Write tool with status: new"
      pattern: "status: new"
    - from: "plan-feature workflow"
      to: "validStatuses via feature-update-status or sed"
      via: "sed or feature-update-status call"
      pattern: "researched.*planning|status: planning"
---

<objective>
Update four existing files to align with the new feature lifecycle: simplify new-feature, update status guards and sed patterns in plan-feature and execute-feature, update the feature template default status, and replace all /gfd:progress references with /gfd:status in updated workflows.

Purpose: The gfd-tools.cjs update (plan 01) and new commands (plans 02-04) are ready. Now the existing workflows must be updated to use the new status values. Without this, new-feature still creates features with "backlog" status and plan-feature still rejects "researched" as invalid.

Output: Four updated files with no references to "backlog" status and /gfd:progress references replaced.
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
  <name>Task 1: Simplify new-feature workflow and update feature template</name>
  <files>
    get-features-done/workflows/new-feature.md
    get-features-done/templates/feature.md
  </files>
  <action>
**Part A: Update get-features-done/templates/feature.md**

Read the current template at ./get-features-done/templates/feature.md.

Make these changes:
1. Change `status: backlog` to `status: new` in the template frontmatter
2. Update the `<guidelines>` Status Values section to list the new statuses:
   - `new` — Created but not yet discussed
   - `discussing` — Scope conversation in progress
   - `discussed` — Scope defined, ready for research
   - `researching` — Research in progress
   - `researched` — Research complete, ready for planning
   - `planning` — Plans being created
   - `planned` — Plans exist, ready for execution
   - `in-progress` — Actively being executed
   - `done` — All acceptance criteria met, verified
3. Update the `<evolution>` section to reflect the new lifecycle order (new → discussing → discussed → researching → researched → planning → planned → in-progress → done)

**Part B: Simplify get-features-done/workflows/new-feature.md**

Read the current workflow at ./get-features-done/workflows/new-feature.md.

The current workflow asks 4 questions (description, acceptance criteria, priority, dependencies). Simplify it to:
1. Slug (from $ARGUMENTS — already handled)
2. One question: "What does [SLUG] do? (one sentence)"

Remove the following questions entirely:
- "How will you know [SLUG] is complete?" (Question 2 — Acceptance Criteria)
- "How important is [SLUG] relative to other work?" (Question 3 — Priority)
- "Does [SLUG] depend on other features being done first?" (Question 4 — Dependencies)

The FEATURE.md written by new-feature should be minimal:
- `status: new` (not backlog)
- `priority: medium` (default, no question asked)
- `depends_on: []` (empty, no question asked)
- `## Description` with just the one-liner
- `## Acceptance Criteria` with placeholder: `- [ ] [To be defined during /gfd:discuss-feature]`
- `## Notes` empty

Update the "Done" section output (step 7 in current workflow):
- Change `Status: backlog` to `Status: new`
- Change the next step suggestion from `/gfd:plan-feature [SLUG]` to `/gfd:discuss-feature [SLUG]`
- Replace any `/gfd:progress` references with `/gfd:status`

The confirmation step (summarize and confirm) should be removed or simplified — since we only collected one piece of information, just proceed directly to creating FEATURE.md.

Keep all other workflow mechanics (slug validation, init, directory creation, STATE.md update, commit) intact.
  </action>
  <verify>
```bash
# Template uses new status
grep "status: new" ./get-features-done/templates/feature.md

# Template no longer has backlog
grep "status: backlog" ./get-features-done/templates/feature.md
# (should return nothing)

# Workflow simplified — no acceptance criteria question
grep -c "Acceptance Criteria" ./get-features-done/workflows/new-feature.md
# (should be 1 or fewer — just the placeholder mention, not the question)

# Workflow points to discuss-feature as next step
grep "discuss-feature" ./get-features-done/workflows/new-feature.md

# No progress references remain
grep "gfd:progress" ./get-features-done/workflows/new-feature.md
# (should return nothing)
```
  </verify>
  <done>
- feature.md template uses `status: new` and lists all 9 statuses in guidelines
- new-feature workflow asks only one question (one-liner description)
- new-feature creates FEATURE.md with `status: new` and placeholder acceptance criteria
- new-feature routes to /gfd:discuss-feature as next step
- No /gfd:progress references remain in new-feature.md
  </done>
</task>

<task type="auto">
  <name>Task 2: Update plan-feature and execute-feature workflows</name>
  <files>
    get-features-done/workflows/plan-feature.md
    get-features-done/workflows/execute-feature.md
  </files>
  <action>
**Part A: Update get-features-done/workflows/plan-feature.md**

Read the current workflow at ./get-features-done/workflows/plan-feature.md.

Make these targeted changes:

1. **Status validation section (step 2):** Find the text:
   ```
   **Valid statuses for planning:** `backlog`, `planning`
   ```
   Replace with:
   ```
   **Valid statuses for planning:** `researched`, `planning` (for re-entry if interrupted)

   **If status is `new`, `discussing`, or `discussed`:**
   Show error: "Feature [SLUG] needs research before planning. Run /gfd:discuss-feature [SLUG] then /gfd:research-feature [SLUG] first."
   Exit.
   ```

2. **sed pattern for status transition (step 4):** Find:
   ```bash
   sed -i 's/^status: backlog$/status: planning/' "${feature_dir}/FEATURE.md"
   ```
   Replace with:
   ```bash
   node $HOME/.claude/get-features-done/bin/gfd-tools.cjs feature-update-status "${SLUG}" "planning"
   ```
   (Using feature-update-status is preferred — validates the transition and returns confirmation.)

3. **Error message referencing /gfd:progress:** Find this text in the feature-not-found error block:
   ```
   Or run /gfd:progress to see all features.
   ```
   Replace with:
   ```
   Or run /gfd:status to see all features.
   ```

4. **Error message for done status:** Find:
   ```
   **To fix:** Run /gfd:progress to see remaining features.
   ```
   Replace with:
   ```
   **To fix:** Run /gfd:status to see remaining features.
   ```

5. **Any other /gfd:progress references:** Search for any remaining `/gfd:progress` references in the file and replace with `/gfd:status`.

**Part B: Update get-features-done/workflows/execute-feature.md**

Read the current workflow at ./get-features-done/workflows/execute-feature.md.

Make these targeted changes:

1. **sed patterns in update_status step:** Find these lines:
   ```bash
   sed -i 's/^status: planned$/status: in-progress/' "${feature_dir}/FEATURE.md"
   # Also handle backlog/planning in case user skips the state checks
   sed -i 's/^status: backlog$/status: in-progress/' "${feature_dir}/FEATURE.md"
   sed -i 's/^status: planning$/status: in-progress/' "${feature_dir}/FEATURE.md"
   ```
   Replace with:
   ```bash
   node $HOME/.claude/get-features-done/bin/gfd-tools.cjs feature-update-status "${SLUG}" "in-progress"
   ```
   (Single authoritative call — no backlog fallback needed. The validator in gfd-tools.cjs handles validation.)

2. **"No — show progress" route for done feature:** Find:
   ```
   If "No": Exit and suggest `/gfd:progress`.
   ```
   Replace with:
   ```
   If "No": Exit and suggest `/gfd:status`.
   ```

3. **All /gfd:progress references in next-step routing:** Find every occurrence of `/gfd:progress` in execute-feature.md and replace with `/gfd:status`. There are at least 3 occurrences:
   - Line ~71: suggest `/gfd:progress` after "No"
   - Line ~484: "Next Up" section for all-done route
   - Line ~498: "Also available" section

After making all changes, verify no `/gfd:progress` references remain in either file.
  </action>
  <verify>
```bash
# plan-feature accepts researched
grep "researched" ./get-features-done/workflows/plan-feature.md

# plan-feature no longer has backlog in sed
grep "status: backlog" ./get-features-done/workflows/plan-feature.md
# (should return nothing)

# execute-feature no longer has backlog in sed
grep "status: backlog" ./get-features-done/workflows/execute-feature.md
# (should return nothing)

# No progress references in either file
grep "gfd:progress" ./get-features-done/workflows/plan-feature.md
grep "gfd:progress" ./get-features-done/workflows/execute-feature.md
# (both should return nothing)

# Confirm gfd:status references added
grep "gfd:status" ./get-features-done/workflows/plan-feature.md
grep "gfd:status" ./get-features-done/workflows/execute-feature.md
```
  </verify>
  <done>
- plan-feature accepts "researched" as valid entry status with clear error for pre-research statuses
- plan-feature uses feature-update-status for the backlog→planning transition (now researched→planning)
- execute-feature uses feature-update-status for status update (no backlog fallback)
- Zero /gfd:progress references remain in either file
- /gfd:status used in all "also available" and "next up" sections
  </done>
</task>

</tasks>

<verification>
```bash
# Full sweep: no gfd:progress references in any updated workflow
grep -r "gfd:progress" \
  ./get-features-done/workflows/new-feature.md \
  ./get-features-done/workflows/plan-feature.md \
  ./get-features-done/workflows/execute-feature.md
# (should return nothing)

# Template uses new status
grep "status: new" ./get-features-done/templates/feature.md

# plan-feature accepts researched
grep "researched" ./get-features-done/workflows/plan-feature.md

# No backlog in sed patterns
grep "backlog" \
  ./get-features-done/workflows/plan-feature.md \
  ./get-features-done/workflows/execute-feature.md
# (should return nothing or only comments/docs, not active sed patterns)
```
</verification>

<success_criteria>
- feature.md template default status is "new" with all 9 statuses documented
- new-feature workflow asks one question (one-liner), sets status: new, routes to discuss-feature
- plan-feature accepts "researched" as valid entry status
- plan-feature status transition uses feature-update-status, no backlog reference
- execute-feature status transition uses feature-update-status, no backlog reference
- Zero /gfd:progress references in any of the four updated files
- /gfd:status used consistently in next-step routing
</success_criteria>

<output>
After completion, create `docs/features/status/05-SUMMARY.md` with what was changed and any deviations from plan.
</output>
