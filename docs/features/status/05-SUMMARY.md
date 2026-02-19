---
feature: status
plan: 05
subsystem: workflows
tags: [gfd, lifecycle, new-feature, plan-feature, execute-feature, status]

# Dependency graph
requires:
  - feature: status
    provides: 9-state lifecycle in gfd-tools.cjs, feature-update-status command
provides:
  - Simplified new-feature workflow — slug + one-liner only, status: new
  - Updated plan-feature — accepts researched entry status, uses feature-update-status
  - Updated execute-feature — single feature-update-status call, no backlog fallbacks
  - Updated feature.md template — status: new default, all 9 statuses documented
affects: [new-feature, plan-feature, execute-feature]

# Tech tracking
tech-stack:
  added: []
  patterns: [use feature-update-status for status transitions instead of sed patterns]

key-files:
  created: []
  modified:
    - get-features-done/templates/feature.md
    - get-features-done/workflows/new-feature.md
    - get-features-done/workflows/plan-feature.md
    - get-features-done/workflows/execute-feature.md

key-decisions:
  - "new-feature asks only slug + one-liner; acceptance criteria deferred to discuss-feature"
  - "plan-feature uses feature-update-status instead of sed for researched→planning transition"
  - "execute-feature uses single feature-update-status call; backlog fallback removed"

patterns-established:
  - "Status transitions use feature-update-status (validated) not raw sed patterns"
  - "next-step routing uses /gfd:status instead of /gfd:progress"

requirements-completed: []

# Metrics
duration: 3min
completed: 2026-02-20
---

# Feature [status] Plan 05: Update Workflows Summary

**Workflows aligned with 9-state lifecycle: new-feature simplified to slug + one-liner, plan-feature accepts researched entry, execute-feature uses feature-update-status with no backlog fallbacks**

## Performance

- **Duration:** ~3 min
- **Started:** 2026-02-20T12:20:31Z
- **Completed:** 2026-02-20T12:23:00Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- feature.md template now defaults to `status: new` with all 9 lifecycle statuses documented in guidelines
- new-feature workflow simplified from 4 questions to 1 (one-liner description only)
- plan-feature updated to accept `researched` as valid entry status with clear error guidance for pre-research statuses
- execute-feature updated to use single `feature-update-status` call (removed 3 sed patterns with backlog fallback)
- All `/gfd:progress` references replaced with `/gfd:status` across all four files

## Task Commits

Each task was committed atomically:

1. **Task 1: Simplify new-feature workflow and update feature template** - `eafe62c` (feat)
2. **Task 2: Update plan-feature and execute-feature workflows** - `0ab1a50` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified
- `get-features-done/templates/feature.md` - Status changed to `new`, guidelines updated with all 9 statuses, evolution section rewritten
- `get-features-done/workflows/new-feature.md` - Reduced to slug + one-liner question; routes to discuss-feature; status: new
- `get-features-done/workflows/plan-feature.md` - Valid statuses updated to researched/planning; sed replaced with feature-update-status; /gfd:status routing
- `get-features-done/workflows/execute-feature.md` - 3 sed patterns replaced with single feature-update-status; backlog routing updated; /gfd:status references

## Decisions Made
- new-feature acceptance criteria deferred entirely to discuss-feature — keeps feature creation low-friction
- Used feature-update-status over sed patterns for validated, consistent transitions
- Kept execute-feature's "features in backlog" routing text updated to "researched (ready for planning)" for accuracy

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Updated stale "backlog" routing comment in execute-feature**
- **Found during:** Task 2 (execute-feature.md verification)
- **Issue:** Plan's verification said no backlog references; execute-feature had "If features are in backlog (need planning)" routing comment in offer_next step
- **Fix:** Updated to "If features are researched (ready for planning)" for accuracy with new lifecycle
- **Files modified:** get-features-done/workflows/execute-feature.md
- **Verification:** grep backlog returns nothing
- **Committed in:** 0ab1a50 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - cleanup of stale routing comment)
**Impact on plan:** Minor accuracy fix. No scope creep.

## Issues Encountered
None — all changes were straightforward targeted edits.

## Next Steps
- All 5 plans for the `status` feature are now complete
- Feature lifecycle is fully implemented end-to-end
- Ready for gfd-verifier to check acceptance criteria

## Self-Check: PASSED

- FOUND: get-features-done/templates/feature.md
- FOUND: get-features-done/workflows/new-feature.md
- FOUND: get-features-done/workflows/plan-feature.md
- FOUND: get-features-done/workflows/execute-feature.md
- FOUND: docs/features/status/05-SUMMARY.md
- FOUND commit: eafe62c (Task 1)
- FOUND commit: 0ab1a50 (Task 2)

---
*Feature: status*
*Completed: 2026-02-20*
