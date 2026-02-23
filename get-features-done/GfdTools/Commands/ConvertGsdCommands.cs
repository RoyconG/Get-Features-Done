using System.CommandLine;
using System.Text.Json;
using System.Text.RegularExpressions;
using GfdTools.Services;

namespace GfdTools.Commands;

public static class ConvertGsdCommands
{
    public static Command Create(string cwd)
    {
        var cmd = new Command("convert-gsd") { Description = "GSD to GFD migration commands" };

        cmd.Add(CreateScan(cwd));
        cmd.Add(CreateExecute(cwd));
        cmd.Add(CreateVerify(cwd));

        return cmd;
    }

    // ─── convert-gsd scan ───────────────────────────────────────────────────
    // Scans .planning/ for phase directories, reads ROADMAP.md, and outputs
    // a complete JSON mapping array with status, goals, criteria, dependencies.

    private static Command CreateScan(string cwd)
    {
        var cmd = new Command("scan") { Description = "Scan GSD phases and build migration mapping" };

        cmd.SetAction(_ =>
        {
            var planningDir = Path.Combine(cwd, ".planning");
            if (!Directory.Exists(planningDir))
            {
                Console.Error.WriteLine(".planning/ directory not found");
                return 1;
            }

            // Scan active phases
            var phases = ScanPhaseDirs(Path.Combine(planningDir, "phases"), false);

            // Scan archived phases in milestones
            var milestonesDir = Path.Combine(planningDir, "milestones");
            if (Directory.Exists(milestonesDir))
            {
                foreach (var milestoneEntry in Directory.GetDirectories(milestonesDir))
                {
                    var phasesSubdir = Path.Combine(milestoneEntry, "phases");
                    phases.AddRange(ScanPhaseDirs(phasesSubdir, true));
                }
            }

            phases.Sort((a, b) => a.PhaseNum.CompareTo(b.PhaseNum));

            // Read ROADMAP.md for goal/criteria/dependency extraction
            var roadmapPath = Path.Combine(planningDir, "ROADMAP.md");
            var roadmap = File.Exists(roadmapPath) ? File.ReadAllText(roadmapPath) : "";

            // Build mapping
            var mapping = new List<Dictionary<string, object?>>();
            foreach (var p in phases)
            {
                var slug = Regex.Replace(p.PhaseName.ToLowerInvariant(), @"[^a-z0-9-]", "-");
                slug = Regex.Replace(slug, @"-+", "-").Trim('-');

                var goal = ExtractPhaseGoal(roadmap, p.PhaseNum);
                var status = DetectStatus(p.DirPath, p.Archived, !string.IsNullOrEmpty(goal));
                var criteria = ExtractSuccessCriteria(roadmap, p.PhaseNum);
                var dependsOnNums = ExtractDependsOn(roadmap, p.PhaseNum);

                mapping.Add(new Dictionary<string, object?>
                {
                    ["phaseDir"] = p.DirName,
                    ["dirPath"] = p.DirPath,
                    ["phaseNum"] = p.PhaseNum,
                    ["phaseName"] = p.PhaseName,
                    ["suggestedSlug"] = slug,
                    ["gfdStatus"] = status,
                    ["goal"] = goal,
                    ["criteria"] = criteria,
                    ["dependsOnPhaseNums"] = dependsOnNums,
                    ["archived"] = p.Archived,
                });
            }

            Console.Write(JsonSerializer.Serialize(mapping));
            return 0;
        });

        return cmd;
    }

    // ─── convert-gsd execute ────────────────────────────────────────────────
    // Reads accepted mappings JSON from stdin. Creates feature directories,
    // writes FEATURE.md, copies/renames artifacts, updates frontmatter, updates tasks.

