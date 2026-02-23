using System.Text.Json;
using GfdTools.Models;

namespace GfdTools.Services;

public static class ConfigService
{
    private static readonly Dictionary<string, Dictionary<string, string>> ModelProfiles = new()
    {
        ["quality"] = new()
        {
            ["gfd-planner"] = "opus",
            ["gfd-executor"] = "opus",
            ["gfd-verifier"] = "opus",
            ["gfd-researcher"] = "opus",
            ["gfd-codebase-mapper"] = "sonnet",
        },
        ["balanced"] = new()
        {
            ["gfd-planner"] = "sonnet",
            ["gfd-executor"] = "sonnet",
            ["gfd-verifier"] = "sonnet",
            ["gfd-researcher"] = "sonnet",
            ["gfd-codebase-mapper"] = "haiku",
        },
        ["budget"] = new()
        {
            ["gfd-planner"] = "sonnet",
            ["gfd-executor"] = "haiku",
            ["gfd-verifier"] = "haiku",
            ["gfd-researcher"] = "haiku",
            ["gfd-codebase-mapper"] = "haiku",
        },
    };

    public static Config LoadConfig(string cwd)
    {
        var configPath = Path.Combine(cwd, "docs", "features", "config.json");
        var defaults = new Config();

        try
        {
            if (!File.Exists(configPath))
                return defaults;

            var raw = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            // Map the actual config.json structure to our Config model
            // The config.json has nested structure, we flatten it
            if (root.TryGetProperty("model_profile", out var mp))
                defaults.ModelProfile = mp.GetString() ?? defaults.ModelProfile;

            // Check workflow object
            if (root.TryGetProperty("workflow", out var workflow))
            {
                if (workflow.TryGetProperty("research", out var research))
                    defaults.Research = research.GetBoolean();
                if (workflow.TryGetProperty("verifier", out var verifier))
                    defaults.Verifier = verifier.GetBoolean();
                if (workflow.TryGetProperty("auto_advance", out var autoAdv))
                    defaults.AutoAdvance = autoAdv.GetBoolean();
                if (workflow.TryGetProperty("plan_check", out var planCheck))
                    defaults.PlanChecker = planCheck.GetBoolean();
                if (workflow.TryGetProperty("plan_checker", out var planChecker))
                    defaults.PlanChecker = planChecker.GetBoolean();
            }

            // Check planning object
            if (root.TryGetProperty("planning", out var planning))
            {
                if (planning.TryGetProperty("search_gitignored", out var sgi))
                    defaults.SearchGitignored = sgi.GetBoolean();
            }

            // Check parallelization (can be bool or object)
            if (root.TryGetProperty("parallelization", out var para))
            {
                if (para.ValueKind == JsonValueKind.True || para.ValueKind == JsonValueKind.False)
                    defaults.Parallelization = para.GetBoolean();
                else if (para.ValueKind == JsonValueKind.Object)
                {
                    if (para.TryGetProperty("enabled", out var enabled))
                        defaults.Parallelization = enabled.GetBoolean();
                }
            }

            // Flat fields (for simple config.json)
            if (root.TryGetProperty("search_gitignored", out var sgiFlat))
                defaults.SearchGitignored = sgiFlat.GetBoolean();
            if (root.TryGetProperty("research", out var resFlat))
                defaults.Research = resFlat.GetBoolean();
            if (root.TryGetProperty("plan_checker", out var pcFlat))
                defaults.PlanChecker = pcFlat.GetBoolean();
            if (root.TryGetProperty("verifier", out var vFlat))
                defaults.Verifier = vFlat.GetBoolean();
            if (root.TryGetProperty("auto_advance", out var aaFlat))
                defaults.AutoAdvance = aaFlat.GetBoolean();
            if (root.TryGetProperty("path_prefix", out var ppFlat))
                defaults.PathPrefix = ppFlat.GetString() ?? defaults.PathPrefix;

            if (root.TryGetProperty("model_overrides", out var overrides) &&
                overrides.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in overrides.EnumerateObject())
                {
                    var val = prop.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(val))
                        defaults.ModelOverrides[prop.Name] = val;
                }
            }

            return defaults;
        }
        catch
        {
            return defaults;
        }
    }

    public static string ResolveModel(string cwd, string agentName)
    {
        var config = LoadConfig(cwd);

        if (config.ModelOverrides.TryGetValue(agentName, out var overrideModel) &&
            !string.IsNullOrEmpty(overrideModel))
            return overrideModel;

        var profile = config.ModelProfile;

        if (!ModelProfiles.TryGetValue(profile, out var profileMap))
            profileMap = ModelProfiles["balanced"];

        return profileMap.TryGetValue(agentName, out var model) ? model : "sonnet";
    }

    /// <summary>
    /// Get all config fields as a flat dictionary for output.
    /// Does NOT include commit_docs (dropped option).
    /// </summary>
    public static Dictionary<string, object?> GetAllFields(string cwd)
    {
        var config = LoadConfig(cwd);
        var result = new Dictionary<string, object?>
        {
            ["model_profile"] = config.ModelProfile,
            ["search_gitignored"] = config.SearchGitignored,
            ["research"] = config.Research,
            ["plan_checker"] = config.PlanChecker,
            ["verifier"] = config.Verifier,
            ["parallelization"] = config.Parallelization,
            ["auto_advance"] = config.AutoAdvance,
            ["path_prefix"] = config.PathPrefix,
        };
        foreach (var kv in config.ModelOverrides)
            result[$"model_override_{kv.Key}"] = kv.Value;
        return result;
    }
}
