---
name: Convert From GSD
slug: convert-from-gsd
status: done
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

- [01-PLAN.md](01-PLAN.md) — Command file + discovery/review workflow (steps 1-5)
- [02-PLAN.md](02-PLAN.md) — Migration execution workflow (steps 6-12)

## Notes

- This feature targets projects that were previously managed with GSD and are migrating to GFD's feature-driven structure.
- The key structural change is from `.planning/<phase>/` (numbered, milestone-scoped) to `docs/features/<slug>/` (named, independently deliverable).
- GSD status mapping: complete → done, in-progress → in-progress, planned → planned, has research → researched, has context/goal → discussed, not started → new.

## Decisions

- gsd_phase field in FEATURE.md frontmatter for traceability back to original GSD phase
- Delete-last pattern: verify all expected FEATURE.md files exist before rm -rf .planning/
- ACCEPTED_MAPPINGS shell variable passed from Step 5 to Plan 02 migration execution
- Archived phases in milestones/*/phases/ auto-marked as done; decimal phase prefixes stripped before slug generation
- Status detection uses disk artifacts (plan/summary/research counts) not ROADMAP.md checkbox state — disk is ground truth

## Blockers

[Active blockers affecting this feature. Remove when resolved.]

---
*Created: 2026-02-20*
*Last updated: 2026-02-20*