    private static Command CreateExecute(string cwd)
    {
        var cmd = new Command("execute") { Description = "Execute GSD migration from accepted mappings (JSON on stdin)" };

        cmd.SetAction(_ =>
        {
            var json = Console.In.ReadToEnd();
            List<Dictionary<string, JsonElement>>? mappings;
            try
            {
                mappings = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json);
            }
            catch
            {
                Console.Error.WriteLine("Invalid JSON on stdin");
                return 1;
            }

            if (mappings == null || mappings.Count == 0)
            {
                Console.Error.WriteLine("No mappings provided");
                return 1;
            }

            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var gitUser = GetGitUser(cwd);

            foreach (var m in mappings)
            {
                var slug = m["slug"].GetString()!;
                var phaseDir = m.TryGetValue("phaseDir", out var pd) ? pd.GetString()! : "";
                var dirPath = m.TryGetValue("dirPath", out var dp) ? dp.GetString()! : "";
                var gfdStatus = m.TryGetValue("gfdStatus", out var gs) ? gs.GetString()! : "new";
                var goal = m.TryGetValue("goal", out var g) ? g.GetString() ?? "" : "";
                var criteria = m.TryGetValue("criteria", out var c) && c.ValueKind == JsonValueKind.Array
                    ? c.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
                    : new List<string>();
                var dependsOnSlugs = m.TryGetValue("dependsOnSlugs", out var ds) && ds.ValueKind == JsonValueKind.Array
                    ? ds.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
                    : new List<string>();

                var featureDir = Path.Combine(cwd, "docs", "features", slug);
                Directory.CreateDirectory(featureDir);

                // Build human-readable name from slug
                var name = string.Join(' ', slug.Split('-').Select(w =>
                    w.Length > 0 ? char.ToUpper(w[0]) + w[1..] : w));

                // Write FEATURE.md
                WriteFeatureMd(featureDir, slug, name, gfdStatus, gitUser, today,
                    goal, phaseDir, criteria, dependsOnSlugs);

                Output.Write("created", Path.Combine("docs", "features", slug, "FEATURE.md"));

                // Migrate artifacts from GSD phase directory
                if (!string.IsNullOrEmpty(dirPath) && Directory.Exists(dirPath))
                {
                    MigrateArtifacts(dirPath, featureDir, phaseDir);
                    UpdateMigratedFrontmatter(featureDir, slug);
                    UpdateTasksSection(featureDir);
                }
            }

            Output.Write("migrated_count", mappings.Count);
            return 0;
        });

