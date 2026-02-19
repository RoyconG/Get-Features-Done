# Get Features Done (GFD)

## What This Is

A feature-driven project management toolkit for AI-assisted development, forked from Get Shit Done (GSD). GFD reorganizes the workflow around features as the primary unit of work instead of milestones and phases, enabling team collaboration without planning document conflicts.

## Core Value

Multiple team members can independently plan and execute features in parallel, with all planning docs committed to version control without conflicts.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] Feature-scoped directory structure (`docs/features/<slug>/`)
- [ ] New-feature workflow creates FEATURE.md with acceptance criteria
- [ ] Plan-feature workflow generates implementation plans per feature
- [ ] Execute-feature workflow runs plans with atomic commits
- [ ] Planning docs commit cleanly to git without cross-feature conflicts
- [ ] Same agent quality patterns as GSD (researcher, plan checker, verifier)
- [ ] Parallel execution of independent plans within a feature

### Out of Scope

- Milestone/phase hierarchy — replaced by flat feature structure
- Multi-version roadmapping — features are independently deliverable
- Cross-feature dependency orchestration — features declare dependencies but don't auto-sequence

## Context

- GFD shares GSD's agent core: researcher, planner, executor, verifier agent types
- The key structural change is `docs/features/<slug>/` instead of `.planning/<phase>/`
- Each feature is self-contained — its plans, research, and state live in its directory
- Built for internal team use, not public release
- GFD is mostly built; remaining work is polish and conversion tooling

## Constraints

- **Shared core**: Must reuse GSD's agent patterns (researcher, planner, executor, verifier)
- **Git-friendly**: All planning artifacts must be committable without merge conflicts across features
- **Team use**: Directory structure must support concurrent work by multiple people

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Features replace milestones as top-level unit | Milestones force artificial grouping; features match how team thinks about work | — Pending |
| Planning docs in `docs/features/` not `.planning/` | Committed to git for team visibility; feature-scoped to avoid conflicts | — Pending |
| Shared GSD agent core | Proven patterns, no need to reinvent researcher/planner/executor/verifier | — Pending |

---
*Last updated: 2026-02-20 after initial setup*
