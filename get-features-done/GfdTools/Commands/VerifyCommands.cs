using System.CommandLine;
using System.Text.RegularExpressions;
using GfdTools.Services;

namespace GfdTools.Commands;

public static class VerifyCommands
{
    public static Command Create(string cwd)
    {
        var verifyCmd = new Command("verify") { Description = "Run verification checks" };

        verifyCmd.Add(CreatePlanStructure(cwd));
        verifyCmd.Add(CreateCommits(cwd));
        verifyCmd.Add(CreateArtifacts(cwd));
        verifyCmd.Add(CreateKeyLinks(cwd));

        return verifyCmd;
    }

    // ─── verify plan-structure ───────────────────────────────────────────────

    private static Command CreatePlanStructure(string cwd)
    {
        var cmd = new Command("plan-structure") { Description = "Verify plan file structure" };
        var fileArg = new Argument<string>("file") { Description = "Plan file path" };
        cmd.Add(fileArg);

        cmd.SetAction(pr =>
        {
            var filePath = pr.GetValue(fileArg)!;
            var fullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(cwd, filePath);

            if (!File.Exists(fullPath))
            {
                Console.Error.WriteLine($"File not found: {filePath}");
                return 1;
            }

            var content = File.ReadAllText(fullPath);
            var fm = FrontmatterService.Extract(content);
            var errors = new List<string>();
            var warnings = new List<string>();

            // Check required frontmatter fields
            var required = new[] { "feature", "plan", "type", "wave", "depends_on", "files_modified", "autonomous" };
            foreach (var field in required)
            {
                if (!fm.ContainsKey(field))
                    errors.Add($"Missing required frontmatter field: {field}");
            }

            // Parse <task> elements
            var taskPattern = new Regex(@"<task[^>]*>([\s\S]*?)<\/task>", RegexOptions.None);
            var taskMatches = taskPattern.Matches(content);
            var taskCount = taskMatches.Count;

            foreach (Match taskMatch in taskMatches)
            {
                var taskContent = taskMatch.Groups[1].Value;

                var nameMatch = Regex.Match(taskContent, @"<name>([\s\S]*?)<\/name>");
                var taskName = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : "unnamed";

                var hasAction = taskContent.Contains("<action>");
                var hasVerify = taskContent.Contains("<verify>");
                var hasDone = taskContent.Contains("<done>");

                if (!nameMatch.Success)
                    errors.Add("Task missing <name> element");
                if (!hasAction)
                    errors.Add($"Task '{taskName}' missing <action>");
                if (!hasVerify)
                    warnings.Add($"Task '{taskName}' missing <verify>");
                if (!hasDone)
                    warnings.Add($"Task '{taskName}' missing <done>");
            }

            if (taskCount == 0)
                warnings.Add("No <task> elements found");

            Output.WriteBool("valid", errors.Count == 0);
            Output.Write("task_count", taskCount);
            Output.WriteList("error", errors);
            Output.WriteList("warning", warnings);
            return 0;
        });

        return cmd;
    }

    // ─── verify commits ──────────────────────────────────────────────────────

    private static Command CreateCommits(string cwd)
    {
        var cmd = new Command("commits") { Description = "Verify commit hashes exist in git" };
        var hashesArg = new Argument<string[]>("hashes") { Description = "Commit hashes to verify" };
        cmd.Add(hashesArg);

        cmd.SetAction(pr =>
        {
            var hashes = pr.GetValue(hashesArg) ?? [];
            if (hashes.Length == 0)
            {
                Console.Error.WriteLine("At least one commit hash required");
                return 1;
            }

            var valid = new List<string>();
            var invalid = new List<string>();

            foreach (var hash in hashes)
            {
                // Use direct git object file inspection as fallback for sandboxed environments
                // where spawning git as a subprocess is restricted.
                if (GitService.CommitExists(cwd, hash))
                    valid.Add(hash);
                else
                    invalid.Add(hash);
            }

            Output.WriteBool("all_valid", invalid.Count == 0);
            Output.Write("total", hashes.Length);
            Output.WriteList("valid", valid);
            Output.WriteList("invalid", invalid);
            return 0;
        });

        return cmd;
    }

    // ─── verify artifacts (NEW — not in gfd-tools.cjs) ──────────────────────

