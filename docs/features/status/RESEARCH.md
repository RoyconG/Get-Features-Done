# Feature: Status — Research

**Researched:** 2026-02-20
**Domain:** GFD internal — feature lifecycle states, command layer, workflow layer, gfd-tools.cjs CLI
**Confidence:** HIGH

## User Constraints (from FEATURE.md)

### Locked Decisions
- Replaces the existing `/gfd:progress` command with `/gfd:status`
- Status table uses raw simple values, no symbols or formatting
- The `backlog` status is removed from the lifecycle entirely
- New features start at `new` instead of `backlog`

### Out of Scope
- Symbol-decorated status tables (explicitly excluded)
- The old `backlog` status (removed entirely)
- Any progress bar rendering (that was a `progress` command concern)

---

## Summary

This feature is a pure internal refactor with zero external dependencies. Every file that needs to change is within the GFD codebase. The domain is: (1) updating the status enum in `gfd-tools.cjs`, (2) updating all workflows and templates that reference the old statuses, (3) creating two new commands (`discuss-feature`, `research-feature`), (4) simplifying the `new-feature` workflow, and (5) replacing `progress.md` with `status.md`.

The key insight is that `feature-update-status` in `gfd-tools.cjs` (line 1082) has a hard-coded `validStatuses` array that acts as the gatekeeper for all status transitions. This is the single authoritative location for the allowed status values — updating it is the core of this feature. Every workflow that calls `feature-update-status` or writes status directly via `sed -i` will need corresponding updates.

The new lifecycle adds four intermediate states (`discussing`, `discussed`, `researching`, `researched`) between `new` and `planning`. This models the fact that features go through distinct phases of discovery before they're ready to plan. The `/gfd:discuss-feature` command handles the `new → discussing → discussed` arc. The `/gfd:research-feature` command handles the `discussed → researching → researched` arc. These commands each own their slice of the lifecycle.

**Primary recommendation:** Update `gfd-tools.cjs` first (the status validator and all status-counting code), then update each workflow in dependency order, then create new commands and workflows. Test with a real feature at each stage.

---

## Standard Stack

### Core (no new dependencies)

| Component | Location | Purpose | Change Required |
|-----------|----------|---------|-----------------|
| `gfd-tools.cjs` | `get-features-done/bin/gfd-tools.cjs` | Status validation, listing, filtering | Update `validStatuses`, sort order, by_status counts |
| `feature.md` template | `get-features-done/templates/feature.md` | Default status in new features | Change `status: backlog` → `status: new` |
| `new-feature` workflow | `get-features-done/workflows/new-feature.md` | Creates FEATURE.md | Change status to `new`, simplify to slug+one-liner |
| `plan-feature` workflow | `get-features-done/workflows/plan-feature.md` | Planning orchestration | Update status transition to accept `researched` |
| `execute-feature` workflow | `get-features-done/workflows/execute-feature.md` | Execution orchestration | Update status transition from `planned` |
| `progress` command | `commands/gfd/progress.md` | Status overview | Replace with `status.md` |
| `progress` workflow | `get-features-done/workflows/progress.md` | Status rendering | Replace with `status.md` workflow |

### New Files to Create

| File | Purpose |
|------|---------|
| `commands/gfd/status.md` | Replaces `progress.md` — simple table display |
| `commands/gfd/discuss-feature.md` | New command for scope discussion |
| `commands/gfd/research-feature.md` | New command spawning gfd-researcher |
| `get-features-done/workflows/status.md` | Status table rendering workflow |
| `get-features-done/workflows/discuss-feature.md` | Discussion workflow |
| `get-features-done/workflows/research-feature.md` | Research workflow |

---

## Architecture Patterns

### Pattern 1: Command + Workflow (existing GFD pattern)

**What:** Every user-facing command is a thin `commands/gfd/*.md` file that delegates to a `get-features-done/workflows/*.md` workflow via `@` reference in `execution_context`.

**When to use:** Always — this is the only command pattern in GFD.

