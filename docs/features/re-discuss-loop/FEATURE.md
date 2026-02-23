---
name: Re-Discuss Loop
slug: re-discuss-loop
status: researched
owner: Conroy
assignees: []
created: 2026-02-21
priority: medium
depends_on: []
---
# Re-Discuss Loop

## Description

When downstream stages (research, planning, execution) encounter blockers they can't resolve — ambiguous scope, conflicting decisions, missing context, or technical impossibility — the agent stops and surfaces the issue. The user can then run `/gfd:discuss-feature` to resolve the blocker through a focused re-discussion, after which the feature status rewinds to `discussing` and follows the normal path forward.

## Acceptance Criteria

- [ ] Research, planning, and execution agents detect unresolvable blockers and stop with an error box showing the blocker and `/gfd:discuss-feature <slug>` as the fix command
- [ ] Blocker details are written to the `## Blockers` section of FEATURE.md so they persist across context windows
- [ ] `discuss-feature` detects blockers in FEATURE.md and runs a focused re-discussion on just the affected area (not a full re-discuss)
- [ ] After re-discussion resolves the blocker, the blocker is removed from FEATURE.md and the user is shown the next command to run
- [ ] Status rewinds to `discussing` then `discussed` during re-discuss, then the user re-runs the stage that triggered it
- [ ] If the same blocker type recurs after a re-discuss, the agent warns the user before stopping
- [ ] In auto-advance mode, the agent jumps directly into discuss-feature instead of stopping

## Tasks

[Populated during planning. Links to plan files.]

## Notes

**Implementation Decisions:**
- Trigger: User-initiated (agent stops and surfaces, user runs the command)
- Auto-advance exception: In auto-advance mode, jumps straight into discuss
- Re-entry: Focused update — discuss only the affected area, surgically update FEATURE.md
- Blocker context: Shown via "RE-DISCUSSING [slug]" banner + summary of what the agent found
- Storage: Blockers written to `## Blockers` section of FEATURE.md
- Status flow: Rewinds to `discussing` (e.g., researching → discussing → discussed → researching)
- Loop guard: Warn on repeat blocker, don't prevent
- Scope: Research + Planning + Execution stages

## Decisions

- Agent stops and surfaces blockers; user initiates re-discuss (except in auto-advance mode)
- Focused re-discussion on affected area only, not full re-discuss
- Blocker context persisted in ## Blockers section of FEATURE.md
- Status rewinds to discussing → discussed, then user re-runs the triggering stage
- Warn on repeat blockers but don't prevent re-discuss

## Blockers

---
*Created: 2026-02-21*
*Last updated: 2026-02-21*
