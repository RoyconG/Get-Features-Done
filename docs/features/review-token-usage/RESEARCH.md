# Feature: Review Token Usage — Research

**Researched:** 2026-02-22
**Domain:** Claude agent token tracking, model configuration, GFD config system, Claude Agent SDK output types
**Confidence:** HIGH

## Summary

This feature has four distinct sub-problems: (1) a one-time audit of all agent roles for model appropriateness, (2) a `/gfd:configure-models` interactive command, (3) runtime token reporting appended to workflow output, and (4) cumulative token tracking in FEATURE.md. Each sub-problem has a clear implementation path in the existing GFD codebase.

The GFD codebase already has the scaffolding needed. `ConfigService.cs` has model profiles (`quality`, `balanced`, `budget`) with per-agent tier assignments, a `resolve-model` CLI command, and a `config.json` schema. Token data is available from the Claude Agent SDK's `SDKResultMessage` type when using `--output-format stream-json`. The `ClaudeService.InvokeHeadless()` method currently uses `--output-format text` and discards token information — switching to `stream-json` and parsing the result line is the primary technical change needed for headless (auto-run) token capture.

For interactive workflows (where orchestrators spawn subagents via `Task()`), token data is NOT surfaced back to the parent agent through the Task tool output in the current SDK. Agents must report their own token estimates or the orchestrator must rely on headless run data. The practical approach is: collect exact data from headless runs; for interactive runs, instruct agents to self-report session totals using markdown output.

**Primary recommendation:** Add `model_overrides` map to config.json (per-role model strings), extend `ConfigService.ResolveModel()` to check overrides first, implement `/gfd:configure-models` as a new command file + workflow file, switch `ClaudeService.InvokeHeadless()` to `--output-format stream-json` to capture cost/token data, and have orchestrators write a token summary block to FEATURE.md after each workflow completes.

## User Constraints (from FEATURE.md)

### Locked Decisions

- **Audit scope:** All agent types including orchestrators (research, plan, execute, verify, mapper)
- **New command:** `/gfd:configure-models` — standalone command, NOT part of `/gfd:settings`
- **Granularity:** Per agent role (not global or profile-level only)
- **Model options:** Claude family presets (haiku/sonnet/opus) plus free text for custom model strings
- **Weak model behavior:** Warn but allow — do not block the user's choice
- **Recommendation display:** Show recommended model per role based on audit findings
- **Persistence:** GFD config file (`docs/features/config.json`)
- **Token reporting:** Per-agent-role summary at end of each major workflow (research, plan, execute)
- **Token accumulation:** Cumulative `## Token Usage` section maintained in FEATURE.md
- **Audit output:** Document in `docs/` with findings and recommendations

### Out of Scope

- Making `/gfd:configure-models` part of `/gfd:settings`
- Blocking weak model choices (warn only)
- Per-task token tracking (role-level is sufficient)

---

## Standard Stack

### Core

| Component | Version/Location | Purpose | Why Standard |
|-----------|-----------------|---------|--------------|
| `ConfigService.cs` | `get-features-done/GfdTools/Services/ConfigService.cs` | Config loading and model resolution | Existing pattern — all agents call `ResolveModel()` |
| `Config.cs` | `get-features-done/GfdTools/Models/Config.cs` | Config model | Existing schema — extend with `ModelOverrides` dict |
| `docs/features/config.json` | Project root | Config persistence | Current config location used by all agents |
| Claude Agent SDK stream-json | `--output-format stream-json` | Token/cost data from headless runs | Official SDK output format with `SDKResultMessage.usage` |
| Command file pattern | `commands/gfd/*.md` | `/gfd:configure-models` entry point | Existing command pattern — see `plan-feature.md`, `execute-feature.md` |
| Workflow file pattern | `get-features-done/workflows/*.md` | Workflow orchestration logic | Existing workflow pattern |

### Supporting

| Component | Purpose | When to Use |
|-----------|---------|-------------|
| `ClaudeService.BuildAutoRunMd()` | AUTO-RUN.md generation with duration | Already captures duration; extend to capture cost |
| `gfd-tools resolve-model <agent>` | Resolve current model for an agent | Needed by `/gfd:configure-models` to show current config |
| `gfd-tools config-get` | Read config values | Use to show current settings in configure-models |