**Command file structure:**
```markdown
---
name: gfd:discuss-feature
description: Refine feature scope through conversation
argument-hint: <feature-slug>
allowed-tools: Read, Write, Edit, Bash, Grep, Glob, AskUserQuestion
---

<objective>Deep conversation to refine feature scope and transition status to `discussed`.</objective>

<execution_context>
@/home/conroy/.claude/get-features-done/workflows/discuss-feature.md
@/home/conroy/.claude/get-features-done/references/ui-brand.md
</execution_context>

<process>Execute the discuss-feature workflow.</process>
```

**Workflow file structure:** Full step-by-step instructions with bash blocks for tool invocations.

### Pattern 2: Status Transition via gfd-tools (existing pattern)

**What:** Workflows update FEATURE.md status via `feature-update-status` CLI subcommand OR via `sed -i` for simple replacements. The CLI subcommand is preferred because it validates against `validStatuses`.

**Existing usage in plan-feature.md:**
```bash
# Update the status field in FEATURE.md frontmatter
sed -i 's/^status: backlog$/status: planning/' "${feature_dir}/FEATURE.md"
```

**Preferred approach (uses validation):**
```bash
node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs feature-update-status "${SLUG}" "discussing"
```

**When to use:** Use `feature-update-status` whenever the workflow is already loading JSON from init (it returns `old_status` and `new_status` for confirmation display). Use `sed -i` only when the transition is a simple single-case replacement and you don't need the return value.

### Pattern 3: Init → Parse JSON → Act

**What:** All workflows start with `gfd-tools.cjs init <command-type> <slug>` to load config, feature state, and file contents atomically.

**For new commands:**
```bash
INIT_RAW=$(node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs init plan-feature "${SLUG}" --include feature,state)
# Large payloads are written to a tmpfile
if [[ "$INIT_RAW" == @file:* ]]; then
  INIT_FILE="${INIT_RAW#@file:}"
  INIT=$(cat "$INIT_FILE")
  rm -f "$INIT_FILE"
else
  INIT="$INIT_RAW"
fi
```

The `discuss-feature` and `research-feature` commands can reuse `init plan-feature` since it already provides all necessary feature context. No new init command is needed.

### Pattern 4: Status Guard in Workflows

**What:** Every workflow checks `feature_status` from init JSON and validates it's acceptable before proceeding.

**Pattern for `discuss-feature`:**
```
Valid entry statuses: `new`
If status is anything else: error with hint about lifecycle order
```

**Pattern for `research-feature`:**
```
Valid entry statuses: `discussed`
If status is `new`: hint to run /gfd:discuss-feature first
If status is already `researched`: offer to re-research
```

### New Feature Lifecycle and State Machine

```
new
  └─► discussing  (discuss-feature starts)
        └─► discussed  (discuss-feature completes)
              └─► researching  (research-feature starts)
                    └─► researched  (research-feature completes)
                          └─► planning  (plan-feature starts)
                                └─► planned  (plan-feature completes)
                                      └─► in-progress  (execute-feature starts)
                                            └─► done  (execute-feature completes)
```

### Simplified new-feature Workflow

The current `new-feature` workflow asks 4 questions (description, acceptance criteria, priority, dependencies) plus a confirm step. The new version captures only:
1. `slug` (from command argument)
2. one-liner description (single question: "What does [SLUG] do in one sentence?")
3. Sets `status: new` and creates minimal FEATURE.md

The rationale: deeper scope work happens in `/gfd:discuss-feature`. The `new-feature` command is now a quick registration step, not a full feature definition session.

**Minimal FEATURE.md after new-feature:**
```yaml
---
name: [Derived from slug]
slug: [SLUG]
status: new
owner: [git user]
assignees: []
created: [today]
priority: medium
depends_on: []
---
# [Feature Name]

## Description

[One-liner from user]

## Acceptance Criteria

- [ ] [To be defined during /gfd:discuss-feature]

## Tasks

[Populated during planning. Links to plan files.]

## Notes

[Empty]
```

### status Command — Simple Table Display

The `/gfd:status` command replaces `/gfd:progress`. Key differences:
- No symbols, no progress bar, no routing logic
- Simple table: Feature Name | Status
- Excludes `done` features
- When no active features: show helpful message with hint

**Output format:**
```
| Feature Name | Status |
|--------------|--------|
| User Auth | in-progress |
| Payment Flow | planned |
| Email Notifications | researched |
```

