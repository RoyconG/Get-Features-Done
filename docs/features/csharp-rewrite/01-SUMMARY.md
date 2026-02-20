---
feature: csharp-rewrite
plan: 01
subsystem: infra
tags: [csharp, dotnet, system-commandline, cli, gfd-tools]

requires: []
provides:
  - C# console app at get-features-done/GfdTools/ targeting net10.0
  - OutputService: centralized key=value output for all CLI commands
  - ConfigService: config loading and model profile resolution
  - GitService: git execution via ArgumentList (no string concatenation)
  - FrontmatterService: YAML frontmatter parse/reconstruct with index-based loop
  - FeatureService: feature discovery and listing
  - 5 command groups: config-get, feature-update-status, feature-plan-index, list-features, frontmatter get/merge
affects: [csharp-rewrite]

tech-stack:
  added: [System.CommandLine 2.0.0-beta5.25306.1, dotnet 10]
  patterns:
    - "All stdout goes through OutputService.Write as key=value pairs"
    - "ArgumentList for git execution (not string concatenation)"
    - "Index-based frontmatter parsing (not indexOf)"

key-files:
  created:
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
  modified: []

key-decisions:
  - "Used System.CommandLine beta5 (2.0.0-beta5.25306.1) not beta4 - beta5 has SetAction/GetValue API"
  - "Renamed internal Regex helper to FilenameHelper to avoid conflict with System.Text.RegularExpressions.Regex"
  - "OutputService.Write normalizes bool to lowercase (C# default bool.ToString() is Title Case)"

patterns-established:
  - "Command pattern: static Create(string cwd) returns Command with SetAction"
  - "All output: Output.Write(key, value) / Output.WriteBool / Output.WriteList"
  - "Frontmatter parsing: index-based loop, not indexOf"

requirements-completed: []

duration: 11min
completed: 2026-02-20
---

# Feature [csharp-rewrite]: C# Rewrite Plan 01 Summary

**C# dotnet console app with OutputService, FrontmatterService (indexOf bug fixed), GitService (ArgumentList), and 5 working CLI command groups producing key=value output**

## Performance

- **Duration:** 11 min
- **Started:** 2026-02-20T15:46:43Z
- **Completed:** 2026-02-20T15:57:53Z
- **Tasks:** 2 (Task 1 was human-action checkpoint, already complete)
- **Files modified:** 14 created

## Accomplishments
- C# project compiles targeting net10.0 with System.CommandLine beta5
- All 5 services implemented (Output, Config, Git, Frontmatter, Feature)
- All 5 command groups produce correct key=value output with no JSON in stdout
- Frontmatter parser uses index-based loop (fixed JS indexOf bug)
- Git operations use ArgumentList (fixed JS string concatenation bug)
- Program.cs wires all commands including Plan 02 stubs (placeholders returning "not yet implemented")

## Task Commits

Each task was committed atomically:

1. **Task 1: Install .NET 10 SDK** - Human action (completed before this agent ran)
2. **Task 2: Create project scaffold and all services** - `3ebcc46` (feat)
3. **Task 3: Implement first batch of commands** - `d16b53a` (feat)

**Plan metadata:** (to be committed with docs)

## Files Created/Modified
- `get-features-done/GfdTools/GfdTools.csproj` - Project targeting net10.0, System.CommandLine beta5
- `get-features-done/GfdTools/Program.cs` - RootCommand with all command routing
- `get-features-done/GfdTools/Services/OutputService.cs` - Centralized key=value output
- `get-features-done/GfdTools/Services/ConfigService.cs` - Config loading and model profiles
- `get-features-done/GfdTools/Services/GitService.cs` - Git execution via ArgumentList
- `get-features-done/GfdTools/Services/FrontmatterService.cs` - YAML parser with index-based loop
- `get-features-done/GfdTools/Services/FeatureService.cs` - Feature discovery and listing
- `get-features-done/GfdTools/Models/FeatureInfo.cs` - Feature data model
- `get-features-done/GfdTools/Models/Config.cs` - Config data model
- `get-features-done/GfdTools/Commands/ConfigGetCommand.cs` - config-get command
- `get-features-done/GfdTools/Commands/FeatureUpdateStatusCommand.cs` - feature-update-status command
- `get-features-done/GfdTools/Commands/FeaturePlanIndexCommand.cs` - feature-plan-index command
- `get-features-done/GfdTools/Commands/ListFeaturesCommand.cs` - list-features command
- `get-features-done/GfdTools/Commands/FrontmatterCommands.cs` - frontmatter get/merge commands

## Decisions Made
- Used System.CommandLine beta5 instead of beta4: the plan said "2.0.3" which doesn't exist; beta5 has the `SetAction`/`GetValue` API the code was written for
- Renamed internal `Regex` helper to `FilenameHelper` to avoid collision with `System.Text.RegularExpressions.Regex` (which is globally available via ImplicitUsings)
- `OutputService.Write` normalizes booleans to lowercase: C# `bool.ToString()` returns "True"/"False" but we need "true"/"false" to match JS output

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] System.CommandLine version mismatch**
- **Found during:** Task 2 (build attempt)
- **Issue:** Plan specified version "2.0.3" which doesn't exist on nuget.org; beta4 (2.0.0-beta4.22272.1) uses `Handler.SetHandler()` API, not `SetAction`/`GetValue`
- **Fix:** Used beta5 (2.0.0-beta5.25306.1) which has the exact API the plan describes
- **Files modified:** GfdTools.csproj
- **Verification:** Build succeeded with 0 errors
- **Committed in:** 3ebcc46 (Task 2 commit)

**2. [Rule 3 - Blocking] Regex class name collision**
- **Found during:** Task 2 (build attempt)
- **Issue:** Custom `internal static class Regex` in FeatureService.cs collided with `System.Text.RegularExpressions.Regex` which is in scope via ImplicitUsings; also FrontmatterService needed explicit `using SystemRegex = ...` alias
- **Fix:** Renamed helper to `FilenameHelper`; added `SystemRegex` type alias in FrontmatterService
- **Files modified:** FeatureService.cs, FrontmatterService.cs
- **Verification:** Build succeeded
- **Committed in:** 3ebcc46 (Task 2 commit)

**3. [Rule 1 - Bug] Boolean output capitalization**
- **Found during:** Task 3 (config-get test)
- **Issue:** `config-get` showed `search_gitignored=False` instead of `search_gitignored=false`; C# `bool.ToString()` returns "True"/"False"
- **Fix:** `OutputService.Write` now checks `value is bool` and formats it as lowercase
- **Files modified:** Services/OutputService.cs
- **Verification:** `config-get` shows all booleans in lowercase
- **Committed in:** d16b53a (Task 3 commit)

---

**Total deviations:** 3 auto-fixed (1 blocking version, 1 blocking name collision, 1 output bug)
**Impact on plan:** All fixes necessary for correctness. No scope creep.

## Issues Encountered
- System.CommandLine beta5 Argument constructor only takes name (no description/defaultValue); set via `.Description` and `.DefaultValueFactory` properties instead
- System.CommandLine beta5 Option constructor only takes name and aliases; `IsRequired` property replaced with `.Required = true`

## User Setup Required
None - .NET 10 SDK was installed by user before this agent ran (Task 1 checkpoint).

## Next Steps
- Plan 02 implements: init new-project, init new-feature, init plan-feature, init execute-feature, init map-codebase, verify plan-structure, verify references, verify commits, validate health, verify-summary
- All Plan 02 commands already have placeholder `SetAction` stubs in Program.cs

---
*Feature: csharp-rewrite*
*Completed: 2026-02-20*
