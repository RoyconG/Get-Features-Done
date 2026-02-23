---
feature: auto-run-feature-toggle
plan: "03"
type: execute
wave: 2
depends_on:
  - "01"
  - "02"
files_modified:
  - get-features-done/GfdTools/Commands/AutoRunCommand.cs
  - get-features-done/GfdTools/Program.cs
  - commands/gfd/run-feature.md
autonomous: true
acceptance_criteria:
  - "A new command starts auto-advancing a feature from its current status"
  - "Each stage runs in a fresh context (equivalent to /clear between stages)"
  - "Auto-advance halts and notifies on stage failure"

must_haves:
  truths:
    - "Running `/gfd:run my-feature` on a `researched` feature triggers plan stage then execute stage (unless stopped by auto_advance_until)"
    - "Running `/gfd:run my-feature` on a `planned` feature triggers only the execute stage"
    - "A failed research stage outputs result=aborted, stage_failed=research, abort_reason=<reason> and stops without running plan or execute"
    - "`auto_advance_until: plan` in FEATURE.md frontmatter stops after plan stage, skipping execute"
    - "Feature with status `done` outputs result=skipped with a reason"
    - "Each stage is run as a separate gfd-tools subprocess — ensuring fresh claude -p context per stage"
    - "`gfd-tools auto-run <slug>` and `/gfd:run <slug>` are both available after this plan"
  artifacts:
    - path: "get-features-done/GfdTools/Commands/AutoRunCommand.cs"
      provides: "Stage sequencing orchestrator with config resolution and subprocess invocation"
      exports: ["AutoRunCommand.Create(string cwd)"]
    - path: "get-features-done/GfdTools/Program.cs"
      provides: "CLI registration for AutoExecuteCommand and AutoRunCommand"
      contains: "AutoExecuteCommand.Create"
    - path: "commands/gfd/run-feature.md"
      provides: "/gfd:run <slug> Claude slash command"
      contains: "auto-run"
  key_links:
    - from: "commands/gfd/run-feature.md"
      to: "gfd-tools auto-run <slug>"
      via: "Bash tool invocation"
      pattern: "auto-run"
    - from: "get-features-done/GfdTools/Commands/AutoRunCommand.cs"
      to: "gfd-tools auto-research / auto-plan / auto-execute"
      via: "Process.Start subprocess invocation via RunStage()"
      pattern: "Process\\.Start"
    - from: "get-features-done/GfdTools/Commands/AutoRunCommand.cs"
      to: "get-features-done/GfdTools/Services/ConfigService.cs"
      via: "ConfigService.ResolveAutoAdvanceUntil(featureInfo.Frontmatter)"
      pattern: "ResolveAutoAdvanceUntil"
---

<objective>
Wire up the complete auto-run pipeline: create AutoRunCommand.cs that sequences stages as subprocesses, register both new commands in Program.cs, and create the /gfd:run Claude slash command.

Purpose: This plan is the user-visible entry point for the entire auto-run feature. Without it, the config methods and AutoExecuteCommand created in Plans 01-02 have no caller.
Output: AutoRunCommand.cs with DetermineStages logic, Program.cs updated with both command registrations, commands/gfd/run-feature.md Claude slash command.
</objective>

<execution_context>
@$HOME/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/auto-run-feature-toggle/FEATURE.md
@docs/features/PROJECT.md
@get-features-done/GfdTools/Program.cs
@get-features-done/GfdTools/Commands/AutoResearchCommand.cs
@get-features-done/GfdTools/Commands/AutoPlanCommand.cs
@get-features-done/GfdTools/Services/ConfigService.cs
@get-features-done/GfdTools/Services/FeatureService.cs
@commands/gfd/research-feature.md
@docs/features/auto-run-feature-toggle/01-SUMMARY.md
@docs/features/auto-run-feature-toggle/02-SUMMARY.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create AutoRunCommand.cs</name>
  <files>
    get-features-done/GfdTools/Commands/AutoRunCommand.cs
  </files>
  <action>