**Empty state:**
```
No active features.

Run /gfd:new-feature <slug> to create your first feature.
```

The status workflow is simple: call `list-features`, filter out `done`, render table. No routing, no recent activity, no decision tracking.

### discuss-feature Workflow

**Purpose:** Deep conversation to refine scope. Sets acceptance criteria, priority, dependencies, design decisions.

**Status transitions:**
- Entry: `new`
- During conversation: transition to `discussing` immediately on start
- On completion: transition to `discussed`

**What it collects:**
- Expanded description (2-3 sentences)
- Acceptance criteria (3-5 observable behaviors)
- Priority
- Dependencies
- Notes/constraints

**Agent involvement:** None — this is a direct conversation between Claude (the orchestrator) and the user. No sub-agents needed.

**Output:** Updated FEATURE.md with all sections populated, status `discussed`.

### research-feature Workflow

**Purpose:** Spawn gfd-researcher to investigate implementation approach.

**Status transitions:**
- Entry: `discussed`
- Immediately on start: transition to `researching`
- On completion: transition to `researched`

**Relationship to existing plan-feature workflow:** The current `plan-feature` workflow already has a research step (spawns gfd-researcher). The new `research-feature` command extracts that step into its own command. When `plan-feature` runs and `feature_status` is `researched`, it can skip research (RESEARCH.md already exists). This is already handled by the `has_research` check in the existing init JSON.

**Key difference from current plan-feature research step:** `research-feature` is a standalone command, not embedded in planning. The planner's existing "skip if has_research" logic already handles this correctly — no change needed in plan-feature for this case.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Status validation | Inline status check in each workflow | `feature-update-status` CLI subcommand | Validates against `validStatuses`, returns old/new for confirmation |
| Feature listing | Direct fs.readdirSync in workflow | `list-features` CLI subcommand | Already handles sorting, filtering, status counting |
| FEATURE.md frontmatter update | Regex in workflow | `feature-update-status` CLI | Single source of truth for status transitions |
| New init command for discuss/research | New `cmdInitDiscussFeature` function | Reuse `init plan-feature` | Already returns all needed context (feature content, state, researcher model) |
| Progress bar | Any rendering | Remove entirely (not in spec) | status command shows table only, no bar |

**Key insight:** The gfd-tools.cjs `validStatuses` array on line 1082 is the single place to add all 9 new status values. Every workflow that does status transitions calls through this validator.

---

## Common Pitfalls

### Pitfall 1: Missing Status Values in Sorting and Counting

**What goes wrong:** Adding new statuses to `validStatuses` but forgetting to update `statusOrder` (line 324) and `by_status` (lines 351-357) in `listFeaturesInternal`. The sort order breaks and by_status counts are wrong.

**Why it happens:** Three separate places all hard-code the known status values in gfd-tools.cjs.

**How to avoid:** Update all three in the same commit:
1. `validStatuses` array in `cmdFeatureUpdateStatus` (line 1082)
2. `statusOrder` object in `listFeaturesInternal` (line 324)
3. `by_status` object in `cmdListFeatures` (lines 351-357)
4. `backlog_count` → replace with counts for new statuses in `cmdInitProgress` (line 1420)

**Warning signs:** Features with new statuses appearing at the end of the list with wrong sort order.

### Pitfall 2: Old Status References in `sed -i` Patterns

**What goes wrong:** Workflows use `sed -i 's/^status: backlog$/status: planning/'`. After this feature, `backlog` no longer exists — the starting status is `new`. Existing `sed` patterns that target `backlog` will silently do nothing.

**Why it happens:** Workflows have hard-coded status transitions.

**How to avoid:** Update every `sed` pattern in every workflow that references `backlog`:
- `plan-feature.md` line: `sed -i 's/^status: backlog$/status: planning/'`
- `execute-feature.md` line: `sed -i 's/^status: backlog$/status: in-progress/'`

New valid transitions for plan-feature: `researched` → `planning`
New valid transitions for execute-feature: `planned` → `in-progress`

### Pitfall 3: progress.md Command Still Referenced After Deletion

