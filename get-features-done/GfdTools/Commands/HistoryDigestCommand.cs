using System.CommandLine;
using GfdTools.Services;

namespace GfdTools.Commands;

public static class HistoryDigestCommand
{
    public static Command Create(string cwd)
    {
        var cmd = new Command("history-digest") { Description = "Digest summaries across all features" };

        cmd.SetAction(pr =>
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

        return cmd;
    }
}
