---
feature: review-token-usage
plan: 2
type: execute
wave: 1
depends_on: []
files_modified:
  - get-features-done/GfdTools/Services/ClaudeService.cs
  - get-features-done/GfdTools/Commands/AutoResearchCommand.cs
  - get-features-done/GfdTools/Commands/AutoPlanCommand.cs
autonomous: true
acceptance_criteria:
  - "Token usage summary (per agent role) displayed at the end of each major workflow (research, plan, execute)"
  - "Cumulative ## Token Usage section maintained in FEATURE.md across workflow runs"
must_haves:
  truths:
    - "AUTO-RUN.md after a successful auto-research or auto-plan includes total cost in USD"
    - "FEATURE.md gets a new row in a ## Token Usage table after each successful headless auto-research or auto-plan run"
    - "Success detection still works correctly after switching to stream-json (checks result field content, not raw stdout)"
    - "auto-research and auto-plan still detect abort conditions (max-turns, ambiguous) correctly"
  artifacts:
    - path: "get-features-done/GfdTools/Services/ClaudeService.cs"
      provides: "stream-json output with token data extraction"
      contains: "stream-json"
    - path: "get-features-done/GfdTools/Commands/AutoResearchCommand.cs"
      provides: "Token row appended to FEATURE.md after successful research"
      contains: "Token Usage"
    - path: "get-features-done/GfdTools/Commands/AutoPlanCommand.cs"
      provides: "Token row appended to FEATURE.md after successful planning"
      contains: "Token Usage"
  key_links:
    - from: "ClaudeService.InvokeHeadless"
      to: "SDKResultMessage.total_cost_usd in stream-json result line"
      via: "--output-format stream-json + JSON parse of last result line"
      pattern: "stream-json"
    - from: "AutoResearchCommand.RunAsync (success path)"
      to: "docs/features/<slug>/FEATURE.md ## Token Usage section"
      via: "AppendTokenUsageToFeatureMd static method"
      pattern: "Token Usage"
---

<objective>
Switch ClaudeService headless invocation to stream-json output format to capture real token/cost data, then write that data to FEATURE.md and AUTO-RUN.md after each auto-research and auto-plan run.

Purpose: Provides exact cost visibility for headless workflow runs. Interactive workflow token tracking (Plan 04) handles the human-driven paths.

Output: ClaudeService captures token data; AutoResearchCommand and AutoPlanCommand write cumulative token rows to FEATURE.md.
</objective>

<execution_context>
@$HOME/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/review-token-usage/FEATURE.md
@docs/features/review-token-usage/RESEARCH.md
@docs/features/PROJECT.md
@get-features-done/GfdTools/Services/ClaudeService.cs
@get-features-done/GfdTools/Commands/AutoResearchCommand.cs
@get-features-done/GfdTools/Commands/AutoPlanCommand.cs
</context>

<tasks>

