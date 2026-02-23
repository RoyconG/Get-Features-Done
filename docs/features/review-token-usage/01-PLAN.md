---
feature: review-token-usage
plan: 1
type: execute
wave: 1
depends_on: []
files_modified:
  - get-features-done/GfdTools/Models/Config.cs
  - get-features-done/GfdTools/Services/ConfigService.cs
  - docs/token-audit.md
autonomous: true
acceptance_criteria:
  - "One-time audit document produced in docs/ analyzing token usage across all agent roles with findings and recommendations"
  - "Model preferences persisted in GFD config file and respected by all workflows"
must_haves:
  truths:
    - "gfd-tools resolve-model gfd-executor returns the override model when model_overrides.gfd-executor is set in docs/features/config.json"
    - "gfd-tools resolve-model gfd-executor falls back to profile-based model when no override is set for that role"
    - "docs/token-audit.md exists with analysis of all 5 agent roles and concrete model recommendations"
  artifacts:
    - path: "get-features-done/GfdTools/Models/Config.cs"
      provides: "ModelOverrides dictionary property on Config class"
      contains: "Dictionary<string, string> ModelOverrides"
    - path: "get-features-done/GfdTools/Services/ConfigService.cs"
      provides: "ResolveModel checks overrides before profile lookup"
      contains: "ModelOverrides.TryGetValue"
    - path: "docs/token-audit.md"
      provides: "Audit of all GFD agent roles with cost estimates and model recommendations"
      min_lines: 80
  key_links:
    - from: "ConfigService.ResolveModel"
      to: "Config.ModelOverrides"
      via: "TryGetValue check inserted before profile lookup"
      pattern: "ModelOverrides\\.TryGetValue"
    - from: "ConfigService.LoadConfig"
      to: "model_overrides key in docs/features/config.json"
      via: "JsonElement.EnumerateObject loop"
      pattern: "model_overrides.*EnumerateObject"
---

<objective>
Extend the GFD config system with per-role model overrides, and produce the one-time token audit document.

Purpose: Establishes the config schema foundation that /gfd:configure-models (Plan 03) will write to. The audit document provides evidence-based recommendations that the configure-models workflow surfaces to users.

Output: Config.cs and ConfigService.cs updated with ModelOverrides support; docs/token-audit.md written with audit findings.
</objective>

<execution_context>
@$HOME/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/review-token-usage/FEATURE.md
@docs/features/review-token-usage/RESEARCH.md
@docs/features/PROJECT.md
@get-features-done/GfdTools/Models/Config.cs
@get-features-done/GfdTools/Services/ConfigService.cs
</context>

<tasks>

<task type="auto">
  <name>Task 1: Add per-role model override support to Config + ConfigService</name>
  <files>
    get-features-done/GfdTools/Models/Config.cs
    get-features-done/GfdTools/Services/ConfigService.cs
  </files>
  <action>
    **Config.cs** — Add one property to the Config class (after `PathPrefix`):
    ```csharp
    public Dictionary<string, string> ModelOverrides { get; set; } = new();
    ```

    **ConfigService.cs — LoadConfig()** — After the flat field parsing block (around line 101, after `path_prefix` handling), add model_overrides parsing before the final `return defaults;`:
    ```csharp
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
    ```

    **ConfigService.cs — ResolveModel()** — Insert override check between the `LoadConfig()` call and the profile lookup:
    ```csharp
    if (config.ModelOverrides.TryGetValue(agentName, out var overrideModel) &&
        !string.IsNullOrEmpty(overrideModel))
        return overrideModel;
    ```

    **ConfigService.cs — GetAllFields()** — After the existing dictionary entries, add model override output so `config-get` surfaces them:
    ```csharp
    foreach (var kv in config.ModelOverrides)
        result[$"model_override_{kv.Key}"] = kv.Value;
    ```
    (Change the return statement to build the dict into a local `result` variable first, then add overrides, then return it.)

    **Rebuild** — Run from the repo root:
    ```bash
    dotnet build get-features-done/GfdTools/GfdTools.csproj -c Release --nologo -q
    ```

    **Smoke test** — Write a temporary JSON snippet to docs/features/config.json adding `"model_overrides": {"gfd-executor": "opus"}`, run `gfd-tools resolve-model gfd-executor` and verify it returns `model=opus`, then remove the test entry and restore config.json.
  </action>
  <verify>
    ```bash
    dotnet build get-features-done/GfdTools/GfdTools.csproj -c Release --nologo -q
    # Exit 0 = success
    gfd-tools resolve-model gfd-executor
    # With no overrides in config.json: returns model=sonnet (balanced profile default)
    ```
    After adding `"model_overrides": {"gfd-executor": "opus"}` to config.json:
    ```bash
    gfd-tools resolve-model gfd-executor
    # Returns: model=opus
    ```
  </verify>
  <done>
    Build succeeds with 0 errors. resolve-model returns override value when model_overrides key is present, and returns profile default when no override exists for that role.
  </done>
</task>

