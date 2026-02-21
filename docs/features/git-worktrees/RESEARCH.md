# Feature: Git Worktrees — Research

**Researched:** 2026-02-20
**Domain:** Git worktree management + C# CLI command implementation
**Confidence:** HIGH

## Summary

This feature adds isolated git worktree execution to GFD's execute-feature workflow. Each feature execution creates a `.worktrees/<slug>/` directory checked out on a new `feature/<slug>` branch off main, runs the executor agents there, then cleans up on success (prompting to merge) or preserves on failure.

All implementation is C# .NET 10 using the existing `GfdTools` project patterns: `GitService.ExecGit(cwd, args[])` for git operations, `Output.Write(key, value)` for key=value output, and `System.CommandLine` v2-beta5 for command registration. No new dependencies are required.

The primary complexity is in the execute-feature workflow orchestration (how the orchestrator communicates the worktree path to executor agents) and in the merge prompt sequence (user choice to merge or keep branch after success).

**Primary recommendation:** Implement `WorktreeCommands.cs` using `GitService.ExecGit`, add `Worktrees` bool to `Config.cs`, expose it in `ConfigService.cs` and `init execute-feature`, update `.gitignore`, and modify the `execute-feature.md` workflow to bracket execution with `worktree-create`/`worktree-remove`.

## User Constraints (from FEATURE.md)

### Locked Decisions

- **Config toggle:** `workflow.worktrees` — enabled by default
- **Integration layer:** gfd-tools C# commands only — orchestrator calls them without knowing git internals
- **Commands:** `worktree-create` and `worktree-remove`
- **Location:** `.worktrees/<slug>/` inside the repo, gitignored
- **Branch model:** `feature/<slug>` branch off main per feature
- **Merge:** User prompted after successful execution — not automatic
- **Conflict handling:** Abort merge, keep branch intact for manual resolution
- **Cleanup:** Auto-remove worktree on success, keep on failure for debugging

### Out of Scope

- No alternative branch naming schemes
- No automatic merge (always prompt)
- No push to remote as part of this feature
- No stash or rebase workflows
- No alternative to `.worktrees/` location

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `GitService.ExecGit` | existing | Runs git subprocesses safely | Already used for all git ops in codebase |
| `System.CommandLine` | 2.0.0-beta5.25306.1 | CLI command definition | Already the project's CLI framework |
| `Output.Write` / `Output.WriteBool` | existing | key=value stdout output | All gfd-tools commands use this protocol |
| `ConfigService.LoadConfig` | existing | Config parsing | Standard config access pattern |

### Supporting

No new NuGet packages required. All tooling is already in `GfdTools.csproj`.

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `GitService.ExecGit` | LibGit2Sharp | LibGit2Sharp is heavy; git binary is already used for all other ops |
| `git worktree add -b` | Manual branch + checkout | git worktree is the canonical tool for isolated checkouts |

**Installation:** None — no new packages needed.

## Architecture Patterns

### Files to Create or Modify

```
get-features-done/GfdTools/
├── Commands/
│   └── WorktreeCommands.cs          # NEW: worktree-create, worktree-remove
├── Models/
│   └── Config.cs                    # MODIFY: add Worktrees bool
├── Services/
│   └── ConfigService.cs             # MODIFY: parse workflow.worktrees
├── Program.cs                       # MODIFY: register WorktreeCommands
└── Commands/
    └── InitCommands.cs              # MODIFY: CreateExecuteFeature outputs worktrees_enabled

get-features-done/workflows/
└── execute-feature.md               # MODIFY: worktree lifecycle steps

docs/features/config.json            # MODIFY: document worktrees toggle

.gitignore                           # MODIFY: add .worktrees/
```

### Pattern 1: WorktreeCommands.cs Structure

**What:** Single static class with `Create(string cwd)` returning a parent `Command` with two subcommands.
**When to use:** Same pattern as `VerifyCommands.cs` (multiple subcommands under one parent).
**Example:**

