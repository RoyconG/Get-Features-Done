# Feature: Auto Run Feature Toggle — Research

**Researched:** 2026-02-22
**Domain:** GFD lifecycle orchestration, GfdTools CLI, config system, headless Claude invocation
**Confidence:** HIGH

## Summary

This feature adds a per-feature and project-level toggle to auto-advance a feature through GFD lifecycle stages (research → plan → execute) in sequence, each in a fresh context. The key insight is that GFD already has most of the infrastructure: `AutoResearchCommand.cs` and `AutoPlanCommand.cs` both invoke `ClaudeService.InvokeHeadless`, which spawns a fresh `claude -p` process per stage. The "fresh context" acceptance criterion is already solved by this architecture.

The feature requires three layers of new work: (1) extending the config system to support per-feature `auto_advance`/`auto_advance_until` frontmatter fields, (2) a new `gfd-tools auto-run <slug>` CLI command that chains existing stage commands, and (3) a new `/gfd:run <slug>` Claude command. An `auto-execute` C# command does not yet exist and will need to be created if execute stage is in scope.

The project config already has `workflow.auto_advance: false` in `config.json` and `ConfigService.cs` already reads it into `Config.AutoAdvance`. The default is false (manual mode). The field exists but has no implementation backing it — no code currently checks `Config.AutoAdvance` to auto-chain stages.

**Primary recommendation:** Add a new `AutoRunCommand.cs` that sequences existing auto-research and auto-plan stage commands (and a new auto-execute command), reads resolved config (project default + feature frontmatter override), and stops at the configured `auto_advance_until` point or on error.

## User Constraints (from FEATURE.md)

### Locked Decisions
- **Project config:** Dedicated config file (`docs/features/config.json`) for project-level default — already exists with `workflow.auto_advance: false`
- **Feature config:** FEATURE.md frontmatter for per-feature override (`auto_advance`) and stop point (`auto_advance_until`)
- **No CLI toggle command:** Users edit config/frontmatter directly — no `gfd-tools config-set auto_advance true` command needed
- **Trigger:** New command (e.g. `/gfd:run <slug>`) — naming at Claude's discretion
- **Context:** Fresh context window between each stage (already provided by `claude -p` subprocess model)
- **Error handling:** Stop and notify on failure — no retries
- **Scope:** Works on all features regardless of when created
- **Inheritance:** Simple resolved value — no need to surface whether mode is inherited or explicit

### Out of Scope
- **Headless/autonomous overnight mode** — deferred to separate feature (`auto-run-manual-mode`)
- **CLI toggle command** — users edit files directly

---

## Standard Stack

### Core (existing — no new dependencies)
| Component | Version | Purpose | Location |
|-----------|---------|---------|----------|
| GfdTools CLI | .NET 10 | Headless stage orchestration | `get-features-done/GfdTools/` |
| `System.CommandLine` | 2.0.0-beta5 | CLI argument parsing | GfdTools.csproj |
| `ClaudeService.InvokeHeadless` | in-repo | Spawns `claude -p` processes | `Services/ClaudeService.cs` |
| `FrontmatterService` | in-repo | YAML frontmatter parse/write | `Services/FrontmatterService.cs` |
| `ConfigService` | in-repo | Config loading + model resolution | `Services/ConfigService.cs` |
| `FeatureService` | in-repo | Feature discovery + info | `Services/FeatureService.cs` |

### New files to create
| File | Purpose |
|------|---------|
| `GfdTools/Commands/AutoRunCommand.cs` | New `auto-run <slug>` orchestration command |
| `GfdTools/Commands/AutoExecuteCommand.cs` | New `auto-execute <slug>` headless execute command |
| `commands/gfd/run-feature.md` | New `/gfd:run <slug>` Claude command |

### Modified files
| File | Change |
|------|--------|
| `GfdTools/Program.cs` | Register `AutoRunCommand` and `AutoExecuteCommand` |
| `GfdTools/Models/Config.cs` | Add `AutoAdvanceUntil` string property |
| `GfdTools/Services/ConfigService.cs` | Add `ResolveAutoAdvance(cwd, featureFm)` method; expose `auto_advance_until` |
| `GfdTools/Commands/InitCommands.cs` | Expose `auto_advance` and `auto_advance_until` in relevant init outputs |
| `get-features-done/templates/feature.md` | Document new frontmatter fields |
| `get-features-done/templates/config.json` | Document `auto_advance_until` default (if project-level is desired) |

**Installation:** No new packages required.

## Architecture Patterns