### Current Model Profile Structure (Codebase Truth)

From `ConfigService.cs` — the existing three profiles:

```
quality:  planner=opus, executor=opus, verifier=opus, researcher=opus, mapper=sonnet
balanced: planner=sonnet, executor=sonnet, verifier=sonnet, researcher=sonnet, mapper=haiku
budget:   planner=sonnet, executor=haiku, verifier=haiku, researcher=haiku, mapper=haiku
```

The `model_profile` field in `config.json` selects the profile. There is no per-role override mechanism — that is what this feature adds.

### Current Claude Models (Official, Feb 2026)

From official Anthropic model docs (`platform.claude.com/docs/en/docs/about-claude/models`):

| Alias | Model ID | Input $/MTok | Output $/MTok | Use Case |
|-------|----------|-------------|--------------|----------|
| `haiku` | `claude-haiku-4-5` | $1 | $5 | Fastest, structured tasks, mapping |
| `sonnet` | `claude-sonnet-4-6` | $3 | $15 | Best speed/intelligence balance |
| `opus` | `claude-opus-4-6` | $5 | $25 | Most intelligent, complex reasoning |

Note: Claude Haiku 3 (`claude-3-haiku-20240307`) is deprecated, retiring April 19, 2026. The `haiku` alias should resolve to `claude-haiku-4-5`.

---

## Architecture Patterns

### Pattern 1: Config Schema Extension for Per-Role Overrides

**What:** Add `model_overrides` object to `config.json` and `Config.cs` model. `ResolveModel()` checks overrides first, falls back to profile.

**When to use:** This is the only schema change needed.

```json
// config.json extension
{
  "model_profile": "balanced",
  "model_overrides": {
    "gfd-planner": "claude-opus-4-6",
    "gfd-executor": "haiku"
  }
}
```

```csharp
// Config.cs extension
public class Config
{
    public string ModelProfile { get; set; } = "balanced";
    public Dictionary<string, string> ModelOverrides { get; set; } = new();
    // ... existing fields
}

// ConfigService.ResolveModel() — updated logic
public static string ResolveModel(string cwd, string agentName)
{
    var config = LoadConfig(cwd);

    // Check per-role override first
    if (config.ModelOverrides.TryGetValue(agentName, out var overrideModel))
        return overrideModel;

    // Fall back to profile
    var profile = config.ModelProfile;
    if (!ModelProfiles.TryGetValue(profile, out var profileMap))
        profileMap = ModelProfiles["balanced"];
    return profileMap.TryGetValue(agentName, out var model) ? model : "sonnet";
}
```

**Config loading extension** — parse `model_overrides` in `LoadConfig()`:

```csharp
if (root.TryGetProperty("model_overrides", out var overrides))
{
    foreach (var prop in overrides.EnumerateObject())
        defaults.ModelOverrides[prop.Name] = prop.Value.GetString() ?? "";
}
```

### Pattern 2: Token Data from Headless Runs

**What:** The `ClaudeService.InvokeHeadless()` method currently uses `--output-format text`. Switching to `--output-format stream-json` and parsing the last `result` JSON line gives full token and cost data.

**Source:** Official TypeScript SDK reference — `SDKResultMessage` type:

```typescript
// From platform.claude.com/docs/en/agent-sdk/typescript
type SDKResultMessage = {
  type: "result";
  subtype: "success" | "error_max_turns" | ...;
  duration_ms: number;
  duration_api_ms: number;
  num_turns: number;
  result: string;                    // Only on success
  total_cost_usd: number;
  usage: {
    input_tokens: number;
    output_tokens: number;
    cache_creation_input_tokens: number;
    cache_read_input_tokens: number;
  };
  modelUsage: {
    [modelName: string]: {           // e.g. "claude-sonnet-4-6"
      inputTokens: number;
      outputTokens: number;
      cacheReadInputTokens: number;
      cacheCreationInputTokens: number;
      webSearchRequests: number;
      costUSD: number;
      contextWindow: number;
    }
  };
}
```

**Implementation approach for `ClaudeService`:**

