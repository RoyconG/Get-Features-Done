---
feature: gitea-ci
plan: 02
subsystem: infra
tags: [gitea-actions, cron, workflow_dispatch, anthropic-api, tea-cli, artifact, concurrency]

# Dependency graph
requires:
  - feature: auto-skills
    provides: gfd-tools auto-research and auto-plan commands invoked inline in the process loop
  - plan: "gitea-ci/01"
    provides: gfd-process-feature.yml sub-workflow (used for manual single-feature invocation via workflow_call; nightly orchestrator runs inline instead)
provides:
  - Nightly orchestrator workflow (.gitea/workflows/gfd-nightly.yml) with cron + workflow_dispatch triggers
  - Auto-detection of eligible features by status (discussed → research, researched → plan)
  - Claude API usage guard (Anthropic Admin API, optional ANTHROPIC_ADMIN_KEY secret)
  - Stale branch and open PR skip logic
  - Configurable concurrency via GFD_MAX_CONCURRENT (default 1, implemented via bash background jobs + wait -n)
  - Nightly summary artifact upload (retention-days: 30)
affects: [gitea-ci]

# Tech tracking
tech-stack:
  added: [christopherhx/gitea-upload-artifact@v4, Anthropic Admin API (usage_report/messages endpoint)]
  patterns:
    - Inline bash per-feature execution (not workflow_call) to work around Gitea dynamic dispatch limitations
    - Background-job concurrency via process_one() & + wait -n with fallback to wait
    - Dual usage guard: initial check before loop + re-check before each feature dispatch
    - GITEA_OUTPUT for step output passing (Gitea equivalent of GITHUB_OUTPUT)
    - if: always() on artifact upload step to ensure summary is captured even on failure

key-files:
  created:
    - .gitea/workflows/gfd-nightly.yml
  modified: []

key-decisions:
  - "Inline bash execution (not workflow_call dispatch) — Gitea Actions does not support dynamic matrix or runtime workflow_call dispatch, so gfd-tools runs inline in a bash loop"
  - "Background jobs via process_one() & for concurrency — GFD_MAX_CONCURRENT=1 (default) is effectively sequential; set to 2+ for parallel"
  - "wait -n 2>/dev/null || wait — bash 4.3+ wait -n waits for any single background job; fallback to wait (wait for all) for older bash"
  - "Dual usage guard: initial check before dispatch loop + re-check before each feature — prevents mid-run overage when running many features"
  - "git ls-remote --exit-code for branch existence — exits non-zero if branch absent, avoids extra parsing"
  - "tea pulls list --fields head --output simple with grep-c for PR existence — with inline comment noting format should be verified on target system"
  - "GITEA_OUTPUT (not GITHUB_OUTPUT) for step output — Gitea Actions equivalent"
  - "if: always() on upload step — summary is uploaded even when process step fails"

patterns-established:
  - "Artifact upload pattern: uses: https://github.com/christopherhx/gitea-upload-artifact@v4 with retention-days: 30"
  - "Usage guard pattern: curl Anthropic Admin API + jq '[.data[].output_tokens] | add // 0' with fallback to 0 on parse failure"
  - "Inline nightly orchestrator pattern: for pair in ${{ steps.discover.outputs.pairs }}; do ... process_one "$SLUG" "$TYPE" &"

requirements-completed: []

# Metrics
duration: 2min
completed: 2026-02-20
---

# Feature [gitea-ci] Plan 02: Orchestrator Summary

**Nightly orchestrator workflow (gfd-nightly.yml) with cron/dispatch triggers, feature auto-discovery, Claude API usage guard, background-job concurrency, and artifact upload**

## Performance

- **Duration:** 2 min
- **Started:** 2026-02-20T19:23:11Z
- **Completed:** 2026-02-20T19:25:08Z
- **Tasks:** 2 (combined into single file creation, both tasks write to same file)
- **Files modified:** 1

