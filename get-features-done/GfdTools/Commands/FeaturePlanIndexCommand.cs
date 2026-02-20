using System.CommandLine;
using GfdTools.Services;

namespace GfdTools.Commands;

public static class FeaturePlanIndexCommand
{
    public static Command Create(string cwd)
    {
        var cmd = new Command("feature-plan-index") { Description = "List plans for a feature with status" };
        var slugArg = new Argument<string>("slug") { Description = "Feature slug" };
        cmd.Add(slugArg);

        cmd.SetAction(pr =>
        {
            var slug = pr.GetValue(slugArg)!;
            var info = FeatureService.FindFeature(cwd, slug);

            if (info == null)
            {
                Output.Write("error", "Feature not found");
                Output.Write("slug", slug);
                return 1;
            }

            var featureDir = Path.Combine(cwd, info.Directory);
            var indexed = new List<PlanEntry>();

            foreach (var planFile in info.Plans)
            {
                var planPath = Path.Combine(featureDir, planFile);
                if (!File.Exists(planPath)) continue;

                var content = File.ReadAllText(planPath);
                var fm = FrontmatterService.Extract(content);
                var planId = FilenameHelper.StripSuffix(planFile, "-PLAN.md");
                var hasSummary = info.Summaries.Any(s =>
                    FilenameHelper.StripSuffix(s, "-SUMMARY.md").Equals(planId, StringComparison.OrdinalIgnoreCase));

                var wave = fm.TryGetValue("wave", out var waveVal) && waveVal is int wi ? wi : 1;
                var autonomous = !fm.TryGetValue("autonomous", out var autoVal) || autoVal is not bool b || b;

                indexed.Add(new PlanEntry(
                    Id: planId,
                    File: planFile,
                    Type: fm.GetString("type") ?? "execute",
                    Wave: wave,
                    Autonomous: autonomous,
                    Status: hasSummary ? "complete" : "pending"
                ));
            }

            // Sort by wave then by plan id
            indexed = [.. indexed.OrderBy(p => p.Wave).ThenBy(p => p.Id)];

            Output.Write("slug", slug);
            Output.Write("plan_count", indexed.Count);
            Output.Write("complete_count", indexed.Count(p => p.Status == "complete"));

            foreach (var plan in indexed)
            {
                Output.Write("plan_id", plan.Id);
                Output.Write("plan_file", plan.File);
                Output.Write("plan_type", plan.Type);
                Output.Write("plan_wave", plan.Wave);
                Output.Write("plan_status", plan.Status);
                Output.WriteBool("plan_autonomous", plan.Autonomous);
            }

            // Wave summary
            var waves = indexed.GroupBy(p => p.Wave).OrderBy(g => g.Key);
            foreach (var wave in waves)
            {
                Output.Write("wave_id", wave.Key);
                Output.Write("wave_plan_count", wave.Count());
                Output.Write("wave_complete_count", wave.Count(p => p.Status == "complete"));
            }

            return 0;
        });

        return cmd;
    }

    private record PlanEntry(string Id, string File, string Type, int Wave, bool Autonomous, string Status);
}