Create new file `get-features-done/GfdTools/Commands/AutoRunCommand.cs`:

```csharp
using System.CommandLine;
using System.Diagnostics;
using GfdTools.Services;

namespace GfdTools.Commands;

public static class AutoRunCommand
{
    public static Command Create(string cwd)
    {
        var cmd = new Command("auto-run") { Description = "Auto-advance a feature through lifecycle stages (research → plan → execute)" };

        var slugArg = new Argument<string>("slug") { Description = "Feature slug" };
        var maxTurnsOpt = new Option<int>("--max-turns") { Description = "Max claude turns per stage (cost guard)", DefaultValueFactory = _ => 30 };
        var modelOpt = new Option<string>("--model") { Description = "Claude model tier (sonnet, opus, haiku)", DefaultValueFactory = _ => "sonnet" };

        cmd.Add(slugArg);
        cmd.Add(maxTurnsOpt);
        cmd.Add(modelOpt);

        cmd.SetAction((ParseResult pr, CancellationToken ct) => RunAsync(pr, ct, cwd, slugArg, maxTurnsOpt, modelOpt));

        return cmd;
    }

    private static async Task<int> RunAsync(
        ParseResult pr,
        CancellationToken ct,
        string cwd,
        Argument<string> slugArg,
        Option<int> maxTurnsOpt,
        Option<string> modelOpt)
    {
        var slug = pr.GetValue(slugArg)!;
        var maxTurns = pr.GetValue(maxTurnsOpt);
        var model = pr.GetValue(modelOpt)!;

        // Step 1 — Find feature
        var featureInfo = FeatureService.FindFeature(cwd, slug);
        if (featureInfo == null)
            return Output.Fail($"Feature '{slug}' not found");

        // Step 2 — Resolve stop point from feature frontmatter
        // Note: /gfd:run always advances regardless of auto_advance flag —
        //       it IS the explicit trigger. auto_advance_until controls where to stop.
        var until = ConfigService.ResolveAutoAdvanceUntil(featureInfo.Frontmatter);

        // Step 3 — Determine which stages to run based on current status and stop point
        var stages = DetermineStages(featureInfo.Status, until).ToList();

        if (stages.Count == 0)
        {
            Output.Write("result", "skipped");
            Output.Write("reason", $"Feature '{slug}' status '{featureInfo.Status}' has no stages to run (already done or unknown status)");
            return 0;
        }

        Output.Write("feature", slug);
        Output.Write("status", featureInfo.Status);
        Output.Write("stages_planned", string.Join(",", stages.Select(s => s.name)));
        if (until != null) Output.Write("until", until);

        // Step 4 — Execute each stage in sequence via subprocess
        // Subprocess invocation preserves: fresh claude -p context, existing pre-flight checks,
        // status updates, and AUTO-RUN.md commits per stage.
        var gfdToolsBin = ResolveBin(cwd);

        foreach (var (cmd_name, stageName) in stages)
        {
            Output.Write("stage", stageName);

            var (success, reason) = await RunStage(cwd, gfdToolsBin, cmd_name, slug, maxTurns, model);

            if (!success)
            {
                Output.Write("result", "aborted");
                Output.Write("stage_failed", stageName);
                Output.Write("abort_reason", reason);
                return 1;
            }

            // Re-read feature info after each stage (status changes after each successful stage)
            featureInfo = FeatureService.FindFeature(cwd, slug)!;
        }

        Output.Write("result", "success");
        Output.Write("stages_completed", string.Join(",", stages.Select(s => s.name)));
        return 0;
    }

    /// <summary>
    /// Determine which stages to run based on current feature status and stop point.
    /// Maps status → first stage to run, and until → last stage to run (inclusive).
    /// "research" = index 0, "plan" = index 1, "execute" = index 2.
    /// </summary>
    private static IEnumerable<(string cmd, string name)> DetermineStages(string currentStatus, string? until)
    {
        var stageOrder = new[] { "research", "plan", "execute" };
        var stageCmds = new[] { "auto-research", "auto-plan", "auto-execute" };

        // Map current status to first stage index that needs to run
        int startStage = currentStatus?.ToLower() switch
        {
            "new" or "discussed" or "discussing" => 0,
            "researching" => 0,        // allow re-entry (sub-command handles idempotency)
            "researched" => 1,
            "planning" => 1,           // allow re-entry
            "planned" or "in-progress" => 2,
            _ => -1                    // "done" or unrecognized → nothing to run
        };

        if (startStage < 0) yield break;

        // Map until to last stage index (inclusive). null = run all stages through execute.
        int untilStage = until?.ToLower() switch
        {
            "research" => 0,
            "plan" => 1,
            _ => 2    // "execute", null, or unrecognized → run all
        };

        for (int i = startStage; i <= Math.Min(untilStage, stageOrder.Length - 1); i++)
            yield return (stageCmds[i], stageOrder[i]);
    }

    /// <summary>
    /// Run a single stage as a gfd-tools subprocess. Returns (success, reason).
    /// Uses ArgumentList.Add to prevent shell injection from slug values.
    /// </summary>
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

        // CRITICAL: Add each arg individually to prevent shell injection
        psi.ArgumentList.Add(command);      // e.g. "auto-research"
        psi.ArgumentList.Add(slug);
        psi.ArgumentList.Add("--max-turns");
        psi.ArgumentList.Add(maxTurns.ToString());
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(model);

        try
        {
            using var proc = Process.Start(psi)!;
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = Task.Run(() => proc.StandardError.ReadToEnd());
            await proc.WaitForExitAsync();
            var stdout = await stdoutTask;
            await stderrTask;

            // Sub-commands write result=success on success, result=aborted on failure
            bool success = proc.ExitCode == 0
                        && stdout.Contains("result=success", StringComparison.Ordinal);

            string reason = success ? "" :
                stdout.Split('\n')
                    .FirstOrDefault(l => l.StartsWith("abort_reason=", StringComparison.Ordinal))
                    ?.Substring("abort_reason=".Length) ?? "stage exited with non-zero code";

            return (success, reason);
        }
        catch (Exception ex)
        {
            return (false, $"failed to start '{command}': {ex.Message}");
        }
    }

    /// <summary>
    /// Resolve the gfd-tools binary path.
    /// Tries project-local installation first, falls back to PATH.
    /// </summary>
    private static string ResolveBin(string cwd)
    {
        var binPath = Path.Combine(cwd, "get-features-done", "bin", "gfd-tools");
        if (File.Exists(binPath)) return binPath;
        return "gfd-tools";
    }
}
```
  </action>
  <verify>
