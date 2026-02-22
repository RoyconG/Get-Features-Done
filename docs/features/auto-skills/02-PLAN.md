---
feature: auto-skills
plan: "02"
type: execute
wave: 2
depends_on: ["01"]
files_modified:
  - get-features-done/GfdTools/Commands/AutoResearchCommand.cs
  - get-features-done/GfdTools/Commands/AutoPlanCommand.cs
  - get-features-done/GfdTools/Program.cs
autonomous: true
acceptance_criteria:
  - "gfd-tools auto-research <slug> runs the research workflow headlessly and produces RESEARCH.md"
  - "gfd-tools auto-plan <slug> runs the planning workflow headlessly and produces PLAN.md files"
  - "Both commands abort cleanly on ambiguous decision points (e.g., feature already researched, plans already exist, checker fails 3x) without making destructive choices"
  - "On abort, partial progress is discarded but an AUTO-RUN.md status file is committed explaining what happened"
  - "On success, normal artifacts are committed plus an AUTO-RUN.md summarizing the run (duration, what was produced)"
  - "Max-turns is configurable (with a sensible default) to prevent runaway token spend"
  - "No AskUserQuestion calls in auto workflows — all interaction stripped, decisions logged to status file"

must_haves:
  truths:
    - "gfd-tools auto-research <slug> exits 0 and RESEARCH.md exists in the feature directory on success"
    - "gfd-tools auto-plan <slug> exits 0 and at least one PLAN.md file exists in the feature directory on success"
    - "Both commands write AUTO-RUN.md and commit it on both success and abort"
    - "Both commands pre-flight check feature state and abort (without calling claude) if already researched/planned"
    - "Both commands accept --max-turns <N> option (default 30) and pass it to ClaudeService.InvokeHeadless()"
    - "On abort, no RESEARCH.md or PLAN.md files are committed — only AUTO-RUN.md"
    - "No interactive prompts or AskUserQuestion patterns are used anywhere in the command logic"
  artifacts:
    - path: "get-features-done/GfdTools/Commands/AutoResearchCommand.cs"
      provides: "auto-research subcommand implementation"
      contains: "AutoResearchCommand"
    - path: "get-features-done/GfdTools/Commands/AutoPlanCommand.cs"
      provides: "auto-plan subcommand implementation"
      contains: "AutoPlanCommand"
    - path: "get-features-done/GfdTools/Program.cs"
      provides: "Command registration for auto-research and auto-plan"
      contains: "AutoResearchCommand.Create"
  key_links:
    - from: "get-features-done/GfdTools/Commands/AutoResearchCommand.cs"
      to: "get-features-done/GfdTools/Services/ClaudeService.cs"
      via: "ClaudeService.InvokeHeadless() async call"
      pattern: "ClaudeService\\.InvokeHeadless"
    - from: "get-features-done/GfdTools/Commands/AutoPlanCommand.cs"
      to: "get-features-done/GfdTools/Services/ClaudeService.cs"
      via: "ClaudeService.InvokeHeadless() async call"
      pattern: "ClaudeService\\.InvokeHeadless"
    - from: "get-features-done/GfdTools/Commands/AutoResearchCommand.cs"
      to: "get-features-done/GfdTools/Services/GitService.cs"
      via: "GitService.ExecGit() for git add + git commit of AUTO-RUN.md"
      pattern: "GitService\\.ExecGit"
    - from: "get-features-done/GfdTools/Program.cs"
      to: "get-features-done/GfdTools/Commands/AutoResearchCommand.cs"
      via: "rootCommand.Add(AutoResearchCommand.Create(cwd))"
      pattern: "AutoResearchCommand\\.Create"
---

<objective>
Create AutoResearchCommand and AutoPlanCommand — the two new CLI subcommands — and register them in Program.cs.

Purpose: These commands are the user-facing entry points for headless research and planning. They handle pre-flight state checks, assemble workflow prompts, delegate to ClaudeService, write AUTO-RUN.md, and commit outcomes to git. No user interaction occurs at any point.

Output: `AutoResearchCommand.cs`, `AutoPlanCommand.cs`, and updated `Program.cs` with both commands registered.
</objective>