        return cmd;
    }

    // ─── convert-gsd verify ─────────────────────────────────────────────────
    // Reads accepted mappings JSON from stdin. Checks all FEATURE.md files exist.

    private static Command CreateVerify(string cwd)
    {
        var cmd = new Command("verify") { Description = "Verify GSD migration completeness (JSON on stdin)" };

        cmd.SetAction(_ =>
        {
            var json = Console.In.ReadToEnd();
            List<Dictionary<string, JsonElement>>? mappings;
            try
            {
                mappings = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json);
            }
            catch
            {
                Console.Error.WriteLine("Invalid JSON on stdin");
                return 1;
            }

            if (mappings == null || mappings.Count == 0)
            {
                Console.Error.WriteLine("No mappings provided");
                return 1;
            }

            var missing = new List<string>();
            foreach (var m in mappings)
            {
                var slug = m["slug"].GetString()!;
                var featurePath = Path.Combine(cwd, "docs", "features", slug, "FEATURE.md");
                if (!File.Exists(featurePath))
                    missing.Add(Path.Combine("docs", "features", slug, "FEATURE.md"));
            }

            Output.WriteBool("all_present", missing.Count == 0);
            Output.Write("total", mappings.Count);
            Output.Write("missing_count", missing.Count);
            Output.WriteList("missing", missing);
            return missing.Count > 0 ? 1 : 0;
        });

        return cmd;
    }

    // ─── Phase scanning helpers ─────────────────────────────────────────────

    private record PhaseEntry(string DirName, string DirPath, double PhaseNum, string PhaseName, bool Archived);

    private static List<PhaseEntry> ScanPhaseDirs(string dir, bool archived)
    {
        var results = new List<PhaseEntry>();
        if (!Directory.Exists(dir)) return results;

        foreach (var entry in Directory.GetDirectories(dir))
        {
            var name = Path.GetFileName(entry);
            if (name.Length == 0 || !char.IsDigit(name[0])) continue;

            var numMatch = Regex.Match(name, @"^(\d+(?:\.\d+)?)");
            var phaseNum = numMatch.Success ? double.Parse(numMatch.Groups[1].Value) : 0;
            var phaseName = Regex.Replace(name, @"^\d+(?:\.\d+)?-", "");

            results.Add(new PhaseEntry(name, entry, phaseNum, phaseName, archived));
        }

        return results;
    }

    private static string DetectStatus(string phaseDir, bool archived, bool hasGoal)
    {
        if (archived) return "done";
        if (!Directory.Exists(phaseDir)) return hasGoal ? "discussed" : "new";

        string[] files;
        try { files = Directory.GetFiles(phaseDir).Select(Path.GetFileName).Where(f => f != null).Select(f => f!).ToArray(); }
        catch { return "new"; }

        var plans = files.Where(f => f.EndsWith("-PLAN.md", StringComparison.OrdinalIgnoreCase)).ToArray();
        var summaries = files.Where(f => f.EndsWith("-SUMMARY.md", StringComparison.OrdinalIgnoreCase)).ToArray();
        var hasVerification = files.Any(f => f.Contains("VERIFICATION.md", StringComparison.OrdinalIgnoreCase));
        var hasResearch = files.Any(f => f.Contains("RESEARCH.md", StringComparison.OrdinalIgnoreCase));
        var hasContext = files.Any(f => f.Contains("CONTEXT.md", StringComparison.OrdinalIgnoreCase));

        if (plans.Length > 0 && plans.Length == summaries.Length && hasVerification) return "done";
        if (summaries.Length > 0) return "in-progress";
        if (plans.Length > 0) return "planned";
        if (hasResearch) return "researched";
        if (hasContext || hasGoal) return "discussed";
        return "new";
    }

    // ─── ROADMAP.md extraction helpers ──────────────────────────────────────

    private static string ExtractPhaseSection(string roadmap, double phaseNum)
    {
        var numStr = ((int)phaseNum).ToString();
        var pattern = $@"###\s+(?:Phase\s+)?{Regex.Escape(numStr)}[^\d][\s\S]*?(?=\n###|\n##|$)";
        var match = Regex.Match(roadmap, pattern);
        return match.Success ? match.Value : "";
    }

    private static string ExtractPhaseGoal(string roadmap, double phaseNum)
    {
        var section = ExtractPhaseSection(roadmap, phaseNum);
        if (string.IsNullOrEmpty(section)) return "";
        var goalMatch = Regex.Match(section, @"\*\*Goal\*\*:\s*(.+)");
        return goalMatch.Success ? goalMatch.Groups[1].Value.Trim() : "";
    }

    private static List<string> ExtractSuccessCriteria(string roadmap, double phaseNum)
    {
        var section = ExtractPhaseSection(roadmap, phaseNum);
        if (string.IsNullOrEmpty(section)) return [];
        return Regex.Matches(section, @"^\s+\d+\.\s+(.+)$", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value.Trim()).ToList();
    }

    private static List<double> ExtractDependsOn(string roadmap, double phaseNum)
    {
        var section = ExtractPhaseSection(roadmap, phaseNum);
        if (string.IsNullOrEmpty(section)) return [];
        var depMatch = Regex.Match(section, @"\*\*Depends on\*\*:\s*(.+)");
        if (!depMatch.Success) return [];
        return Regex.Matches(depMatch.Groups[1].Value, @"Phase\s+(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)
            .Select(m => double.Parse(m.Groups[1].Value)).ToList();
    }

    // ─── Feature creation helpers ───────────────────────────────────────────

    private static void WriteFeatureMd(string featureDir, string slug, string name,
        string status, string owner, string today, string goal, string phaseDir,
        List<string> criteria, List<string> dependsOnSlugs)
    {
        var criteriaLines = criteria.Count > 0
            ? string.Join('\n', criteria.Select(c => $"- [{(status == "done" ? "x" : " ")}] {c}"))
            : "- [ ] [Phase goal not yet defined — update before planning]";

        var description = !string.IsNullOrEmpty(goal)
            ? goal
            : $"{name} — migrated from GSD phase {phaseDir}. Update description before planning.";

        var dependsOnJson = dependsOnSlugs.Count > 0
            ? $"[{string.Join(", ", dependsOnSlugs)}]"
            : "[]";

        var content = $"""
            ---
            name: {name}
            slug: {slug}
            status: {status}
            owner: {owner}
            assignees: []
            created: {today}
            priority: medium
            depends_on: {dependsOnJson}
            gsd_phase: {phaseDir}
            ---
            # {name}

            ## Description

            {description}

            ## Acceptance Criteria

            {criteriaLines}

            ## Tasks

            [Populated during planning. Links to plan files.]

            ## Notes

            - Migrated from GSD phase: `{phaseDir}`
            - GSD status at migration: {status}

            ---
            *Created: {today}
            *Last updated: {today}
            """;

        File.WriteAllText(Path.Combine(featureDir, "FEATURE.md"), content + "\n");
    }

    private static void MigrateArtifacts(string srcDir, string dstDir, string phaseDir)
    {
        var numMatch = Regex.Match(phaseDir, @"^(\d+(?:\.\d+)?)");
        var padded = numMatch.Success
            ? numMatch.Groups[1].Value.Replace(".", "-").PadLeft(2, '0')
            : "";

        foreach (var file in Directory.GetFiles(srcDir, "*.md"))
        {
            var fileName = Path.GetFileName(file);
            var newName = RenameGsdFile(fileName, padded);
            var dst = Path.Combine(dstDir, newName);
            File.Copy(file, dst, overwrite: true);
            Output.Write("copied", $"{fileName} -> {newName}");
        }
    }

    private static string RenameGsdFile(string filename, string phaseNumStr)
    {
        if (string.IsNullOrEmpty(phaseNumStr)) return filename;

        // Strip phase prefix from plan/summary files: NN-MM-PLAN.md → MM-PLAN.md
        var planPattern = new Regex($"^{Regex.Escape(phaseNumStr)}-([0-9]+-(?:PLAN|SUMMARY)\\.md)$");
        var planMatch = planPattern.Match(filename);
        if (planMatch.Success) return planMatch.Groups[1].Value;

        // Strip phase prefix from single files: NN-RESEARCH.md → RESEARCH.md
        var singlePattern = new Regex($"^{Regex.Escape(phaseNumStr)}-(.+\\.md)$");
        var singleMatch = singlePattern.Match(filename);
        if (singleMatch.Success) return singleMatch.Groups[1].Value.ToUpperInvariant();

        return filename;
    }

    private static void UpdateMigratedFrontmatter(string featureDir, string slug)
    {
        var files = Directory.GetFiles(featureDir)
            .Where(f => f.EndsWith("-PLAN.md") || f.EndsWith("-SUMMARY.md"));

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var fm = FrontmatterService.Extract(content);
            fm["feature"] = slug;
            var newContent = FrontmatterService.Splice(content, fm);
            File.WriteAllText(file, newContent);
            Output.Write("frontmatter_updated", Path.GetFileName(file));
        }
    }

    private static void UpdateTasksSection(string featureDir)
    {
        var planFiles = Directory.GetFiles(featureDir, "*-PLAN.md")
            .Select(Path.GetFileName)
            .Where(f => f != null && Regex.IsMatch(f!, @"^\d+-PLAN\.md$"))
            .Select(f => f!)
            .Order()
            .ToList();

        if (planFiles.Count == 0) return;

        var featureMdPath = Path.Combine(featureDir, "FEATURE.md");
        if (!File.Exists(featureMdPath)) return;

        var taskLinks = string.Join('\n',
            planFiles.Select(f => $"- [{f}]({f}) — Plan {f.Replace("-PLAN.md", "")}"));

        var content = File.ReadAllText(featureMdPath);
        content = Regex.Replace(content,
            @"## Tasks\n\n\[Populated during planning.*?\]",
            $"## Tasks\n\n{taskLinks}");
        File.WriteAllText(featureMdPath, content);
        Output.Write("tasks_updated", Path.GetFileName(featureDir));
    }

    private static string GetGitUser(string cwd)
    {
        var result = GitService.ExecGit(cwd, ["config", "user.name"]);
        return result.ExitCode == 0 && !string.IsNullOrEmpty(result.Stdout)
            ? result.Stdout
            : "unassigned";
    }
}

