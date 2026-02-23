---
feature: git-worktrees
plan: 02
type: execute
wave: 1
depends_on: []
files_modified:
  - .gitignore
  - docs/features/config.json
  - get-features-done/workflows/execute-feature.md
autonomous: true
acceptance_criteria:
  - ".worktrees/ is gitignored"
  - "workflow.worktrees config toggle in config.json controls whether worktrees are used (default: enabled)"
  - "execute-feature orchestrator calls worktree-create before execution and worktree-remove after, when toggle is enabled"
  - "On successful execution, the user is prompted to merge feature/<slug> into main or keep the branch"
  - "Merge conflicts abort the merge cleanly and preserve the feature branch"
  - "When toggle is disabled, execute-feature behaves as it does today (no worktree, no feature branch)"

must_haves:
  truths:
    - ".gitignore contains .worktrees/ to prevent staging worktree files"
    - "config.json has workflow.worktrees: true as the default"
    - "execute-feature workflow extracts worktrees_enabled from init output"
    - "execute-feature creates a worktree before execution when worktrees_enabled=true"
    - "execute-feature passes worktree_path and branch to executor agents when worktrees_enabled=true"
    - "execute-feature preserves the worktree on failure (does not call worktree remove)"
    - "execute-feature prompts user to merge or keep after successful execution when worktrees_enabled=true"
    - "merge conflicts are aborted cleanly (git merge --abort) and branch is preserved"
    - "when worktrees_enabled=false all worktree steps are skipped; execution is unchanged from today"
  artifacts:
    - path: ".gitignore"
      provides: ".worktrees/ exclusion from git tracking"
      contains: ".worktrees/"
    - path: "docs/features/config.json"
      provides: "worktrees toggle in workflow section"
      contains: "worktrees"
    - path: "get-features-done/workflows/execute-feature.md"
      provides: "worktree lifecycle integrated into execute workflow"
      contains: "worktree_create"
  key_links:
    - from: "get-features-done/workflows/execute-feature.md"
      to: "gfd-tools worktree create"
      via: "worktree_create step calls gfd-tools worktree create <slug>"
      pattern: "worktree create"
    - from: "get-features-done/workflows/execute-feature.md"
      to: "gfd-tools worktree remove"
      via: "worktree_merge_prompt step calls gfd-tools worktree remove <slug>"
      pattern: "worktree remove"
---

<objective>
Wire the worktree config toggle into config.json, add .worktrees/ to .gitignore, and integrate the worktree lifecycle into the execute-feature orchestration workflow.

Purpose: The execute-feature orchestrator needs to: (1) create a worktree before execution, (2) pass the worktree path to executor agents for correct git commit targeting, (3) preserve the worktree on failure, and (4) prompt the user to merge or keep the branch after success.

Output:
- MODIFIED: .gitignore (add .worktrees/)
- MODIFIED: docs/features/config.json (add workflow.worktrees: true)
- MODIFIED: get-features-done/workflows/execute-feature.md (5 integration points)
</objective>

<execution_context>
@$HOME/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/git-worktrees/FEATURE.md
@docs/features/git-worktrees/RESEARCH.md
@docs/features/PROJECT.md
@get-features-done/workflows/execute-feature.md
@.gitignore
@docs/features/config.json
</context>

<tasks>

<task type="auto">
  <name>Task 1: Add .worktrees/ to .gitignore and worktrees toggle to config.json</name>
  <files>
.gitignore
docs/features/config.json
  </files>
  <action>
**1. .gitignore** — Append a new section after the `# IDE files` block (after `*.suo`):

```
# Worktrees
.worktrees/
```

**2. docs/features/config.json** — Add `"worktrees": true` inside the `"workflow"` object, after `"auto_advance": false`:

```json
    "auto_advance": false,
    "worktrees": true
```

The result should be:
```json
  "workflow": {
    "research": true,
    "plan_check": true,
    "verifier": true,
    "auto_advance": false,
    "worktrees": true
  },
```
  </action>
  <verify>
grep ".worktrees" .gitignore
# Must output: .worktrees/

grep "worktrees" docs/features/config.json
# Must output: "worktrees": true
  </verify>
  <done>
- .gitignore includes .worktrees/ entry
- config.json has "worktrees": true inside the "workflow" object
  </done>
</task>

<task type="auto">
  <name>Task 2: Integrate worktree lifecycle into execute-feature.md</name>
  <files>get-features-done/workflows/execute-feature.md</files>
  <action>
