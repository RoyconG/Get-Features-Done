---
feature: gitea-ci
plan: 01
subsystem: infra
tags: [gitea-actions, workflow_call, dotnet, tea-cli, anthropic, ci-cd]

# Dependency graph
requires:
  - feature: auto-skills
    provides: gfd-tools auto-research and auto-plan commands invoked by the sub-workflow
provides:
  - Reusable Gitea Actions sub-workflow (.gitea/workflows/gfd-process-feature.yml) triggered by workflow_call
  - Per-feature branch creation (ci/<slug>) and PR creation via tea CLI
  - .NET 10 setup and gfd-tools environment initialization in CI
affects: [gitea-ci]

# Tech tracking
tech-stack:
  added: [actions/setup-dotnet@v5, tea CLI (PR operations), Gitea Actions workflow_call]
  patterns: [reusable sub-workflow pattern (workflow_call with slug + type inputs), tea login --non-interactive for CI auth]

key-files:
  created:
    - .gitea/workflows/gfd-process-feature.yml
  modified: []

key-decisions:
  - "Used actions/checkout@v4 with token: GITEA_TOKEN to configure git credential helper automatically (avoids auth errors on git push)"
  - "Used tea login add --non-interactive in workflow step (not pre-configured on runner) to handle CI auth portably"
  - "Used set -e in run_gfd step so non-zero exit propagates as job failure — orchestrator treats job failure as feature failure"
  - "git diff --cached --quiet || git commit pattern avoids failing when gfd-tools already committed results"
  - "No continue-on-error (unsupported in Gitea) — job failure is the signal to orchestrator that this feature failed"

patterns-established:
  - "CI workflow pattern: checkout with token + setup-dotnet@v5 + chmod gfd-tools + tea login --non-interactive"
  - "Branch strategy: git checkout -B ci/<slug> for idempotent branch creation"

requirements-completed: []

# Metrics
duration: 1min
completed: 2026-02-20
---

# Feature [gitea-ci] Plan 01: Sub-Workflow Summary

**Gitea Actions reusable sub-workflow that checks out, sets up .NET 10 + tea CLI, runs auto-research or auto-plan on a ci/<slug> branch, and creates a PR**

## Performance

- **Duration:** 1 min
- **Started:** 2026-02-20T19:19:43Z
- **Completed:** 2026-02-20T19:20:45Z
- **Tasks:** 2 (combined into single file creation)
- **Files modified:** 1

## Accomplishments
- Created `.gitea/workflows/gfd-process-feature.yml` as a reusable `workflow_call` sub-workflow
- Implemented full feature processing pipeline: checkout -> .NET 10 setup -> gfd-tools chmod -> tea auth -> git config -> branch -> run -> commit -> push -> PR
- Correctly handles ANTHROPIC_API_KEY injection for Claude CLI invocation
- Handles both `research` and `plan` operation types via bash conditional

## Task Commits

Each task was committed atomically:

1. **Task 1 + Task 2: Create sub-workflow file** - `ce2fb3d` (feat)

Plan tasks 1 and 2 were combined into a single atomic write since Task 2 was purely additive steps to the same file created in Task 1. All 10 steps specified across both tasks are present in the single commit.

**Plan metadata:** (see final commit)

## Files Created/Modified
- `.gitea/workflows/gfd-process-feature.yml` - Reusable Gitea Actions sub-workflow for per-feature GFD processing (72 lines, 10 steps)

## Decisions Made
- Used `actions/checkout@v4` with `token: ${{ secrets.GITEA_TOKEN }}` so git push works without manual credential configuration (Pitfall 6 from research)
- Used `tea login add --non-interactive` in a workflow step rather than pre-configuring on the runner, making the workflow more portable
- Used `set -e` in the `run_gfd` step so any gfd-tools failure propagates as a step failure, causing the job to fail — this is the mechanism by which the orchestrator knows a feature failed
- Used `git checkout -B` (capital B) for idempotent branch creation (creates or resets)
- Committed both Task 1 (setup steps) and Task 2 (processing steps) together since they form a single coherent file

## Deviations from Plan

None - plan executed exactly as written. All 10 steps from the plan specification are present in the workflow file.

## Issues Encountered
None.

## User Setup Required

**External services require manual configuration:**

- **GITEA_TOKEN secret:** Gitea repo -> Settings -> Actions -> Secrets. Generate at Gitea -> Settings -> Applications -> Generate Token (with repo permissions)
- **ANTHROPIC_API_KEY secret:** Gitea repo -> Settings -> Actions -> Secrets
- **GITEA_URL variable:** Gitea repo -> Settings -> Actions -> Variables. Set to your Gitea instance URL (e.g., https://gitea.example.com)
- **tea CLI on runner host:** `wget -O /usr/local/bin/tea https://dl.gitea.com/tea/0.10.1/tea-0.10.1-linux-amd64 && chmod +x /usr/local/bin/tea` (one-time runner setup)

## Next Steps
- Plan 02 (Orchestrator): Create `gfd-nightly.yml` with cron schedule, manual dispatch, feature discovery, stale branch/PR skipping, Claude usage guard, and artifact upload
- The sub-workflow is ready to be called via `uses: ./.gitea/workflows/gfd-process-feature.yml` with `slug` and `type` inputs and `secrets: inherit`

---
*Feature: gitea-ci*
*Completed: 2026-02-20*
