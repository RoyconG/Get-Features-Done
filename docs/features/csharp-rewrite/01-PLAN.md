---
feature: csharp-rewrite
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - get-features-done/GfdTools/global.json
  - get-features-done/GfdTools/GfdTools.csproj
  - get-features-done/GfdTools/Program.cs
  - get-features-done/GfdTools/Services/OutputService.cs
  - get-features-done/GfdTools/Services/ConfigService.cs
  - get-features-done/GfdTools/Services/GitService.cs
  - get-features-done/GfdTools/Services/FrontmatterService.cs
  - get-features-done/GfdTools/Services/FeatureService.cs
  - get-features-done/GfdTools/Models/FeatureInfo.cs
  - get-features-done/GfdTools/Models/Config.cs
  - get-features-done/GfdTools/Commands/ConfigGetCommand.cs
  - get-features-done/GfdTools/Commands/FeatureUpdateStatusCommand.cs
  - get-features-done/GfdTools/Commands/FeaturePlanIndexCommand.cs
  - get-features-done/GfdTools/Commands/ListFeaturesCommand.cs
  - get-features-done/GfdTools/Commands/FrontmatterCommands.cs
autonomous: false
acceptance_criteria:
  - "C# console app builds and runs via `dotnet run`"
  - "Output uses key=value pairs instead of JSON"
  - "Bugs found during porting are fixed, not replicated"
user_setup:
  - service: dotnet-sdk-10
    why: ".NET 10 SDK required to build the project"
    env_vars: []
    dashboard_config:
      - task: "Install .NET 10 SDK"
        location: "Terminal: sudo dnf install dotnet-sdk-10.0"

must_haves:
  truths:
    - "C# project compiles without errors targeting net10.0"
    - "dotnet run --project get-features-done/GfdTools/ produces output"
    - "All output goes through OutputService as key=value pairs"
    - "Frontmatter parser uses index-based iteration (bug fix from JS)"
    - "Git operations use ArgumentList (array form, not string concatenation)"
  artifacts:
    - path: "get-features-done/GfdTools/GfdTools.csproj"
      provides: "Project file targeting net10.0 with System.CommandLine 2.0.3"
      contains: "net10.0"
    - path: "get-features-done/GfdTools/Program.cs"
      provides: "CLI entry point with all command routing"
      contains: "RootCommand"
    - path: "get-features-done/GfdTools/Services/OutputService.cs"
      provides: "Centralized key=value output"
      exports: ["Write", "WriteBool", "WriteList", "Fail"]
    - path: "get-features-done/GfdTools/Services/FrontmatterService.cs"
      provides: "YAML frontmatter parsing with index-based loop (fixed bug)"
      exports: ["Extract", "Reconstruct", "Splice"]
    - path: "get-features-done/GfdTools/Services/FeatureService.cs"
      provides: "Feature discovery and listing"
      exports: ["FindFeature", "ListFeatures"]
  key_links:
    - from: "get-features-done/GfdTools/Commands/*.cs"
      to: "get-features-done/GfdTools/Services/OutputService.cs"
      via: "Output.Write calls"
      pattern: "Output\\.Write"
---

<objective>
Create the C# console app project with core services and the first batch of commands: config-get, feature-update-status, list-features, feature-plan-index, frontmatter get/merge.

Purpose: Establish the full project structure, all shared services, and the simpler commands so Plan 02 can focus on the more complex init and verify commands.
Output: A compilable C# project at get-features-done/GfdTools/ with ~7 working commands.
</objective>

<execution_context>
@/home/conroy/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/csharp-rewrite/FEATURE.md
@docs/features/csharp-rewrite/RESEARCH.md
@get-features-done/bin/gfd-tools.cjs
</context>

<tasks>

<task type="checkpoint:human-action" gate="blocking">
  <name>Task 1: Install .NET 10 SDK</name>
  <action>
The .NET 10 SDK must be installed before the project can be created. This requires sudo privileges which Claude cannot provide.

Run: `sudo dnf install dotnet-sdk-10.0`

After installation, verify with: `dotnet --list-sdks` (should show 10.x.x alongside 8.x.x)
  </action>
  <resume-signal>Type "installed" after running `sudo dnf install dotnet-sdk-10.0` and confirming `dotnet --list-sdks` shows 10.x.x</resume-signal>
</task>

<task type="auto">
  <name>Task 2: Create project scaffold and all services</name>
  <files>
    get-features-done/GfdTools/global.json
    get-features-done/GfdTools/GfdTools.csproj
    get-features-done/GfdTools/Program.cs
    get-features-done/GfdTools/Services/OutputService.cs
    get-features-done/GfdTools/Services/ConfigService.cs
    get-features-done/GfdTools/Services/GitService.cs
    get-features-done/GfdTools/Services/FrontmatterService.cs
    get-features-done/GfdTools/Services/FeatureService.cs
    get-features-done/GfdTools/Models/FeatureInfo.cs
    get-features-done/GfdTools/Models/Config.cs
  </files>
  <action>