<execution_context>
@$HOME/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/auto-skills/FEATURE.md
@docs/features/auto-skills/RESEARCH.md
@docs/features/auto-skills/01-SUMMARY.md
@get-features-done/GfdTools/Services/ClaudeService.cs
@get-features-done/GfdTools/Services/GitService.cs
@get-features-done/GfdTools/Services/FeatureService.cs
@get-features-done/GfdTools/Services/ConfigService.cs
@get-features-done/GfdTools/Commands/InitCommands.cs
@get-features-done/GfdTools/Program.cs
@$HOME/.claude/agents/gfd-researcher.md
@$HOME/.claude/agents/gfd-planner.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create AutoResearchCommand</name>
  <files>get-features-done/GfdTools/Commands/AutoResearchCommand.cs</files>
  <action>
Create `get-features-done/GfdTools/Commands/AutoResearchCommand.cs` following the `InitCommands.cs` factory pattern.

**Command structure:**
```csharp
namespace GfdTools.Commands;

public static class AutoResearchCommand
{
    public static Command Create(string cwd) { ... }
}
```

**Arguments and options:**
- `Argument<string> slugArg` — required, "feature slug"
- `Option<int> maxTurnsOpt` — `--max-turns`, default `30`, description "Max claude turns (cost guard)"
- `Option<string> modelOpt` — `--model`, default `"sonnet"`, description "Claude model tier (sonnet, opus, haiku)"

**SetAction implementation** (must be `async` — `cmd.SetAction(async pr => { ... })`):

**Step 1 — Pre-flight checks (abort without calling claude):**
```csharp
var slug = pr.GetValue(slugArg)!;
var maxTurns = pr.GetValue(maxTurnsOpt);
var model = pr.GetValue(modelOpt)!;

var featureInfo = FeatureService.FindFeature(cwd, slug);
if (featureInfo == null)
    return Output.Fail($"Feature '{slug}' not found");

// Abort if already researched
if (featureInfo.HasResearch)
{
    var abortMd = ClaudeService.BuildAutoRunMd(slug, "auto-research",
        new RunResult(false, "", "", 0, 0, "feature already has RESEARCH.md"),
        DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), []);
    CommitAutoRunMd(cwd, slug, featureInfo.Directory, abortMd, "abort: already researched");
    return Output.Fail($"Aborted: {slug} already has RESEARCH.md. Delete it first to re-research.");
}
```

**Step 2 — Assemble the research prompt:**

Build the prompt string that mirrors what `/gfd:research-feature` sends to the gfd-researcher agent. The prompt MUST include:
- The researcher agent role (read from `$HOME/.claude/agents/gfd-researcher.md` via `File.ReadAllText`)
- The feature slug and context instruction
- An explicit instruction to NOT use `AskUserQuestion` and to abort on ambiguity by outputting `## ABORT: <reason>` to stdout

Inline prompt assembly (do NOT read workflow files at runtime — build the string directly):
```csharp
var agentMd = File.ReadAllText("$HOME/.claude/agents/gfd-researcher.md");
var featureMdPath = Path.Combine(cwd, featureInfo.Directory, "FEATURE.md");
var featureMdContent = File.ReadAllText(featureMdPath);

var prompt = $"""
{agentMd}

---

## Auto-Research Task

**Feature slug:** {slug}
**Feature directory:** {featureInfo.Directory}
**Working directory:** {cwd}

## FEATURE.md Contents

{featureMdContent}

## Critical Auto-Run Rules

- You are running HEADLESSLY. There is NO user to ask questions.
- Do NOT call AskUserQuestion or emit any checkpoint prompts.
- If you encounter an ambiguous state that requires a user decision, output exactly:
  `## ABORT: <one-line reason>`
  Then stop immediately.
- On successful completion of research, output exactly:
  `## RESEARCH COMPLETE`
