using System.CommandLine;
using GfdTools.Commands;
using GfdTools.Services;

var rootCommand = new RootCommand("GFD Tools CLI");
var cwd = Directory.GetCurrentDirectory();

// ─── config-get ──────────────────────────────────────────────────────────────
rootCommand.Add(ConfigGetCommand.Create(cwd));

// ─── feature-update-status ────────────────────────────────────────────────────
rootCommand.Add(FeatureUpdateStatusCommand.Create(cwd));

// ─── feature-plan-index ───────────────────────────────────────────────────────
rootCommand.Add(FeaturePlanIndexCommand.Create(cwd));

// ─── list-features ────────────────────────────────────────────────────────────
rootCommand.Add(ListFeaturesCommand.Create(cwd));

// ─── frontmatter ──────────────────────────────────────────────────────────────
rootCommand.Add(FrontmatterCommands.Create(cwd));

// ─── resolve-model ────────────────────────────────────────────────────────────
var resolveModelCmd = new Command("resolve-model") { Description = "Resolve model tier for an agent" };
var resolveModelArg = new Argument<string>("agent-name") { Description = "Agent name (e.g. gfd-executor)" };
resolveModelCmd.Add(resolveModelArg);
resolveModelCmd.SetAction(pr =>
{
    var agent = pr.GetValue(resolveModelArg)!;
    var model = ConfigService.ResolveModel(cwd, agent);
    Output.Write("agent", agent);
    Output.Write("model", model);
    return 0;
});
rootCommand.Add(resolveModelCmd);

// ─── find-feature ─────────────────────────────────────────────────────────────
var findFeatureCmd = new Command("find-feature") { Description = "Find a feature by slug" };
var findFeatureArg = new Argument<string>("slug") { Description = "Feature slug" };
findFeatureCmd.Add(findFeatureArg);
findFeatureCmd.SetAction(pr =>
{
    var slug = pr.GetValue(findFeatureArg)!;
    var info = FeatureService.FindFeature(cwd, slug);
    if (info == null)
    {
        Output.WriteBool("found", false);
        Output.Write("slug", slug);
        return 0;
    }
    Output.WriteBool("found", true);
    Output.Write("slug", info.Slug);
    Output.Write("name", info.Name);
    Output.Write("status", info.Status);
    Output.Write("owner", info.Owner ?? "");
    Output.Write("priority", info.Priority);
    Output.Write("directory", info.Directory);
    Output.Write("plan_count", info.Plans.Count);
    Output.Write("summary_count", info.Summaries.Count);
    Output.Write("incomplete_count", info.IncompletePlans.Count);
    Output.WriteList("plan", info.Plans);
    Output.WriteList("summary", info.Summaries);
    Output.WriteList("incomplete_plan", info.IncompletePlans);
    Output.WriteBool("has_research", info.HasResearch);
    Output.WriteBool("has_verification", info.HasVerification);
    return 0;
});
rootCommand.Add(findFeatureCmd);

