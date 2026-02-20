using System.CommandLine;
using GfdTools.Services;

namespace GfdTools.Commands;

public static class AutoPlanCommand
{
    public static Command Create(string cwd)
    {
        var cmd = new Command("auto-plan") { Description = "Run planning workflow headlessly via claude -p" };

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

        // Abort if plans already exist
        if (featureInfo.Plans.Count > 0)
        {
            var abortMd = ClaudeService.BuildAutoRunMd(slug, "auto-plan",
                new RunResult(false, "", "", 0, 0, $"feature already has {featureInfo.Plans.Count} PLAN file(s)"),
                DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), []);
            CommitAutoRunMd(cwd, slug, featureInfo.Directory, abortMd, "abort: plans already exist");
            return Output.Fail($"Aborted: {slug} already has PLAN files. Delete them first to re-plan.");
        }

        // Warn but don't abort if no RESEARCH.md (planning can proceed without research)

        // Step 2 — Assemble the planning prompt
        var agentMd = File.ReadAllText("/home/conroy/.claude/agents/gfd-planner.md");
        var featureMdPath = Path.Combine(cwd, featureInfo.Directory, "FEATURE.md");
        var featureMdContent = File.ReadAllText(featureMdPath);

        // Include RESEARCH.md if it exists
        var researchContent = "";
        var researchPath = Path.Combine(cwd, featureInfo.Directory, "RESEARCH.md");
        if (File.Exists(researchPath))
            researchContent = $"\n\n## RESEARCH.md Contents\n\n{File.ReadAllText(researchPath)}";

        var prompt = $"""
{agentMd}

---

## Auto-Plan Task

**Feature slug:** {slug}
**Feature directory:** {featureInfo.Directory}
**Working directory:** {cwd}

## FEATURE.md Contents

{featureMdContent}{researchContent}

## Critical Auto-Run Rules

- You are running HEADLESSLY. There is NO user to ask questions.
- Do NOT call AskUserQuestion or emit any checkpoint prompts.
- If you encounter an ambiguous state that requires a user decision, output exactly:
  `## ABORT: <one-line reason>`
  Then stop immediately.
- On successful completion of planning, output exactly:
  `## PLANNING COMPLETE`
- Write all PLAN.md files to: {Path.Combine(cwd, featureInfo.Directory)}
- File naming: 01-PLAN.md, 02-PLAN.md, etc.
""";

        // Step 3 — Allowed tools
        var allowedTools = new[]
        {
            "Read", "Write", "Bash", "Glob", "Grep", "WebFetch"
        };

        // Step 4 — Invoke
        var startedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var result = await ClaudeService.InvokeHeadless(cwd, prompt, allowedTools, maxTurns, model);

        // Step 5 — Detect artifacts produced
        // Scan the feature directory for any *-PLAN.md files created after invocation
        var featureDirFull = Path.Combine(cwd, featureInfo.Directory);
        var planFiles = Directory.GetFiles(featureDirFull, "*-PLAN.md")
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Select(f => f!)
            .OrderBy(f => f)
            .ToArray();

        var artifactsProduced = result.Success && planFiles.Length > 0
            ? planFiles
            : Array.Empty<string>();

        // If claude claimed success but no PLAN.md files exist, treat as abort
        if (result.Success && artifactsProduced.Length == 0)
        {
            result = result with { Success = false, AbortReason = "no PLAN.md files found after completion signal" };
        }

        // Step 6 — Commit AUTO-RUN.md
        // On abort: commit ONLY AUTO-RUN.md. Do NOT add any partial PLAN.md files.
        // On success: commit AUTO-RUN.md + all plan files + FEATURE.md (status update).
        var autoRunMd = ClaudeService.BuildAutoRunMd(slug, "auto-plan", result, startedAt, artifactsProduced);
        var commitMessage = result.Success
            ? $"feat({slug}): auto-plan complete ({artifactsProduced.Length} plan(s))"
            : $"docs({slug}): auto-plan aborted — {result.AbortReason}";

        if (result.Success)
        {
            // Update feature status to "planned" in FEATURE.md
            var featureMd = Path.Combine(cwd, featureInfo.Directory, "FEATURE.md");
            var featureContent = File.ReadAllText(featureMd);
            featureContent = System.Text.RegularExpressions.Regex.Replace(
                featureContent, @"^status:\s*.+$", "status: planned",
                System.Text.RegularExpressions.RegexOptions.Multiline);
            File.WriteAllText(featureMd, featureContent);

            // Include FEATURE.md + all plan files in commit
            CommitAutoRunMd(cwd, slug, featureInfo.Directory, autoRunMd, commitMessage,
                artifactsProduced.Append("FEATURE.md").ToArray());
        }
        else
        {
            // Abort: delete any partial PLAN.md files before committing
            foreach (var planFile in planFiles)
            {
                var planPath = Path.Combine(featureDirFull, planFile);
                if (File.Exists(planPath)) File.Delete(planPath);
            }
            CommitAutoRunMd(cwd, slug, featureInfo.Directory, autoRunMd, commitMessage);
        }

        // Step 7 — Output
        if (result.Success)
        {
            Output.Write("result", "success");
            Output.Write("plan_count", artifactsProduced.Length);
            foreach (var p in artifactsProduced) Output.Write("plan", p);
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

        // git add
        var addArgs = new[] { "add" }.Concat(filesToAdd).ToArray();
        GitService.ExecGit(cwd, addArgs);

        // git commit
        GitService.ExecGit(cwd, ["commit", "-m", commitMessage]);
    }
}
