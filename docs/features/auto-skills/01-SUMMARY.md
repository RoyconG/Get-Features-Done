---
feature: auto-skills
plan: "01"
subsystem: infra
tags: [csharp, subprocess, process, claude-cli, headless]

# Dependency graph
requires: []
provides:
  - ClaudeService.InvokeHeadless() — async headless claude subprocess invocation with concurrent stdout/stderr capture
  - RunResult record — carries Success, Stdout, Stderr, ExitCode, DurationSeconds, AbortReason
  - ClaudeService.BuildAutoRunMd() — assembles AUTO-RUN.md markdown for success and abort cases
affects: [auto-skills]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ArgumentList.Add() for all subprocess args (no string concatenation) — matches GitService pattern"
    - "Concurrent stdout/stderr read via Task.Run + ReadToEndAsync to prevent pipe buffer deadlock"
    - "Success detection by stdout terminal signal strings, not exit code alone"
    - "Abort reason classification: max-turns, ambiguous decision point, no completion signal"

key-files:
  created:
    - get-features-done/GfdTools/Services/ClaudeService.cs
  modified: []

key-decisions:
  - "Prompt delivered via stdin (process.StandardInput.Write + Close) rather than as a CLI argument — avoids arg length limits for large prompts"
  - "Success signals are ## RESEARCH COMPLETE and ## PLANNING COMPLETE checked in stdout"
  - "Max-turns detection checks both stderr for 'max turns' and stdout for 'max-turns'"
  - "Abort reasons classified into 3 categories: max-turns reached, ambiguous decision point, no completion signal found"

patterns-established:
  - "ClaudeService.InvokeHeadless: async Task<RunResult> with ArgumentList.Add() for all args"
  - "Concurrent stderr read: Task.Run(() => process.StandardError.ReadToEnd()) alongside ReadToEndAsync()"

requirements-completed: []

# Metrics
duration: 1min
completed: 2026-02-20
---

# Feature [auto-skills] Plan 01: ClaudeService Summary

**Async headless claude subprocess service using ArgumentList.Add(), concurrent pipe reads, and stdout-signal success detection — shared infrastructure for all auto commands**

## Performance

- **Duration:** 1 min
- **Started:** 2026-02-20T19:02:27Z
- **Completed:** 2026-02-20T19:03:31Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- `ClaudeService.InvokeHeadless()` implemented with full subprocess lifecycle management
- Concurrent stdout/stderr read pattern prevents pipe buffer deadlock on large claude output
- `RunResult` record carries all 6 fields needed by Wave 2 commands
- `BuildAutoRunMd()` produces consistent AUTO-RUN.md content for both success and abort cases

## Task Commits

Each task was committed atomically:

1. **Task 1: Create ClaudeService with InvokeHeadless and RunResult** - `6d9c3ac` (feat)

**Plan metadata:** (docs commit — see final commit)

## Files Created/Modified
- `get-features-done/GfdTools/Services/ClaudeService.cs` — ClaudeService with InvokeHeadless(), RunResult record, and BuildAutoRunMd()

## Decisions Made
- Prompt delivered via stdin (Write then Close) rather than as a CLI arg — avoids shell arg length limits for large research/plan prompts
- `--allowedTools` added one flag per tool (ArgumentList.Add per tool), not comma-joined — matches claude CLI's expected format
- Success detection checks stdout for `## RESEARCH COMPLETE` OR `## PLANNING COMPLETE` (handles both auto commands with one service)
- Max-turns detection checks stderr for "max turns" (case-insensitive) AND stdout for "max-turns" to handle both signal paths documented in research

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Steps
- Wave 2 (Plan 02): AutoResearchCommand and AutoPlanCommand can import `ClaudeService` and `RunResult` without modification to this file
- Both commands use `InvokeHeadless()` for subprocess invocation and `BuildAutoRunMd()` for status artifact generation

---
*Feature: auto-skills*
*Completed: 2026-02-20*