```csharp
// Same pattern as VerifyCommands.Create()
public static class WorktreeCommands
{
    public static Command Create(string cwd)
    {
        var cmd = new Command("worktree") { Description = "Manage git worktrees for feature execution" };
        cmd.Add(CreateWorktreeCreate(cwd));
        cmd.Add(CreateWorktreeRemove(cwd));
        return cmd;
    }
}
```

### Pattern 2: worktree-create Command

**What:** Creates `.worktrees/<slug>/` with branch `feature/<slug>` off `main`.
**Git sequence:**

```csharp
// Source: git worktree documentation (git 2.53.0)
// Step 1: Ensure .worktrees/ parent exists (Directory.CreateDirectory is idempotent)
// Step 2: Check if worktree already exists (via git worktree list --porcelain)
// Step 3: Check if branch already exists (via git show-ref refs/heads/feature/<slug>)
// Step 4: Create worktree

// If branch does not exist:
ExecGit(cwd, ["worktree", "add", ".worktrees/<slug>", "-b", "feature/<slug>", "main"])

// If branch already exists (resumption case):
ExecGit(cwd, ["worktree", "add", ".worktrees/<slug>", "feature/<slug>"])

// Output:
Output.Write("worktree_path", ".worktrees/<slug>");
Output.Write("branch", "feature/<slug>");
Output.WriteBool("created", true);
```

### Pattern 3: worktree-remove Command

**What:** Removes `.worktrees/<slug>/` worktree reference. Optionally deletes branch.
**Git sequence:**

```csharp
// Source: git worktree documentation (git 2.53.0)
// Step 1: Remove the worktree (--force handles dirty working trees)
ExecGit(cwd, ["worktree", "remove", "--force", ".worktrees/<slug>"])

// Step 2: Prune stale references
ExecGit(cwd, ["worktree", "prune"])

// Optional: delete branch (separate flag controls this)
ExecGit(cwd, ["branch", "-d", "feature/<slug>"])  // safe: fails if unmerged
ExecGit(cwd, ["branch", "-D", "feature/<slug>"])  // force: use when user declines merge

// Output:
Output.WriteBool("removed", true);
Output.Write("branch", "feature/<slug>");
Output.WriteBool("branch_deleted", deletedBranch);
```

### Pattern 4: Config Extension

**What:** Add `Worktrees` bool to `Config.cs`, parse it in `ConfigService.cs`.

```csharp
// Config.cs — same pattern as existing booleans
public bool Worktrees { get; set; } = true;  // enabled by default

// ConfigService.cs — in the workflow parsing block
if (workflow.TryGetProperty("worktrees", out var wt))
    defaults.Worktrees = wt.GetBoolean();

// ConfigService.GetAllFields — add to output dict
["worktrees"] = config.Worktrees,

// InitCommands.cs — CreateExecuteFeature — add one line:
Output.WriteBool("worktrees_enabled", config.Worktrees);
```

### Pattern 5: execute-feature.md Workflow Modifications

**What:** Two new steps bracket the execute_waves step.
**When to use:** Always when `worktrees_enabled=true` from init output.

```markdown
<!-- After update_status step, before discover_and_group_plans: -->

## [NEW] worktree_create step (when worktrees_enabled=true)

WORKTREE=$(gfd-tools worktree create <slug>)
# Extract: worktree_path, branch from key=value output
# Pass worktree_path to executor agents in their prompt context

<!-- After aggregate_results, in offer_next — before update_feature_status: -->

## [NEW] worktree_merge_prompt step (when worktrees_enabled=true)

Prompt user: "Merge feature/<slug> into main or keep branch?"
- If merge: git merge --no-ff feature/<slug> (from main branch CWD)
  - If conflict: git merge --abort; report conflict; keep branch
  - If success: git branch -d feature/<slug>
- If keep: do nothing (branch persists)
Then: gfd-tools worktree remove <slug>

<!-- On failure in execute_waves (when worktrees_enabled=true): -->
# Do NOT call worktree-remove — preserve for debugging
# Inform user: worktree preserved at .worktrees/<slug>/
```