<task type="auto">
  <name>Task 1: Switch ClaudeService to stream-json and capture token data</name>
  <files>
    get-features-done/GfdTools/Services/ClaudeService.cs
  </files>
  <action>
    **Extend RunResult record** (lines 5-12) — add four optional token fields after AbortReason:
    ```csharp
    public record RunResult(
        bool Success,
        string Stdout,
        string Stderr,
        int ExitCode,
        double DurationSeconds,
        string AbortReason,
        double TotalCostUsd = 0,
        int InputTokens = 0,
        int OutputTokens = 0,
        int CacheReadTokens = 0
    );
    ```

    **Switch output format** (line 54) — Change `"text"` to `"stream-json"`:
    ```csharp
    psi.ArgumentList.Add("--output-format");
    psi.ArgumentList.Add("stream-json");
    ```

    **Add JSON result line parsing** — After `var stderr = await stderrTask;` (line 71) and before the durationSeconds calculation, insert:
    ```csharp
    // With stream-json, each stdout line is a JSON object.
    // The final "result" type line contains the agent's text output and token data.
    string resultText = "";
    double totalCostUsd = 0;
    int inputTokens = 0, outputTokens = 0, cacheReadTokens = 0;

    var resultLine = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .LastOrDefault(l => l.TrimStart().StartsWith("{") && l.Contains("\"type\":\"result\""));

    if (resultLine != null)
    {
        try
        {
            using var resultDoc = JsonDocument.Parse(resultLine);
            var root = resultDoc.RootElement;
            if (root.TryGetProperty("result", out var resultProp))
                resultText = resultProp.GetString() ?? "";
            if (root.TryGetProperty("total_cost_usd", out var costProp))
                totalCostUsd = costProp.GetDouble();
            if (root.TryGetProperty("usage", out var usageProp))
            {
                if (usageProp.TryGetProperty("input_tokens", out var inp))
                    inputTokens = inp.GetInt32();
                if (usageProp.TryGetProperty("output_tokens", out var outp))
                    outputTokens = outp.GetInt32();
                if (usageProp.TryGetProperty("cache_read_input_tokens", out var cache))
                    cacheReadTokens = cache.GetInt32();
            }
        }
        catch { /* Parsing failure: token data unavailable, proceed without it */ }
    }
    ```

    **Update success detection** (lines 77-78) — Replace `stdout.Contains(...)` checks with `resultText.Contains(...)`:
    ```csharp
    bool success = resultText.Contains("## RESEARCH COMPLETE", StringComparison.Ordinal)
                || resultText.Contains("## PLANNING COMPLETE", StringComparison.Ordinal);
    ```

    **Update abort detection** — `isMaxTurns` stays on `stderr` (fine). For `isAmbiguous`, check both `stdout` (for JSON objects containing the signals) and `resultText`:
    ```csharp
    bool isAmbiguous = stdout.Contains("AskUserQuestion", StringComparison.Ordinal)
                    || stdout.Contains("## CHECKPOINT", StringComparison.Ordinal)
                    || resultText.Contains("AskUserQuestion", StringComparison.Ordinal)
                    || resultText.Contains("## CHECKPOINT", StringComparison.Ordinal);
    ```

    **Update return statement** (line 102) — Include token data:
    ```csharp
    return new RunResult(success, stdout, stderr, process.ExitCode, durationSeconds, abortReason,
        totalCostUsd, inputTokens, outputTokens, cacheReadTokens);
    ```

    **Update BuildAutoRunMd()** — Add cost line after Duration when cost is available. In the template string (line 136+), change:
    ```csharp
    **Duration:** {result.DurationSeconds:F1}s
    ```
    to:
    ```csharp
    **Duration:** {result.DurationSeconds:F1}s
    {(result.TotalCostUsd > 0 ? $"**Cost:** ${result.TotalCostUsd:F4}" : "")}
    ```
    (Use string interpolation inline or a local variable — avoid a bare empty line if cost is 0.)

    **Rebuild:**
    ```bash
    dotnet build get-features-done/GfdTools/GfdTools.csproj -c Release --nologo -q
    ```
  </action>
  <verify>
    ```bash
    dotnet build get-features-done/GfdTools/GfdTools.csproj -c Release --nologo -q
    # Exit 0 = build success
    grep "stream-json" get-features-done/GfdTools/Services/ClaudeService.cs
    # Confirms format was changed
    grep "TotalCostUsd" get-features-done/GfdTools/Services/ClaudeService.cs
    # Confirms RunResult record was extended
    grep "resultText" get-features-done/GfdTools/Services/ClaudeService.cs
    # Confirms success detection uses resultText
    ```
  </verify>
  <done>
    Build succeeds. ClaudeService uses --output-format stream-json. RunResult has TotalCostUsd, InputTokens, OutputTokens, CacheReadTokens fields. Success detection uses the parsed result field content, not raw stdout.
  </done>
</task>