1. Create `get-features-done/GfdTools/` directory structure: Commands/, Models/, Services/

2. Create `global.json` pinning SDK to 10.0.100 with `"rollForward": "latestFeature"`

3. Create `GfdTools.csproj` targeting `net10.0`, referencing `System.CommandLine` version `2.0.3`. Set `<OutputType>Exe</OutputType>`. No other NuGet dependencies.

4. Create `Services/OutputService.cs` — static class `Output`:
   - `Write(string key, object? value)` -> `Console.WriteLine($"{key}={value}")`
   - `WriteBool(string key, bool value)` -> lowercase true/false
   - `WriteList(string key, IEnumerable<string> values)` -> repeated keys (one line per value)
   - `Fail(string message)` -> `Console.Error.WriteLine(message)`, return 1
   All stdout output in the entire project MUST go through this class.

5. Create `Services/ConfigService.cs` — port `loadConfig` from gfd-tools.cjs (lines 36-57). Reads `docs/features/config.json`, merges with defaults. Defaults: model_profile=balanced, search_gitignored=false, research=true, plan_checker=true, verifier=true, parallelization=true, auto_advance=false, path_prefix=docs/features. Do NOT include `commit_docs` — docs are always committed. Use `System.Text.Json` for JSON parsing (it's built-in, not a NuGet dep).

6. Create `Services/GitService.cs` — port `execGit` and `isGitIgnored`:
   - `ExecGit(string cwd, string[] args)` returns `(int exitCode, string stdout, string stderr)`.
   - CRITICAL: Use `ProcessStartInfo` with `ArgumentList` (add each arg individually via `psi.ArgumentList.Add(arg)`), NOT string concatenation. This fixes shell quoting bugs in the JS version.
   - `IsGitIgnored(string cwd, string relPath)` — runs `git check-ignore -q <relPath>`.

7. Create `Services/FrontmatterService.cs` — port `extractFrontmatter`, `reconstructFrontmatter`, `spliceFrontmatter`:
   - CRITICAL BUG FIX: The JS version uses `lines.indexOf(line)` at line 134 of gfd-tools.cjs to find the current line index. This breaks when two lines have identical content. Port using `for (int i = 0; i < lines.Length; i++)` and access `lines[i + 1]` directly.
   - Handle: top-level key:value, inline arrays `[a, b]`, multi-line arrays (`- item`), nested objects (2-4 space indent), boolean/integer coercion, quoted strings.
   - `Reconstruct` should match JS behavior: empty arrays as `[]`, short arrays inline, long arrays as multi-line `- item`.

8. Create `Services/FeatureService.cs` — port `findFeatureInternal` and `listFeaturesInternal`:
   - `FindFeature(string cwd, string slug)` returns `FeatureInfo?`
   - `ListFeatures(string cwd)` returns sorted list (by priority then status, same order as JS)
   - Skip `codebase` directory when listing

9. Create `Models/FeatureInfo.cs` — record/class with: Found, Slug, Name, Status, Owner, Assignees, Priority, DependsOn, Directory, FeatureMd, Plans, Summaries, IncompletePlans, HasResearch, HasVerification, Frontmatter

10. Create `Models/Config.cs` — class with all config fields matching JS defaults

11. Create `Program.cs` — Set up `RootCommand("GFD Tools CLI")`. Wire ALL commands that will be implemented across this plan and Plan 02. For Plan 02 commands not yet implemented, add the command/subcommand routing with a placeholder `Output.Fail("not yet implemented")` action. Use System.CommandLine 2.0 API: `new Command(name, description)`, `Arguments.Add()`, `Options.Add()`, `SetAction(pr => { ... })`. Use `pr.GetValue(arg)` to extract values.

12. Run `dotnet build` to verify compilation. Fix any errors.

Model resolution: Port the MODEL_PROFILES dictionary and `resolveModelInternal` into ConfigService. The profiles map agent names to model tiers (quality/balanced/budget).
  </action>
  <verify>
Run `dotnet build --project get-features-done/GfdTools/` — must succeed with 0 errors.
Run `dotnet run --project get-features-done/GfdTools/ -- --help` — must show command list.
  </verify>
  <done>Project compiles, --help shows available commands, all services exist with correct implementations including the frontmatter index-based loop bug fix and git ArgumentList fix.</done>
</task>

<task type="auto">
  <name>Task 3: Implement first batch of commands</name>
  <files>
    get-features-done/GfdTools/Commands/ConfigGetCommand.cs
    get-features-done/GfdTools/Commands/FeatureUpdateStatusCommand.cs
    get-features-done/GfdTools/Commands/FeaturePlanIndexCommand.cs
    get-features-done/GfdTools/Commands/ListFeaturesCommand.cs
    get-features-done/GfdTools/Commands/FrontmatterCommands.cs
    get-features-done/GfdTools/Program.cs
  </files>
  <action>
Implement each command by porting the corresponding JS function from gfd-tools.cjs. All output goes through OutputService. All commands return int (0=success, 1=error via Output.Fail).

1. **ConfigGetCommand.cs** — port `cmdConfigGet` (lines 452-459):
   - Argument: `key` (string, optional)
   - If key provided: output `key=value`. If no key: output all config fields as key=value pairs.
   - Do NOT output `commit_docs` — that config option is removed.

2. **FeatureUpdateStatusCommand.cs** — port `cmdFeatureUpdateStatus` (lines 894-916):
   - Arguments: `slug`, `status`
   - Validate status against allowed list
   - Output: `updated=true/false`, `old_status=`, `new_status=`

3. **FeaturePlanIndexCommand.cs** — port `cmdFeaturePlanIndex` (lines 365-419):
   - Argument: `slug`
   - Read each plan's frontmatter, determine completion status
   - Output: `slug=`, `plan_count=`, `complete_count=`, then for each plan: `plan_id=`, `plan_file=`, `plan_type=`, `plan_wave=`, `plan_status=`, `plan_autonomous=`. Group wave info.

4. **ListFeaturesCommand.cs** — port `cmdListFeatures` (lines 341-363):
   - Option: `--status` (string, optional filter)
   - Output: `count=`, `total=`, then per-feature: `feature_slug=`, `feature_name=`, `feature_status=`, `feature_owner=`, `feature_priority=`. Also `by_status_new=`, `by_status_planned=`, etc.

5. **FrontmatterCommands.cs** — port `cmdFrontmatterGet` and `cmdFrontmatterMerge` (lines 544-585):
   - `frontmatter get <file> [--field <name>]`
   - `frontmatter merge <file> --data <json>`
   - Output: field values as key=value pairs
   - Do NOT port `frontmatter set` — `feature-update-status` handles the common case, Edit tool handles the rest.
   - Do NOT port `frontmatter validate` here — that goes in Plan 02 as a new command.

6. Update `Program.cs` to wire all these commands with proper argument/option definitions. Remove placeholder actions for commands implemented here.

7. Run `dotnet build` to verify. Test each command:
    - `dotnet run --project get-features-done/GfdTools/ -- list-features`
    - `dotnet run --project get-features-done/GfdTools/ -- config-get model_profile`
    - `dotnet run --project get-features-done/GfdTools/ -- frontmatter get docs/features/csharp-rewrite/FEATURE.md`
  </action>
  <verify>
Run all three test commands above and confirm key=value output format. No JSON in stdout.
`dotnet run --project get-features-done/GfdTools/ -- list-features` should show csharp-rewrite feature.
`dotnet run --project get-features-done/GfdTools/ -- frontmatter get docs/features/csharp-rewrite/FEATURE.md --field status` should output `status=planning`.
  </verify>
  <done>All 5 command groups (config-get, feature-update-status, feature-plan-index, list-features, frontmatter get/merge) produce key=value output. Dropped commands: commit (plain git), feature add-decision/add-blocker (Claude Edit tool), frontmatter set (feature-update-status covers it), progress bar (dropped feature).</done>
</task>

</tasks>

<verification>
- `dotnet build --project get-features-done/GfdTools/` succeeds
- `dotnet run --project get-features-done/GfdTools/ -- --help` shows all commands
- `dotnet run --project get-features-done/GfdTools/ -- list-features` outputs key=value pairs (no JSON)
- `dotnet run --project get-features-done/GfdTools/ -- frontmatter get docs/features/csharp-rewrite/FEATURE.md` returns frontmatter fields as key=value
- `dotnet run --project get-features-done/GfdTools/ -- config-get` returns config values
</verification>

<success_criteria>
- C# project compiles targeting net10.0 with System.CommandLine 2.0.3
- All services (Output, Config, Git, Frontmatter, Feature) are implemented
- All 5 command groups produce correct key=value output
- Frontmatter parser uses index-based loop (not indexOf)
- Git operations use ArgumentList (not string concatenation)
</success_criteria>

<output>
After completion, create `docs/features/csharp-rewrite/01-SUMMARY.md`
</output>
