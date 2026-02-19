---
feature: status
plan: 01
subsystem: infra
tags: [gfd-tools, lifecycle, status, feature-management]

# Dependency graph
requires: []
provides:
  - 9-state feature lifecycle validation in gfd-tools.cjs (new, discussing, discussed, researching, researched, planning, planned, in-progress, done)
  - Updated statusOrder sort comparator for all 9 states
  - Updated by_status counts for all 9 states in list-features output
  - Default status fallback changed from 'backlog' to 'new'
affects: [status, convert-from-gsd]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "validStatuses array in gfd-tools.cjs is the single authoritative source for valid status values"
    - "statusOrder comparator positions in-progress first, done last, new second-to-last"

key-files:
  created: []
  modified:
    - get-features-done/bin/gfd-tools.cjs

key-decisions:
  - "backlog status is removed entirely; validStatuses rejects it with a clear error message"
  - "statusOrder uses sequential integers (0-8) with in-progress at 0, done at 8"
  - "new_count replaces backlog_count in init command output for consistency"

patterns-established:
  - "Status validation: all status transitions go through feature-update-status which checks validStatuses array"
  - "Status ordering: statusOrder hash in listFeaturesInternal controls feature sort priority"

requirements-completed:
  - "Feature lifecycle uses new states: new, discussing, discussed, researching, researched, planning, planned, in-progress, done"

# Metrics
duration: 1min
completed: 2026-02-20
---

# Feature [status] Plan 01: Update gfd-tools.cjs for 9-state lifecycle Summary

**9-state feature lifecycle implemented in gfd-tools.cjs: validStatuses, statusOrder, by_status, and default fallback all updated to replace backlog with the new progression**

## Performance

- **Duration:** ~1 min
- **Started:** 2026-02-20T12:12:05Z
- **Completed:** 2026-02-20T12:13:42Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- Updated `validStatuses` to include all 9 new lifecycle states; `backlog` now rejected with clear error
- Updated `statusOrder` so features sort correctly (in-progress first, done last)
- Updated `by_status` in `cmdListFeatures` to count all 9 new states
- Changed default status fallback from `'backlog'` to `'new'` in both `findFeatureInternal` and `cmdFeatureUpdateStatus`
- Replaced `backlog_count` with `new_count` in `init` command output

## Task Commits

Each task was committed atomically:

1. **Task 1: Update validStatuses, statusOrder, by_status, and default fallback** - `97fc59f` (feat)

**Plan metadata:** _(to be added by final commit)_

## Files Created/Modified
- `get-features-done/bin/gfd-tools.cjs` - Updated 4 targeted locations for 9-state lifecycle support

## Decisions Made
- Replaced `backlog` entirely (not as an alias or deprecated fallback) — `validStatuses` excludes it and the error message lists only the 9 valid states
- `new_count` replaces `backlog_count` in the init output for consistency with the new lifecycle

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Replaced backlog_count with new_count in init command output**
- **Found during:** Task 1 (Update validStatuses, statusOrder, by_status, and default fallback)
- **Issue:** Line 1420 still referenced `backlog_count: features.filter(f => f.status === 'backlog').length` — with backlog removed from the lifecycle this field would always return 0 and the field name would be misleading
- **Fix:** Replaced with `new_count: features.filter(f => f.status === 'new').length`
- **Files modified:** `get-features-done/bin/gfd-tools.cjs`
- **Verification:** `node gfd-tools.cjs list-features` returns all 9 by_status keys correctly; backlog rejected
- **Committed in:** `97fc59f` (Task 1 commit)

**2. [Rule 1 - Bug] Fixed oldStatus fallback in cmdFeatureUpdateStatus**
- **Found during:** Task 1
- **Issue:** Line 1095 `const oldStatus = fm.status || 'backlog'` would log 'backlog' as old_status for features without a status field, which is now invalid
- **Fix:** Changed to `fm.status || 'new'` to match the new default
- **Files modified:** `get-features-done/bin/gfd-tools.cjs`
- **Verification:** feature-update-status correctly reports old_status for all 9 lifecycle states
- **Committed in:** `97fc59f` (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (2 Rule 1 bugs)
**Impact on plan:** Both fixes needed for internal consistency after removing backlog. No scope creep.

## Issues Encountered
None — all edits were clean targeted replacements.

## User Setup Required
None - no external service configuration required.

## Next Steps
- gfd-tools.cjs is now the authoritative source for 9-state lifecycle validation
- Plan 02 can build `/gfd:status` command using the new state ordering
- All subsequent plans (03-05) depend on these validated status values

---
*Feature: status*
*Completed: 2026-02-20*