<task type="auto">
  <name>Task 2: Append token usage rows to FEATURE.md from auto-research and auto-plan</name>
  <files>
    get-features-done/GfdTools/Commands/AutoResearchCommand.cs
    get-features-done/GfdTools/Commands/AutoPlanCommand.cs
  </files>
  <action>
    Add a private static helper method `AppendTokenUsageToFeatureMd` to both command files (or place in a shared static class — adding to each command file is simpler since there is no shared utility file for commands).

    **Helper method signature and implementation:**
    ```csharp
    private static void AppendTokenUsageToFeatureMd(
        string featureMdPath,
        string workflow,   // "research" or "plan"
        string agentRole,  // "gfd-researcher" or "gfd-planner"
        string model,      // resolved model tier (e.g. "sonnet")
        double totalCostUsd)
    {
        if (!File.Exists(featureMdPath)) return;

        var content = File.ReadAllText(featureMdPath);
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var costStr = totalCostUsd > 0 ? $"${totalCostUsd:F4}" : "est.";
        var newRow = $"| {workflow} | {date} | {agentRole} | {model} | {costStr} |";

        const string sectionHeader = "## Token Usage";
        const string tableHeader = "| Workflow | Date | Agent Role | Model | Cost |\n|----------|------|------------|-------|------|";

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
    ```

    **In AutoResearchCommand.RunAsync()** — After the `Output.Write("duration_seconds", ...)` call in the success path (Step 7, around line 126), before `return 0`, add:
    ```csharp
    // Append token usage to FEATURE.md
    var resolvedModel = ConfigService.ResolveModel(cwd, "gfd-researcher");
    AppendTokenUsageToFeatureMd(featureMdPath, "research", "gfd-researcher",
        resolvedModel, result.TotalCostUsd);
    // Re-add FEATURE.md to the commit — it was already staged in Step 6's status update
    // (FEATURE.md status was updated to "researched" and written; token row is an additional edit)
    // Stage the additional change:
    GitService.ExecGit(cwd, ["add", featureMdPath]);
    GitService.ExecGit(cwd, ["commit", "--amend", "--no-edit"]);
    ```
    Wait — amending the commit is risky. Instead, add the token row BEFORE the CommitAutoRunMd call:
    - Move the AppendTokenUsageToFeatureMd call to BEFORE Step 6's CommitAutoRunMd call.
    - In the success path: (1) update status to "researched", (2) append token row, (3) call CommitAutoRunMd with FEATURE.md in artifactsProduced.

    Revised placement in AutoResearchCommand: Between the status update (Step 7 success block, currently lines 117-122) and the Output.Write calls:
    ```csharp
    if (result.Success)
    {
        // Update status
        var featureMd = Path.Combine(cwd, featureInfo.Directory, "FEATURE.md");
        var featureContent = File.ReadAllText(featureMd);
        featureContent = Regex.Replace(featureContent, @"^status:\s*.+$", "status: researched",
            RegexOptions.Multiline);
        File.WriteAllText(featureMd, featureContent);

        // Append token usage row
        var resolvedModel = ConfigService.ResolveModel(cwd, "gfd-researcher");
        AppendTokenUsageToFeatureMd(featureMd, "research", "gfd-researcher",
            resolvedModel, result.TotalCostUsd);

        // NOTE: CommitAutoRunMd already happens in Step 6 above with FEATURE.md included.
        // Since we edited FEATURE.md after the commit... we need to stage it again.
        // Better: do all FEATURE.md edits BEFORE the commit. Restructure so token append
        // happens in the success path BEFORE CommitAutoRunMd is called.
    ```

    **Actual implementation approach** — restructure the success path so all FEATURE.md edits happen before the commit:

    In AutoResearchCommand, Step 6 currently builds autoRunMd and calls CommitAutoRunMd. In the success path, FEATURE.md is currently committed inside CommitAutoRunMd via `artifactsProduced`. Restructure:
    1. Do the status update in FEATURE.md (currently Step 7)
    2. Append token row to FEATURE.md
    3. Then call CommitAutoRunMd with FEATURE.md in the artifacts list (move this to Step 6)

    Look at the current code: Step 7 does the status update and output writes, and Step 6 does the commit. The commit currently does NOT include FEATURE.md for auto-research (Step 7 happens after Step 6). So update the code to:
    - In the success path after InvokeHeadless returns:
      a. Update status in FEATURE.md
      b. Append token row to FEATURE.md
      c. Call CommitAutoRunMd with `artifactsProduced.Append("FEATURE.md").ToArray()`
    - Keep Output.Write calls after the commit.

    Apply the same restructuring pattern to **AutoPlanCommand** (workflow="plan", agentRole="gfd-planner").
    Note: AutoPlanCommand's success path ALREADY includes FEATURE.md in the commit (line 143: `artifactsProduced.Append("FEATURE.md")`). Add the token row append BEFORE that commit call.

    **Rebuild:**
    ```bash
    dotnet build get-features-done/GfdTools/GfdTools.csproj -c Release --nologo -q
    ```
  </action>
  <verify>
    ```bash
    dotnet build get-features-done/GfdTools/GfdTools.csproj -c Release --nologo -q
    # Exit 0
    grep "Token Usage" get-features-done/GfdTools/Commands/AutoResearchCommand.cs
    grep "Token Usage" get-features-done/GfdTools/Commands/AutoPlanCommand.cs
    grep "AppendTokenUsageToFeatureMd" get-features-done/GfdTools/Commands/AutoResearchCommand.cs
    grep "AppendTokenUsageToFeatureMd" get-features-done/GfdTools/Commands/AutoPlanCommand.cs
    ```
  </verify>
  <done>
    Build succeeds. Both AutoResearchCommand and AutoPlanCommand call AppendTokenUsageToFeatureMd on the success path. The helper correctly writes or updates the ## Token Usage section in FEATURE.md. FEATURE.md with the token row is included in the git commit.
  </done>
</task>

</tasks>

<verification>
After both tasks complete:
1. `dotnet build get-features-done/GfdTools/GfdTools.csproj -c Release --nologo -q` exits 0
2. `grep "stream-json" get-features-done/GfdTools/Services/ClaudeService.cs` matches
3. `grep "TotalCostUsd" get-features-done/GfdTools/Services/ClaudeService.cs` matches
4. `grep "Token Usage" get-features-done/GfdTools/Commands/AutoResearchCommand.cs` matches
5. `grep "Token Usage" get-features-done/GfdTools/Commands/AutoPlanCommand.cs` matches
</verification>

<success_criteria>
- ClaudeService uses --output-format stream-json
- RunResult has TotalCostUsd, InputTokens, OutputTokens, CacheReadTokens
- Success detection uses resultText (parsed from JSON result line), not raw stdout
- AUTO-RUN.md includes Cost line when TotalCostUsd > 0
- AutoResearchCommand appends research token row to FEATURE.md on success
- AutoPlanCommand appends plan token row to FEATURE.md on success
- GfdTools builds with 0 errors
</success_criteria>

<output>
After completion, create `docs/features/review-token-usage/02-SUMMARY.md` following the summary template.
</output>