### Recommended Project Structure (no changes to directory structure)
The feature extends existing GfdTools patterns without structural changes:

```
GfdTools/
├── Commands/
│   ├── AutoResearchCommand.cs   (existing)
│   ├── AutoPlanCommand.cs       (existing)
│   ├── AutoRunCommand.cs        (NEW — sequences stages)
│   └── AutoExecuteCommand.cs   (NEW — headless execute)
├── Models/
│   └── Config.cs                (add AutoAdvanceUntil)
└── Services/
    └── ConfigService.cs         (add ResolveAutoAdvance)
```

### Pattern 1: Stage Sequencing in AutoRunCommand

`AutoRunCommand.cs` sequences stages by invoking existing stage commands as sub-processes via `Process.Start`. This preserves the fresh-context guarantee: each stage spawns an independent `claude -p` process through the sub-command's existing `ClaudeService.InvokeHeadless` call.

**Recommended approach — recursive gfd-tools sub-invocation:**

```csharp
// Source: pattern from ClaudeService.InvokeHeadless, adapted for gfd-tools sub-commands
private static async Task<(bool success, string reason)> RunStage(
    string cwd, string gfdToolsBin, string command, string slug, int maxTurns, string model)
{
    var psi = new ProcessStartInfo(gfdToolsBin)
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        WorkingDirectory = cwd,
    };
    psi.ArgumentList.Add(command);      // "auto-research" | "auto-plan" | "auto-execute"
    psi.ArgumentList.Add(slug);
    psi.ArgumentList.Add("--max-turns");
    psi.ArgumentList.Add(maxTurns.ToString());
    psi.ArgumentList.Add("--model");
    psi.ArgumentList.Add(model);

    using var proc = Process.Start(psi)!;
    var stdout = await proc.StandardOutput.ReadToEndAsync();
    var stderr = Task.Run(() => proc.StandardError.ReadToEnd());
    await proc.WaitForExitAsync();
    await stderr;

    // Sub-commands write result=success|aborted to stdout
    bool success = stdout.Contains("result=success");
    string reason = success ? "" :
        stdout.Split('\n').FirstOrDefault(l => l.StartsWith("abort_reason="))?.Substring(13) ?? "unknown";
    return (success, reason);
}
```

**Why sub-process over direct method call:** Each stage already has its own pre-flight checks, status updates, git commits, and AUTO-RUN.md writing. Reusing them via subprocess avoids duplicating this logic. The gfd-tools binary resolves its own path correctly (the wrapper script handles this).

### Pattern 2: Resolved Config

`auto_advance` is a merged value: feature frontmatter overrides project config.

```csharp
// Add to ConfigService
public static bool ResolveAutoAdvance(string cwd, Dictionary<string, object?> featureFrontmatter)
{
    // Feature-level overrides project-level (simple resolution, per FEATURE.md Notes)
    if (featureFrontmatter.TryGetValue("auto_advance", out var featureVal))
    {
        if (featureVal is bool b) return b;
        if (featureVal?.ToString() is "true") return true;
        if (featureVal?.ToString() is "false") return false;
    }
    var config = LoadConfig(cwd);
    return config.AutoAdvance;
}

public static string? ResolveAutoAdvanceUntil(Dictionary<string, object?> featureFrontmatter)
{
    // auto_advance_until is feature-only (no project-level default per FEATURE.md Notes)
    return featureFrontmatter.TryGetValue("auto_advance_until", out var val)
        ? val?.ToString()
        : null;  // null = run to completion (execute)
}
```

### Pattern 3: Stage-to-Status Mapping

The lifecycle pipeline:

| Current Status | Stage to Run | Sub-command | Next Status |
|---------------|--------------|-------------|-------------|
| `discussed` | Research | `auto-research` | `researched` |
| `researched` | Plan | `auto-plan` | `planned` |
| `planned` | Execute | `auto-execute` | `done` |
| `in-progress` | Execute | `auto-execute` | `done` |

`auto_advance_until` stops AFTER the named stage:
- `"research"` → run research only
- `"plan"` → run research + plan (skip execute)
- `"execute"` or null → run all stages

### Pattern 4: AutoRunCommand.cs Structure

