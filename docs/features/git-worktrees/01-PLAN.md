---
feature: git-worktrees
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - get-features-done/GfdTools/Commands/WorktreeCommands.cs
  - get-features-done/GfdTools/Models/Config.cs
  - get-features-done/GfdTools/Services/ConfigService.cs
  - get-features-done/GfdTools/Program.cs
  - get-features-done/GfdTools/Commands/InitCommands.cs
autonomous: true
acceptance_criteria:
  - "workflow.worktrees config toggle in config.json controls whether worktrees are used (default: enabled)"
  - "gfd-tools worktree-create <slug> creates a worktree at .worktrees/<slug>/ on a new feature/<slug> branch from main"
  - "gfd-tools worktree-remove <slug> removes the worktree directory and optionally deletes the branch"
  - "execute-feature orchestrator calls worktree-create before execution when toggle is enabled (init output: worktrees_enabled)"

must_haves:
  truths:
    - "Running `gfd-tools worktree create <slug>` creates .worktrees/<slug>/ on a feature/<slug> branch"
    - "Running `gfd-tools worktree remove <slug>` removes the worktree with --force (handles dirty trees)"
    - "`gfd-tools init execute-feature <slug>` outputs worktrees_enabled=true when config has workflow.worktrees=true"
    - "`gfd-tools init execute-feature <slug>` outputs worktrees_enabled=false when config has workflow.worktrees=false"
    - "worktree create is idempotent: handles branch-already-exists and directory-already-exists without crashing"
    - "dotnet build succeeds with no errors after all changes"
  artifacts:
    - path: "get-features-done/GfdTools/Commands/WorktreeCommands.cs"
      provides: "worktree create and worktree remove CLI subcommands"
      contains: "WorktreeCommands"
    - path: "get-features-done/GfdTools/Models/Config.cs"
      provides: "Worktrees bool property defaulting to true"
      contains: "public bool Worktrees"
    - path: "get-features-done/GfdTools/Services/ConfigService.cs"
      provides: "workflow.worktrees JSON parsing and GetAllFields output"
      contains: "worktrees"
    - path: "get-features-done/GfdTools/Program.cs"
      provides: "worktree command registered in CLI root"
      contains: "WorktreeCommands"
    - path: "get-features-done/GfdTools/Commands/InitCommands.cs"
      provides: "worktrees_enabled output in init execute-feature"
      contains: "worktrees_enabled"
  key_links:
    - from: "get-features-done/GfdTools/Program.cs"
      to: "get-features-done/GfdTools/Commands/WorktreeCommands.cs"
      via: "rootCommand.Add(WorktreeCommands.Create(cwd))"
      pattern: "WorktreeCommands"
    - from: "get-features-done/GfdTools/Services/ConfigService.cs"
      to: "get-features-done/GfdTools/Models/Config.cs"
      via: "workflow.worktrees JSON → Config.Worktrees"
      pattern: "Worktrees"
    - from: "get-features-done/GfdTools/Commands/InitCommands.cs"
      to: "get-features-done/GfdTools/Models/Config.cs"
      via: "config.Worktrees → Output.WriteBool(\"worktrees_enabled\", ...)"
      pattern: "worktrees_enabled"
---

<objective>
Implement the gfd-tools C# CLI commands for git worktree management and wire the config toggle.

Purpose: Provide `gfd-tools worktree create <slug>` and `gfd-tools worktree remove <slug>` as the integration layer the execute-feature orchestrator calls. The config toggle `workflow.worktrees` (default: true) controls whether worktrees are used, exposed via `gfd-tools init execute-feature` output.

Output:
- NEW: get-features-done/GfdTools/Commands/WorktreeCommands.cs
- MODIFIED: Config.cs, ConfigService.cs, Program.cs, InitCommands.cs
</objective>