```csharp
// Switch from text to stream-json
psi.ArgumentList.Add("--output-format");
psi.ArgumentList.Add("stream-json");

// After reading stdout, find the result line
var resultLine = stdout.Split('\n')
    .LastOrDefault(l => l.TrimStart().StartsWith("{") && l.Contains("\"type\":\"result\""));

TokenUsage? tokenUsage = null;
if (resultLine != null)
{
    // Parse JSON to extract total_cost_usd, usage.input_tokens, usage.output_tokens
    using var doc = JsonDocument.Parse(resultLine);
    // extract fields...
}
```

Note: `ClaudeService.InvokeHeadless()` currently detects success by checking stdout for `## RESEARCH COMPLETE` / `## PLANNING COMPLETE`. With `stream-json`, these strings appear inside the JSON `result` field, NOT as bare text. The success-detection logic must be updated to parse the result field content.

### Pattern 3: Token Reporting in Interactive Workflows

**What:** In interactive mode (orchestrator using `Task()` tool), the SDK does NOT surface `total_cost_usd` or token counts back to the parent agent. The `TaskOutput` type documents `usage` and `total_cost_usd` fields, but these are optional and may not be populated in all contexts.

**Practical approach:** Have workflow orchestrators write token summary to FEATURE.md based on what they know (model used, rough turn count from watching subagent work). For exact data, only headless runs (auto-research, auto-plan) have access.

**Token summary format for FEATURE.md:**

```markdown
## Token Usage

| Workflow | Date | Agent Role | Model | Est. Cost |
|----------|------|------------|-------|-----------|
| research | 2026-02-22 | gfd-researcher | claude-sonnet-4-6 | $0.04 |
| plan | 2026-02-22 | gfd-planner | claude-sonnet-4-6 | $0.08 |
| execute | 2026-02-22 | gfd-executor | claude-sonnet-4-6 | $0.15 |
| execute | 2026-02-22 | gfd-verifier | claude-sonnet-4-6 | $0.03 |
| **Total** | | | | **$0.30** |
```

For headless runs, populate from `SDKResultMessage.total_cost_usd`. For interactive runs, mark as "est." if only model/turns is known.

### Pattern 4: `/gfd:configure-models` Command

**What:** New command that walks users through each agent role with AskUserQuestion, shows current model, recommends model, warns on weak choices, persists to config.json.

**Files to create:**
- `commands/gfd/configure-models.md` — command entry point (follows same pattern as `plan-feature.md`)
- `get-features-done/workflows/configure-models.md` — workflow logic (or inline in command)

**Command entry point pattern** (from existing commands):

```markdown
---
name: gfd:configure-models
description: Configure which Claude model powers each agent role
allowed-tools: Read, Write, Edit, Bash, AskUserQuestion
---

<objective>Walk through each agent role to configure model selection, with recommendations and warnings.</objective>

<execution_context>
@/home/conroy/.claude/get-features-done/workflows/configure-models.md
</execution_context>
```

**Workflow steps:**
1. Load current model for each role via `gfd-tools resolve-model <agent>`
2. Load available model tiers from hardcoded list (haiku, sonnet, opus) + free text
3. For each agent role, use `AskUserQuestion` with recommended model highlighted
4. Warn if user picks a model weaker than minimum recommended for that role
5. Write `model_overrides` to `docs/features/config.json`
6. Show summary of changes

**Model recommendations by role (based on audit findings):**

| Agent Role | Minimum Viable | Recommended | Notes |
|------------|---------------|-------------|-------|
| `gfd-researcher` | haiku | sonnet | Needs web search, research synthesis |
| `gfd-planner` | sonnet | sonnet | Structured document creation |
| `gfd-executor` | sonnet | sonnet | Code changes, complex reasoning |
| `gfd-verifier` | haiku | haiku | Structured comparison, less creative |
| `gfd-codebase-mapper` | haiku | haiku | Structured summarization |

**Warning thresholds:**

| Agent Role | Warn if weaker than |
|------------|---------------------|
| `gfd-executor` | sonnet |
| `gfd-planner` | sonnet |
| `gfd-researcher` | haiku (anything works but haiku for research is low quality) |
| `gfd-verifier` | haiku |
| `gfd-codebase-mapper` | haiku |

