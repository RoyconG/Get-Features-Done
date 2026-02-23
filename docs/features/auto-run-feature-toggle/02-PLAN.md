---
feature: auto-run-feature-toggle
plan: "02"
type: execute
wave: 1
depends_on: []
files_modified:
  - get-features-done/GfdTools/Services/ClaudeService.cs
  - get-features-done/GfdTools/Commands/AutoExecuteCommand.cs
autonomous: true
acceptance_criteria:
  - "Each stage runs in a fresh context (equivalent to /clear between stages)"

must_haves:
  truths:
    - "Running `gfd-tools auto-execute <slug>` on a feature with non-autonomous plans aborts with a message listing which plan files require manual execution"
    - "Running `gfd-tools auto-execute <slug>` on a feature with no plans aborts with a clear message"
    - "`## EXECUTION COMPLETE` in claude stdout is recognized as a successful auto-execute run"
    - "Each auto-execute invocation spawns a fresh claude -p process (no shared context with prior stages)"
    - "Auto-execute writes AUTO-RUN.md and commits it (with SUMMARY.md files) on success"
  artifacts:
    - path: "get-features-done/GfdTools/Commands/AutoExecuteCommand.cs"
      provides: "Headless execute stage command"
      exports: ["AutoExecuteCommand.Create(string cwd)"]
    - path: "get-features-done/GfdTools/Services/ClaudeService.cs"
      provides: "Success detection for execute completion signal"
      contains: "EXECUTION COMPLETE"
  key_links:
    - from: "get-features-done/GfdTools/Commands/AutoExecuteCommand.cs"
      to: "get-features-done/GfdTools/Services/ClaudeService.cs"
      via: "ClaudeService.InvokeHeadless() — spawns fresh claude -p process"
      pattern: "InvokeHeadless"
    - from: "get-features-done/GfdTools/Services/ClaudeService.cs"
      to: "stdout from claude -p"
      via: "stdout.Contains(\"## EXECUTION COMPLETE\")"
      pattern: "EXECUTION COMPLETE"
---

<objective>
Create the `auto-execute` gfd-tools command that runs the execute workflow headlessly and add its completion signal to ClaudeService's success detection.

Purpose: AutoRunCommand (Plan 03) invokes `gfd-tools auto-execute <slug>` as a subprocess for the execute stage. Without this command, the execute stage cannot be automated. ClaudeService must recognize `## EXECUTION COMPLETE` as a success signal or the command will always report abort.
Output: New AutoExecuteCommand.cs following the AutoResearchCommand/AutoPlanCommand pattern exactly. Updated ClaudeService.cs with one additional OR condition in success detection.
</objective>

<execution_context>
@$HOME/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/auto-run-feature-toggle/FEATURE.md
@docs/features/PROJECT.md
@get-features-done/GfdTools/Services/ClaudeService.cs
@get-features-done/GfdTools/Commands/AutoResearchCommand.cs
@get-features-done/GfdTools/Commands/AutoPlanCommand.cs
@get-features-done/GfdTools/Services/FeatureService.cs
@get-features-done/GfdTools/Services/FrontmatterService.cs
</context>

<tasks>

<task type="auto">
  <name>Task 1: Add EXECUTION COMPLETE to ClaudeService success detection</name>
  <files>
    get-features-done/GfdTools/Services/ClaudeService.cs
  </files>
  <action>
In `ClaudeService.cs`, find the success detection block:

```csharp
bool success = stdout.Contains("## RESEARCH COMPLETE", StringComparison.Ordinal)
            || stdout.Contains("## PLANNING COMPLETE", StringComparison.Ordinal);
```

Replace it with:

```csharp
bool success = stdout.Contains("## RESEARCH COMPLETE", StringComparison.Ordinal)
            || stdout.Contains("## PLANNING COMPLETE", StringComparison.Ordinal)
            || stdout.Contains("## EXECUTION COMPLETE", StringComparison.Ordinal);
```

This is the only change to ClaudeService.cs.
  </action>
  <verify>
Run `dotnet build get-features-done/GfdTools/GfdTools.csproj` — must succeed. Grep ClaudeService.cs for "EXECUTION COMPLETE" — must appear once in the success detection block.
  </verify>
  <done>
ClaudeService.cs success detection includes `## EXECUTION COMPLETE` as a third OR condition. Build passes.
  </done>
</task>

<task type="auto">
  <name>Task 2: Create AutoExecuteCommand.cs</name>
  <files>
    get-features-done/GfdTools/Commands/AutoExecuteCommand.cs
  </files>
  <action>
Create new file `get-features-done/GfdTools/Commands/AutoExecuteCommand.cs` modeled on AutoResearchCommand.cs and AutoPlanCommand.cs. Full implementation:

```csharp
using System.CommandLine;
using GfdTools.Services;

namespace GfdTools.Commands;

public static class AutoExecuteCommand
{
    public static Command Create(string cwd)
    {
        var cmd = new Command("auto-execute") { Description = "Run execute workflow headlessly via claude -p" };

        var slugArg = new Argument<string>("slug") { Description = "Feature slug" };
        var maxTurnsOpt = new Option<int>("--max-turns") { Description = "Max claude turns (cost guard)", DefaultValueFactory = _ => 30 };
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

        // Step 1 — Pre-flight checks
        var featureInfo = FeatureService.FindFeature(cwd, slug);
        if (featureInfo == null)
            return Output.Fail($"Feature '{slug}' not found");

        if (featureInfo.Plans.Count == 0)
        {
            var abortMd = ClaudeService.BuildAutoRunMd(slug, "auto-execute",
                new RunResult(false, "", "", 0, 0, "feature has no PLAN files"),
                DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), []);
            CommitAutoRunMd(cwd, slug, featureInfo.Directory, abortMd, $"docs({slug}): auto-execute aborted — no plans");
            return Output.Fail($"Aborted: {slug} has no PLAN files. Run /gfd:plan-feature first.");
        }

        if (featureInfo.IncompletePlans.Count == 0)
        {
            var abortMd = ClaudeService.BuildAutoRunMd(slug, "auto-execute",
                new RunResult(false, "", "", 0, 0, "all plans already have SUMMARY files"),
                DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), []);
            CommitAutoRunMd(cwd, slug, featureInfo.Directory, abortMd, $"docs({slug}): auto-execute aborted — already complete");
            return Output.Fail($"Aborted: {slug} all plans already executed.");
        }

        // Abort if any incomplete plan is non-autonomous (has checkpoints requiring human interaction)
        var featureDirFull = Path.Combine(cwd, featureInfo.Directory);
        var nonAutonomousPlans = new List<string>();
        foreach (var planId in featureInfo.IncompletePlans)
        {
            var planPath = Path.Combine(featureDirFull, $"{planId}-PLAN.md");
            if (!File.Exists(planPath)) continue;

            var planContent = File.ReadAllText(planPath);
            var planFm = FrontmatterService.Extract(planContent);
            if (planFm.TryGetValue("autonomous", out var autonomousVal))
            {
                bool isAutonomous = autonomousVal is bool b ? b :
                    !string.Equals(autonomousVal?.ToString(), "false", StringComparison.OrdinalIgnoreCase);
                if (!isAutonomous)
                    nonAutonomousPlans.Add($"{planId}-PLAN.md");
            }
        }

        if (nonAutonomousPlans.Count > 0)
        {
            var reason = $"plans with checkpoints require manual execution: {string.Join(", ", nonAutonomousPlans)}";
            var abortMd = ClaudeService.BuildAutoRunMd(slug, "auto-execute",
                new RunResult(false, "", "", 0, 0, reason),
                DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), []);
            CommitAutoRunMd(cwd, slug, featureInfo.Directory, abortMd, $"docs({slug}): auto-execute aborted — checkpoint plans");
            return Output.Fail($"Aborted: {reason}. Run /gfd:execute-feature {slug} instead.");
        }

        // Step 2 — Assemble the execute prompt
        var agentMd = File.ReadAllText(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude/agents/gfd-executor.md"));
        var featureMdPath = Path.Combine(cwd, featureInfo.Directory, "FEATURE.md");
        var featureMdContent = File.ReadAllText(featureMdPath);

        // Include all incomplete plan files in order
        var plansContent = new System.Text.StringBuilder();
        foreach (var planId in featureInfo.IncompletePlans)
        {
            var planPath = Path.Combine(featureDirFull, $"{planId}-PLAN.md");
            if (File.Exists(planPath))
            {
                plansContent.AppendLine($"\n## Plan: {planId}-PLAN.md\n");
                plansContent.AppendLine(File.ReadAllText(planPath));
            }
        }

        var planList = string.Join(", ", featureInfo.IncompletePlans.Select(p => $"{p}-PLAN.md"));

        var prompt = $"""
{agentMd}

---

## Auto-Execute Task

**Feature slug:** {slug}
**Feature directory:** {featureInfo.Directory}
**Working directory:** {cwd}
**Plans to execute (in order):** {planList}

## FEATURE.md Contents

{featureMdContent}

## Plans to Execute

{plansContent}

## Critical Auto-Run Rules

- You are running HEADLESSLY. There is NO user to ask questions.
- Do NOT call AskUserQuestion or emit any checkpoint prompts.
- Execute plans in the order listed above, SEQUENTIALLY (no parallel wave execution in headless mode).
- For each completed plan, create a SUMMARY.md at: {featureDirFull}/{{planId}}-SUMMARY.md
- If you encounter an ambiguous state that requires a user decision, output exactly:
  `## ABORT: <one-line reason>`
  Then stop immediately.
- On successful completion of ALL listed plans, output exactly:
  `## EXECUTION COMPLETE`