### Pattern 6: Executor Agent Worktree Context

**What:** Executor agents need to commit to the feature branch (in `.worktrees/<slug>/`), not to main.
**How:** The orchestrator passes `worktree_path` in the executor prompt; executor runs git operations using `-C <worktree_path>`.

```markdown
<!-- In execute-feature.md executor prompt template: -->
<worktree_context>
When worktrees_enabled: all git add/commit operations must use:
  git -C {worktree_path} add <files>
  git -C {worktree_path} commit -m "..."
The feature branch is {branch} (feature/<slug>).
</worktree_context>
```

### Anti-Patterns to Avoid

- **Committing from main worktree CWD when worktrees enabled:** Git commits go to whatever HEAD is checked out at the CWD. If executor runs `git add && git commit` from the main worktree root, commits go to main, not `feature/<slug>`.
- **Deleting branch before merge:** Always remove worktree first, then offer merge, then delete branch only if user merged or explicitly declined.
- **Using `-b` when branch already exists:** `git worktree add -b feature/<slug>` fails if `feature/<slug>` already exists. Check with `git show-ref` first, use bare `git worktree add .worktrees/<slug> feature/<slug>` if branch exists.
- **Not running worktree prune after remove:** Stale metadata in `.git/worktrees/` can cause "already exists" errors on re-create.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Isolated branch checkout | Custom file copy/branch logic | `git worktree add` | git handles locks, HEAD files, pack-file sharing |
| Dirty working tree detection | File scanning | `git worktree remove --force` | git knows what's tracked vs untracked |
| Stale worktree cleanup | Manual `.git/worktrees/` manipulation | `git worktree prune` | git's own metadata cleanup |
| Branch existence check | Regex on `git branch` output | `git show-ref refs/heads/feature/<slug>` | Exact, no parsing ambiguity |
| Worktree existence check | Directory.Exists only | `git worktree list --porcelain` | Directory may exist but not be registered |

**Key insight:** Git worktrees maintain their own `.git` file (not directory) in the worktree directory pointing back to `.git/worktrees/<name>/` in the main repo. Never touch `.git/worktrees/` directly — use `git worktree` subcommands.

## Common Pitfalls

### Pitfall 1: Branch Already Exists on Resume

**What goes wrong:** User re-runs execute-feature on a feature that was previously attempted. `git worktree add -b feature/<slug>` fails with "fatal: A branch named 'feature/<slug>' already exists."

**Why it happens:** The branch was created on the first attempt. The worktree was removed on failure, but the branch persists.

**How to avoid:** Check `git show-ref refs/heads/feature/<slug>` before calling `worktree add`. If branch exists, use `git worktree add .worktrees/<slug>/ feature/<slug>` (no `-b` flag).

**Warning signs:** `ExitCode != 0` from `worktree add` with stderr containing "already exists".

---

### Pitfall 2: Worktree Directory Already Exists

**What goes wrong:** `.worktrees/<slug>/` directory exists (stale from a crash) but `git worktree list` doesn't include it. `git worktree add` fails: "fatal: '<path>' already exists".

**Why it happens:** A previous execution crashed before cleanup, or the user manually deleted the git worktree registration without removing the directory.

**How to avoid:** Run `git worktree prune` before `worktree add`. If the directory still exists after prune, `Directory.Delete(.worktrees/<slug>, recursive: true)` before adding.

**Warning signs:** `ExitCode != 0` from `worktree add` with stderr containing "already exists" but `git worktree list --porcelain` does NOT show the path.

---

### Pitfall 3: Executor Commits Go to Wrong Branch

**What goes wrong:** Executor agent runs `git add . && git commit` from its working directory (the main worktree root). Commits go to `main` (or whatever branch is checked out there), not `feature/<slug>`.