    private static Command CreateArtifacts(string cwd)
    {
        var cmd = new Command("artifacts") { Description = "Verify plan must_haves artifacts exist" };
        var fileArg = new Argument<string>("plan-file") { Description = "Plan file path" };
        cmd.Add(fileArg);

        cmd.SetAction(pr =>
        {
            var filePath = pr.GetValue(fileArg)!;
            var fullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(cwd, filePath);

            if (!File.Exists(fullPath))
            {
                Console.Error.WriteLine($"File not found: {filePath}");
                return 1;
            }

            var content = File.ReadAllText(fullPath);
            var fm = FrontmatterService.Extract(content);

            // Extract must_haves.artifacts
            var artifacts = new List<(string path, int? minLines)>();
            if (fm.TryGetValue("must_haves", out var mh) && mh is Dictionary<string, object?> mhDict)
            {
                if (mhDict.TryGetValue("artifacts", out var arts) && arts is List<string> artList)
                {
                    // Simple list of paths
                    foreach (var a in artList)
                        artifacts.Add((a, null));
                }
            }

            // Also parse from raw YAML block in content for nested object arrays
            // must_haves.artifacts is an array of objects with 'path' and optional 'min_lines'
            artifacts.AddRange(ParseArtifactsFromContent(content));

            // Deduplicate (keep first occurrence)
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deduped = new List<(string path, int? minLines)>();
            foreach (var a in artifacts)
            {
                if (seen.Add(a.path))
                    deduped.Add(a);
            }
            artifacts = deduped;

            var total = artifacts.Count;
            var passed = 0;

            foreach (var (artifactPath, minLines) in artifacts)
            {
                var artifactFullPath = Path.IsPathRooted(artifactPath)
                    ? artifactPath
                    : Path.Combine(cwd, artifactPath);
                var exists = File.Exists(artifactFullPath) || Directory.Exists(artifactFullPath);

                bool lineCheckPassed = true;
                if (exists && minLines.HasValue && File.Exists(artifactFullPath))
                {
                    var lineCount = File.ReadLines(artifactFullPath).Count();
                    lineCheckPassed = lineCount >= minLines.Value;
                    if (!lineCheckPassed)
                        exists = false; // treat as failed if min_lines not met
                }

                if (exists) passed++;
                Output.Write("artifact_path", artifactPath);
                Output.WriteBool("artifact_exists", exists);
                if (minLines.HasValue)
                    Output.Write("artifact_min_lines", minLines.Value);
            }

            Output.WriteBool("all_passed", passed == total);
            Output.Write("passed", passed);
            Output.Write("total", total);
            return 0;
        });

        return cmd;
    }

    // ─── verify key-links (NEW — not in gfd-tools.cjs) ──────────────────────

    private static Command CreateKeyLinks(string cwd)
    {
        var cmd = new Command("key-links") { Description = "Verify key links between files exist" };
        var fileArg = new Argument<string>("plan-file") { Description = "Plan file path" };
        cmd.Add(fileArg);

        cmd.SetAction(pr =>
        {
            var filePath = pr.GetValue(fileArg)!;
            var fullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(cwd, filePath);

            if (!File.Exists(fullPath))
            {
                Console.Error.WriteLine($"File not found: {filePath}");
                return 1;
            }

            var content = File.ReadAllText(fullPath);
            var links = ParseKeyLinksFromContent(content);

            var total = links.Count;
            var verified = 0;

            foreach (var (from, to, pattern) in links)
            {
                Output.Write("link_from", from);
                Output.Write("link_to", to);

                var fromFullPath = Path.IsPathRooted(from) ? from : Path.Combine(cwd, from);
                bool linkVerified = false;

                if (File.Exists(fromFullPath) && !string.IsNullOrEmpty(pattern))
                {
                    var fromContent = File.ReadAllText(fromFullPath);
                    linkVerified = fromContent.Contains(pattern, StringComparison.Ordinal);
                }

                if (linkVerified) verified++;
                Output.WriteBool("link_verified", linkVerified);
            }

            Output.WriteBool("all_verified", verified == total && total > 0);
            Output.Write("verified", verified);
            Output.Write("total", total);
            return 0;
        });

        return cmd;
    }

    // ─── Frontmatter artifact/key-link parsing helpers ──────────────────────

