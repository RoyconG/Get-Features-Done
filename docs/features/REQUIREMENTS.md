# Requirements: Get Features Done (GFD)

**Defined:** 2026-02-20
**Core Value:** Multiple team members can independently plan and execute features in parallel, with planning docs committed without conflicts.

## v1 Requirements

### Workflow

- [ ] **WKFL-01**: User can create a new feature with `/gfd:new-feature <slug>`
- [ ] **WKFL-02**: User can plan a feature with `/gfd:plan-feature <slug>`
- [ ] **WKFL-03**: User can execute a feature with `/gfd:execute-feature <slug>`
- [ ] **WKFL-04**: User can check project progress with `/gfd:progress`

### Structure

- [ ] **STRC-01**: Each feature gets its own directory at `docs/features/<slug>/`
- [ ] **STRC-02**: Feature planning docs (FEATURE.md, PLAN.md, research) live inside the feature directory
- [ ] **STRC-03**: All planning docs commit to git without cross-feature conflicts

### Agents

- [ ] **AGNT-01**: Researcher agent investigates domain before planning a feature
- [ ] **AGNT-02**: Planner agent creates implementation plans for a feature
- [ ] **AGNT-03**: Executor agent runs plans with atomic commits
- [ ] **AGNT-04**: Verifier agent confirms acceptance criteria are met after execution

### Conversion

- [ ] **CONV-01**: Existing GSD agent types are adapted to work within feature-scoped directories
- [ ] **CONV-02**: GFD tooling (bin/gfd-tools.cjs) handles init, commit, and state management

## v2 Requirements

### Team

- **TEAM-01**: Team members can assign themselves to features
- **TEAM-02**: Progress view shows who is working on what

## Out of Scope

| Feature | Reason |
|---------|--------|
| Milestone orchestration | Replaced by flat feature model |
| Cross-feature auto-sequencing | Features declare deps but execute independently |
| Public release packaging | Internal team tool |

## Traceability

Which features cover which requirements. Updated during feature creation.

| Requirement | Feature | Status |
|-------------|---------|--------|
| WKFL-01 | — | Pending |
| WKFL-02 | — | Pending |
| WKFL-03 | — | Pending |
| WKFL-04 | — | Pending |
| STRC-01 | — | Pending |
| STRC-02 | — | Pending |
| STRC-03 | — | Pending |
| AGNT-01 | — | Pending |
| AGNT-02 | — | Pending |
| AGNT-03 | — | Pending |
| AGNT-04 | — | Pending |
| CONV-01 | — | Pending |
| CONV-02 | — | Pending |

**Coverage:**
- v1 requirements: 13 total
- Mapped to features: 0
- Unmapped: 13

---
*Requirements defined: 2026-02-20*
*Last updated: 2026-02-20 after initial definition*