## Accomplishments
- Created `.gitea/workflows/gfd-nightly.yml` — 224-line orchestrator workflow with 9 steps
- Implemented full feature discovery pipeline: gfd-tools list-features --status → slug:type pairs → GITEA_OUTPUT
- Implemented Claude API usage guard using Anthropic Admin API (optional; degrades gracefully when ANTHROPIC_ADMIN_KEY absent)
- Implemented stale branch + open PR skip logic before each dispatch
- Implemented background-job concurrency (GFD_MAX_CONCURRENT variable, default 1) with wait -n
- Implemented nightly summary artifact upload with if: always() and 30-day retention

## Task Commits

Each task was committed atomically:

1. **Task 1 + Task 2: Create orchestrator workflow** - `dd72e37` (feat)

Plan tasks 1 and 2 were combined into a single atomic commit since both tasks write to the same file. The complete 9-step workflow was written in a single pass and verified before committing.

**Plan metadata:** (see final commit)

## Files Created/Modified
- `.gitea/workflows/gfd-nightly.yml` — Nightly orchestrator workflow (224 lines, 9 steps)

## Decisions Made

- **Inline bash instead of workflow_call dispatch:** Gitea Actions does not support dynamic matrix or runtime dispatch (you cannot call a reusable workflow with a dynamically computed slug). The orchestrator runs gfd-tools auto-research / auto-plan inline via bash rather than dispatching gfd-process-feature.yml. The sub-workflow from Plan 01 remains available for manual single-feature invocation.

- **Background jobs for concurrency:** `process_one "$SLUG" "$TYPE" &` with `JOB_COUNT` tracking and `wait -n` for drain. Default `GFD_MAX_CONCURRENT=1` is sequential in practice. Users can set to 2-3 for parallel processing.

- **`wait -n 2>/dev/null || wait` fallback:** `wait -n` (bash 4.3+) waits for any single background job to finish; the `|| wait` fallback waits for all if the runner has an older bash.

- **Dual usage guard:** Initial check before the loop prevents starting at all when already over threshold. Per-feature re-check before each dispatch prevents overage mid-run on long lists.

- **GFD_USAGE_THRESHOLD variable:** Defaults to 100000 output tokens/hour. Configurable via Gitea repo -> Settings -> Actions -> Variables.

- **Key configuration variables:** GFD_MAX_CONCURRENT (parallelism), GFD_USAGE_THRESHOLD (usage guard), GITEA_URL (required for tea auth).

## Deviations from Plan

None - plan executed exactly as written. All steps from both task specifications are present in the workflow file in the correct order.

## Issues Encountered

None.

## User Setup Required

The following must be configured manually in Gitea before the nightly workflow is functional:

**Required secrets (Gitea repo -> Settings -> Actions -> Secrets):**
- `GITEA_TOKEN` — Gitea personal access token with repo permissions (also used by Plan 01 sub-workflow)
- `ANTHROPIC_API_KEY` — Anthropic API key for Claude CLI invocation

**Optional secrets:**
- `ANTHROPIC_ADMIN_KEY` — Anthropic Admin API key (org account only). Enables accurate usage guard. If absent, usage guard is disabled and the workflow logs a notice.

**Required variables (Gitea repo -> Settings -> Actions -> Variables):**
- `GITEA_URL` — Your Gitea instance URL (e.g., https://gitea.example.com) — required for tea auth

**Optional variables:**
- `GFD_MAX_CONCURRENT` — Max concurrent background jobs (default: 1). Set to 2-3 for parallel feature processing.
- `GFD_USAGE_THRESHOLD` — Output token threshold for hard-stop (default: 100000 tokens/hour)

## Next Steps
- Both workflow files are complete. The gitea-ci feature is fully implemented.
- The nightly workflow will be functional once auto-skills is merged (provides gfd-tools auto-research and auto-plan).
- Workflow files can be committed now; nightly runs will fail gracefully until auto-skills is merged.

## Self-Check: PASSED

- `.gitea/workflows/gfd-nightly.yml` — FOUND
- `docs/features/gitea-ci/02-SUMMARY.md` — FOUND
- Commit `dd72e37` — FOUND

---
*Feature: gitea-ci*
*Completed: 2026-02-20*