Run `dotnet build get-features-done/GfdTools/GfdTools.csproj` — must succeed (after Program.cs is updated in Task 2).
  </verify>
  <done>
AutoRunCommand.cs exists at `get-features-done/GfdTools/Commands/AutoRunCommand.cs`. Contains: DetermineStages() with status→startStage mapping, RunStage() with ArgumentList.Add subprocess invocation, ResolveBin() helper. Status mapping covers: new, discussed, discussing, researching, researched, planning, planned, in-progress, done.
  </done>
</task>

<task type="auto">
  <name>Task 2: Register commands in Program.cs and create /gfd:run command file</name>
  <files>
    get-features-done/GfdTools/Program.cs
    commands/gfd/run-feature.md
  </files>
  <action>
**Program.cs** — Add both new command registrations after the existing `rootCommand.Add(AutoPlanCommand.Create(cwd));` line:

```csharp
rootCommand.Add(AutoExecuteCommand.Create(cwd));
rootCommand.Add(AutoRunCommand.Create(cwd));
```

These two lines must be added immediately after `rootCommand.Add(AutoPlanCommand.Create(cwd));` to keep auto-* commands grouped together.

**commands/gfd/run-feature.md** — Create new file:

```markdown
---
name: gfd:run-feature
description: Auto-advance a feature through lifecycle stages (research → plan → execute)
argument-hint: <feature-slug>
allowed-tools: Read, Bash
---

<objective>Auto-advance feature `$ARGUMENTS` through its remaining lifecycle stages. Each stage runs in a fresh context via `claude -p`. Halts on failure.</objective>

<process>

## Steps

1. **Validate argument.** Extract the feature slug from `$ARGUMENTS`. If missing or empty, run `gfd-tools list-features` and tell the user to provide a slug.

2. **Resolve model.**
   ```bash
   gfd-tools resolve-model gfd-executor
   ```
   Extract `model=<value>` from output.

3. **Run auto-advance.**
   ```bash
   gfd-tools auto-run <slug> --model <model>
   ```

   Parse output lines:
   - `result=success` → success path
   - `result=aborted` → failure path (also read `stage_failed=` and `abort_reason=`)
   - `result=skipped` → skipped path (read `reason=`)
   - `stages_planned=` → stages that were planned to run
   - `stages_completed=` → stages that completed successfully
   - `until=` → stop point if feature had auto_advance_until set

4. **Report outcome.**

   **Success:** Report which stages completed. Example:
   > Feature `<slug>` auto-advanced through: research, plan, execute.
   > Run `/gfd:status` to see current state.

   **Aborted:** Report which stage failed and why. Example:
   > Auto-advance halted at the `<stage_failed>` stage.
   > Reason: `<abort_reason>`
   > Check `docs/features/<slug>/AUTO-RUN.md` for the full claude output.

   **Skipped:** Report that no stages were needed. Example:
   > Feature `<slug>` has no stages to run (status: <status from reason>).
   > The feature may already be done or in an unrecognized state.

</process>
```

