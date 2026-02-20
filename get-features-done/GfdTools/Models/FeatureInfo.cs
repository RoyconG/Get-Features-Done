namespace GfdTools.Models;

public class FeatureInfo
{
    public bool Found { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "new";
    public string? Owner { get; set; }
    public List<string> Assignees { get; set; } = [];
    public string Priority { get; set; } = "medium";
    public List<string> DependsOn { get; set; } = [];
    public string Directory { get; set; } = string.Empty;
    public string FeatureMd { get; set; } = string.Empty;
    public List<string> Plans { get; set; } = [];
    public List<string> Summaries { get; set; } = [];
    public List<string> IncompletePlans { get; set; } = [];
    public bool HasResearch { get; set; }
    public bool HasVerification { get; set; }
    public Dictionary<string, object?> Frontmatter { get; set; } = [];
}
