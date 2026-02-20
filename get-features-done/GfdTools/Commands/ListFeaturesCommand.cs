using System.CommandLine;
using GfdTools.Services;

namespace GfdTools.Commands;

public static class ListFeaturesCommand
{
    public static Command Create(string cwd)
    {
        var cmd = new Command("list-features") { Description = "List all features with status" };
        var statusOpt = new Option<string?>("--status") { Description = "Filter by status" };
        cmd.Add(statusOpt);

        cmd.SetAction(pr =>
        {
            var statusFilter = pr.GetValue(statusOpt);
            var features = FeatureService.ListFeatures(cwd);
            var filtered = statusFilter != null
                ? features.Where(f => f.Status == statusFilter).ToList()
                : features;

            Output.Write("count", filtered.Count);
            Output.Write("total", features.Count);

            foreach (var f in filtered)
            {
                Output.Write("feature_slug", f.Slug);
                Output.Write("feature_name", f.Name);
                Output.Write("feature_status", f.Status);
                Output.Write("feature_owner", f.Owner ?? "");
                Output.Write("feature_priority", f.Priority);
            }

            // Status counts
            var allStatuses = new[] { "new", "discussing", "discussed", "researching", "researched", "planning", "planned", "in-progress", "done" };
            foreach (var s in allStatuses)
            {
                Output.Write($"by_status_{s.Replace("-", "_")}", features.Count(f => f.Status == s));
            }

            return 0;
        });

        return cmd;
    }
}