**What goes wrong:** Deleting `commands/gfd/progress.md` but leaving references to `/gfd:progress` in workflow success criteria, "also available" sections, and routing suggestions.

**Why it happens:** The progress command is referenced in multiple workflow "next up" sections.

**How to avoid:** After creating `status.md`, grep for all `gfd:progress` references:
```bash
grep -r "gfd:progress" /var/home/conroy/Projects/GFD/get-features-done/
```
Update each reference to `/gfd:status`.

**Warning signs:** Any workflow still showing `/gfd:progress` in its output.

### Pitfall 4: discuss-feature Must Update FEATURE.md Content, Not Just Status

**What goes wrong:** The discuss-feature workflow transitions status to `discussing` then `discussed` but forgets to write the expanded description and acceptance criteria back to FEATURE.md.

**Why it happens:** Status transition is one operation; content update is another.

**How to avoid:** The discuss-feature workflow must use the Write tool to rewrite FEATURE.md with:
- Expanded `## Description` (2-3 sentences)
- Populated `## Acceptance Criteria` (3-5 items)
- Updated `priority` in frontmatter
- Populated `depends_on` in frontmatter
- `## Notes` with design decisions from conversation

Status transition happens separately via `feature-update-status`.

### Pitfall 5: plan-feature Valid Status Guard Must Accept `researched`

**What goes wrong:** `plan-feature` workflow currently accepts `backlog` and `planning` as valid entry statuses (line 70 in plan-feature.md: "Valid statuses for planning: `backlog`, `planning`"). After this change, the expected entry status is `researched`.

**Why it happens:** The valid-statuses guard in plan-feature is not updated.

**How to avoid:** Update plan-feature's status validation to accept: `researched`, `planning` (for re-entry if interrupted). Also keep `new`, `discussing`, `discussed` with a clear error: "Run /gfd:discuss-feature and /gfd:research-feature first."

### Pitfall 6: feature.md Template Still Has `status: backlog`

**What goes wrong:** The template at `get-features-done/templates/feature.md` has `status: backlog` as the default. New features created after this feature ships would still start at `backlog` until the template is updated.

**Why it happens:** The template is a separate file from the workflow.

**How to avoid:** Update `get-features-done/templates/feature.md` to use `status: new` and update the status values listed in the `<guidelines>` section.

---

## Code Examples

### Updating validStatuses in gfd-tools.cjs

```javascript
// Source: gfd-tools.cjs line 1082 (current)
const validStatuses = ['backlog', 'planning', 'planned', 'in-progress', 'done'];

// After this feature:
const validStatuses = ['new', 'discussing', 'discussed', 'researching', 'researched', 'planning', 'planned', 'in-progress', 'done'];
```

### Updating statusOrder for Sort

```javascript
// Source: gfd-tools.cjs line 324 (current)
const statusOrder = { 'in-progress': 0, planned: 1, planning: 2, backlog: 3, done: 4 };

// After this feature:
const statusOrder = {
  'in-progress': 0,
  planned: 1,
  planning: 2,
  researched: 3,
  researching: 4,
  discussed: 5,
  discussing: 6,
  new: 7,
  done: 8,
};
```

### Updating by_status Counts

```javascript
// Source: gfd-tools.cjs lines 351-357 (current)
by_status: {
  backlog: features.filter(f => f.status === 'backlog').length,
  planning: features.filter(f => f.status === 'planning').length,
  planned: features.filter(f => f.status === 'planned').length,
  'in-progress': features.filter(f => f.status === 'in-progress').length,
  done: features.filter(f => f.status === 'done').length,
},

// After this feature:
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

### Default Status for findFeatureInternal

```javascript
// Source: gfd-tools.cjs line 282 (current)
status: fm.status || 'backlog',

// After this feature:
status: fm.status || 'new',
```

### status Workflow Table Rendering

```bash
# List features, filter done, render table
FEATURES=$(node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs list-features --raw)

# The workflow parses the JSON and renders:
# | Feature Name | Status |
# |--------------|--------|
# | [name]       | [status] |
# (for each feature where status != 'done')
```

### discuss-feature Status Transition Pattern

```bash
# Transition to discussing on start
node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs feature-update-status "${SLUG}" "discussing"