```csharp
// Source: follows same structure as AutoResearchCommand.cs and AutoPlanCommand.cs
public static class AutoRunCommand
{
    public static Command Create(string cwd)
    {
        var cmd = new Command("auto-run") { Description = "Auto-advance a feature through lifecycle stages" };
        var slugArg = new Argument<string>("slug");
        var maxTurnsOpt = new Option<int>("--max-turns") { DefaultValueFactory = _ => 30 };
        var modelOpt = new Option<string>("--model") { DefaultValueFactory = _ => "sonnet" };
        // ...
    }

    private static async Task<int> RunAsync(...)
    {
        // 1. Pre-flight: find feature
        var featureInfo = FeatureService.FindFeature(cwd, slug);
        if (featureInfo == null) return Output.Fail($"Feature '{slug}' not found");

        // 2. Resolve config: feature frontmatter overrides project config
        var autoAdvance = ConfigService.ResolveAutoAdvance(cwd, featureInfo.Frontmatter);
        var until = ConfigService.ResolveAutoAdvanceUntil(featureInfo.Frontmatter);
        // Note: /gfd:run command bypasses the auto_advance flag check —
        // if user explicitly runs auto-run, they want auto-advance regardless.

        // 3. Determine stages to run
        var stages = DetermineStages(featureInfo.Status, until);

        // 4. Execute each stage in sequence, stop on failure
        foreach (var (stageCmd, stageName) in stages)
        {
            Output.Write("stage", stageName);
            var (success, reason) = await RunStage(cwd, gfdToolsBin, stageCmd, slug, maxTurns, model);
            if (!success)
            {
                Output.Write("result", "aborted");
                Output.Write("stage_failed", stageName);
                Output.Write("abort_reason", reason);
                return 1;
            }

            // Re-read feature info after each stage (status changes)
            featureInfo = FeatureService.FindFeature(cwd, slug)!;
        }

        Output.Write("result", "success");
        return 0;
    }
}
```

### Pattern 5: AutoExecuteCommand.cs

The execute stage is the most complex. The execute workflow uses wave-based parallel agents and a verifier. For headless auto-execute, simplify to:
- Sequential plan execution (no wave parallelism — parallel requires multiple concurrent `claude -p` processes which is complex to orchestrate headlessly)
- Plans with `autonomous: false` cause abort (cannot do checkpoints headlessly)
- Verifier runs after all plans complete (same as interactive mode)

The headless execute prompt would embed the gfd-executor agent instructions and drive each plan sequentially, with a success signal of `## EXECUTION COMPLETE`.

### Anti-Patterns to Avoid

- **Don't implement a new config key for `auto_advance_until` at project level.** The FEATURE.md Notes explicitly say "Configurable stop point per-feature in FEATURE.md frontmatter" — it's intentionally feature-only.
- **Don't check `Config.AutoAdvance` inside `/gfd:run` to gate execution.** The `/gfd:run` command IS the explicit trigger — it always runs. `Config.AutoAdvance` and feature `auto_advance` are for other workflows to check (e.g., if execute-feature were to auto-chain to the next feature automatically).
- **Don't make `auto-run` call `RunAsync` of sub-commands directly** (tight coupling, bypass their state machine and pre-flight checks). Use subprocess invocation instead.
- **Don't add wave parallelism to auto-execute.** Headless multi-agent parallelism is scope creep. Sequential is correct for the auto-advance use case.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Fresh context between stages | Custom context-clearing mechanism | `ClaudeService.InvokeHeadless` (each call = new process) | Each `claude -p` is already a fresh context |
| YAML frontmatter parsing | Custom parser | `FrontmatterService.Extract()` | Already handles all GFD frontmatter edge cases |
| Frontmatter writing | String manipulation | `FrontmatterService.Splice()` | Already handles reconstruction |
| Feature discovery | Directory scanning | `FeatureService.FindFeature()` | Handles all cases including Frontmatter, plans, summaries |
| Config loading | JSON parsing | `ConfigService.LoadConfig()` | Handles both flat and nested config.json |
| Git operations | Custom shell commands | `GitService.ExecGit()` | Safe, uses ArgumentList (no injection) |
| Output formatting | Console.WriteLine | `Output.Write()` / `Output.WriteBool()` | Project-wide key=value protocol |
| Process argument building | String concatenation | `psi.ArgumentList.Add()` | Prevents shell injection |

**Key insight:** The existing infrastructure handles all the complex edge cases. New commands should be thin orchestrators over existing services.

## Common Pitfalls

### Pitfall 1: auto_advance_until Semantics

**What goes wrong:** Treating `auto_advance_until` as "start up to stage X" vs "stop after stage X" is ambiguous. For example, `auto_advance_until: plan` could mean "run up to and including plan" or "stop at plan (don't do plan)".

**How to avoid:** Interpret as "stop AFTER the named stage completes successfully." So `plan` means research + plan run, execute does not. Validate values as: `research`, `plan`, `execute` (or null for all).