- Write RESEARCH.md to: {Path.Combine(cwd, featureInfo.Directory, "RESEARCH.md")}
""";
```

**Step 3 — Determine allowed tools:**
```csharp
var allowedTools = new[]
{
    "Read", "Write", "Bash", "Glob", "Grep", "WebFetch"
};
```

**Step 4 — Record start time and invoke:**
```csharp
var startedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
var result = await ClaudeService.InvokeHeadless(cwd, prompt, allowedTools, maxTurns, model);
```

**Step 5 — Determine artifacts produced:**
```csharp
var researchPath = Path.Combine(cwd, featureInfo.Directory, "RESEARCH.md");
var artifactsProduced = result.Success && File.Exists(researchPath)
    ? new[] { "RESEARCH.md" }
    : Array.Empty<string>();

// If claude claimed success but RESEARCH.md doesn't exist, treat as abort
if (result.Success && artifactsProduced.Length == 0)
{
    result = result with { Success = false, AbortReason = "RESEARCH.md not found after completion signal" };
}
```

**Step 6 — Build and commit AUTO-RUN.md:**
```csharp
var autoRunMd = ClaudeService.BuildAutoRunMd(slug, "auto-research", result, startedAt, artifactsProduced);
var commitMessage = result.Success
    ? $"feat({slug}): auto-research complete"
    : $"docs({slug}): auto-research aborted — {result.AbortReason}";
CommitAutoRunMd(cwd, slug, featureInfo.Directory, autoRunMd, commitMessage, artifactsProduced);
```

**Step 7 — Update feature status and output:**
```csharp
if (result.Success)
{
    // Update status via gfd-tools feature-update-status (call the C# logic directly)
    var featureMd = Path.Combine(cwd, featureInfo.Directory, "FEATURE.md");
    // Read FEATURE.md, replace status line, write back
    var featureContent = File.ReadAllText(featureMd);
    featureContent = System.Text.RegularExpressions.Regex.Replace(
        featureContent, @"^status:\s*.+$", "status: researched",
        System.Text.RegularExpressions.RegexOptions.Multiline);
    File.WriteAllText(featureMd, featureContent);

    Output.Write("result", "success");
    Output.Write("artifact", "RESEARCH.md");
    Output.Write("duration_seconds", result.DurationSeconds.ToString("F1"));
    return 0;
}
else
{
    Output.Write("result", "aborted");
    Output.Write("abort_reason", result.AbortReason);
    return 1;
}
```

**CommitAutoRunMd helper** (private static method in same class):
```csharp
private static void CommitAutoRunMd(string cwd, string slug, string featureDir,
    string autoRunMd, string commitMessage, string[]? artifactsProduced = null)
{
    var autoRunPath = Path.Combine(cwd, featureDir, "AUTO-RUN.md");
    File.WriteAllText(autoRunPath, autoRunMd);

    var filesToAdd = new List<string> { Path.Combine(featureDir, "AUTO-RUN.md") };
    if (artifactsProduced != null)
        filesToAdd.AddRange(artifactsProduced.Select(a => Path.Combine(featureDir, a)));

    // git add
    var addArgs = new[] { "add" }.Concat(filesToAdd).ToArray();
    GitService.ExecGit(cwd, addArgs);

    // git commit
    GitService.ExecGit(cwd, ["commit", "-m", commitMessage]);
}
```
  </action>
  <verify>
```bash
cd ./get-features-done/GfdTools && dotnet build 2>&1
```
Build exits 0. Then confirm file exists:
```bash
ls ./get-features-done/GfdTools/Commands/AutoResearchCommand.cs
```
  </verify>
  <done>
`dotnet build` exits 0. `AutoResearchCommand.cs` exists. The command has `--max-turns` option with default 30. The command pre-flight checks for existing RESEARCH.md and aborts without calling claude. `CommitAutoRunMd` helper handles both success and abort paths.
  </done>
</task>

<task type="auto">
  <name>Task 2: Create AutoPlanCommand</name>
  <files>get-features-done/GfdTools/Commands/AutoPlanCommand.cs</files>
  <action>
Create `get-features-done/GfdTools/Commands/AutoPlanCommand.cs` following the same pattern as `AutoResearchCommand.cs`.

**Command structure:**
```csharp
namespace GfdTools.Commands;

public static class AutoPlanCommand
{
    public static Command Create(string cwd) { ... }
}
```

**Arguments and options:** Same as AutoResearchCommand: `slugArg`, `--max-turns` (default 30), `--model` (default "sonnet").

**SetAction implementation** (async):

**Step 1 — Pre-flight checks:**
```csharp
var slug = pr.GetValue(slugArg)!;
var maxTurns = pr.GetValue(maxTurnsOpt);
var model = pr.GetValue(modelOpt)!;

var featureInfo = FeatureService.FindFeature(cwd, slug);
if (featureInfo == null)
    return Output.Fail($"Feature '{slug}' not found");

// Abort if plans already exist
if (featureInfo.Plans.Count > 0)
{
    var abortMd = ClaudeService.BuildAutoRunMd(slug, "auto-plan",
        new RunResult(false, "", "", 0, 0, $"feature already has {featureInfo.Plans.Count} PLAN file(s)"),
        DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), []);
    CommitAutoRunMd(cwd, slug, featureInfo.Directory, abortMd, "abort: plans already exist");
    return Output.Fail($"Aborted: {slug} already has PLAN files. Delete them first to re-plan.");
}

// Warn but don't abort if no RESEARCH.md (planning can proceed without research)
```

**Step 2 — Assemble the planning prompt:**
```csharp
var agentMd = File.ReadAllText("$HOME/.claude/agents/gfd-planner.md");
var featureMdPath = Path.Combine(cwd, featureInfo.Directory, "FEATURE.md");
var featureMdContent = File.ReadAllText(featureMdPath);

// Include RESEARCH.md if it exists
var researchContent = "";
var researchPath = Path.Combine(cwd, featureInfo.Directory, "RESEARCH.md");
if (File.Exists(researchPath))
    researchContent = $"\n\n## RESEARCH.md Contents\n\n{File.ReadAllText(researchPath)}";

var prompt = $"""
{agentMd}

