---
feature: review-token-usage
plan: 02
subsystem: infra
tags: [dotnet, csharp, token-tracking, cost-reporting, stream-json, claude-api]

# Dependency graph
requires: []
provides:
  - stream-json output format in ClaudeService headless invocation
  - Token cost data (TotalCostUsd, InputTokens, OutputTokens, CacheReadTokens) on RunResult
  - AUTO-RUN.md Cost line when cost data is available
  - FEATURE.md Token Usage table row appended after each successful auto-research or auto-plan run
affects: [review-token-usage]

# Tech tracking
tech-stack:
  added: [System.Text.Json (JsonDocument for stream-json result line parsing)]
  patterns:
    - Parse final stream-json result line to extract both agent text output and token data
    - Accumulate FEATURE.md edits before git commit to keep all changes in one atomic commit

key-files:
  created: []
  modified:
    - get-features-done/GfdTools/Services/ClaudeService.cs
    - get-features-done/GfdTools/Commands/AutoResearchCommand.cs
    - get-features-done/GfdTools/Commands/AutoPlanCommand.cs

key-decisions:
  - "Use stream-json output format to get both agent text and token cost data from a single invocation"
  - "Parse only the final result-type JSON line — earlier lines are tool calls and intermediary messages"
  - "Maintain FEATURE.md edits (status + token row) BEFORE the git commit so one commit captures all changes"
  - "Fall back to est. in cost column when TotalCostUsd is zero (agent may not return cost data)"

patterns-established:
  - "RunResult record carries optional token fields with defaults — callers that don't need token data are unaffected"
  - "AppendTokenUsageToFeatureMd static helper: creates ## Token Usage section if absent, appends row to existing section"

requirements-completed: []

# Metrics
duration: 3min
completed: 2026-02-23
---

# Feature [review-token-usage] Plan 02: Token Usage in Headless Workflows Summary

**ClaudeService switches to stream-json output to capture real token cost data, which is appended as a cumulative table row to FEATURE.md after each successful auto-research or auto-plan run.**

## Performance

- **Duration:** 3 min
- **Started:** 2026-02-23T21:05:43Z
- **Completed:** 2026-02-23T21:08:41Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- ClaudeService now uses `--output-format stream-json` and parses the final result JSON line to extract `resultText`, `TotalCostUsd`, `InputTokens`, `OutputTokens`, and `CacheReadTokens`
- RunResult record extended with four optional token fields; all existing callers remain compatible via default values
- SUCCESS/abort detection updated to use parsed `resultText` (not raw stdout), which is the correct source with stream-json format
- AUTO-RUN.md includes a `**Cost:** $X.XXXX` line when cost data is available
- Both AutoResearchCommand and AutoPlanCommand append a `## Token Usage` table row to FEATURE.md on the success path before the git commit

## Task Commits

Each task was committed atomically:

1. **Task 1: Switch ClaudeService to stream-json and capture token data** - `0ebcfd5` (feat)
2. **Task 2: Append token usage rows to FEATURE.md from auto-research and auto-plan** - `117b8cc` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified
- `get-features-done/GfdTools/Services/ClaudeService.cs` - stream-json format, RunResult token fields, result line parsing, cost line in AUTO-RUN.md
- `get-features-done/GfdTools/Commands/AutoResearchCommand.cs` - AppendTokenUsageToFeatureMd helper, restructured success path to write FEATURE.md before commit
- `get-features-done/GfdTools/Commands/AutoPlanCommand.cs` - AppendTokenUsageToFeatureMd helper, token row appended before existing FEATURE.md commit

## Decisions Made
- **stream-json vs text format:** Switched to stream-json because it embeds token/cost data in the final result line. The agent text output moves from raw stdout into the `result` field of the JSON, requiring `resultText` extraction for success/abort detection.
- **Single result-line parse:** Only the line with `"type":"result"` at the end is parsed for token data. Intermediate tool-call lines are ignored.
- **FEATURE.md write ordering:** All FEATURE.md mutations (status update + token row) now happen before `CommitAutoRunMd` is called, so the commit is one atomic unit containing AUTO-RUN.md, research/plan artifacts, and updated FEATURE.md.
- **`est.` fallback:** When `totalCostUsd` is 0 (no data or free tier), the cost column shows `est.` rather than `$0.0000` to avoid misleading precision.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- Initial build with `-q` flag produced a confusing "Question build FAILED" error (the word "Question" in MSBuild output was the first word on the error line, not a build code error). Switching to full output confirmed the build succeeded. Not a real issue.

## User Setup Required
None - no external service configuration required.

## Next Steps
- Plan 02 complete: headless auto-research and auto-plan now capture token cost and write it to FEATURE.md
- Plan 03 (configure-models command) can proceed independently
- Plan 04 (interactive workflow token reporting) handles human-driven paths

---
*Feature: review-token-usage*
*Completed: 2026-02-23*
