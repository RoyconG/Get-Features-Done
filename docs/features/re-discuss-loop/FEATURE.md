---
name: Re-Discuss Loop
slug: re-discuss-loop
status: done
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

- [x] Research, planning, and execution agents detect unresolvable blockers and stop with an error box showing the blocker and `/gfd:discuss-feature <slug>` as the fix command
- [x] Blocker details are written to the `## Blockers` section of FEATURE.md so they persist across context windows
- [x] `discuss-feature` detects blockers in FEATURE.md and runs a focused re-discussion on just the affected area (not a full re-discuss)
- [x] After re-discussion resolves the blocker, the blocker is removed from FEATURE.md and the user is shown the next command to run
- [x] Status rewinds to `discussing` then `discussed` during re-discuss, then the user re-runs the stage that triggered it
- [x] If the same blocker type recurs after a re-discuss, the agent warns the user before stopping
- [x] In auto-advance mode, the agent jumps directly into discuss-feature instead of stopping

## Tasks

- [01-PLAN.md](01-PLAN.md) — Add blocker detection to researcher, planner, executor agents
- [02-PLAN.md](02-PLAN.md) — Add Step 2.5 blocker-detection branch to discuss-feature workflow

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
- [01-PLAN] Executor blocker threshold is higher (Rule 4 equivalent) — must require user input, not Claude's discretion
- [01-PLAN] Status rewind targets are stage-specific: researcher→discussed, planner→researched, executor→planned
- [01-PLAN] Executor reuses existing AUTO_CFG variable (single declaration point in auto_mode_detection step)
- [01-PLAN] All three agents share identical four blocker type strings for consistent repeat detection across stages
- [02-PLAN] Active blocker detection uses `### [type:` line pattern (not any content) to avoid false positives on placeholder text
- [02-PLAN] Re-discuss path exits before Step 3 status guard — no guard modification needed
- [02-PLAN] Next command mapping: researcher→research-feature, planner→plan-feature, executor→execute-feature

## Blockers

## Token Usage

| Workflow | Date | Agent Role | Model | Cost |
|----------|------|------------|-------|------|
| execute | 2026-02-23 | gfd-executor | sonnet | est. |
| execute | 2026-02-23 | gfd-executor | sonnet | est. |
| execute | 2026-02-23 | gfd-verifier | sonnet | est. |

---
*Created: 2026-02-21*
*Last updated: 2026-02-23*
