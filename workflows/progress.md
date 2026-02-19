<purpose>
Check project progress, summarize feature status and recent work, then intelligently route to the next action. Provides situational awareness before continuing work — shows what's done, what's in progress, and what's next.
</purpose>

<required_reading>
Read all files referenced by the invoking prompt's execution_context before starting.

@/home/conroy/.claude/get-features-done/references/ui-brand.md
</required_reading>

<process>

<step name="init_context">
Load progress context (with file contents to avoid redundant reads):

```bash
INIT_RAW=$(node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs init progress --include state,project)
# Large payloads are written to a tmpfile — output starts with @file:/path
if [[ "$INIT_RAW" == @file:* ]]; then
  INIT_FILE="${INIT_RAW#@file:}"
  INIT=$(cat "$INIT_FILE")
  rm -f "$INIT_FILE"
else
  INIT="$INIT_RAW"
fi
```

Extract from init JSON: `project_exists`, `state_exists`, `feature_count`, `done_count`.

**File contents (from --include):** `state_content`, `project_content`. These are null if files don't exist.

**If `project_exists` is false:**

```
No GFD project found.

Run /gfd:new-project to start a new project.
```

Exit.

**If `state_exists` is false:**

```
No STATE.md found. Project may be partially initialized.

Run /gfd:new-project to re-initialize.
```

Exit.
</step>

<step name="scan_features">
Scan all features by looking for FEATURE.md files in `docs/features/`:

```bash
FEATURES=$(node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs list-features)
```

This returns structured JSON with:
- All features with slug, name, status, priority, plan count, completion
- Aggregated stats: total, done, in-progress, planned, planning, backlog counts
- Current focus (most recently active feature)

**If no features found:**

Proceed directly to routing — will hit "no features" route.
</step>

<step name="recent_activity">
Gather recent work context from STATE.md:

Use `state_content` from init JSON (already loaded).

Extract from STATE.md:
- Last activity entry
- Current focus feature
- Any recent decisions or blockers

Also find recent SUMMARY.md files for richer context:

```bash
# Find the 3 most recently modified SUMMARY.md files
find docs/features -name "*-SUMMARY.md" -newer docs/features/STATE.md 2>/dev/null | head -3
```

For each found summary, extract the one-liner to show "what we've been working on."
</step>

<step name="report">
Generate progress bar from gfd-tools:

```bash
PROGRESS_BAR=$(node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs progress bar --raw)
```

Present the status report:

```
# [Project Name from PROJECT.md]

**Progress:** {PROGRESS_BAR}

## Features

| Feature | Status | Priority | Plans | Progress |
|---------|--------|----------|-------|----------|
| [slug] | ✓ done | high | 3/3 | 100% |
| [slug] | ◆ in-progress | high | 1/4 | 25% |
| [slug] | ○ planned | medium | 0/3 | 0% |
| [slug] | ○ backlog | low | — | — |

**Counts:** {done} done · {in-progress} active · {planned} ready · {backlog} pending

## Recent Work
- [slug]: [what was accomplished — 1 line from summary or state]
- [slug]: [what was accomplished — 1 line from summary or state]

## Current Focus
Feature: [slug] ([Feature Name])
Status: [status]
Last activity: [date] — [what happened from STATE.md]

## Decisions Made
- [decision 1 from STATE.md]
- [decision 2]

(Show "None yet" if empty)

## Blockers/Concerns
- [any blockers or concerns from STATE.md]

(Omit section if none)
```

**Status symbols:**
- `✓` — done
- `◆` — in-progress
- `○` — planned or backlog
- `⚠` — has concerns
</step>

<step name="route">
Determine next action based on feature states.

**Step 1: Collect feature status counts**

From the features scan:
- `in_progress_features` — features with status "in-progress"
- `planned_features` — features with status "planned" (have plans, not started)
- `backlog_features` — features with status "backlog" or "planning"
- `done_features` — features with status "done"
- `total_features` — total feature count

**Step 2: Route based on state**

| Condition | Route |
|-----------|-------|
| `total_features` = 0 | Route A — no features yet |
| `in_progress_features` > 0 | Route B — continue in-progress |
| `planned_features` > 0 | Route C — execute planned feature |
| `backlog_features` > 0 | Route D — plan a backlog feature |
| all done | Route E — everything complete |

---

**Route A: No features yet**

```
───────────────────────────────────────────────────────────────

## ▶ Next Up

**Create your first feature** — define what to build

`/gfd:new-feature <feature-slug>`

<sub>`/clear` first → fresh context window</sub>

───────────────────────────────────────────────────────────────

**Also available:**
- `/gfd:map-codebase` — map existing codebase first (brownfield)

───────────────────────────────────────────────────────────────
```

---

**Route B: Feature in-progress**

Find the most recently active in-progress feature (from STATE.md current focus or most recent SUMMARY.md).

```
───────────────────────────────────────────────────────────────

## ▶ Next Up

**Continue [slug]** — [Feature Name] is in progress

`/gfd:execute-feature [slug]`

<sub>`/clear` first → fresh context window</sub>

───────────────────────────────────────────────────────────────

**Also available:**
- `/gfd:plan-feature [slug]` — add more plans to this feature
- `/gfd:new-feature <slug>` — create another feature

───────────────────────────────────────────────────────────────
```

---

**Route C: Planned feature ready to execute**

Find the highest-priority planned feature (sort by: critical → high → medium → low, then by creation date).

```
───────────────────────────────────────────────────────────────

## ▶ Next Up

**Execute [slug]** — [Feature Name] is planned and ready

`/gfd:execute-feature [slug]`

<sub>`/clear` first → fresh context window</sub>

───────────────────────────────────────────────────────────────

**Also available:**
- `/gfd:execute-feature [other-slug]` — different planned feature
- `/gfd:plan-feature [backlog-slug]` — plan a backlog feature first

───────────────────────────────────────────────────────────────
```

---

**Route D: Backlog feature needs planning**

Find the highest-priority backlog feature.

```
───────────────────────────────────────────────────────────────

## ▶ Next Up

**Plan [slug]** — [Feature Name] needs plans

`/gfd:plan-feature [slug]`

<sub>`/clear` first → fresh context window</sub>

───────────────────────────────────────────────────────────────

**Also available:**
- `/gfd:new-feature <slug>` — create another feature first
- `/gfd:plan-feature [other-slug]` — plan a different feature

───────────────────────────────────────────────────────────────
```

---

**Route E: All features done**

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► PROJECT COMPLETE ✓
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

All {N} features complete! Project is done.

───────────────────────────────────────────────────────────────

## ▶ Next Up

**Add more features** — continue building

`/gfd:new-feature <slug>`

───────────────────────────────────────────────────────────────

**Also available:**
- Review any feature: `cat docs/features/<slug>/FEATURE.md`

───────────────────────────────────────────────────────────────
```
</step>

</process>

<success_criteria>

- [ ] Project existence checked
- [ ] All features scanned with current status
- [ ] Progress bar generated
- [ ] Feature table shown with status symbols
- [ ] By-status counts shown (backlog, planning, planned, in-progress, done)
- [ ] Recent activity from STATE.md shown
- [ ] Blockers/concerns surfaced if present
- [ ] Smart routing: correct next command suggested based on state
- [ ] Next Up block always present at the end

</success_criteria>