// ─── generate-slug ────────────────────────────────────────────────────────────
var generateSlugCmd = new Command("generate-slug") { Description = "Generate a URL-safe slug from text" };
var generateSlugArg = new Argument<string>("text") { Description = "Text to slugify" };
generateSlugCmd.Add(generateSlugArg);
generateSlugCmd.SetAction(pr =>
{
    var text = pr.GetValue(generateSlugArg)!;
    var slug = System.Text.RegularExpressions.Regex.Replace(
        text.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
    Output.Write("slug", slug);
    return 0;
});
rootCommand.Add(generateSlugCmd);

// ─── current-timestamp ────────────────────────────────────────────────────────
var timestampCmd = new Command("current-timestamp") { Description = "Get current timestamp" };
var timestampFmtArg = new Argument<string?>("format") { Description = "Format: date, iso", DefaultValueFactory = _ => null };
timestampCmd.Add(timestampFmtArg);
timestampCmd.SetAction(pr =>
{
    var fmt = pr.GetValue(timestampFmtArg) ?? "iso";
    var now = DateTime.UtcNow;
    var result = fmt == "date" ? now.ToString("yyyy-MM-dd") : now.ToString("yyyy-MM-ddTHH:mm:ssZ");
    Output.Write("timestamp", result);
    Output.Write("format", fmt);
    return 0;
});
rootCommand.Add(timestampCmd);

// ─── verify-path-exists ───────────────────────────────────────────────────────
var verifyPathCmd = new Command("verify-path-exists") { Description = "Check if a path exists" };
var verifyPathArg = new Argument<string>("path") { Description = "Path to check" };
verifyPathCmd.Add(verifyPathArg);
verifyPathCmd.SetAction(pr =>
{
    var targetPath = pr.GetValue(verifyPathArg)!;
    var fullPath = Path.IsPathRooted(targetPath) ? targetPath : Path.Combine(cwd, targetPath);
    var exists = File.Exists(fullPath) || Directory.Exists(fullPath);
    Output.WriteBool("exists", exists);
    Output.Write("path", targetPath);
    return 0;
});
rootCommand.Add(verifyPathCmd);

// ─── history-digest ───────────────────────────────────────────────────────────
var historyDigestCmd = new Command("history-digest") { Description = "Digest summaries across all features" };
historyDigestCmd.SetAction(pr =>
{
    var features = FeatureService.ListFeatures(cwd);
    var summaryCount = 0;
    var decisions = new List<(string feature, string decision)>();
    var techAdded = new HashSet<string>();
    var summaryLines = new List<(string feature, string file, string? oneLiner)>();

    foreach (var feature in features)
    {
        var featureDir = Path.Combine(cwd, feature.Directory);
        foreach (var summaryFile in feature.Summaries)
        {
            var summaryPath = Path.Combine(featureDir, summaryFile);
            if (!File.Exists(summaryPath)) continue;

            var content = File.ReadAllText(summaryPath);
            var fm = FrontmatterService.Extract(content);
            summaryCount++;

            var oneLiner = fm.GetString("one-liner");
            summaryLines.Add((feature.Slug, summaryFile, oneLiner));

            if (fm.TryGetValue("key-decisions", out var kd) && kd is List<string> kdList)
                foreach (var d in kdList)
                    decisions.Add((feature.Slug, d));

            if (fm.TryGetValue("tech-stack", out var ts) && ts is Dictionary<string, object?> tsDict)
            {
                if (tsDict.TryGetValue("added", out var added))
                {
                    if (added is List<string> addedList)
                        foreach (var t in addedList) techAdded.Add(t);
                    else if (added is string addedStr && !string.IsNullOrEmpty(addedStr))
                        techAdded.Add(addedStr);
                }
            }
        }
    }

    Output.Write("summary_count", summaryCount);
    foreach (var (feature, file, oneLiner) in summaryLines)
    {
        Output.Write("summary_feature", feature);
        Output.Write("summary_file", file);
        Output.Write("summary_one_liner", oneLiner ?? "");
    }
    Output.Write("decision_count", decisions.Count);
    foreach (var (feature, decision) in decisions)
    {
        Output.Write("decision_feature", feature);
        Output.Write("decision_text", decision);
    }
    Output.WriteList("tech_added", techAdded);
    return 0;
});
rootCommand.Add(historyDigestCmd);

// ─── summary-extract ──────────────────────────────────────────────────────────
var summaryExtractCmd = new Command("summary-extract") { Description = "Extract fields from a summary file" };
var summaryExtractArg = new Argument<string>("summary-path") { Description = "Path to summary file" };
var summaryExtractFieldsOpt = new Option<string?>("--fields") { Description = "Comma-separated list of fields to extract" };
summaryExtractCmd.Add(summaryExtractArg);
summaryExtractCmd.Add(summaryExtractFieldsOpt);
summaryExtractCmd.SetAction(pr =>
{
    var summaryPath = pr.GetValue(summaryExtractArg)!;
    var fieldsStr = pr.GetValue(summaryExtractFieldsOpt);
    var fields = fieldsStr?.Split(',').Select(f => f.Trim()).ToHashSet();

    var fullPath = Path.IsPathRooted(summaryPath) ? summaryPath : Path.Combine(cwd, summaryPath);
    if (!File.Exists(fullPath))
        return Output.Fail($"File not found: {summaryPath}");

    var content = File.ReadAllText(fullPath);
    var fm = FrontmatterService.Extract(content);

    var oneLiner = fm.GetString("one-liner");
    var keyFiles = fm.GetStringList("key-files");
    var decisions = fm.GetStringList("key-decisions");

    if (fields == null || fields.Contains("one_liner"))
        Output.Write("one_liner", oneLiner ?? "");
    if (fields == null || fields.Contains("decisions"))
        Output.WriteList("decisions", decisions);
    if (fields == null || fields.Contains("key_files"))
        Output.WriteList("key_files", keyFiles);
    Output.Write("path", summaryPath);
    return 0;
});
rootCommand.Add(summaryExtractCmd);

// ─── init ─────────────────────────────────────────────────────────────────────
rootCommand.Add(InitCommands.Create(cwd));

// ─── verify ───────────────────────────────────────────────────────────────────
var verifyCmd = new Command("verify") { Description = "Run verification checks" };
rootCommand.Add(verifyCmd);

// verify plan-structure (Plan 02)
var verifyPlanStructureCmd = new Command("plan-structure") { Description = "Verify plan file structure" };
var verifyPlanStructureArg = new Argument<string>("file") { Description = "Plan file path" };
verifyPlanStructureCmd.Add(verifyPlanStructureArg);
verifyPlanStructureCmd.SetAction(pr => Output.Fail("not yet implemented"));
verifyCmd.Add(verifyPlanStructureCmd);

// verify references (Plan 02)
var verifyReferencesCmd = new Command("references") { Description = "Verify @-references in a file" };
var verifyReferencesArg = new Argument<string>("file") { Description = "File path" };
verifyReferencesCmd.Add(verifyReferencesArg);
verifyReferencesCmd.SetAction(pr => Output.Fail("not yet implemented"));
verifyCmd.Add(verifyReferencesCmd);

// verify commits (Plan 02)
var verifyCommitsCmd = new Command("commits") { Description = "Verify commit hashes exist" };
var verifyCommitsArg = new Argument<string[]>("hashes") { Description = "Commit hashes to verify" };
verifyCommitsCmd.Add(verifyCommitsArg);
verifyCommitsCmd.SetAction(pr => Output.Fail("not yet implemented"));
verifyCmd.Add(verifyCommitsCmd);

// ─── validate ─────────────────────────────────────────────────────────────────
var validateCmd = new Command("validate") { Description = "Validation commands" };
rootCommand.Add(validateCmd);

var validateHealthCmd = new Command("health") { Description = "Validate project health" };
var validateHealthRepairOpt = new Option<bool>("--repair") { Description = "Attempt to repair issues" };
validateHealthCmd.Add(validateHealthRepairOpt);
validateHealthCmd.SetAction(pr => Output.Fail("not yet implemented"));
validateCmd.Add(validateHealthCmd);

// ─── verify-summary ───────────────────────────────────────────────────────────
var verifySummaryCmd = new Command("verify-summary") { Description = "Verify a summary file" };
var verifySummaryArg = new Argument<string>("summary-path") { Description = "Path to summary file" };
var verifySummaryCountOpt = new Option<int>("--check-count") { Description = "Minimum task section count", DefaultValueFactory = _ => 2 };
verifySummaryCmd.Add(verifySummaryArg);
verifySummaryCmd.Add(verifySummaryCountOpt);
verifySummaryCmd.SetAction(pr => Output.Fail("not yet implemented"));
rootCommand.Add(verifySummaryCmd);

// ─── config-set ───────────────────────────────────────────────────────────────
var configSetCmd = new Command("config-set") { Description = "Set a config value" };
var configSetKeyArg = new Argument<string>("key") { Description = "Config key" };
var configSetValueArg = new Argument<string>("value") { Description = "Config value" };
configSetCmd.Add(configSetKeyArg);
configSetCmd.Add(configSetValueArg);
configSetCmd.SetAction(pr => Output.Fail("not yet implemented"));
rootCommand.Add(configSetCmd);

// ─── state ────────────────────────────────────────────────────────────────────
var stateCmd = new Command("state") { Description = "State management commands" };
rootCommand.Add(stateCmd);

var stateAdvancePlanCmd = new Command("advance-plan") { Description = "Advance the current plan index" };
stateAdvancePlanCmd.SetAction(pr => Output.Fail("not yet implemented"));
stateCmd.Add(stateAdvancePlanCmd);

var stateUpdateProgressCmd = new Command("update-progress") { Description = "Recalculate progress from disk" };
stateUpdateProgressCmd.SetAction(pr => Output.Fail("not yet implemented"));
stateCmd.Add(stateUpdateProgressCmd);

var stateRecordMetricCmd = new Command("record-metric") { Description = "Record execution metrics" };
stateRecordMetricCmd.SetAction(pr => Output.Fail("not yet implemented"));
stateCmd.Add(stateRecordMetricCmd);

var stateRecordSessionCmd = new Command("record-session") { Description = "Record session info" };
stateRecordSessionCmd.SetAction(pr => Output.Fail("not yet implemented"));
stateCmd.Add(stateRecordSessionCmd);

return await new CommandLineConfiguration(rootCommand).InvokeAsync(args);
