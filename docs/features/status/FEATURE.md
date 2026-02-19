---
name: Status
slug: status
status: done
owner: Conroy
assignees: []
created: 2026-02-20
priority: high
depends_on: []
---
# Status

## Description

Introduces a reworked feature lifecycle with new states (`new` → `discussing` → `discussed` → `researching` → `researched` → `planning` → `planned` → `in-progress` → `done`) and builds all the commands to support it. Replaces the current progress command with a clean status table showing active features. Simplifies `new-feature` to minimal input, creates `discuss-feature` and `research-feature` commands, and updates existing commands to use the new states.

## Acceptance Criteria

- [ ] Running `/gfd:status` displays a Feature Name | Status table, excluding `done` features
- [ ] When no active features exist, a helpful message is shown with a hint to create one
- [ ] Feature lifecycle uses new states: new, discussing, discussed, researching, researched, planning, planned, in-progress, done
- [ ] `/gfd:new-feature` simplified to slug + one-liner, sets status to `new`
- [ ] `/gfd:discuss-feature` exists — deep conversation to refine scope, transitions `new` → `discussing` → `discussed`
- [ ] `/gfd:research-feature` exists — investigates implementation approach, transitions `discussed` → `researching` → `researched`
- [ ] `/gfd:plan-feature` updated to transition `researched` → `planning` → `planned`
- [ ] `/gfd:execute-feature` updated to transition `planned` → `in-progress` → `done`

## Tasks

- [01-PLAN.md](01-PLAN.md) — Update gfd-tools.cjs for 9-state lifecycle
- [02-PLAN.md](02-PLAN.md) — Create /gfd:status command and workflow
- [03-PLAN.md](03-PLAN.md) — Create /gfd:discuss-feature command and workflow
- [04-PLAN.md](04-PLAN.md) — Create /gfd:research-feature command and workflow
- [05-PLAN.md](05-PLAN.md) — Update new-feature, plan-feature, execute-feature, and template

## Notes

- Replaces the existing `/gfd:progress` command
- Status table uses raw simple values, no symbols or formatting
- The `backlog` status is removed from the lifecycle entirely
- New features start at `new` instead of `backlog`

---
*Created: 2026-02-20*
*Last updated: 2026-02-20*
