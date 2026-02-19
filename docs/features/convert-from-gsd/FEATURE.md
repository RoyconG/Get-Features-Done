---
name: Convert From GSD
slug: convert-from-gsd
status: planned
owner: Conroy
assignees: []
created: 2026-02-20
priority: high
depends_on: []
---
# Convert From GSD

## Description

A migration tool that scans a project's GSD `.planning/` directory, maps phases and milestones to GFD features, and creates the corresponding `docs/features/<slug>/` structure. All GSD artifacts — research docs, plans, summaries, verification reports — are migrated into the feature directory. After conversion, the original `.planning/` directory is removed.

## Acceptance Criteria

- [ ] Scans `.planning/` and discovers all GSD phases, milestones, roadmap, research, summaries, and verification docs
- [ ] Presents a summary table mapping each GSD phase/milestone to a suggested GFD feature slug with GSD status
- [ ] User can accept, rename, or skip each suggested mapping before conversion proceeds
- [ ] Creates `docs/features/<slug>/FEATURE.md` for each accepted mapping, populated with context from the GSD plans
- [ ] Migrates all related GSD documents (RESEARCH.md, PLAN.md, SUMMARY.md, VERIFICATION.md, etc.) into the feature directory
- [ ] GSD statuses are mapped to GFD statuses (complete → done, in-progress → in-progress, etc.)
- [ ] Deletes `.planning/` directory after successful conversion

## Tasks

[Populated during planning. Links to plan files.]

## Notes

- This feature targets projects that were previously managed with GSD and are migrating to GFD's feature-driven structure.
- The key structural change is from `.planning/<phase>/` (numbered, milestone-scoped) to `docs/features/<slug>/` (named, independently deliverable).
- GSD status mapping: complete → done, in-progress → in-progress, planned → planned, not started → backlog.

---
*Created: 2026-02-20*
*Last updated: 2026-02-20*