# ... run conversation ...

# Transition to discussed on completion
node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs feature-update-status "${SLUG}" "discussed"
```

### research-feature Workflow Pattern (mirrors existing plan-feature research step)

```bash
# Transition to researching
node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs feature-update-status "${SLUG}" "researching"

# Load context (reuse plan-feature init)
INIT_RAW=$(node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs init plan-feature "${SLUG}" --include feature,state)

# Spawn researcher (same prompt as current plan-feature research step)
# Task(prompt=research_prompt, model=researcher_model, ...)

# Transition to researched
node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs feature-update-status "${SLUG}" "researched"
```

---

## State of the Art

| Old Approach | New Approach | Impact |
|--------------|-------------|--------|
| `backlog` as default status | `new` as default status | Cleaner semantics — "backlog" implied deprioritized |
| Research embedded in plan-feature | Standalone `research-feature` command | Users can trigger research independently |
| No discussion phase | `discuss-feature` command | Scope is refined before research begins |
| `progress` command with symbols, routing | `status` command — raw table only | Simpler, less opinionated output |
| new-feature asks 4 questions | new-feature asks 1 question | Feature creation is fast; detail comes in discuss-feature |

**Deprecated:**
- `/gfd:progress` command: replaced by `/gfd:status`
- `backlog` status: removed entirely — use `new` for not-yet-started features
- The routing logic in progress.md: removed — status is display only, no routing

---

## Open Questions

1. **Should existing features with `backlog` status be migrated automatically?**
   - What we know: The `feature-update-status` validator will reject `backlog` as a target status after this change. But existing features with `status: backlog` in their frontmatter will still read fine since `findFeatureInternal` just reads the raw string value.
   - What's unclear: Will `listFeaturesInternal` sort and count them correctly if `backlog` is no longer in `statusOrder`?
   - Recommendation: Add a migration note in the plan: after updating gfd-tools.cjs, run a one-time migration to update existing `backlog` features to `new`. OR add `backlog` as an alias that maps to `new` in the sort order. The simpler option: treat unknown statuses with a low sort priority (the `|| 3` default already exists) and let them appear at the bottom of the list.

2. **Should plan-feature still work if user skips discuss-feature and research-feature?**
   - What we know: The current plan-feature accepts `backlog` (bypassing research). The feature spec says plan-feature accepts `researched` entry status.
   - What's unclear: Is skipping discuss/research valid? The feature spec doesn't explicitly address this.
   - Recommendation: plan-feature should accept any pre-planning status (`new`, `discussing`, `discussed`, `researching`, `researched`, `planning`) with appropriate warnings when steps are skipped, but not hard-block. This preserves the existing flexible behavior. Only `done` and `in-progress` should hard-block.

---

## Sources

### Primary (HIGH confidence)
- `/var/home/conroy/Projects/GFD/get-features-done/bin/gfd-tools.cjs` — inspected live source, lines 282, 324, 351-357, 1082-1100, 1399-1434
- `/var/home/conroy/Projects/GFD/get-features-done/workflows/plan-feature.md` — full workflow inspected
- `/var/home/conroy/Projects/GFD/get-features-done/workflows/new-feature.md` — full workflow inspected
- `/var/home/conroy/Projects/GFD/get-features-done/workflows/progress.md` — full workflow inspected
- `/var/home/conroy/Projects/GFD/get-features-done/workflows/execute-feature.md` — first 80 lines inspected
- `/var/home/conroy/Projects/GFD/get-features-done/templates/feature.md` — template inspected
- `/var/home/conroy/Projects/GFD/docs/features/codebase/ARCHITECTURE.md` — architecture patterns inspected
- `/var/home/conroy/Projects/GFD/docs/features/codebase/CONVENTIONS.md` — conventions inspected

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all files directly inspected, no external dependencies
- Architecture patterns: HIGH — existing GFD patterns are the authoritative source
- Pitfalls: HIGH — derived from reading the actual code that must change
- Open questions: MEDIUM — the migration/backward-compatibility questions require a decision but don't block the core work

**Research date:** 2026-02-20
**Valid until:** 2026-03-22 (stable internal codebase — 30 days)