Run `dotnet build get-features-done/GfdTools/GfdTools.csproj` to confirm the full build succeeds with all three new files in place.
  </action>
  <verify>
1. Run `dotnet build get-features-done/GfdTools/GfdTools.csproj` — must exit 0 with no errors.
2. Run `gfd-tools --help` (or `get-features-done/bin/gfd-tools --help`) — `auto-execute` and `auto-run` must appear in the command list.
3. Run `gfd-tools auto-run --help` — must show slug argument and --max-turns, --model options.
4. Check `commands/gfd/run-feature.md` exists and contains "auto-run".
  </verify>
  <done>
Program.cs registers both AutoExecuteCommand and AutoRunCommand. `gfd-tools auto-run --help` works. `gfd-tools auto-execute --help` works. `commands/gfd/run-feature.md` exists with correct structure (name, description, argument-hint, allowed-tools, process steps).
  </done>
</task>

</tasks>

<verification>
1. `dotnet build get-features-done/GfdTools/GfdTools.csproj` exits 0
2. `get-features-done/bin/gfd-tools auto-run --help` shows usage with slug, --max-turns, --model
3. `get-features-done/bin/gfd-tools auto-execute --help` shows usage with slug, --max-turns, --model
4. `commands/gfd/run-feature.md` exists with frontmatter `name: gfd:run-feature`
5. DetermineStages: "researched" → starts at plan stage; "planned" → starts at execute stage; "done" → no stages
6. ResolveBin: tries `{cwd}/get-features-done/bin/gfd-tools` first, falls back to PATH
</verification>

<success_criteria>
- Full build passes with all new files
- `auto-run` and `auto-execute` commands appear in `gfd-tools --help` output
- DetermineStages correctly maps all documented feature statuses
- /gfd:run command file has correct argument-hint and process steps
- Stage subprocess calls use ArgumentList.Add (no string concatenation)
- auto_advance_until: null → all stages; "plan" → stops after plan; "research" → stops after research
</success_criteria>

<output>
After completion, create `docs/features/auto-run-feature-toggle/03-SUMMARY.md` following the summary template.
</output>
