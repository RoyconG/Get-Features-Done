---
feature: auto-run-feature-toggle
plan: "01"
type: execute
wave: 1
depends_on: []
files_modified:
  - get-features-done/GfdTools/Models/Config.cs
  - get-features-done/GfdTools/Services/ConfigService.cs
  - get-features-done/templates/feature.md
autonomous: true
acceptance_criteria:
  - "Project-level default mode configurable in a dedicated config file (default: manual)"
  - "Per-feature override via FEATURE.md frontmatter (`auto_advance` field)"
  - "Configurable stop point per-feature in FEATURE.md frontmatter (`auto_advance_until` field)"

must_haves:
  truths:
    - "Feature with `auto_advance: true` frontmatter returns true from ResolveAutoAdvance even when project config has auto_advance: false"
    - "Feature with `auto_advance: false` frontmatter returns false from ResolveAutoAdvance even when project config has auto_advance: true"
    - "Feature without `auto_advance` frontmatter field inherits the project config value from config.json"
    - "ResolveAutoAdvanceUntil returns null when auto_advance_until field is absent from frontmatter"
    - "ResolveAutoAdvanceUntil returns 'plan' when auto_advance_until field is set to 'plan'"
  artifacts:
    - path: "get-features-done/GfdTools/Models/Config.cs"
      provides: "AutoAdvanceUntil nullable string property"
      contains: "public string? AutoAdvanceUntil"
    - path: "get-features-done/GfdTools/Services/ConfigService.cs"
      provides: "Config resolution methods for auto-advance settings"
      exports:
        - "ResolveAutoAdvance(string cwd, Dictionary<string, object?> featureFrontmatter)"
        - "ResolveAutoAdvanceUntil(Dictionary<string, object?> featureFrontmatter)"
    - path: "get-features-done/templates/feature.md"
      provides: "Documentation of new frontmatter fields for feature authors"
      contains: "auto_advance_until"
  key_links:
    - from: "get-features-done/GfdTools/Services/ConfigService.cs"
      to: "get-features-done/GfdTools/Models/Config.cs"
      via: "LoadConfig() returning Config with AutoAdvance bool"
      pattern: "config\\.AutoAdvance"
    - from: "ConfigService.ResolveAutoAdvance"
      to: "featureFrontmatter[\"auto_advance\"]"
      via: "TryGetValue key-presence check (not truthiness check)"
      pattern: "TryGetValue.*auto_advance"
---

<objective>
Extend the config system with auto-advance resolution methods. Adds `AutoAdvanceUntil` to the Config model and two static helper methods to ConfigService that resolve the merged auto-advance settings from feature frontmatter (which overrides) and project config.json (the default).

Purpose: AutoRunCommand (Plan 03) needs these methods to determine whether to run and where to stop. The resolution logic must be in ConfigService so it's reusable and testable.
Output: Updated Config.cs, ConfigService.cs with two new public methods, updated feature.md template documenting the new optional frontmatter fields.
</objective>

<execution_context>
@$HOME/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/auto-run-feature-toggle/FEATURE.md
@docs/features/PROJECT.md
@get-features-done/GfdTools/Models/Config.cs
@get-features-done/GfdTools/Services/ConfigService.cs
@get-features-done/templates/feature.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Add AutoAdvanceUntil to Config and resolution methods to ConfigService</name>
  <files>
    get-features-done/GfdTools/Models/Config.cs
    get-features-done/GfdTools/Services/ConfigService.cs
  </files>
  <action>
**Config.cs** — Add one property after the existing `AutoAdvance` property (line ~9):

```csharp
public string? AutoAdvanceUntil { get; set; } = null; // null = run to execute (all stages)
```

**ConfigService.cs** — Add two public static methods at the end of the class, before the closing brace:

```csharp
/// <summary>
/// Resolve auto_advance setting: feature frontmatter overrides project config.
/// IMPORTANT: checks key PRESENCE, not truthiness — false is a valid override.
/// </summary>
public static bool ResolveAutoAdvance(string cwd, Dictionary<string, object?> featureFrontmatter)
{
    if (featureFrontmatter.TryGetValue("auto_advance", out var featureVal) && featureVal != null)
    {
        if (featureVal is bool b) return b;
        var s = featureVal.ToString();
        if (s == "true") return true;
        if (s == "false") return false;
    }
    var config = LoadConfig(cwd);
    return config.AutoAdvance;
}

/// <summary>
/// Resolve auto_advance_until: feature-only field, no project-level default.
/// Returns null when absent (meaning: run all stages through execute).
/// Valid values: "research", "plan", "execute" (or null for all).
/// </summary>
public static string? ResolveAutoAdvanceUntil(Dictionary<string, object?> featureFrontmatter)
{
    if (featureFrontmatter.TryGetValue("auto_advance_until", out var val) && val != null)
        return val.ToString();
    return null;
}
```

Do NOT modify GetAllFields() — the new methods are additive only.
  </action>
  <verify>
Run `dotnet build get-features-done/GfdTools/GfdTools.csproj` from the repo root — must succeed with 0 errors.
  </verify>
  <done>
Config.cs has `public string? AutoAdvanceUntil { get; set; } = null;` property. ConfigService.cs has both `ResolveAutoAdvance` and `ResolveAutoAdvanceUntil` as public static methods. Build passes.
  </done>
</task>

<task type="auto">
  <name>Task 2: Document new frontmatter fields in feature template</name>
  <files>
    get-features-done/templates/feature.md
  </files>
  <action>
In `get-features-done/templates/feature.md`, find the `<template>` block containing the YAML frontmatter. Add two commented optional fields after the `depends_on: []` line:

```yaml
depends_on: []
# Optional: auto-advance fields (uncomment to override project config.json defaults)
# auto_advance: true          # true = auto-advance this feature, false = manual (inherits workflow.auto_advance from config.json)
# auto_advance_until: plan    # research | plan | execute (default when absent: execute = run all stages)
```

Do not change anything else in the file.
  </action>
  <verify>
Read `get-features-done/templates/feature.md` and confirm both `auto_advance` and `auto_advance_until` appear as commented optional fields after `depends_on`.
  </verify>
  <done>
Feature template shows both optional fields with comments explaining valid values. No other content changed.
  </done>
</task>

</tasks>

<verification>
1. `dotnet build get-features-done/GfdTools/GfdTools.csproj` exits 0
2. Config.cs contains `AutoAdvanceUntil` property with `string?` type defaulting to null
3. ConfigService.cs contains `ResolveAutoAdvance(string cwd, Dictionary<string, object?> featureFrontmatter)` public static method
4. ConfigService.cs contains `ResolveAutoAdvanceUntil(Dictionary<string, object?> featureFrontmatter)` public static method
5. templates/feature.md shows auto_advance and auto_advance_until as commented optional frontmatter fields
</verification>

<success_criteria>
- Build succeeds: `dotnet build` exits 0
- ResolveAutoAdvance correctly uses TryGetValue (key-presence check) not truthiness check
- ResolveAutoAdvanceUntil returns null when key absent (not empty string)
- Feature template is updated without breaking existing template structure
</success_criteria>

<output>
After completion, create `docs/features/auto-run-feature-toggle/01-SUMMARY.md` following the summary template.
</output>