### Pattern 5: Config.json Write Operation

**What:** The GfdTools CLI currently only READS config.json. A new `config-set` command is needed to write model overrides, OR the `/gfd:configure-models` workflow can write directly using the Write tool (as the workflow runs in an agent with Write access).

**Recommended approach:** Have the configure-models workflow write `docs/features/config.json` directly using the Write tool — same pattern used by other GFD agents for FEATURE.md updates. No new CLI command needed.

**Reading current config for display:**

```bash
# In configure-models workflow
CURRENT_PLANNER=$(gfd-tools resolve-model gfd-planner)
CURRENT_EXECUTOR=$(gfd-tools resolve-model gfd-executor)
# etc.
```

### Pattern 6: Audit Document

**What:** One-time document in `docs/` (e.g., `docs/token-audit.md`) analyzing all agent roles.

**Content structure:**
- Current model assignments per role (from ConfigService.cs profiles)
- Task complexity analysis per role (what does each agent actually do)
- Token consumption estimates per role (rough order of magnitude)
- Recommendations (model changes, prompt optimizations, redundancy elimination)

**Sources for audit:** Read all agent markdown files in `agents/`, all workflow files in `get-features-done/workflows/`, and the ConfigService model profiles.

### Anti-Patterns to Avoid

- **Adding a new CLI command for config writes:** The agent already has Write tool access. Adding a `config-set` CLI command creates unnecessary complexity.
- **Trying to intercept Task() token data in interactive mode:** The SDK does not reliably surface this. Accept the limitation and focus on headless-run accuracy.
- **Blocking weak model choices:** FEATURE.md is explicit: warn but allow.
- **Changing `--output-format text` without updating success detection:** The completion signal strings will appear inside JSON, not as bare text.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Model resolution | Custom lookup logic | `ConfigService.ResolveModel()` extension | Already in place, all agents call it |
| Config persistence | Custom config format | Extend existing `docs/features/config.json` | All agents already read this file |
| Interactive model selection | Custom UI | `AskUserQuestion` tool | GFD's standard interactive pattern |
| JSON parsing for token data | Regex hacks | `System.Text.Json.JsonDocument.Parse()` | Already used throughout `ConfigService.cs` |
| Cost calculation | Manual token math | `SDKResultMessage.total_cost_usd` | SDK provides this directly |

**Key insight:** The GFD codebase is already well-structured. The token tracking feature is an extension of existing patterns, not a new system.

---

## Common Pitfalls

### Pitfall 1: Success Detection Breaks When Switching to stream-json

**What goes wrong:** `ClaudeService.InvokeHeadless()` checks `stdout.Contains("## RESEARCH COMPLETE")`. With `--output-format stream-json`, each line is a JSON object. The completion strings appear inside the `"result"` field of the final JSON line, not as bare text. The existing check stops working.

**Why it happens:** The success check was written for `--output-format text` output.

**How to avoid:** Update `InvokeHeadless()` to parse the stream-json result line and check the `result` field content:

```csharp
// With stream-json: look for the result line and parse it
var resultLine = stdout.Split('\n')
    .LastOrDefault(l => l.Contains("\"type\":\"result\""));
bool success = false;
if (resultLine != null)
{
    using var doc = JsonDocument.Parse(resultLine);
    if (doc.RootElement.TryGetProperty("result", out var resultField))
    {
        var resultText = resultField.GetString() ?? "";
        success = resultText.Contains("## RESEARCH COMPLETE")
               || resultText.Contains("## PLANNING COMPLETE");
    }
}
```

**Warning signs:** Auto-research or auto-plan always reports "no completion signal found" after switching to stream-json.

### Pitfall 2: model_overrides Not Persisted Correctly

**What goes wrong:** The workflow writes model_overrides but the config.json format doesn't match what `LoadConfig()` expects, so overrides are silently ignored.

**Why it happens:** `LoadConfig()` has no `model_overrides` parsing until added in this feature.

