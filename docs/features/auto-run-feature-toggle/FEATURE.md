---
name: Auto Run Feature Toggle
slug: auto-run-feature-toggle
status: researched
owner: Conroy
assignees: []
created: 2026-02-20
priority: medium
depends_on: []
---
# Auto Run Feature Toggle

## Description

Add an auto-advance capability to GFD that chains lifecycle stages together automatically. A dedicated command starts auto-advancing a feature from its current status through subsequent stages (research → plan → execute), with each stage running in a fresh context. The pipeline halts at a configurable stop point or on error. Manual mode (one stage at a time) remains the default.

## Acceptance Criteria

- [ ] Project-level default mode configurable in a dedicated config file (default: manual)
- [ ] Per-feature override via FEATURE.md frontmatter (`auto_advance` field)
- [ ] Configurable stop point per-feature in FEATURE.md frontmatter (`auto_advance_until` field)
- [ ] A new command starts auto-advancing a feature from its current status
- [ ] Each stage runs in a fresh context (equivalent to /clear between stages)
- [ ] Auto-advance halts and notifies on stage failure

## Tasks

[Populated during planning. Links to plan files.]

## Notes

### Implementation Decisions
- **Granularity:** Project-level default + per-feature override (applies to all stages of a feature)
- **Project config:** Dedicated config file for the project-level default
- **Feature config:** FEATURE.md frontmatter for per-feature override and stop point
- **No CLI toggle command:** Users edit config/frontmatter directly to set defaults
- **Trigger:** A new command (Claude's discretion on naming — e.g. `/gfd:run <slug>`) kicks off auto-advance from current status
- **Context:** Fresh context window between each stage
- **Error handling:** Stop and notify on failure — no retries
- **Scope:** Works on all features regardless of when they were created
- **Inheritance:** Simple resolved value — no need to surface whether mode is inherited or explicit

### Deferred Ideas
- **Headless/autonomous mode:** Running features overnight on CI agents without human interaction — separate `/gfd:new-feature`

## Decisions

## Blockers

---
*Created: 2026-02-20*
*Last updated: 2026-02-21*