    /// <summary>
    /// Parses must_haves.artifacts from plan content using YAML block parsing.
    /// Handles object arrays with path, provides, min_lines fields.
    /// </summary>
    private static List<(string path, int? minLines)> ParseArtifactsFromContent(string content)
    {
        var result = new List<(string, int?)>();

        // Find the must_haves block in the frontmatter
        var fmMatch = Regex.Match(content, @"^---\n([\s\S]*?)\n---", RegexOptions.Multiline);
        if (!fmMatch.Success) return result;

        var yaml = fmMatch.Groups[1].Value;
        var lines = yaml.Split('\n');

        // Find must_haves section
        bool inMustHaves = false;
        bool inArtifacts = false;
        string? currentPath = null;
        int? currentMinLines = null;

        foreach (var line in lines)
        {
            if (Regex.IsMatch(line, @"^must_haves:"))
            {
                inMustHaves = true;
                inArtifacts = false;
                continue;
            }

            if (inMustHaves && Regex.IsMatch(line, @"^  artifacts:"))
            {
                inArtifacts = true;
                continue;
            }

            // Any other top-level key ends must_haves
            if (inMustHaves && Regex.IsMatch(line, @"^\w") && !Regex.IsMatch(line, @"^must_haves"))
            {
                // Flush last artifact
                if (currentPath != null) result.Add((currentPath, currentMinLines));
                currentPath = null;
                currentMinLines = null;
                inMustHaves = false;
                inArtifacts = false;
                continue;
            }

            if (inArtifacts)
            {
                // New artifact item (starts with 4-space "    - ")
                var newItemMatch = Regex.Match(line, @"^    -\s+(.*)$");
                if (newItemMatch.Success)
                {
                    // Flush previous
                    if (currentPath != null) result.Add((currentPath, currentMinLines));
                    currentPath = null;
                    currentMinLines = null;

                    // Inline path (e.g., "    - path: something")
                    var inlinePathMatch = Regex.Match(newItemMatch.Groups[1].Value, @"^path:\s*(.+)$");
                    if (inlinePathMatch.Success)
                        currentPath = inlinePathMatch.Groups[1].Value.Trim().Trim('"');
                    continue;
                }

                // Field of current artifact item (6+ spaces)
                var fieldMatch = Regex.Match(line, @"^      (\w[\w_]*):\s*(.*)$");
                if (fieldMatch.Success)
                {
                    var key = fieldMatch.Groups[1].Value;
                    var val = fieldMatch.Groups[2].Value.Trim().Trim('"');

                    if (key == "path") currentPath = val;
                    else if (key == "min_lines" && int.TryParse(val, out var ml)) currentMinLines = ml;
                    continue;
                }

                // End of artifacts (indentation drops)
                if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("  "))
                {
                    if (currentPath != null) result.Add((currentPath, currentMinLines));
                    currentPath = null;
                    currentMinLines = null;
                    inArtifacts = false;
                }
            }
        }

        // Flush final
        if (currentPath != null) result.Add((currentPath, currentMinLines));

        return result;
    }

    /// <summary>
    /// Parses must_haves.key_links from plan content.
    /// Returns list of (from, to, pattern) tuples.
    /// </summary>
    private static List<(string from, string to, string pattern)> ParseKeyLinksFromContent(string content)
    {
        var result = new List<(string, string, string)>();

        var fmMatch = Regex.Match(content, @"^---\n([\s\S]*?)\n---", RegexOptions.Multiline);
        if (!fmMatch.Success) return result;

        var yaml = fmMatch.Groups[1].Value;
        var lines = yaml.Split('\n');

        bool inMustHaves = false;
        bool inKeyLinks = false;
        string? currentFrom = null;
        string? currentTo = null;
        string? currentPattern = null;

        void FlushLink()
        {
            if (currentFrom != null)
                result.Add((currentFrom, currentTo ?? "", currentPattern ?? ""));
            currentFrom = null;
            currentTo = null;
            currentPattern = null;
        }

        foreach (var line in lines)
        {
            if (Regex.IsMatch(line, @"^must_haves:"))
            {
                inMustHaves = true;
                inKeyLinks = false;
                continue;
            }

            if (inMustHaves && Regex.IsMatch(line, @"^  key_links:"))
            {
                inKeyLinks = true;
                continue;
            }

            // Any other top-level key ends must_haves
            if (inMustHaves && Regex.IsMatch(line, @"^\w") && !Regex.IsMatch(line, @"^must_haves"))
            {
                FlushLink();
                inMustHaves = false;
                inKeyLinks = false;
                continue;
            }

            if (inKeyLinks)
            {
                // New key_link item
                var newItemMatch = Regex.Match(line, @"^    -\s+(.*)$");
                if (newItemMatch.Success)
                {
                    FlushLink();
                    var inlineFromMatch = Regex.Match(newItemMatch.Groups[1].Value, @"^from:\s*(.+)$");
                    if (inlineFromMatch.Success)
                        currentFrom = inlineFromMatch.Groups[1].Value.Trim().Trim('"');
                    continue;
                }

                // Field of current key_link
                var fieldMatch = Regex.Match(line, @"^      (\w[\w_]*):\s*(.*)$");
                if (fieldMatch.Success)
                {
                    var key = fieldMatch.Groups[1].Value;
                    var val = fieldMatch.Groups[2].Value.Trim().Trim('"');

                    if (key == "from") currentFrom = val;
                    else if (key == "to") currentTo = val;
                    else if (key == "pattern") currentPattern = val;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("  "))
                {
                    FlushLink();
                    inKeyLinks = false;
                }
            }
        }

        FlushLink();
        return result;
    }
}