<execution_context>
@$HOME/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/git-worktrees/FEATURE.md
@docs/features/git-worktrees/RESEARCH.md
@docs/features/PROJECT.md
@get-features-done/GfdTools/Commands/VerifyCommands.cs
@get-features-done/GfdTools/Models/Config.cs
@get-features-done/GfdTools/Services/ConfigService.cs
@get-features-done/GfdTools/Program.cs
@get-features-done/GfdTools/Commands/InitCommands.cs
@get-features-done/GfdTools/Services/GitService.cs
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create WorktreeCommands.cs</name>
  <files>get-features-done/GfdTools/Commands/WorktreeCommands.cs</files>
  <action>
Create a new file following the VerifyCommands.cs pattern: a static class with a `Create(string cwd)` factory method returning a parent `Command` with two subcommands.

File header:
```csharp
using System.CommandLine;
using GfdTools.Services;

namespace GfdTools.Commands;
```

Class: `public static class WorktreeCommands` with `public static Command Create(string cwd)` that creates a `"worktree"` command and adds two private subcommands: `CreateWorktreeCreate(cwd)` and `CreateWorktreeRemove(cwd)`.

**Subcommand 1: `"create"`**

Argument: `Argument<string>("slug")`.

Implementation logic (order matters):

1. Compute `worktreePath = Path.Combine(".worktrees", slug)` and `branchName = $"feature/{slug}"`.
2. Run `GitService.ExecGit(cwd, ["worktree", "prune"])` — prune stale metadata.
3. Run `GitService.ExecGit(cwd, ["worktree", "list", "--porcelain"])` — check `listResult.Stdout.Contains(worktreePath)` to determine `alreadyRegistered`.
4. If NOT `alreadyRegistered`:
   a. If `Directory.Exists(Path.Combine(cwd, worktreePath))`: call `Directory.Delete(Path.Combine(cwd, worktreePath), recursive: true)` — remove stale directory.
   b. Check branch existence: `GitService.ExecGit(cwd, ["show-ref", "--verify", "--quiet", $"refs/heads/{branchName}"]).ExitCode == 0` → `branchExists`.
   c. Build args: if `branchExists` → `["worktree", "add", worktreePath, branchName]`; else → `["worktree", "add", worktreePath, "-b", branchName, "main"]`.
   d. Run `GitService.ExecGit(cwd, addArgs)`. If `ExitCode != 0`: `return Output.Fail($"worktree add failed: {result.Stderr}")`.
5. Output:
   - `Output.Write("worktree_path", worktreePath)`
   - `Output.Write("branch", branchName)`
   - `Output.WriteBool("created", !alreadyRegistered)`
6. Return 0.

**Subcommand 2: `"remove"`**

Argument: `Argument<string>("slug")`.

Implementation logic:

1. Compute `worktreePath = Path.Combine(".worktrees", slug)` and `branchName = $"feature/{slug}"`.
2. Run `GitService.ExecGit(cwd, ["worktree", "remove", "--force", worktreePath])` — `--force` handles dirty/locked trees.
3. Run `GitService.ExecGit(cwd, ["worktree", "prune"])` — prune stale metadata.
4. Output:
   - `Output.WriteBool("removed", removeResult.ExitCode == 0)`
   - `Output.Write("branch", branchName)`
   - NOTE: Do NOT delete the branch here — the orchestrator handles the merge/delete decision.
5. Return 0.

Do NOT use string interpolation in git args arrays — pass discrete strings as shown. This matches the existing `GitService.ExecGit` safe-args pattern.
  </action>
  <verify>
cd get-features-done/GfdTools && dotnet build 2>&1 | tail -5
# Must show: Build succeeded, 0 Error(s)

dotnet run -- worktree --help 2>&1
# Must show "create" and "remove" as subcommands

dotnet run -- worktree create --help 2>&1
# Must show "slug" argument
  </verify>
  <done>
