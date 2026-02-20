using System.CommandLine;
using GfdTools.Services;

namespace GfdTools.Commands;

public static class FeatureUpdateStatusCommand
{
    private static readonly string[] ValidStatuses =
    [
        "new", "discussing", "discussed", "researching", "researched",
        "planning", "planned", "in-progress", "done"
    ];

    public static Command Create(string cwd)
    {
        var cmd = new Command("feature-update-status") { Description = "Update the status of a feature" };
        var slugArg = new Argument<string>("slug") { Description = "Feature slug" };
        var statusArg = new Argument<string>("status") { Description = "New status" };
        cmd.Add(slugArg);
        cmd.Add(statusArg);

        cmd.SetAction(pr =>
        {
            var slug = pr.GetValue(slugArg)!;
            var newStatus = pr.GetValue(statusArg)!;

            if (!ValidStatuses.Contains(newStatus))
                return Output.Fail($"Invalid status: {newStatus}. Valid: {string.Join(", ", ValidStatuses)}");

            var featureMdPath = Path.Combine(cwd, "docs", "features", slug, "FEATURE.md");
            if (!File.Exists(featureMdPath))
            {
                Output.WriteBool("updated", false);
                Output.Write("error", "Feature not found");
                Output.Write("slug", slug);
                return 1;
            }

            var content = File.ReadAllText(featureMdPath);
            var fm = FrontmatterService.Extract(content);
            var oldStatus = fm.GetString("status") ?? "new";
            fm["status"] = newStatus;
            var newContent = FrontmatterService.Splice(content, fm);
            File.WriteAllText(featureMdPath, newContent);

            Output.WriteBool("updated", true);
            Output.Write("slug", slug);
            Output.Write("old_status", oldStatus);
            Output.Write("new_status", newStatus);
            return 0;
        });

        return cmd;
    }
}