Make five targeted edits to execute-feature.md. Read the file first to confirm line numbers before editing. All changes are additive — existing content is preserved as-is.

---

**Edit 1: initialize step — extract worktrees_enabled**

Find the line:
```
- Each `plans=` line is a plan entry; each `incomplete_plan=` line is an incomplete plan
```

Add immediately after it:
```
- `worktrees_enabled` (grep "^worktrees_enabled=" | cut -d= -f2-)
- `worktree_path` (only set if resuming; typically empty on first run)
```

---

**Edit 2: Insert `worktree_create` step after `update_status`**

Find the closing `</step>` tag of the `update_status` step (the one containing `feature-update-status "${SLUG}" "in-progress"`). Insert the following complete new step immediately after it:

```markdown
<step name="worktree_create">
**If `worktrees_enabled` is `false`:** Skip this step entirely — execution runs on the current branch as today.

Create an isolated worktree for this feature execution:

```bash
WORKTREE=$($HOME/.claude/get-features-done/bin/gfd-tools worktree create "${SLUG}")
```

Extract from key=value output:
- `worktree_path` (grep "^worktree_path=" | cut -d= -f2-)
- `branch` (grep "^branch=" | cut -d= -f2-)

**If the command fails** (non-zero exit or `error=` in output): report the error message and stop. Do not proceed with execution.

Inform user:
```
◆ Worktree created: .worktrees/{SLUG}/ on branch feature/{SLUG}
```
</step>
```

---

**Edit 3: Executor prompt — add `<worktree_context>` and update files_to_read**

In the `execute_waves` step, find the executor prompt template's `<files_to_read>` block:
```
       <files_to_read>
       Read these files at execution start using the Read tool:
       - Feature: {feature_dir}/FEATURE.md
       - Plan: {feature_dir}/{plan_file}
       - Config: docs/features/config.json
       </files_to_read>
```

Add the following immediately after that block (before `<acceptance_criteria>`):

```
       <worktree_context>
       {If worktrees_enabled=true:}
       All git add/commit operations MUST target the worktree, not the main repo root.
       Use git -C flag for every git operation:
         git -C {worktree_path} add <files>
         git -C {worktree_path} commit -m "feat(git-worktrees): ..."
       Active branch: {branch}
       Worktree path: {worktree_path}

       {If worktrees_enabled=false:}
       Run git operations from the current directory (no worktree isolation).
       </worktree_context>
```

Also update the Task prompt to interpolate `{worktree_path}` and `{branch}` — these come from the `worktree_create` step output variables. When `worktrees_enabled=false`, omit the `<worktree_context>` block from the prompt entirely.

---

**Edit 4: Failure handling — add worktree preservation note**

In step 6 of `execute_waves` ("Handle failures"), find:
```
   For real failures: report which plan failed → ask "Continue?" or "Stop?" → if continue, dependent plans may also fail. If stop, partial completion report.
```

Add immediately after it:

```
   **Worktree preservation on failure (when `worktrees_enabled=true`):** Do NOT call `gfd-tools worktree remove` on failure — preserve the worktree at `.worktrees/{SLUG}/` for debugging. Inform the user: "Worktree preserved at .worktrees/{SLUG}/. Branch: feature/{SLUG}. Inspect and retry, or run `git worktree remove --force .worktrees/{SLUG}` to clean up manually."
```

---

**Edit 5: Insert `worktree_merge_prompt` step after `aggregate_results`**

Find the closing `</step>` tag of the `aggregate_results` step (the step that produces the "## [feature_name] Execution Complete" markdown table). Insert the following complete new step immediately after it (before `<step name="verify_feature_goal">`):