**Warning signs:** If users report their feature stops one stage too early or too late, this is the culprit.

### Pitfall 2: Feature Status Stale After Each Stage

**What goes wrong:** `AutoRunCommand` reads `featureInfo.Status` at the start, determines all stages, then runs them. But each stage updates the FEATURE.md status. If the auto-research stage fails partway through and leaves status as `researching`, the next `auto-run` invocation needs to recover correctly.

**How to avoid:** Re-read feature info after each stage using `FeatureService.FindFeature()`. The existing auto-research and auto-plan commands already handle recovery (they check `HasResearch` and plan count, not just status). So `auto-run` needs to let the sub-commands do their own pre-flight checks — don't pre-filter stages based on stale status.

**Warning signs:** `auto-run` skips a stage that should run, or tries to run a completed stage.

### Pitfall 3: Autonomous: false Plans Block Auto-Execute

**What goes wrong:** Plans with `autonomous: false` in their frontmatter require user interaction (checkpoints). Auto-execute cannot handle these headlessly.

**How to avoid:** In `AutoExecuteCommand`, pre-scan plan files for `autonomous: false`. If any exist, abort with a clear message: "Feature has checkpoint plans that require manual execution. Run /gfd:execute-feature instead."

**Warning signs:** Auto-execute hangs waiting for user input that never comes (or times out on max-turns).

### Pitfall 4: /gfd:run Command Duplication with /gfd:execute-feature

**What goes wrong:** If `/gfd:run` re-implements the execute logic rather than calling `auto-execute`, there are two code paths to maintain.

**How to avoid:** `/gfd:run` should invoke `gfd-tools auto-run <slug>`, which in turn calls `gfd-tools auto-execute <slug>` for the execute stage. Single responsibility.

### Pitfall 5: Config Resolution — Missing Frontmatter Field

