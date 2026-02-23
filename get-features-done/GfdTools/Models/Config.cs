namespace GfdTools.Models;

public class Config
{
    public string ModelProfile { get; set; } = "balanced";
    public bool SearchGitignored { get; set; } = false;
    public bool Research { get; set; } = true;
    public bool PlanChecker { get; set; } = true;
    public bool Verifier { get; set; } = true;
    public bool Parallelization { get; set; } = true;
    public bool AutoAdvance { get; set; } = false;
    public string PathPrefix { get; set; } = "docs/features";
    public Dictionary<string, string> ModelOverrides { get; set; } = new();
    public TeamConfig Team { get; set; } = new();
}

public class TeamConfig
{
    public List<string> Members { get; set; } = [];
}