**How to avoid:** Update both `Config.cs` (add property) and `LoadConfig()` (add parsing) and `GetAllFields()` (add output) simultaneously. Test with `gfd-tools resolve-model gfd-executor` after writing.

**Warning signs:** `gfd-tools resolve-model <agent>` returns profile-default model even after configure-models was run.

### Pitfall 3: Token Data Absent for Interactive Workflow Runs

**What goes wrong:** Orchestrators try to collect token data from Task() return values but get null/missing data.

**Why it happens:** The `TaskOutput.usage` field is optional in the SDK. For interactive Claude Code sessions (not headless), token data may not be surfaced to the parent agent context.

**How to avoid:** Design the token reporting for interactive runs to use a "best effort" model — log the model name and mark cost as "est." For exact data, add cost capture only to `ClaudeService.InvokeHeadless()` (auto-run paths).

**Warning signs:** Token costs always show zero or null for interactive workflow runs.

### Pitfall 4: config.json Concurrent Write

**What goes wrong:** If two parallel agents both try to write model_overrides to config.json, one write overwrites the other.

**Why it happens:** The GFD parallelization can run multiple plan executors concurrently.

**How to avoid:** The `/gfd:configure-models` command is a user-interactive command that runs alone (not in parallel). config.json writes only happen during configure-models. This is not a runtime concern; document it as a usage note.

### Pitfall 5: Orphaned model_profile After Adding model_overrides

**What goes wrong:** Users run configure-models and set per-role overrides, then later change `model_profile` in config.json manually. The overrides take precedence, making the profile change have no effect on overridden roles.

**Why it happens:** Priority ordering: overrides > profile.

**How to avoid:** The configure-models command should show both current effective model (after override) and profile-based model. Offer a "clear all overrides" option that removes model_overrides from config.json.

---

## Code Examples

Verified patterns from official sources:

### SDKResultMessage Structure (from official SDK TypeScript reference)

```typescript
// Source: platform.claude.com/docs/en/agent-sdk/typescript
// SDKResultMessage (success case)
{
  type: "result",
  subtype: "success",
  uuid: "...",
  session_id: "...",
  duration_ms: 45230,
  duration_api_ms: 38100,
  is_error: false,
  num_turns: 12,
  result: "## RESEARCH COMPLETE\n...",  // The text output
  total_cost_usd: 0.0423,
  usage: {
    input_tokens: 18432,
    output_tokens: 3210,
    cache_creation_input_tokens: 0,
    cache_read_input_tokens: 14200
  },
  modelUsage: {
    "claude-sonnet-4-6": {
      inputTokens: 18432,
      outputTokens: 3210,
      cacheReadInputTokens: 14200,
      cacheCreationInputTokens: 0,
      webSearchRequests: 0,
      costUSD: 0.0423,
      contextWindow: 200000
    }
  },
  permission_denials: []
}
```

### TaskOutput Structure (Task tool return)

```typescript
// Source: platform.claude.com/docs/en/agent-sdk/typescript — TaskOutput type
interface TaskOutput {
  result: string;           // Final result message from the subagent
  usage?: {
    input_tokens: number;
    output_tokens: number;
    cache_creation_input_tokens?: number;
    cache_read_input_tokens?: number;
  };
  total_cost_usd?: number;  // Note: optional — may not be populated
  duration_ms?: number;
}
```

### ModelUsage Structure (per-model breakdown in result)

```typescript
// Source: platform.claude.com/docs/en/agent-sdk/typescript — ModelUsage type
type ModelUsage = {
  inputTokens: number;
  outputTokens: number;
  cacheReadInputTokens: number;
  cacheCreationInputTokens: number;
  webSearchRequests: number;
  costUSD: number;
  contextWindow: number;
}
```

### Existing ConfigService.ResolveModel (codebase)

```csharp
// Source: get-features-done/GfdTools/Services/ConfigService.cs (current)
public static string ResolveModel(string cwd, string agentName)
{
    var config = LoadConfig(cwd);
    var profile = config.ModelProfile;

    if (!ModelProfiles.TryGetValue(profile, out var profileMap))
        profileMap = ModelProfiles["balanced"];

    return profileMap.TryGetValue(agentName, out var model) ? model : "sonnet";
}
```

