using System.CommandLine;
using GfdTools.Commands;
using GfdTools.Services;

var rootCommand = new RootCommand("GFD Tools CLI");
var cwd = Directory.GetCurrentDirectory();

// ─── config-get ──────────────────────────────────────────────────────────────
rootCommand.Add(ConfigGetCommand.Create(cwd));

// ─── feature-update-status ───────────────────────────────────────────────────
rootCommand.Add(FeatureUpdateStatusCommand.Create(cwd));

// ─── feature-plan-index ──────────────────────────────────────────────────────
rootCommand.Add(FeaturePlanIndexCommand.Create(cwd));

// ─── list-features ───────────────────────────────────────────────────────────
rootCommand.Add(ListFeaturesCommand.Create(cwd));

// ─── frontmatter ─────────────────────────────────────────────────────────────
rootCommand.Add(FrontmatterCommands.Create(cwd));

// ─── init ────────────────────────────────────────────────────────────────────
rootCommand.Add(InitCommands.Create(cwd));

// ─── verify ──────────────────────────────────────────────────────────────────
rootCommand.Add(VerifyCommands.Create(cwd));

// ─── history-digest ──────────────────────────────────────────────────────────
rootCommand.Add(HistoryDigestCommand.Create(cwd));

// ─── summary-extract ─────────────────────────────────────────────────────────
rootCommand.Add(SummaryExtractCommand.Create(cwd));

// ─── resolve-model ───────────────────────────────────────────────────────────
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

// ─── find-feature ────────────────────────────────────────────────────────────
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

// ─── generate-slug ───────────────────────────────────────────────────────────
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

// ─── current-timestamp ───────────────────────────────────────────────────────
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

// ─── verify-path-exists ──────────────────────────────────────────────────────
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

return await new CommandLineConfiguration(rootCommand).InvokeAsync(args);
