using System.CommandLine;
using GfdTools.Services;

namespace GfdTools.Commands;

public static class AutoResearchCommand
{
    public static Command Create(string cwd)
    {
        var cmd = new Command("auto-research") { Description = "Run research workflow headlessly via claude -p" };

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

        // Step 1 — Pre-flight checks (abort without calling claude)
        var featureInfo = FeatureService.FindFeature(cwd, slug);
        if (featureInfo == null)
            return Output.Fail($"Feature '{slug}' not found");

        // Abort if already researched
        if (featureInfo.HasResearch)
        {
            var abortMd = ClaudeService.BuildAutoRunMd(slug, "auto-research",
                new RunResult(false, "", "", 0, 0, "feature already has RESEARCH.md"),
                DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), []);
            CommitAutoRunMd(cwd, slug, featureInfo.Directory, abortMd, "abort: already researched");
            return Output.Fail($"Aborted: {slug} already has RESEARCH.md. Delete it first to re-research.");
        }

        // Step 2 — Assemble the research prompt
        var agentMd = File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude/agents/gfd-researcher.md"));
        var featureMdPath = Path.Combine(cwd, featureInfo.Directory, "FEATURE.md");
        var featureMdContent = File.ReadAllText(featureMdPath);

        var prompt = $"""
{agentMd}

---

## Auto-Research Task

**Feature slug:** {slug}
**Feature directory:** {featureInfo.Directory}
**Working directory:** {cwd}

## FEATURE.md Contents

{featureMdContent}

## Critical Auto-Run Rules

- You are running HEADLESSLY. There is NO user to ask questions.
- Do NOT call AskUserQuestion or emit any checkpoint prompts.
- If you encounter an ambiguous state that requires a user decision, output exactly:
  `## ABORT: <one-line reason>`
  Then stop immediately.
- On successful completion of research, output exactly:
  `## RESEARCH COMPLETE`
- Write RESEARCH.md to: {Path.Combine(cwd, featureInfo.Directory, "RESEARCH.md")}
""";

        // Step 3 — Determine allowed tools
        var allowedTools = new[]
        {
            "Read", "Write", "Bash", "Glob", "Grep", "WebFetch"
        };

        // Step 4 — Record start time and invoke
        var startedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var result = await ClaudeService.InvokeHeadless(cwd, prompt, allowedTools, maxTurns, model);

        // Step 5 — Determine artifacts produced
        var researchPath = Path.Combine(cwd, featureInfo.Directory, "RESEARCH.md");
        var artifactsProduced = result.Success && File.Exists(researchPath)
            ? new[] { "RESEARCH.md" }
            : Array.Empty<string>();

        // If claude claimed success but RESEARCH.md doesn't exist, treat as abort
        if (result.Success && artifactsProduced.Length == 0)
        {
            result = result with { Success = false, AbortReason = "RESEARCH.md not found after completion signal" };
        }

        // Step 6 — On success, update FEATURE.md (status + token row) before committing
        if (result.Success)
        {
            // Update status via direct FEATURE.md edit
            var featureMd = Path.Combine(cwd, featureInfo.Directory, "FEATURE.md");
            var featureContent = File.ReadAllText(featureMd);
            featureContent = System.Text.RegularExpressions.Regex.Replace(
                featureContent, @"^status:\s*.+$", "status: researched",
                System.Text.RegularExpressions.RegexOptions.Multiline);
            File.WriteAllText(featureMd, featureContent);

            // Append token usage row to FEATURE.md
            var resolvedModel = ConfigService.ResolveModel(cwd, "gfd-researcher");
            AppendTokenUsageToFeatureMd(featureMd, "research", "gfd-researcher",
                resolvedModel, result.InputTokens, result.OutputTokens, result.CacheReadTokens);
        }

        // Step 7 — Build and commit AUTO-RUN.md (include FEATURE.md in artifacts on success)
        var autoRunMd = ClaudeService.BuildAutoRunMd(slug, "auto-research", result, startedAt, artifactsProduced);
        var commitMessage = result.Success
            ? $"feat({slug}): auto-research complete"
            : $"docs({slug}): auto-research aborted — {result.AbortReason}";

        if (result.Success)
        {
            // Include FEATURE.md in the commit alongside RESEARCH.md
            CommitAutoRunMd(cwd, slug, featureInfo.Directory, autoRunMd, commitMessage,
                artifactsProduced.Append("FEATURE.md").ToArray());
        }
        else
        {
            CommitAutoRunMd(cwd, slug, featureInfo.Directory, autoRunMd, commitMessage, artifactsProduced);
        }

        // Step 8 — Output results
        if (result.Success)
        {
            Output.Write("result", "success");
            Output.Write("artifact", "RESEARCH.md");
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

    private static void AppendTokenUsageToFeatureMd(
        string featureMdPath,
        string workflow,   // "research" or "plan"
        string agentRole,  // "gfd-researcher" or "gfd-planner"
        string model,      // resolved model tier (e.g. "sonnet")
        int inputTokens,
        int outputTokens,
        int cacheReadTokens)
    {
        if (!File.Exists(featureMdPath)) return;

        var content = File.ReadAllText(featureMdPath);
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var inputStr = inputTokens > 0 ? $"{inputTokens:N0}" : "—";
        var outputStr = outputTokens > 0 ? $"{outputTokens:N0}" : "—";
        var cacheStr = cacheReadTokens > 0 ? $"{cacheReadTokens:N0}" : "—";
        var newRow = $"| {workflow} | {date} | {agentRole} | {model} | {inputStr} | {outputStr} | {cacheStr} |";

        const string sectionHeader = "## Token Usage";
        const string tableHeader = "| Workflow | Date | Agent Role | Model | Input | Output | Cache Read |\n|----------|------|------------|-------|-------|--------|------------|";

        if (content.Contains(sectionHeader, StringComparison.Ordinal))
        {
            // Find end of existing table and insert row before any trailing content
            var insertPoint = content.IndexOf(sectionHeader, StringComparison.Ordinal);
            // Append the new row after the last table row in the section
            // Simple approach: find the section, append the row at end of file or before next ##
            var afterSection = content.IndexOf("\n## ", insertPoint + sectionHeader.Length, StringComparison.Ordinal);
            if (afterSection == -1)
            {
                // Section is at the end of file
                content = content.TrimEnd() + "\n" + newRow + "\n";
            }
            else
            {
                content = content.Substring(0, afterSection).TrimEnd()
                    + "\n" + newRow + "\n"
                    + content.Substring(afterSection);
            }
        }
        else
        {
            // Add new section at end of file
            content = content.TrimEnd()
                + $"\n\n{sectionHeader}\n\n{tableHeader}\n{newRow}\n";
        }

        File.WriteAllText(featureMdPath, content);
    }
}