---

## Auto-Plan Task

**Feature slug:** {slug}
**Feature directory:** {featureInfo.Directory}
**Working directory:** {cwd}

## FEATURE.md Contents

{featureMdContent}{researchContent}

## Critical Auto-Run Rules

- You are running HEADLESSLY. There is NO user to ask questions.
- Do NOT call AskUserQuestion or emit any checkpoint prompts.
- If you encounter an ambiguous state that requires a user decision, output exactly:
  `## ABORT: <one-line reason>`
  Then stop immediately.
- On successful completion of planning, output exactly:
  `## PLANNING COMPLETE`
- Write all PLAN.md files to: {Path.Combine(cwd, featureInfo.Directory)}
- File naming: 01-PLAN.md, 02-PLAN.md, etc.
""";
```

**Step 3 — Allowed tools:**
```csharp
var allowedTools = new[]
{
    "Read", "Write", "Bash", "Glob", "Grep", "WebFetch"
};
```

**Step 4 — Invoke:**
```csharp
var startedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
var result = await ClaudeService.InvokeHeadless(cwd, prompt, allowedTools, maxTurns, model);
```

**Step 5 — Detect artifacts produced:**
```csharp
// Scan the feature directory for any *-PLAN.md files created after invocation
var featureDirFull = Path.Combine(cwd, featureInfo.Directory);
var planFiles = Directory.GetFiles(featureDirFull, "*-PLAN.md")
    .Select(Path.GetFileName)
    .Where(f => f != null)
    .Select(f => f!)
    .OrderBy(f => f)
    .ToArray();

var artifactsProduced = result.Success && planFiles.Length > 0
    ? planFiles
    : Array.Empty<string>();

// If claude claimed success but no PLAN.md files exist, treat as abort
if (result.Success && artifactsProduced.Length == 0)
{
    result = result with { Success = false, AbortReason = "no PLAN.md files found after completion signal" };
}
```

**Step 6 — Commit AUTO-RUN.md:**

On **abort**: commit ONLY AUTO-RUN.md. Do NOT add any partial PLAN.md files.
On **success**: commit AUTO-RUN.md + all plan files + FEATURE.md (status update).

```csharp
var autoRunMd = ClaudeService.BuildAutoRunMd(slug, "auto-plan", result, startedAt, artifactsProduced);
var commitMessage = result.Success
    ? $"feat({slug}): auto-plan complete ({artifactsProduced.Length} plan(s))"
    : $"docs({slug}): auto-plan aborted — {result.AbortReason}";

if (result.Success)
{
    // Update feature status to "planned" in FEATURE.md
    var featureMd = Path.Combine(cwd, featureInfo.Directory, "FEATURE.md");
    var featureContent = File.ReadAllText(featureMd);
    featureContent = System.Text.RegularExpressions.Regex.Replace(
        featureContent, @"^status:\s*.+$", "status: planned",
        System.Text.RegularExpressions.RegexOptions.Multiline);
    File.WriteAllText(featureMd, featureContent);

    // Include FEATURE.md + all plan files in commit
    CommitAutoRunMd(cwd, slug, featureInfo.Directory, autoRunMd, commitMessage,
        artifactsProduced.Append("FEATURE.md").ToArray());
}
else
{
    // Abort: delete any partial PLAN.md files before committing
    foreach (var planFile in planFiles)
    {
        var planPath = Path.Combine(featureDirFull, planFile);
        if (File.Exists(planPath)) File.Delete(planPath);
    }
    CommitAutoRunMd(cwd, slug, featureInfo.Directory, autoRunMd, commitMessage);
}
```

**Step 7 — Output:**
```csharp
if (result.Success)
{
    Output.Write("result", "success");
    Output.Write("plan_count", artifactsProduced.Length);
    foreach (var p in artifactsProduced) Output.Write("plan", p);
    Output.Write("duration_seconds", result.DurationSeconds.ToString("F1"));
    return 0;
}
else
{
    Output.Write("result", "aborted");
    Output.Write("abort_reason", result.AbortReason);
    return 1;
}
```

**CommitAutoRunMd helper** (same pattern as AutoResearchCommand — copy the private static method identically, or extract to a shared location if preferred; keeping it duplicated in each command class is acceptable per the existing codebase pattern of self-contained command files).
  </action>
  <verify>
```bash
cd ./get-features-done/GfdTools && dotnet build 2>&1
```
Build exits 0. Confirm file exists:
```bash
ls ./get-features-done/GfdTools/Commands/AutoPlanCommand.cs
```
  </verify>
  <done>
`dotnet build` exits 0. `AutoPlanCommand.cs` exists. The command pre-flight aborts if PLAN.md files already exist. Abort path deletes partial PLAN.md files before committing AUTO-RUN.md only. Success path commits FEATURE.md + all plan files + AUTO-RUN.md.
  </done>
</task>

<task type="auto">
  <name>Task 3: Register commands in Program.cs and verify end-to-end build</name>
  <files>get-features-done/GfdTools/Program.cs</files>
  <action>
Add the two new commands to `Program.cs` following the existing registration pattern.

Read the current `Program.cs`. Find the `// ─── summary-extract ─────────────────────────────────────────────────────────` section (last registered command before the inline commands). After it, add:

```csharp
// ─── auto-research ───────────────────────────────────────────────────────────
rootCommand.Add(AutoResearchCommand.Create(cwd));

// ─── auto-plan ───────────────────────────────────────────────────────────────
rootCommand.Add(AutoPlanCommand.Create(cwd));
```

The namespace `GfdTools.Commands` is already referenced via `using GfdTools.Commands;` at the top of Program.cs — confirm it is present and add if missing.

After updating Program.cs, run a full build and then verify the commands appear in the CLI help:
```bash
cd ./get-features-done/GfdTools && dotnet build -o /tmp/gfd-tools-build 2>&1
/tmp/gfd-tools-build/gfd-tools --help 2>&1 | grep -E "auto-research|auto-plan"
```

Both `auto-research` and `auto-plan` must appear in the help output.

Also verify `--max-turns` option appears in subcommand help:
```bash
/tmp/gfd-tools-build/gfd-tools auto-research --help 2>&1
/tmp/gfd-tools-build/gfd-tools auto-plan --help 2>&1
```

Both must show `--max-turns` with default value of `30`.
  </action>
  <verify>
```bash
cd ./get-features-done/GfdTools && dotnet build 2>&1 && echo "BUILD OK"
```
Then:
```bash
cd ./get-features-done/GfdTools && dotnet build -o /tmp/gfd-tools-build -q && /tmp/gfd-tools-build/gfd-tools --help
```
  </verify>
  <done>
`dotnet build` exits 0. `gfd-tools --help` lists `auto-research` and `auto-plan`. `gfd-tools auto-research --help` shows `--max-turns` option. `gfd-tools auto-plan --help` shows `--max-turns` option. `Program.cs` has registration lines for both commands following the established comment-header pattern.
  </done>
</task>

</tasks>

<verification>
1. `dotnet build` from `get-features-done/GfdTools/` exits 0 with no errors or warnings
2. `gfd-tools --help` lists `auto-research` and `auto-plan`
3. `gfd-tools auto-research --help` shows: slug argument, --max-turns option (default 30), --model option
4. `gfd-tools auto-plan --help` shows: slug argument, --max-turns option (default 30), --model option
5. Both command files follow the factory pattern: `public static Command Create(string cwd)`
6. Both commands use `ClaudeService.InvokeHeadless()` — no direct `Process.Start` in command files
7. Both commands write and commit AUTO-RUN.md on both success and abort paths
8. AutoPlanCommand deletes partial PLAN.md files on abort before committing
9. No `AskUserQuestion` calls anywhere in the implementation
</verification>

<success_criteria>
`gfd-tools auto-research <slug>` and `gfd-tools auto-plan <slug>` are available as CLI subcommands. Both accept `--max-turns` and `--model` options. Both perform pre-flight state checks. Both commit AUTO-RUN.md on success and abort. The build is clean with zero errors.
</success_criteria>

<output>
After completion, create `docs/features/auto-skills/02-SUMMARY.md` following the summary template at `$HOME/.claude/get-features-done/templates/summary.md`.
</output>
