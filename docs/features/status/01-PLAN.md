---
feature: status
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - get-features-done/bin/gfd-tools.cjs
autonomous: true
acceptance_criteria:
  - Feature lifecycle uses new states: new, discussing, discussed, researching, researched, planning, planned, in-progress, done
must_haves:
  truths:
    - "feature-update-status accepts all 9 new status values without error"
    - "feature-update-status rejects 'backlog' with a clear error"
    - "list-features sorts features using the new status priority order"
    - "by_status counts include all 9 new statuses"
    - "findFeatureInternal defaults to 'new' when status is missing"
  artifacts:
    - path: "get-features-done/bin/gfd-tools.cjs"
      provides: "Status validation, sort order, and counting for all 9 lifecycle states"
      contains: "validStatuses = ['new', 'discussing', 'discussed', 'researching', 'researched', 'planning', 'planned', 'in-progress', 'done']"
  key_links:
    - from: "cmdFeatureUpdateStatus"
      to: "validStatuses array"
      via: "includes() check"
      pattern: "validStatuses\\.includes"
    - from: "listFeaturesInternal"
      to: "statusOrder"
      via: "sort comparator"
      pattern: "statusOrder"
---

<objective>
Update gfd-tools.cjs to support the new 9-state feature lifecycle.

Purpose: gfd-tools.cjs is the single authoritative source for valid status values. Every workflow that transitions status flows through `feature-update-status`, which validates against `validStatuses`. Three internal locations must all be updated together: `validStatuses`, `statusOrder`, and `by_status` counts. Until this is done, no workflow can use the new status values.

Output: Updated gfd-tools.cjs where all 9 new status values are valid, sort correctly, and count correctly.
</objective>

<execution_context>
@/home/conroy/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/status/FEATURE.md
@docs/features/status/RESEARCH.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Update validStatuses, statusOrder, by_status, and default fallback in gfd-tools.cjs</name>
  <files>get-features-done/bin/gfd-tools.cjs</files>
  <action>
Make four targeted edits to /var/home/conroy/Projects/GFD/get-features-done/bin/gfd-tools.cjs:

**Edit 1 — validStatuses (line ~1082):**
Replace:
```javascript
const validStatuses = ['backlog', 'planning', 'planned', 'in-progress', 'done'];
```
With:
```javascript
const validStatuses = ['new', 'discussing', 'discussed', 'researching', 'researched', 'planning', 'planned', 'in-progress', 'done'];
```

**Edit 2 — statusOrder (line ~324):**
Replace:
```javascript
const statusOrder = { 'in-progress': 0, planned: 1, planning: 2, backlog: 3, done: 4 };
```
With:
```javascript
const statusOrder = { 'in-progress': 0, planned: 1, planning: 2, researched: 3, researching: 4, discussed: 5, discussing: 6, new: 7, done: 8 };
```

**Edit 3 — by_status (lines ~351-357):**
Replace the entire by_status object:
```javascript
by_status: {
  backlog: features.filter(f => f.status === 'backlog').length,
  planning: features.filter(f => f.status === 'planning').length,
  planned: features.filter(f => f.status === 'planned').length,
  'in-progress': features.filter(f => f.status === 'in-progress').length,
  done: features.filter(f => f.status === 'done').length,
},
```
With:
```javascript
by_status: {
  new: features.filter(f => f.status === 'new').length,
  discussing: features.filter(f => f.status === 'discussing').length,
  discussed: features.filter(f => f.status === 'discussed').length,
  researching: features.filter(f => f.status === 'researching').length,
  researched: features.filter(f => f.status === 'researched').length,
  planning: features.filter(f => f.status === 'planning').length,
  planned: features.filter(f => f.status === 'planned').length,
  'in-progress': features.filter(f => f.status === 'in-progress').length,
  done: features.filter(f => f.status === 'done').length,
},
```

**Edit 4 — default status fallback (line ~282):**
Replace:
```javascript
status: fm.status || 'backlog',
```
With:
```javascript
status: fm.status || 'new',
```

Do NOT change anything else in the file. These are all targeted single-line (or block) replacements.
  </action>
  <verify>
Run these checks:
```bash
node /var/home/conroy/Projects/GFD/get-features-done/bin/gfd-tools.cjs feature-update-status status "new" 2>&1
node /var/home/conroy/Projects/GFD/get-features-done/bin/gfd-tools.cjs feature-update-status status "discussing" 2>&1
node /var/home/conroy/Projects/GFD/get-features-done/bin/gfd-tools.cjs feature-update-status status "researched" 2>&1
node /var/home/conroy/Projects/GFD/get-features-done/bin/gfd-tools.cjs feature-update-status status "backlog" 2>&1
node /var/home/conroy/Projects/GFD/get-features-done/bin/gfd-tools.cjs list-features 2>&1 | head -5
```
First three should return `{"updated":true,...}`. The "backlog" call should error with "Invalid status". list-features should return JSON with by_status containing "new", "discussing", etc.
  </verify>
  <done>
- `feature-update-status` accepts all 9 new statuses without error
- `feature-update-status` rejects "backlog" with "Invalid status: backlog"
- `list-features` JSON includes all 9 keys in by_status
- Default fallback is "new" (visible in findFeatureInternal return for features without a status field)
  </done>
</task>

</tasks>

<verification>
```bash
# Confirm all 9 statuses are accepted
for s in new discussing discussed researching researched planning planned in-progress done; do
  result=$(node /var/home/conroy/Projects/GFD/get-features-done/bin/gfd-tools.cjs feature-update-status status "$s" 2>&1)
  echo "$s: $(echo $result | grep -o '\"updated\":true' || echo FAILED)"
done

# Confirm backlog is rejected
node /var/home/conroy/Projects/GFD/get-features-done/bin/gfd-tools.cjs feature-update-status status "backlog" 2>&1 | grep "Invalid"

# Restore to planning after tests
node /var/home/conroy/Projects/GFD/get-features-done/bin/gfd-tools.cjs feature-update-status status "planning" 2>&1
```
</verification>

<success_criteria>
- All 9 status values accepted by feature-update-status
- "backlog" rejected with clear error message
- by_status object in list-features output has all 9 keys
- statusOrder positions in-progress first, done last, new second-to-last
- Default fallback changed from "backlog" to "new"
</success_criteria>

<output>
After completion, create `docs/features/status/01-SUMMARY.md` with what was changed and any deviations from plan.
</output>
