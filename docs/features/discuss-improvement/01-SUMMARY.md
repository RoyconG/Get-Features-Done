---
feature: discuss-improvement
plan: 01
subsystem: ui
tags: [workflow, cli, discuss-feature, context-gathering]

# Dependency graph
requires: []
provides:
  - Step 5 (Gather Source Context) in discuss-feature workflow with file-path and free-text branches
  - FILE_PATH variable wired from Step 1 argument parsing into Step 5
  - SOURCE_CONTEXT variable wired from Step 5 into Step 6 (Analyze Feature) and Step 10 (Update FEATURE.md)
  - Updated argument-hint in discuss-feature command definition to show optional [context-file]
affects: [discuss-feature, discuss-improvement]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Two-branch context injection: file path (silent) OR interactive prompt (skippable)"
    - "SOURCE_CONTEXT as workflow-scoped variable threaded through multiple steps"

key-files:
  created: []
  modified:
    - get-features-done/workflows/discuss-feature.md
    - commands/gfd/discuss-feature.md

key-decisions:
  - "FILE_PATH extraction placed in Step 1 alongside slug parsing to keep argument handling in one place"
  - "File read failure falls through to interactive prompt rather than hard-failing"
  - "SOURCE_CONTEXT omission from FEATURE.md Notes is conditional (heading omitted entirely when empty)"
  - "Claude discretion applied to raw vs. summarized context: roughly 500 words threshold"

patterns-established:
  - "Workflow variable threading: set variable early (Step 1/5), consume late (Step 6/10)"
  - "Conditional FEATURE.md sections: omit heading entirely rather than writing an empty section"

requirements-completed: []

# Metrics
duration: 8min
completed: 2026-02-23
---

# Feature [discuss-improvement] Plan 01: Discuss Improvement Summary

**Skippable source context gathering added to discuss-feature workflow via FILE_PATH argument or interactive prompt, feeding into gray area analysis and FEATURE.md Notes**

## Performance

- **Duration:** ~8 min
- **Started:** 2026-02-23T20:54:41Z
- **Completed:** 2026-02-23T21:02:00Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments

- Added optional FILE_PATH argument parsing in Step 1 of discuss-feature workflow
- Inserted new Step 5 (Gather Source Context) with three branches: read from file, interactive prompt, or empty fallback
- Renumbered old Steps 5-12 to Steps 6-13 to accommodate the new step
- Enhanced Step 6 (Analyze Feature) to incorporate SOURCE_CONTEXT for domain-specific gray area analysis
- Enhanced Step 10 (Update FEATURE.md) to conditionally write `### Source Context` heading to Notes
- Added two Source Context success_criteria checklist items
- Updated discuss-feature command `argument-hint` to document optional `[context-file]` argument

## Task Commits

Each task was committed atomically:

1. **Task 1: Insert context-gathering step and wire SOURCE_CONTEXT through the workflow** - `999f330` (feat)
2. **Task 2: Update command definition argument-hint** - `94e0ff2` (feat)

**Plan metadata:** (to be added in final commit)

## Files Created/Modified

- `/var/home/conroy/Projects/GFD/get-features-done/workflows/discuss-feature.md` - Added FILE_PATH parsing in Step 1, new Step 5 (Gather Source Context), enhanced Step 6 and Step 10, renumbered Steps 6-13, updated success_criteria
- `/var/home/conroy/Projects/GFD/commands/gfd/discuss-feature.md` - Updated argument-hint from `<feature-slug>` to `<feature-slug> [context-file]`

## Decisions Made

- File read failures (missing file, permission denied) fall through to the interactive prompt rather than hard-failing the workflow — provides graceful degradation
- SOURCE_CONTEXT threshold of ~500 words for raw vs. summarized text in FEATURE.md Notes left to Claude's discretion, consistent with FEATURE.md acceptance criteria
- The `### Source Context` heading is entirely omitted when SOURCE_CONTEXT is empty (no blank section written)
- Step numbering insertion placed at Step 5 to group context-gathering logically between banner display (Step 4) and analysis (Step 6)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Steps

- All four acceptance criteria addressed by the workflow changes
- The discuss-feature command now documents the optional context-file argument
- No further plans defined for this feature

## Self-Check: PASSED

- FOUND: get-features-done/workflows/discuss-feature.md
- FOUND: commands/gfd/discuss-feature.md
- FOUND: docs/features/discuss-improvement/01-SUMMARY.md
- FOUND commit: 999f330
- FOUND commit: 94e0ff2

---
*Feature: discuss-improvement*
*Completed: 2026-02-23*