""";

        // Step 3 — Allowed tools (executor needs write access for code changes)
        var allowedTools = new[]
        {
            "Read", "Write", "Edit", "Bash", "Glob", "Grep"
        };

        // Step 4 — Invoke
        var startedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var result = await ClaudeService.InvokeHeadless(cwd, prompt, allowedTools, maxTurns, model);

        // Step 5 — Detect artifacts (SUMMARY.md files created)
        var summaryFiles = Directory.GetFiles(featureDirFull, "*-SUMMARY.md")
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Select(f => f!)
            .OrderBy(f => f)
            .ToArray();

        if (result.Success && summaryFiles.Length == 0)
        {
            result = result with { Success = false, AbortReason = "no SUMMARY.md files found after completion signal" };
        }

        var artifactsProduced = result.Success ? summaryFiles : Array.Empty<string>();

        // Step 6 — Build and commit AUTO-RUN.md
        var autoRunMd = ClaudeService.BuildAutoRunMd(slug, "auto-execute", result, startedAt, artifactsProduced);
        var commitMessage = result.Success
            ? $"feat({slug}): auto-execute complete ({summaryFiles.Length} plan(s))"
            : $"docs({slug}): auto-execute aborted — {result.AbortReason}";

        if (result.Success)
        {
            // Update feature status to "done"
            var featureMd = Path.Combine(cwd, featureInfo.Directory, "FEATURE.md");
            var featureContent = File.ReadAllText(featureMd);
            featureContent = System.Text.RegularExpressions.Regex.Replace(
                featureContent, @"^status:\s*.+$", "status: done",
                System.Text.RegularExpressions.RegexOptions.Multiline);
            File.WriteAllText(featureMd, featureContent);

            CommitAutoRunMd(cwd, slug, featureInfo.Directory, autoRunMd, commitMessage,
                artifactsProduced.Append("FEATURE.md").ToArray());
        }
        else
        {
            CommitAutoRunMd(cwd, slug, featureInfo.Directory, autoRunMd, commitMessage);
        }

        // Step 7 — Output
        if (result.Success)
        {
            Output.Write("result", "success");
            Output.Write("summary_count", summaryFiles.Length);
            foreach (var s in summaryFiles) Output.Write("summary", s);
            Output.Write("duration_seconds", result.DurationSeconds.ToString("F1"));
            return 0;
        }
        else
        {
            Output.Write("result", "aborted");
            Output.Write("abort_reason", result.AbortReason);
            return 1;
        }
    }

    private static void CommitAutoRunMd(string cwd, string slug, string featureDir,
        string autoRunMd, string commitMessage, string[]? artifactsProduced = null)
    {
        var autoRunPath = Path.Combine(cwd, featureDir, "AUTO-RUN.md");
        File.WriteAllText(autoRunPath, autoRunMd);

        var filesToAdd = new List<string> { Path.Combine(featureDir, "AUTO-RUN.md") };
        if (artifactsProduced != null)
            filesToAdd.AddRange(artifactsProduced.Select(a => Path.Combine(featureDir, a)));

        var addArgs = new[] { "add" }.Concat(filesToAdd).ToArray();
        GitService.ExecGit(cwd, addArgs);

        GitService.ExecGit(cwd, ["commit", "-m", commitMessage]);
    }
}
```

Note: AutoExecuteCommand is NOT registered in Program.cs here — that is done in Plan 03 to keep file ownership clean.
  </action>
  <verify>
Run `dotnet build get-features-done/GfdTools/GfdTools.csproj` — must succeed with 0 errors. (The command is not yet wired in Program.cs, but the class must compile cleanly.)
  </verify>
  <done>
AutoExecuteCommand.cs exists at `get-features-done/GfdTools/Commands/AutoExecuteCommand.cs`. File compiles. Pre-flight checks cover: feature not found, no plans, all plans complete, non-autonomous plans. Prompt includes gfd-executor agent + FEATURE.md + plan files. Success signal is `## EXECUTION COMPLETE`.
  </done>
</task>

</tasks>

<verification>
1. `dotnet build get-features-done/GfdTools/GfdTools.csproj` exits 0
2. `grep -r "EXECUTION COMPLETE" get-features-done/GfdTools/Services/ClaudeService.cs` returns a match
3. `AutoExecuteCommand.cs` exists in `get-features-done/GfdTools/Commands/`
4. AutoExecuteCommand follows same CommitAutoRunMd pattern as AutoResearchCommand and AutoPlanCommand
5. Non-autonomous plan detection uses `autonomous: false` frontmatter check with proper boolean parsing
</verification>

<success_criteria>
- Build passes with no errors
- ClaudeService.cs recognizes EXECUTION COMPLETE as success signal
- AutoExecuteCommand pre-flight checks abort gracefully for: no plans, all complete, checkpoint plans
- AutoExecuteCommand uses FrontmatterService.Extract for autonomous field parsing (not string search)
- CommitAutoRunMd pattern matches existing AutoResearchCommand and AutoPlanCommand exactly
</success_criteria>

<output>
After completion, create `docs/features/auto-run-feature-toggle/02-SUMMARY.md` following the summary template.
</output>