### ClaudeService.InvokeHeadless (codebase — current text mode)

```csharp
// Source: get-features-done/GfdTools/Services/ClaudeService.cs
// Currently uses --output-format text
psi.ArgumentList.Add("--output-format");
psi.ArgumentList.Add("text");
// Success detection: stdout.Contains("## RESEARCH COMPLETE")
```

### Existing Command File Pattern (from commands/gfd/plan-feature.md)

```markdown
---
name: gfd:plan-feature
description: Create detailed plans for a feature
argument-hint: <feature-slug>
allowed-tools: Read, Write, Edit, Bash, Grep, Glob, AskUserQuestion, Task, WebSearch, WebFetch
---

<objective>Create executable plans...</objective>

<execution_context>
@/home/conroy/.claude/get-features-done/workflows/plan-feature.md
@...
</execution_context>
```

---

## State of the Art

| Old Approach | Current Approach | Impact |
|--------------|-----------------|--------|
| Node.js CLI (gfd-tools.cjs) | C# CLI (GfdTools.csproj) | C# is the current codebase — all additions go here |
| Global model profiles only | Per-role overrides (this feature) | More granular control |
| No token visibility | stream-json result capture | Exact cost data for headless runs |
| `--output-format text` | `--output-format stream-json` | Enables token/cost capture in auto-run |

---

## Open Questions

1. **TaskOutput.usage reliability in interactive mode**
   - What we know: The field is documented as optional in the SDK TypeScript reference
   - What's unclear: Whether it is populated in practice when an orchestrator calls Task() in a normal interactive session (not headless)
   - Recommendation: Design for "best effort" — only report exact data from headless runs; mark interactive-run costs as estimates

2. **configure-models command — profile vs override interaction**
   - What we know: FEATURE.md says "per agent role granularity"
   - What's unclear: Should configure-models offer to change the whole profile, or only add/clear overrides?
   - Recommendation: Support both — show profile first, then per-role overrides. "Reset to profile defaults" clears model_overrides dict.

3. **Audit document location**
   - What we know: FEATURE.md says "document in docs/"
   - What's unclear: `docs/` or `docs/features/` — top-level `docs/` has no existing markdown files, all are in `docs/features/`
   - Recommendation: Write to `docs/token-audit.md` at the top level of `docs/` since it's a project-wide document, not feature-specific

---

## Sources

### Primary (HIGH confidence)

- `get-features-done/GfdTools/Services/ConfigService.cs` — Current model profile structure, ResolveModel logic, config parsing
- `get-features-done/GfdTools/Services/ClaudeService.cs` — InvokeHeadless implementation, current output-format usage
- `get-features-done/GfdTools/Models/Config.cs` — Config schema
- `docs/features/config.json` — Actual deployed config
- `get-features-done/workflows/execute-feature.md` — How executor/verifier agents are spawned
- `get-features-done/workflows/plan-feature.md` — How researcher/planner/checker agents are spawned
- `commands/gfd/*.md` — Command file structure patterns

### Secondary (MEDIUM confidence)

- `platform.claude.com/docs/en/agent-sdk/typescript` — SDKResultMessage, TaskOutput, ModelUsage type definitions (fetched Feb 22, 2026)
- `platform.claude.com/docs/en/docs/about-claude/models` — Current model IDs, pricing, capabilities (fetched Feb 22, 2026)
- `code.claude.com/docs/en/cli-reference` — CLI flags including --output-format options (fetched Feb 22, 2026)

### Tertiary (LOW confidence)

- TaskOutput.usage field reliability in interactive mode — documented as optional, real-world behavior unverified without running tests

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — codebase read directly, SDK docs fetched from official source
- Architecture (config extension): HIGH — clear extension of existing patterns
- Architecture (stream-json token capture): HIGH — official SDK types verified
- Architecture (interactive token capture): MEDIUM — optional field, behavior unverified
- Pitfalls: HIGH — derived from direct codebase analysis
- Model recommendations for audit: MEDIUM — based on task analysis, not measured data

**Research date:** 2026-02-22
**Valid until:** 2026-05-22 (90 days — SDK APIs are stable but Claude models change frequently; verify model IDs before implementation)
