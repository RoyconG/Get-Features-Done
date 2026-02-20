using GfdTools.Models;

namespace GfdTools.Services;

public static class FeatureService
{
    private static readonly Dictionary<string, int> PriorityOrder = new()
    {
        ["critical"] = -1,
        ["high"] = 0,
        ["medium"] = 1,
        ["low"] = 2,
    };

    private static readonly Dictionary<string, int> StatusOrder = new()
    {
        ["in-progress"] = 0,
        ["planned"] = 1,
        ["planning"] = 2,
        ["researched"] = 3,
        ["researching"] = 4,
        ["discussed"] = 5,
        ["discussing"] = 6,
        ["new"] = 7,
        ["done"] = 8,
    };

    public static FeatureInfo? FindFeature(string cwd, string slug)
    {
        if (string.IsNullOrEmpty(slug))
            return null;

        var featureDir = Path.Combine(cwd, "docs", "features", slug);
        var featureMd = Path.Combine(featureDir, "FEATURE.md");

        if (!File.Exists(featureMd))
            return null;

        var content = File.ReadAllText(featureMd);
        var fm = FrontmatterService.Extract(content);

        string[] files;
        try
        {
            files = Directory.GetFiles(featureDir).Select(Path.GetFileName).Where(f => f != null).Select(f => f!).ToArray();
        }
        catch
        {
            files = [];
        }

        var plans = files.Where(f => f.EndsWith("-PLAN.md", StringComparison.OrdinalIgnoreCase))
                         .Order().ToList();
        var summaries = files.Where(f => f.EndsWith("-SUMMARY.md", StringComparison.OrdinalIgnoreCase))
                             .Order().ToList();
        var hasResearch = files.Any(f => f == "RESEARCH.md" ||
                                        f.EndsWith("-RESEARCH.md", StringComparison.OrdinalIgnoreCase));
        var hasVerification = files.Any(f => f == "VERIFICATION.md" ||
                                             f.EndsWith("-VERIFICATION.md", StringComparison.OrdinalIgnoreCase));

        var planIds = plans.Select(p => FilenameHelper.StripSuffix(p, "-PLAN.md")).ToList();
        var summaryIds = new HashSet<string>(
            summaries.Select(s => FilenameHelper.StripSuffix(s, "-SUMMARY.md")),
            StringComparer.OrdinalIgnoreCase);
        var incompletePlans = planIds.Where(id => !summaryIds.Contains(id)).ToList();

        return new FeatureInfo
        {
            Found = true,
            Slug = slug,
            Name = fm.GetString("name") ?? slug,
            Status = fm.GetString("status") ?? "new",
            Owner = fm.GetString("owner"),
            Assignees = fm.GetStringList("assignees"),
            Priority = fm.GetString("priority") ?? "medium",
            DependsOn = fm.GetStringList("depends_on"),
            Directory = Path.Combine("docs", "features", slug),
            FeatureMd = Path.Combine("docs", "features", slug, "FEATURE.md"),
            Plans = plans,
            Summaries = summaries,
            IncompletePlans = incompletePlans,
            HasResearch = hasResearch,
            HasVerification = hasVerification,
            Frontmatter = fm,
        };
    }

    public static List<FeatureInfo> ListFeatures(string cwd)
    {
        var featuresDir = Path.Combine(cwd, "docs", "features");
        var features = new List<FeatureInfo>();

        if (!Directory.Exists(featuresDir))
            return features;

        IEnumerable<string> entries;
        try
        {
            entries = Directory.GetDirectories(featuresDir);
        }
        catch
        {
            return features;
        }

        foreach (var entry in entries)
        {
            var name = Path.GetFileName(entry);
            if (name == "codebase") continue;

            var info = FindFeature(cwd, name);
            if (info != null)
                features.Add(info);
        }

        features.Sort((a, b) =>
        {
            var aPri = PriorityOrder.TryGetValue(a.Priority, out var ap) ? ap : 1;
            var bPri = PriorityOrder.TryGetValue(b.Priority, out var bp) ? bp : 1;
            var priDiff = aPri - bPri;
            if (priDiff != 0) return priDiff;

            var aStat = StatusOrder.TryGetValue(a.Status, out var astat) ? astat : 3;
            var bStat = StatusOrder.TryGetValue(b.Status, out var bstat) ? bstat : 3;
            return aStat - bStat;
        });

        return features;
    }
}

/// <summary>
/// Extension methods for frontmatter dictionary access.
/// </summary>
public static class FrontmatterExtensions
{
    public static string? GetString(this Dictionary<string, object?> fm, string key)
    {
        return fm.TryGetValue(key, out var v) ? v?.ToString() : null;
    }

    public static List<string> GetStringList(this Dictionary<string, object?> fm, string key)
    {
        if (!fm.TryGetValue(key, out var v)) return [];
        if (v is List<string> list) return list;
        if (v is string s && !string.IsNullOrEmpty(s)) return [s];
        return [];
    }
}

/// <summary>
/// Helper to strip suffixes from filenames.
/// </summary>
internal static class FilenameHelper
{
    public static string StripSuffix(string s, string suffix)
    {
        return s.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? s[..^suffix.Length]
            : s;
    }
}
