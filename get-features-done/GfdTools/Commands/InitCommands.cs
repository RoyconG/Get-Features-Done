using System.CommandLine;
using GfdTools.Services;

namespace GfdTools.Commands;

public static class InitCommands
{
    public static Command Create(string cwd)
    {
        var initCmd = new Command("init") { Description = "Initialize a workflow context" };

        initCmd.Add(CreateNewProject(cwd));
        initCmd.Add(CreateNewFeature(cwd));
        initCmd.Add(CreatePlanFeature(cwd));
        initCmd.Add(CreateExecuteFeature(cwd));
        initCmd.Add(CreateMapCodebase(cwd));

        return initCmd;
    }

    // ─── init new-project ────────────────────────────────────────────────────

    private static Command CreateNewProject(string cwd)
    {
        var cmd = new Command("new-project") { Description = "Initialize new project context" };

        cmd.SetAction(pr =>
        {
            var config = ConfigService.LoadConfig(cwd);

            // Check for existing code files (mirrors JS find command)
            bool hasCode = false;
            try
            {
                var extensions = new[] { ".ts", ".js", ".py", ".go", ".rs", ".swift", ".java", ".cs" };
                hasCode = Directory.EnumerateFiles(cwd, "*", SearchOption.AllDirectories)
                    .Where(f =>
                    {
                        var rel = f[cwd.Length..].Replace('\\', '/');
                        return !rel.Contains("/node_modules/") && !rel.Contains("/.git/");
                    })
                    .Take(500)
                    .Any(f => extensions.Contains(Path.GetExtension(f)));
            }
            catch { }

            var hasPackageFile = PathExists(cwd, "package.json")
                              || PathExists(cwd, "requirements.txt")
                              || PathExists(cwd, "Cargo.toml")
                              || PathExists(cwd, "go.mod")
                              || PathExists(cwd, "Package.swift");

            var hasCwdCodebase = PathExists(cwd, "docs/features/codebase");

            Output.Write("researcher_model", ConfigService.ResolveModel(cwd, "gfd-researcher"));
            Output.WriteBool("project_exists", PathExists(cwd, "docs/features/PROJECT.md"));
            Output.WriteBool("has_codebase_map", hasCwdCodebase);
            Output.WriteBool("features_dir_exists", PathExists(cwd, "docs/features"));
            Output.WriteBool("has_existing_code", hasCode);
            Output.WriteBool("has_package_file", hasPackageFile);
            Output.WriteBool("is_brownfield", hasCode || hasPackageFile);
            Output.WriteBool("needs_codebase_map", (hasCode || hasPackageFile) && !hasCwdCodebase);
            Output.WriteBool("has_git", PathExists(cwd, ".git"));
            return 0;
        });

        return cmd;
    }

    // ─── init new-feature ────────────────────────────────────────────────────

    private static Command CreateNewFeature(string cwd)
    {
        var cmd = new Command("new-feature") { Description = "Initialize new feature context" };
        var slugArg = new Argument<string>("slug") { Description = "Feature slug" };
        cmd.Add(slugArg);

        cmd.SetAction(pr =>
        {
            var slug = pr.GetValue(slugArg)!;
            if (string.IsNullOrWhiteSpace(slug))
                return Output.Fail("feature slug required");

            var existingFeature = FeatureService.FindFeature(cwd, slug);

            Output.Write("slug", slug);
            Output.WriteBool("feature_exists", existingFeature != null);
            Output.Write("existing_status", existingFeature?.Status ?? "");
            Output.WriteBool("features_dir_exists", PathExists(cwd, "docs/features"));
            Output.Write("feature_dir", $"docs/features/{slug}");
            Output.Write("feature_md", $"docs/features/{slug}/FEATURE.md");
            Output.WriteBool("project_exists", PathExists(cwd, "docs/features/PROJECT.md"));
            return 0;
        });

        return cmd;
    }

    // ─── init plan-feature ───────────────────────────────────────────────────