- WorktreeCommands.cs compiles without errors
- `gfd-tools worktree --help` lists create and remove subcommands
- `gfd-tools worktree create --help` and `gfd-tools worktree remove --help` show slug argument
  </done>
</task>

<task type="auto">
  <name>Task 2: Wire Worktrees config toggle and register command</name>
  <files>
get-features-done/GfdTools/Models/Config.cs
get-features-done/GfdTools/Services/ConfigService.cs
get-features-done/GfdTools/Program.cs
get-features-done/GfdTools/Commands/InitCommands.cs
  </files>
  <action>
Four targeted edits — minimal changes, no reformatting of existing code:

**1. Config.cs** — Add one property after `public bool AutoAdvance { get; set; } = false;`:

```csharp
    public bool Worktrees { get; set; } = true;
```

Default is `true` (enabled by default, per feature spec).

**2. ConfigService.cs** — Two additions:

a) In the `workflow` parsing block (inside `if (root.TryGetProperty("workflow", out var workflow))`), after the `plan_checker` block, add:

```csharp
                if (workflow.TryGetProperty("worktrees", out var wt))
                    defaults.Worktrees = wt.GetBoolean();
```

b) In `GetAllFields`, after the `["auto_advance"] = config.AutoAdvance,` entry, add:

```csharp
            ["worktrees"] = config.Worktrees,
```

**3. Program.cs** — Add after the `// ─── verify ───` section (after `rootCommand.Add(VerifyCommands.Create(cwd));`):

```csharp
// ─── worktree ────────────────────────────────────────────────────────────────
rootCommand.Add(WorktreeCommands.Create(cwd));
```

**4. InitCommands.cs** — In `CreateExecuteFeature`, after `Output.WriteBool("verifier_enabled", config.Verifier);`, add:

```csharp
            Output.WriteBool("worktrees_enabled", config.Worktrees);
```

This ensures the execute-feature orchestrator can read `worktrees_enabled=true/false` from init output.
  </action>
  <verify>
cd get-features-done/GfdTools && dotnet build 2>&1 | tail -5
# Must show: Build succeeded, 0 Error(s)

dotnet run -- init execute-feature git-worktrees 2>&1 | grep worktrees_enabled
# Must output: worktrees_enabled=true

dotnet run -- config-get worktrees 2>&1
# Must output the worktrees config value (true by default)
  </verify>
  <done>
- `dotnet build` succeeds with 0 errors
- `gfd-tools init execute-feature <slug>` outputs `worktrees_enabled=true` by default
- Setting `workflow.worktrees: false` in config.json makes `worktrees_enabled=false`
- `gfd-tools worktree create <slug>` and `gfd-tools worktree remove <slug>` are registered and callable
  </done>
</task>

</tasks>

<verification>
After both tasks:

```bash
cd get-features-done/GfdTools && dotnet build 2>&1 | grep -E "error|warning|succeeded"
# Build succeeded, 0 Error(s)

dotnet run -- worktree --help 2>&1 | grep -E "create|remove"
# Shows both subcommands

dotnet run -- init execute-feature git-worktrees 2>&1
# Contains: worktrees_enabled=true
```
</verification>

<success_criteria>
- [ ] WorktreeCommands.cs exists and compiles
- [ ] `gfd-tools worktree create <slug>` creates .worktrees/<slug>/ with feature/<slug> branch
- [ ] `gfd-tools worktree create` is idempotent (branch-exists and directory-exists cases handled)
- [ ] `gfd-tools worktree remove <slug>` removes worktree with --force
- [ ] Config.Worktrees defaults to true
- [ ] workflow.worktrees: false in config.json makes worktrees_enabled=false in init output
- [ ] dotnet build passes with 0 errors
</success_criteria>

<output>
After completion, create `docs/features/git-worktrees/01-SUMMARY.md` with:
- What was built (WorktreeCommands.cs, config changes)
- Git commands tested (if any)
- Any deviations from the plan
- Key implementation decisions
</output>
