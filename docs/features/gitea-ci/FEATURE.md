---
name: Gitea CI
slug: gitea-ci
status: done
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

- [01-PLAN.md](01-PLAN.md) — Sub-workflow: per-feature processing unit (workflow_call)
- [02-PLAN.md](02-PLAN.md) — Orchestrator: cron + dispatch + usage guard + artifact upload

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

### Plan 02 — Orchestrator workflow (2026-02-20)
- **Inline bash instead of workflow_call dispatch:** Gitea Actions does not support dynamic workflow dispatch at runtime — gfd-tools runs inline in a bash loop rather than via workflow_call
- **Background jobs for concurrency:** `process_one "$SLUG" "$TYPE" &` with `wait -n` drain; GFD_MAX_CONCURRENT variable (default 1) controls parallelism
- **Dual usage guard:** Initial check before loop + per-feature re-check prevents both pre-run and mid-run overage
- **GITEA_OUTPUT (not GITHUB_OUTPUT):** Gitea Actions equivalent for step output passing
- **git ls-remote --exit-code for branch existence:** Exits non-zero if branch absent — no extra parsing needed

### Plan 01 — Sub-workflow execution (2026-02-20)
- **actions/checkout with token:** Used `token: ${{ secrets.GITEA_TOKEN }}` so git credential helper is configured automatically, preventing auth errors on `git push` (Gitea Pitfall 6)
- **tea login in-workflow:** Run `tea login add --non-interactive` as a workflow step rather than pre-configuring on the runner, making the workflow portable across runner instances
- **set -e in run_gfd step:** Ensures non-zero gfd-tools exit propagates as step failure; job failure is the orchestrator's signal that this feature failed
- **git diff --cached --quiet || git commit:** Avoids failing if gfd-tools already committed results; idempotent commit step
- **git checkout -B:** Capital B for idempotent branch creation (creates or resets existing branch)
- **No continue-on-error:** Unsupported in Gitea Actions — job failure is intentionally the failure signal

## Blockers

[Active blockers affecting this feature. Remove when resolved.]

---
*Created: 2026-02-20*
*Last updated: 2026-02-20*