    private static Command CreatePlanFeature(string cwd)
    {
        var cmd = new Command("plan-feature") { Description = "Initialize plan-feature context" };
        var slugArg = new Argument<string>("slug") { Description = "Feature slug" };
        cmd.Add(slugArg);

        cmd.SetAction(pr =>
        {
            var slug = pr.GetValue(slugArg)!;
            if (string.IsNullOrWhiteSpace(slug))
                return Output.Fail("feature slug required");

            var featureInfo = FeatureService.FindFeature(cwd, slug);

            Output.Write("researcher_model", ConfigService.ResolveModel(cwd, "gfd-researcher"));
            Output.Write("planner_model", ConfigService.ResolveModel(cwd, "gfd-planner"));
            Output.Write("checker_model", ConfigService.ResolveModel(cwd, "gfd-verifier"));

            var config = ConfigService.LoadConfig(cwd);
            Output.WriteBool("research_enabled", config.Research);
            Output.WriteBool("plan_checker_enabled", config.PlanChecker);

            Output.WriteBool("feature_found", featureInfo != null);
            Output.Write("feature_dir", featureInfo?.Directory ?? "");
            Output.Write("slug", slug);
            Output.Write("feature_name", featureInfo?.Name ?? "");
            Output.Write("feature_status", featureInfo?.Status ?? "");

            Output.WriteBool("has_research", featureInfo?.HasResearch ?? false);
            Output.WriteBool("has_plans", (featureInfo?.Plans.Count ?? 0) > 0);
            Output.Write("plan_count", featureInfo?.Plans.Count ?? 0);

            Output.WriteBool("features_dir_exists", PathExists(cwd, "docs/features"));
            return 0;
        });

        return cmd;
    }

    // ─── init execute-feature ────────────────────────────────────────────────

    private static Command CreateExecuteFeature(string cwd)
    {
        var cmd = new Command("execute-feature") { Description = "Initialize execute-feature context" };
        var slugArg = new Argument<string>("slug") { Description = "Feature slug" };
        cmd.Add(slugArg);

        cmd.SetAction(pr =>
        {
            var slug = pr.GetValue(slugArg)!;
            if (string.IsNullOrWhiteSpace(slug))
                return Output.Fail("feature slug required");

            var config = ConfigService.LoadConfig(cwd);
            var featureInfo = FeatureService.FindFeature(cwd, slug);

            Output.Write("executor_model", ConfigService.ResolveModel(cwd, "gfd-executor"));
            Output.Write("verifier_model", ConfigService.ResolveModel(cwd, "gfd-verifier"));

            Output.WriteBool("parallelization", config.Parallelization);
            Output.WriteBool("verifier_enabled", config.Verifier);

            Output.WriteBool("feature_found", featureInfo != null);
            Output.Write("feature_dir", featureInfo?.Directory ?? "");
            Output.Write("slug", slug);
            Output.Write("feature_name", featureInfo?.Name ?? "");
            Output.Write("feature_status", featureInfo?.Status ?? "");

            Output.Write("plan_count", featureInfo?.Plans.Count ?? 0);
            Output.Write("incomplete_count", featureInfo?.IncompletePlans.Count ?? 0);

            // Repeated keys for lists
            if (featureInfo != null)
            {
                Output.WriteList("plan", featureInfo.Plans);
                Output.WriteList("summary", featureInfo.Summaries);
                Output.WriteList("incomplete_plan", featureInfo.IncompletePlans);
            }

            Output.WriteBool("config_exists", PathExists(cwd, "docs/features/config.json"));
            return 0;
        });

        return cmd;
    }

    // ─── init map-codebase ───────────────────────────────────────────────────

    private static Command CreateMapCodebase(string cwd)
    {
        var cmd = new Command("map-codebase") { Description = "Initialize map-codebase context" };

        cmd.SetAction(pr =>
        {
            var config = ConfigService.LoadConfig(cwd);
            var codebaseDir = Path.Combine(cwd, "docs", "features", "codebase");

            var existingMaps = new List<string>();
            try
            {
                if (Directory.Exists(codebaseDir))
                    existingMaps = Directory.GetFiles(codebaseDir, "*.md")
                        .Select(Path.GetFileName)
                        .Where(f => f != null)
                        .Select(f => f!)
                        .Order()
                        .ToList();
            }
            catch { }

            Output.Write("mapper_model", ConfigService.ResolveModel(cwd, "gfd-codebase-mapper"));
            Output.WriteBool("search_gitignored", config.SearchGitignored);
            Output.WriteBool("parallelization", config.Parallelization);
            Output.Write("codebase_dir", "docs/features/codebase");
            Output.WriteBool("has_maps", existingMaps.Count > 0);
            Output.WriteBool("features_dir_exists", PathExists(cwd, "docs/features"));
            Output.WriteBool("codebase_dir_exists", PathExists(cwd, "docs/features/codebase"));
            Output.WriteList("existing_map", existingMaps);
            return 0;
        });

        return cmd;
    }

    // ─── Helper ──────────────────────────────────────────────────────────────

    private static bool PathExists(string cwd, string relativePath)
    {
        var fullPath = Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.Combine(cwd, relativePath);
        return File.Exists(fullPath) || Directory.Exists(fullPath);
    }
}