**Why it happens:** Git commits go to the HEAD of the working directory you're in. The executor's CWD is the main worktree unless explicitly told otherwise.

**How to avoid:** Orchestrator must pass `worktree_path` to executor agents. Executor must use `git -C <worktree_path>` for all git operations, OR the executor must `cd` to the worktree directory.

**Warning signs:** After execution, `git log feature/<slug>` shows no new commits; `git log main` shows feature commits.

---

### Pitfall 4: Merge from Wrong Branch/CWD

**What goes wrong:** The merge prompt runs `git merge feature/<slug>` while the main worktree is on a different branch, or the current working directory is inside `.worktrees/<slug>/`.

**Why it happens:** The orchestrator forgets to ensure it's on `main` before merging.

**How to avoid:** Before merge, verify `git -C <repo_root> rev-parse --abbrev-ref HEAD` returns `main`. If not, checkout main first: `git -C <repo_root> checkout main`. Then merge.

**Warning signs:** Merge succeeds but feature changes appear on wrong branch.

---

### Pitfall 5: Gitignore Doesn't Prevent Worktree from Being Staged

**What goes wrong:** A developer runs `git add .` from the repo root and accidentally stages files from `.worktrees/`.

**Why it happens:** `.worktrees/` not in `.gitignore`.

**How to avoid:** Add `.worktrees/` to `.gitignore` as part of this feature. The worktree's own `.git` file (not directory) inside `.worktrees/<slug>/` also won't be staged since git treats it as a special git file.

**Note:** `.gitignore` only prevents untracked files from being staged — it does NOT prevent `git worktree add` from creating the directory there.

---

### Pitfall 6: --force Required for Dirty Worktree Removal

**What goes wrong:** On failure, the feature execution is preserved. Later, when re-executing, `git worktree remove .worktrees/<slug>` fails: "fatal: '<path>' contains modified or untracked files, use --force to delete it."

**Why it happens:** The worktree has uncommitted changes (expected on failure).

**How to avoid:** Always use `git worktree remove --force .worktrees/<slug>` in `worktree-remove`. This is safe because we're intentionally discarding or have already committed the worktree's work.

## Code Examples

### Check if branch exists

```csharp
// Source: git documentation — git show-ref exits 0 if found, 1 if not
var branchCheck = GitService.ExecGit(cwd, ["show-ref", "--verify", "--quiet", $"refs/heads/feature/{slug}"]);
bool branchExists = branchCheck.ExitCode == 0;
```

### Create worktree (idempotent)

```csharp
// Source: git worktree documentation (git 2.53.0)
var worktreePath = Path.Combine(".worktrees", slug);
var branchName = $"feature/{slug}";

// Prune stale metadata first
GitService.ExecGit(cwd, ["worktree", "prune"]);

// Check if already registered
var listResult = GitService.ExecGit(cwd, ["worktree", "list", "--porcelain"]);
bool alreadyRegistered = listResult.Stdout.Contains(worktreePath);

if (!alreadyRegistered)
{
    bool branchExists = GitService.ExecGit(cwd, ["show-ref", "--verify", "--quiet", $"refs/heads/{branchName}"]).ExitCode == 0;

    string[] addArgs = branchExists
        ? ["worktree", "add", worktreePath, branchName]
        : ["worktree", "add", worktreePath, "-b", branchName, "main"];

    var result = GitService.ExecGit(cwd, addArgs);
    if (result.ExitCode != 0)
        return Output.Fail($"worktree add failed: {result.Stderr}");
}

Output.Write("worktree_path", worktreePath);
Output.Write("branch", branchName);
Output.WriteBool("created", true);
```

### Remove worktree

```csharp
// Source: git worktree documentation (git 2.53.0)
var worktreePath = Path.Combine(".worktrees", slug);
var branchName = $"feature/{slug}";

// --force handles dirty/locked worktrees
var removeResult = GitService.ExecGit(cwd, ["worktree", "remove", "--force", worktreePath]);
GitService.ExecGit(cwd, ["worktree", "prune"]);

Output.WriteBool("removed", removeResult.ExitCode == 0);
Output.Write("branch", branchName);
// Branch deletion is NOT done here — orchestrator handles merge/delete decision
```