**What goes wrong:** If `auto_advance` is absent from feature frontmatter (most features won't have it initially), the resolution must fall back to project config — not default to false.

**How to avoid:** The resolution logic must check "is the key present in frontmatter?" (not "is the value truthy?"), since `false` is a valid override.

```csharp
// CORRECT:
if (featureFrontmatter.TryGetValue("auto_advance", out var val) && val != null)
    // use feature value
else
    // use project config

// WRONG:
if (featureFrontmatter.GetValueOrDefault("auto_advance") is bool b && b)
    // misses explicit false override
```

### Pitfall 6: gfd-tools Binary Path in AutoRunCommand

**What goes wrong:** `AutoRunCommand` needs to invoke `gfd-tools` to run sub-commands. Hardcoding the path fails across environments.

**How to avoid:** Resolve the gfd-tools binary path relative to the currently executing assembly:
```csharp
var gfdToolsBin = Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..", "bin", "gfd-tools");
// Or use the same wrapper script resolution: look for bin/gfd-tools relative to cwd
```
Alternatively, call the `dotnet run` invocation from the wrapper script. The safest approach: expose a `static string ResolveBin()` helper in `ClaudeService` or a new helper class.

## Code Examples

### Resolving binary path from cwd

```csharp
// Source: pattern from get-features-done/bin/gfd-tools wrapper script logic
// The gfd-tools wrapper resolves to the GfdTools project. From cwd, the bin is:
private static string ResolveBin(string cwd)
{
    // Walk up to find get-features-done/bin/gfd-tools relative to cwd
    // gfd-tools is at: {repo_root}/get-features-done/bin/gfd-tools
    var binPath = Path.Combine(cwd, "get-features-done", "bin", "gfd-tools");
    if (File.Exists(binPath)) return binPath;
    return "gfd-tools"; // fall back to PATH
}
```

### FEATURE.md frontmatter with new fields

```yaml
---
name: My Feature
slug: my-feature
status: discussed
owner: Conroy
assignees: []
created: 2026-02-22
priority: medium
depends_on: []
auto_advance: true
auto_advance_until: plan
---
```

### Determining stages from current status and until-point

```csharp
// Source: pattern matches status lifecycle from FeatureService.StatusOrder
private static IEnumerable<(string cmd, string name)> DetermineStages(string currentStatus, string? until)
{
    var allStages = new[]
    {
        ("discussed",  "auto-research", "research"),
        ("researcing", "auto-research", "research"),  // allow re-entry
        ("researched", "auto-plan",     "plan"),
        ("planning",   "auto-plan",     "plan"),      // allow re-entry
        ("planned",    "auto-execute",  "execute"),
        ("in-progress","auto-execute",  "execute"),   // allow re-entry
    };

    var stages = new List<(string cmd, string name)>();
    bool inScope = false;

    // Map until to the last stage to include
    var stageOrder = new[] { "research", "plan", "execute" };
    int untilIndex = until == null ? 2 :
        Array.IndexOf(stageOrder, until.ToLower());
    if (untilIndex < 0) untilIndex = 2; // invalid until → run all

    foreach (var (triggerStatus, cmd, stageName) in allStages)
    {
        if (triggerStatus == currentStatus) inScope = true;
        if (!inScope) continue;

        int stageIndex = Array.IndexOf(stageOrder, stageName);
        if (stageIndex <= untilIndex)
            stages.Add((cmd, stageName));
    }

    return stages.DistinctBy(s => s.cmd); // deduplicate research/plan re-entry
}
```

### Config.cs update

```csharp
// Source: get-features-done/GfdTools/Models/Config.cs — add this property
public string? AutoAdvanceUntil { get; set; } = null; // null = run to execute
```

### Feature template update (new frontmatter fields)

```markdown
# Optional: auto-advance fields
# auto_advance: true          # override project config (default: inherits workflow.auto_advance)
# auto_advance_until: plan    # research | plan | execute (default: execute = all stages)
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Manual stage-by-stage commands | Auto-research + Auto-plan via `gfd-tools` | Already exists | CI pipeline works; interactive chaining missing |
| Context carried between stages | Fresh `claude -p` per stage | Already exists | No context bleed between stages |
| No feature-level config | Project-level `config.json` only | Already exists | Need to add feature frontmatter fields |

**Existing but unused:**
- `Config.AutoAdvance`: Parsed from config.json but nothing acts on it. This feature provides the implementation.

## Open Questions

1. **Execute stage scope**
   - What we know: `AutoExecuteCommand.cs` does not exist. The interactive execute workflow is complex (wave parallelism, checkpoints).
   - What's unclear: Does this feature ship with execute support, or stop at plan? FEATURE.md lists execute as a stage but Notes don't address it specifically.
   - Recommendation: Implement `auto-execute` with simplified sequential execution (no wave parallelism, abort on `autonomous: false` plans). Document the limitation clearly in the command help text.

2. **`auto_advance` flag behavior when using `/gfd:run` directly**
   - What we know: `/gfd:run` is an explicit trigger — user wants auto-advance.
   - What's unclear: Should `/gfd:run` refuse to run if `auto_advance` is false (both project and feature)?
   - Recommendation: No — `/gfd:run` always auto-advances. The `auto_advance` flag controls OTHER workflows that might check whether to auto-chain (e.g., future enhancements). Document this clearly.

3. **Auto-execute and non-autonomous plans**
   - What we know: Plans have `autonomous: false` for checkpoint plans.
   - What's unclear: Abort with message, or skip those plans?
   - Recommendation: Abort with clear message listing which plans need manual execution. Skipping silently is worse.

## Sources

### Primary (HIGH confidence)

All findings are based on direct codebase analysis — ground truth, HIGH confidence:

- `get-features-done/GfdTools/Commands/AutoResearchCommand.cs` — headless research pattern
- `get-features-done/GfdTools/Commands/AutoPlanCommand.cs` — headless plan pattern
- `get-features-done/GfdTools/Services/ClaudeService.cs` — `InvokeHeadless` implementation
- `get-features-done/GfdTools/Services/ConfigService.cs` — config loading + model resolution
- `get-features-done/GfdTools/Models/Config.cs` — `AutoAdvance` already present
- `get-features-done/GfdTools/Services/FrontmatterService.cs` — frontmatter parse/write
- `get-features-done/GfdTools/Services/FeatureService.cs` — feature discovery
- `get-features-done/templates/config.json` — project config template structure
- `docs/features/config.json` — current project config (`auto_advance: false`)
- `.gitea/workflows/gfd-nightly.yml` + `gfd-process-feature.yml` — CI already chains stages
- `commands/gfd/*.md` — existing command file structure and patterns
- `get-features-done/workflows/*.md` — workflow orchestration patterns

### Tertiary (LOW confidence — not needed, no external research required)

This feature is entirely internal to the GFD codebase. No external libraries or documentation were needed.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — entire implementation uses existing in-repo infrastructure
- Architecture: HIGH — patterns are directly derived from existing AutoResearchCommand.cs and AutoPlanCommand.cs
- Pitfalls: HIGH — derived from reading actual code behavior
- Execute stage: MEDIUM — no existing auto-execute; implementation is speculative but follows established patterns

**Research date:** 2026-02-22
**Valid until:** Stable (internal implementation — not affected by external library changes)
