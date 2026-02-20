---
feature: auto-skills
plan: "02"
subsystem: infra
tags: [csharp, cli, subprocess, claude-cli, headless, system-commandline]

# Dependency graph
requires:
  - feature: auto-skills
    plan: "01"
    provides: "ClaudeService.InvokeHeadless(), RunResult, BuildAutoRunMd() — headless subprocess infrastructure"
provides:
  - AutoResearchCommand — gfd-tools auto-research <slug> subcommand with pre-flight checks, prompt assembly, artifact verification, and git commit
  - AutoPlanCommand — gfd-tools auto-plan <slug> subcommand with pre-flight checks, prompt assembly, abort cleanup, and git commit
affects: [auto-skills]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Async SetAction via Func<ParseResult, CancellationToken, Task<int>> — required for async command handlers in System.CommandLine beta5"
    - "Pre-flight abort pattern: check feature state before calling claude, commit AUTO-RUN.md with abort reason"
    - "Abort cleanup pattern: delete partial PLAN.md files before committing AUTO-RUN.md on failure"
    - "Prompt assembly: read agent .md file at runtime + embed FEATURE.md content inline"

key-files:
  created:
    - get-features-done/GfdTools/Commands/AutoResearchCommand.cs
    - get-features-done/GfdTools/Commands/AutoPlanCommand.cs
  modified:
    - get-features-done/GfdTools/Program.cs

key-decisions:
  - "Async SetAction uses Func<ParseResult, CancellationToken, Task<int>> overload (not async lambda directly) — required by System.CommandLine beta5 API which does not accept async lambdas returning int via SetAction(async pr => ...)"
  - "CommitAutoRunMd helper is duplicated in each command class (not shared) — consistent with existing codebase pattern of self-contained command files"
  - "AutoPlanCommand deletes partial PLAN.md files before committing on abort — ensures no partial artifacts are committed"
  - "FEATURE.md status update is done inline via Regex.Replace in the command, not via gfd-tools feature-update-status subprocess"

patterns-established:
  - "Factory pattern: public static Command Create(string cwd) with private static async Task<int> RunAsync(...) for async commands"
  - "Pre-flight state check: FeatureService.FindFeature() + feature state properties (HasResearch, Plans.Count) before any expensive operations"
  - "Both success and abort paths write and commit AUTO-RUN.md via CommitAutoRunMd helper"

requirements-completed: []

# Metrics
duration: 4min
completed: 2026-02-20
---

# Feature [auto-skills] Plan 02: AutoResearchCommand + AutoPlanCommand Summary

**AutoResearchCommand and AutoPlanCommand CLI subcommands with pre-flight state checks, headless claude invocation via ClaudeService.InvokeHeadless(), and git-committed AUTO-RUN.md artifacts on both success and abort paths**

## Performance

- **Duration:** 4 min
- **Started:** 2026-02-20T19:05:21Z
- **Completed:** 2026-02-20T19:09:27Z
- **Tasks:** 3
- **Files modified:** 3

## Accomplishments
- `gfd-tools auto-research <slug>` subcommand implemented — pre-flight checks, prompt assembly, headless claude invocation, RESEARCH.md verification, git commit
- `gfd-tools auto-plan <slug>` subcommand implemented — pre-flight checks, prompt assembly, abort cleanup (deletes partial PLAN.md files), headless claude invocation, git commit
- Both commands registered in Program.cs following established comment-header pattern
- Both commands accept `--max-turns` (default 30) and `--model` (default sonnet) options
- Build verified clean with zero errors/warnings

## Task Commits

Each task was committed atomically:

1. **Task 1: Create AutoResearchCommand** - `8baf079` (feat)
2. **Task 2: Create AutoPlanCommand** - `14a7e15` (feat)
3. **Task 3: Register commands in Program.cs** - `b7d2e39` (feat)

**Plan metadata:** (docs commit — see final commit)

## Files Created/Modified
- `get-features-done/GfdTools/Commands/AutoResearchCommand.cs` — auto-research subcommand: pre-flight (HasResearch check), prompt from gfd-researcher.md agent, InvokeHeadless(), RESEARCH.md artifact verification, CommitAutoRunMd helper
- `get-features-done/GfdTools/Commands/AutoPlanCommand.cs` — auto-plan subcommand: pre-flight (Plans.Count check), prompt from gfd-planner.md agent + optional RESEARCH.md, InvokeHeadless(), abort cleanup (delete partial plans), CommitAutoRunMd helper
- `get-features-done/GfdTools/Program.cs` — added auto-research and auto-plan registration after summary-extract section

## Decisions Made
- **Async SetAction pattern:** The plan specified `cmd.SetAction(async pr => { ... })` but System.CommandLine beta5 does not accept async lambdas returning `int` via that overload. Fixed by extracting logic to `private static async Task<int> RunAsync(...)` and using `cmd.SetAction((ParseResult pr, CancellationToken ct) => RunAsync(...))`. This matches the available `Func<ParseResult, CancellationToken, Task<int>>` overload.
- **CommitAutoRunMd duplication:** Kept as private static in each command class (not extracted to shared helper) per the plan's explicit guidance and existing codebase pattern.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed async SetAction signature incompatibility**
- **Found during:** Task 1 (AutoResearchCommand creation)
- **Issue:** Plan's `cmd.SetAction(async pr => { return ...; })` pattern generates CS4010/CS8030/CS8031 compiler errors in System.CommandLine beta5 — the `SetAction(Func<ParseResult, int>)` overload cannot accept an async lambda, and the `SetAction(Func<ParseResult, Task>)` overload cannot return a value
- **Fix:** Extracted lambda body to `private static async Task<int> RunAsync(...)` and registered via `cmd.SetAction((ParseResult pr, CancellationToken ct) => RunAsync(...))` which matches the available `Func<ParseResult, CancellationToken, Task<int>>` overload
- **Files modified:** AutoResearchCommand.cs, AutoPlanCommand.cs (same pattern applied to both)
- **Verification:** `dotnet build` exits 0 with 0 errors, 0 warnings

---

**Total deviations:** 1 auto-fixed (Rule 1 - Bug)
**Impact on plan:** Fix required for compilation. Pattern is functionally equivalent to what the plan intended. No scope creep.

## Issues Encountered
None beyond the async SetAction fix documented above.

## User Setup Required
None - no external service configuration required.

## Next Steps
- Both `auto-research` and `auto-plan` commands are available as CLI subcommands
- Feature auto-skills acceptance criteria fully met: all 7 acceptance criteria satisfied
- The feature is now complete (Plans 01 + 02 both have summaries)

---
*Feature: auto-skills*
*Completed: 2026-02-20*
