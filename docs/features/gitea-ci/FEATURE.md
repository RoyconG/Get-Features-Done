---
name: Gitea CI
slug: gitea-ci
status: planned
owner: Conroy
assignees: []
created: 2026-02-20
priority: medium
depends_on: [auto-skills]
---
# Gitea CI

## Description

Gitea Actions workflows that run overnight on a cron schedule to autonomously process GFD features through research and planning. A main orchestrator workflow scans for eligible features (by status), dispatches configurable-parallel sub-workflows for each, monitors Claude API usage with a hard-stop guard, and creates per-feature PRs with results. Supports manual dispatch with slug + type parameters.

## Acceptance Criteria

- [ ] Orchestrator workflow runs on a cron schedule (configurable, default ~3 AM) and via manual `workflow_dispatch` with optional slug + type (research/plan) parameters
- [ ] Orchestrator auto-detects eligible features: `discussed` → research, `researched` → plan
- [ ] Sub-workflows run `gfd-tools auto-research` or `gfd-tools auto-plan` for each dispatched feature
- [ ] Configurable max concurrent sub-workflows (default 1)
- [ ] Each feature's results are committed to a per-feature branch (`ci/<slug>`) with an auto-created PR
- [ ] Features with existing unmerged branches/PRs are skipped
- [ ] Orchestrator monitors Claude API usage and hard-stops (no new dispatches) when threshold is hit
- [ ] Sub-workflow failures don't block other features — orchestrator continues with the next
- [ ] Workflow setup step installs .NET 10 and gfd-tools on the `claude` runner
- [ ] Nightly run summary uploaded as a Gitea Actions workflow artifact

## Tasks

[Populated during planning. Links to plan files.]

## Notes

### Implementation Decisions
- **Feature selection:** Auto-detect by FEATURE.md status (discussed → research, researched → plan)
- **Architecture:** Orchestrator workflow dispatches sub-workflows with feature slug parameter
- **Parallelism:** Configurable max concurrent (default 1)
- **Runner:** Self-hosted `claude` runner, Claude CLI pre-installed, needs .NET 10 + gfd-tools setup
- **Secrets:** `ANTHROPIC_API_KEY` stored as Gitea secret
- **Branch strategy:** Per-feature branches (`ci/<slug>`), auto-create PRs
- **Stale branches:** Skip features with existing unmerged branches/PRs
- **Usage guard:** Monitor Claude API usage, hard-stop on threshold
- **Failure policy:** Continue with next feature, log failure
- **Summary:** Workflow artifact (not committed to repo)
- **Notifications:** None — check PRs and workflow UI manually
- **Manual dispatch:** Accepts optional slug + type (research vs plan) parameters

### Claude's Discretion
- None — user specified all key decisions

### Deferred Ideas
- None

## Decisions

[Key decisions made during planning and execution of this feature.]

## Blockers

[Active blockers affecting this feature. Remove when resolved.]

---
*Created: 2026-02-20*
*Last updated: 2026-02-20*