<task type="auto">
  <name>Task 2: Write docs/token-audit.md</name>
  <files>
    docs/token-audit.md
  </files>
  <action>
    Read these files to gather audit evidence:
    - `agents/gfd-researcher.md` — researcher role, tasks, tools used
    - `agents/gfd-planner.md` — planner role and complexity
    - `agents/gfd-executor.md` — executor role and complexity
    - `agents/gfd-verifier.md` — verifier role and complexity
    - `agents/gfd-codebase-mapper.md` — mapper role and complexity
    - `get-features-done/GfdTools/Services/ConfigService.cs` — current model profile assignments
    - `docs/features/review-token-usage/RESEARCH.md` — research findings and model pricing

    Write `docs/token-audit.md` with the following structure:

    ```markdown
    # GFD Agent Token Audit

    **Date:** <today>
    **Purpose:** Analyze model assignments across all GFD agent roles for cost efficiency.

    ## Current Model Assignments

    Table showing quality/balanced/budget profile assignments for each role.

    ## Agent Role Analysis

    ### gfd-researcher
    - Tasks: web search, synthesis, writing RESEARCH.md
    - Complexity: medium-high (web search + synthesis requires reasoning)
    - Minimum viable model: haiku (usable but low quality for synthesis)
    - Recommended: sonnet
    - Notes on quality vs cost tradeoff

    ### gfd-planner
    - Tasks: codebase exploration, dependency analysis, writing PLAN.md files
    - Complexity: high (structured reasoning + file system navigation)
    - Minimum viable model: sonnet (haiku produces vague plans)
    - Recommended: sonnet

    ### gfd-executor
    - Tasks: code changes, running builds, multi-step implementation
    - Complexity: high (multi-file edits, debugging, sequential reasoning)
    - Minimum viable model: sonnet (haiku produces incomplete implementations)
    - Recommended: sonnet

    ### gfd-verifier
    - Tasks: structured comparison of artifacts against acceptance criteria
    - Complexity: low-medium (pattern matching + boolean evaluation)
    - Minimum viable model: haiku (structured comparison fits haiku well)
    - Recommended: haiku

    ### gfd-codebase-mapper
    - Tasks: reading files, summarizing architecture into structured docs
    - Complexity: low (reads + summarizes, minimal reasoning)
    - Minimum viable model: haiku
    - Recommended: haiku

    ## Recommendations

    Table of recommended defaults per role with rationale.

    | Agent Role | Recommended | Minimum | Notes |
    |---|---|---|---|
    | gfd-researcher | sonnet | haiku | ... |
    | gfd-planner | sonnet | sonnet | ... |
    | gfd-executor | sonnet | sonnet | ... |
    | gfd-verifier | haiku | haiku | ... |
    | gfd-codebase-mapper | haiku | haiku | ... |

    ## Profile Assessment

    Assessment of which built-in profile best matches the recommendations.
    "balanced" profile is well-calibrated. "quality" (all opus) wastes cost on
    verifier and mapper. "budget" risks quality for executor and planner.

    ## Pricing Reference (Feb 2026)

    | Model | ID | Input $/MTok | Output $/MTok |
    |---|---|---|---|
    | haiku | claude-haiku-4-5 | $1 | $5 |
    | sonnet | claude-sonnet-4-6 | $3 | $15 |
    | opus | claude-opus-4-6 | $5 | $25 |

    ## Estimated Per-Workflow Cost (balanced profile)

    Rough estimates based on typical agent task size:
    - research: ~$0.05-0.15 (sonnet, ~40-100K tokens)
    - plan: ~$0.05-0.10 (sonnet)
    - execute (per plan): ~$0.10-0.30 (sonnet)
    - verify: ~$0.02-0.05 (haiku)
    ```

    Write this document with actual analysis based on the agent files you read. Fill in specific reasoning for each role. The above is a structural template, not the final content — derive the content from the actual agent markdown files.
  </action>
  <verify>
    ```bash
    wc -l docs/token-audit.md
    # Should be 80+ lines
    grep -c "^###" docs/token-audit.md
    # Should be 5 (one per agent role)
    ```
  </verify>
  <done>
    docs/token-audit.md exists, is 80+ lines, has a section for each of the 5 agent roles, includes a recommendations table, and includes model pricing reference.
  </done>
</task>

</tasks>

<verification>
After both tasks:
1. `dotnet build get-features-done/GfdTools/GfdTools.csproj` exits 0
2. `gfd-tools resolve-model gfd-verifier` returns `model=haiku` (balanced profile default, no override set)
3. `docs/token-audit.md` exists with 80+ lines and 5 agent role sections
4. No other files were modified
</verification>

<success_criteria>
- Config.cs has ModelOverrides property
- ConfigService.cs checks overrides before profile in ResolveModel()
- ConfigService.cs parses model_overrides from config.json in LoadConfig()
- GetAllFields() emits model_override_* keys when overrides exist
- GfdTools builds with 0 errors
- docs/token-audit.md is a complete audit document with evidence-based recommendations
</success_criteria>

<output>
After completion, create `docs/features/review-token-usage/01-SUMMARY.md` following the summary template.
</output>
