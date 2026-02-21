---
name: Git Worktrees
slug: git-worktrees
status: researched
owner: Conroy
assignees: []
created: 2026-02-20
priority: low
depends_on: []
---
# Git Worktrees

## Description

Integrate git worktrees into GFD's execute-feature workflow so each feature execution runs in an isolated worktree. Controlled by a `workflow.worktrees` config toggle (enabled by default). When enabled, `gfd-tools worktree-create` creates a `.worktrees/<slug>/` directory with a `feature/<slug>` branch off main, and `gfd-tools worktree-remove` cleans up after execution. On success, the worktree is auto-removed and the user is prompted to merge or keep the branch. On failure, the worktree is preserved for debugging. When disabled, execution runs on the current branch as it does today.

## Acceptance Criteria

- [ ] `workflow.worktrees` config toggle in config.json controls whether worktrees are used (default: enabled)
- [ ] `gfd-tools worktree-create <slug>` creates a worktree at `.worktrees/<slug>/` on a new `feature/<slug>` branch from main
- [ ] `gfd-tools worktree-remove <slug>` removes the worktree directory and optionally deletes the branch
- [ ] `execute-feature` orchestrator calls `worktree-create` before execution and `worktree-remove` after, when toggle is enabled
- [ ] On successful execution, the user is prompted to merge `feature/<slug>` into main or keep the branch
- [ ] Merge conflicts abort the merge cleanly and preserve the feature branch
- [ ] `.worktrees/` is gitignored
- [ ] When toggle is disabled, execute-feature behaves as it does today (no worktree, no feature branch)

## Tasks

[Populated during planning. Links to plan files.]

## Notes

### Implementation Decisions
- **Config toggle:** `workflow.worktrees` — enabled by default
- **Integration layer:** gfd-tools C# commands only — orchestrator calls them without knowing git internals
- **Commands:** `worktree-create` and `worktree-remove`
- **Location:** `.worktrees/<slug>/` inside the repo, gitignored
- **Branch model:** `feature/<slug>` branch off main per feature
- **Merge:** User prompted after successful execution — not automatic
- **Conflict handling:** Abort merge, keep branch intact for manual resolution
- **Cleanup:** Auto-remove worktree on success, keep on failure for debugging

### Claude's Discretion
- None — user specified all key decisions

### Deferred Ideas
- None

## Decisions

[Key decisions made during planning and execution of this feature.]

## Blockers

[Active blockers affecting this feature. Remove when resolved.]

---
*Created: 2026-02-20*
*Last updated: 2026-02-20*