### Merge with conflict handling (in execute-feature workflow, not gfd-tools)

```bash
# Source: git merge documentation
# Called by orchestrator after user chooses "merge"
git checkout main
git merge --no-ff feature/<slug>
# Exit code 0 = success; non-0 = conflict
# On conflict:
git merge --abort          # restores main to pre-merge state; feature/<slug> branch untouched
# Then: report conflict to user; keep branch for manual resolution
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `git checkout -b` + manual directory | `git worktree add` | Git 2.5 (2015) | No need to stash or switch branches; multiple branches simultaneously |
| Manual worktree cleanup | `git worktree prune` | Git 2.7 (2016) | Safe cleanup of stale metadata |
| `--relative-paths` option | Available in git 2.53 | 2024 | Option exists but not needed here |

**No deprecated approaches:** `git worktree` API has been stable since ~git 2.15. The `git 2.53.0` version in this environment supports all required operations.

## Open Questions

1. **Executor CWD passing mechanism**
   - What we know: Executor agents make `git commit` calls; these need to happen in the worktree path
   - What's unclear: Whether the execute-feature workflow passes `worktree_path` in the executor prompt or whether the executor uses `git -C <path>` vs changing directory
   - Recommendation: Planner should define the exact prompt template addition; both approaches work. `git -C <path>` is cleaner (no shell state mutation).

2. **`worktrees_enabled` in init execute-feature output**
   - What we know: `init execute-feature` emits config flags like `parallelization`, `verifier_enabled`
   - What's unclear: Whether `worktrees_enabled` should also emit `worktree_path` if a worktree already exists (resumption)
   - Recommendation: Keep `init execute-feature` simple (just emit `worktrees_enabled`); `worktree-create` handles idempotency.

3. **Default branch determination**
   - What we know: FEATURE.md specifies "branch off main" — hardcoded
   - What's unclear: What if the repo's default branch is not named `main`?
   - Recommendation: Hardcode `main` for now (per spec). If this becomes an issue, add `git symbolic-ref refs/remotes/origin/HEAD` lookup later.

## Sources

### Primary (HIGH confidence)

- `git worktree --help` (git 2.53.0) — verified all subcommands and flags
- `get-features-done/GfdTools/Services/GitService.cs` — existing `ExecGit` pattern (ground truth)
- `get-features-done/GfdTools/Models/Config.cs` — existing config structure (ground truth)
- `get-features-done/GfdTools/Services/ConfigService.cs` — config parsing pattern (ground truth)
- `get-features-done/GfdTools/Commands/InitCommands.cs` — command registration pattern (ground truth)
- `get-features-done/GfdTools/Program.cs` — command registration in root (ground truth)
- `get-features-done/workflows/execute-feature.md` — orchestrator workflow (ground truth)
- `.gitignore` — existing gitignore (ground truth)
- `docs/features/config.json` — actual config structure in use (ground truth)

### Secondary (MEDIUM confidence)

- Git worktree behavior with gitignored paths: inferred from git object model (worktrees stored in `.git/worktrees/`, not working tree tracking)

### Tertiary (LOW confidence)

- None — all claims are grounded in official git docs and codebase inspection.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — codebase is C# .NET 10, all patterns verified from source
- Architecture: HIGH — git worktree commands verified from `git --help`; command patterns verified from codebase
- Pitfalls: HIGH — derived from git behavior (branch-already-exists, dirty removal, CWD semantics)
- Execute-feature changes: MEDIUM — workflow text pattern is clear but executor CWD mechanism needs planner decision

**Research date:** 2026-02-20
**Valid until:** 2026-08-20 (git worktree API is stable; C# patterns won't change without project changes)