```markdown
<step name="worktree_merge_prompt">
**If `worktrees_enabled` is `false`:** Skip this step entirely.

After all plans complete, prompt the user to merge the feature branch or keep it:

Use AskUserQuestion:
- header: "Branch"
- question: "Execution complete on branch feature/{SLUG}. Merge into main or keep the branch?"
- options:
  - label: "Merge into main" — merges feature/{SLUG} into main with --no-ff, deletes branch, removes worktree
  - label: "Keep branch" — removes worktree only; branch feature/{SLUG} preserved for manual review

**If "Merge into main":**

```bash
git checkout main
git merge --no-ff feature/{SLUG}
```

- **If merge succeeds (exit code 0):**
  ```bash
  git branch -d feature/{SLUG}
  $HOME/.claude/get-features-done/bin/gfd-tools worktree remove "${SLUG}"
  ```
  Inform user: "Merged feature/{SLUG} into main. Worktree cleaned up."
  Proceed to `verify_feature_goal`.

- **If merge conflict (exit code != 0):**
  ```bash
  git merge --abort
  $HOME/.claude/get-features-done/bin/gfd-tools worktree remove "${SLUG}"
  ```
  Report:
  ```
  ╔══════════════════════════════════════════════════════════════╗
  ║  MERGE CONFLICT                                              ║
  ╚══════════════════════════════════════════════════════════════╝

  Merge aborted cleanly. Branch feature/{SLUG} preserved.

  To resolve manually:
    git checkout main
    git merge --no-ff feature/{SLUG}
    # resolve conflicts
    git add <resolved-files>
    git commit
    git branch -d feature/{SLUG}
  ```
  Skip `verify_feature_goal`. Go to `offer_next` without marking feature as done.

**If "Keep branch":**

```bash
$HOME/.claude/get-features-done/bin/gfd-tools worktree remove "${SLUG}"
```

Inform user: "Worktree removed. Branch feature/{SLUG} preserved for manual review."
Skip `verify_feature_goal`. Go to `offer_next` without marking feature as done.
</step>
```

---

**Edit 6: success_criteria — add worktree items**

Find the `<success_criteria>` closing tag. Add these bullet items before it:

```
- [ ] worktrees_enabled extracted from init output in initialize step
- [ ] Worktree created before execution when worktrees_enabled=true (worktree_create step)
- [ ] Executor agents receive worktree_path and branch context when worktrees_enabled=true
- [ ] Worktree preserved on failure (not removed) when worktrees_enabled=true
- [ ] User prompted to merge or keep branch after successful execution when worktrees_enabled=true
- [ ] Merge conflicts aborted cleanly (git merge --abort) and branch preserved
- [ ] Worktree removed after merge decision (success or conflict)
- [ ] All worktree steps skipped when worktrees_enabled=false
```
  </action>
  <verify>
grep -n "worktrees_enabled" get-features-done/workflows/execute-feature.md | head -5
# Must show extraction line in initialize step

grep -n "worktree_create" get-features-done/workflows/execute-feature.md
# Must show the new worktree_create step name

grep -n "worktree_merge_prompt" get-features-done/workflows/execute-feature.md
# Must show the new worktree_merge_prompt step name

grep -n "worktree_path" get-features-done/workflows/execute-feature.md
# Must show worktree_path in multiple places (extraction, executor prompt)

grep -n "merge --abort" get-features-done/workflows/execute-feature.md
# Must show conflict handling
  </verify>
  <done>
- execute-feature.md has worktrees_enabled extraction in initialize step
- execute-feature.md has worktree_create step (with if-disabled guard) after update_status
- Executor prompt includes worktree_context with git -C instructions when worktrees_enabled=true
- Failure handling notes worktree preservation (do not remove on failure)
- execute-feature.md has worktree_merge_prompt step after aggregate_results
- Merge conflict handling uses git merge --abort and preserves the feature branch
- success_criteria includes all worktree checklist items
  </done>
</task>

</tasks>

<verification>
```bash
grep ".worktrees/" .gitignore
# .worktrees/

grep "worktrees" docs/features/config.json
# "worktrees": true

grep -c "worktree" get-features-done/workflows/execute-feature.md
# Multiple matches across the 5 integration points
```
</verification>

<success_criteria>
- [ ] .gitignore includes .worktrees/ entry
- [ ] config.json has workflow.worktrees: true
- [ ] execute-feature.md extracts worktrees_enabled in initialize step
- [ ] execute-feature.md has worktree_create step that guards on worktrees_enabled=false
- [ ] execute-feature.md passes worktree_path/branch to executor agents via worktree_context
- [ ] execute-feature.md preserves worktree on failure (explicit note in failure handling)
- [ ] execute-feature.md has worktree_merge_prompt step with merge/keep options
- [ ] merge --abort used on conflict, branch preserved, worktree removed
- [ ] all worktree steps are guarded: "If worktrees_enabled is false: Skip this step"
</success_criteria>

<output>
After completion, create `docs/features/git-worktrees/02-SUMMARY.md` with:
- What was changed in each file (.gitignore, config.json, execute-feature.md)
- Summary of the 6 integration points added to execute-feature.md
- Any deviations from the plan
</output>
