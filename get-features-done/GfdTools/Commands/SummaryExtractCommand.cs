using System.CommandLine;
using GfdTools.Services;

namespace GfdTools.Commands;

public static class SummaryExtractCommand
{
    public static Command Create(string cwd)
    {
        var cmd = new Command("summary-extract") { Description = "Extract fields from a summary file" };
        var summaryPathArg = new Argument<string>("summary-path") { Description = "Path to summary file" };
        var fieldsOpt = new Option<string?>("--fields") { Description = "Comma-separated list of fields to extract" };
        cmd.Add(summaryPathArg);
        cmd.Add(fieldsOpt);

        cmd.SetAction(pr =>
        {
            var summaryPath = pr.GetValue(summaryPathArg)!;
            var fieldsStr = pr.GetValue(fieldsOpt);
            var fields = fieldsStr?.Split(',').Select(f => f.Trim()).ToHashSet();

            var fullPath = Path.IsPathRooted(summaryPath) ? summaryPath : Path.Combine(cwd, summaryPath);
            if (!File.Exists(fullPath))
            {
                Console.Error.WriteLine($"File not found: {summaryPath}");
                return 1;
            }

            var content = File.ReadAllText(fullPath);
            var fm = FrontmatterService.Extract(content);

            var oneLiner = fm.GetString("one-liner");
            var keyFiles = fm.GetStringList("key-files");
            var techAdded = GetTechAdded(fm);
            var decisions = fm.GetStringList("key-decisions");

            if (fields == null || fields.Contains("one_liner"))
                Output.Write("one_liner", oneLiner ?? "");
            if (fields == null || fields.Contains("decisions"))
                Output.WriteList("decisions", decisions);
            if (fields == null || fields.Contains("key_files"))
                Output.WriteList("key_files", keyFiles);
            if (fields == null || fields.Contains("tech_added"))
                Output.WriteList("tech_added", techAdded);
            Output.Write("path", summaryPath);
            return 0;
        });

        return cmd;
    }

    private static List<string> GetTechAdded(Dictionary<string, object?> fm)
    {
        if (!fm.TryGetValue("tech-stack", out var ts)) return [];
        if (ts is not Dictionary<string, object?> tsDict) return [];
        if (!tsDict.TryGetValue("added", out var added)) return [];

        if (added is List<string> list) return list;
        if (added is string s && !string.IsNullOrEmpty(s)) return [s];
        return [];
    }
}
