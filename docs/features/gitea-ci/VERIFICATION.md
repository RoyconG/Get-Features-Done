---
feature: gitea-ci
verified: 2026-02-20T00:00:00Z
status: passed
score: 10/10 must-haves verified
re_verification: false
human_verification:
  - test: "Trigger gfd-nightly.yml via workflow_dispatch with slug + type inputs in Gitea UI"
    expected: "Workflow runs and processes only the specified feature, skipping auto-detect"
    why_human: "Requires live Gitea Actions runner with ANTHROPIC_API_KEY and GITEA_TOKEN secrets configured"
  - test: "Let nightly cron fire and confirm a feature in 'discussed' status gets a RESEARCH.md committed to ci/<slug> branch with a PR opened"
    expected: "PR titled 'ci(<slug>): auto-research' appears in Gitea with correct base=main and head=ci/<slug>"
    why_human: "Requires live runner, real Anthropic API key, and features in discussed/researched status"
  - test: "Confirm artifact 'gfd-nightly-summary-<run_number>' appears in the workflow run's artifacts list"
    expected: "Markdown file uploaded with run summary"
    why_human: "Requires live Gitea Actions execution to verify artifact upload action works"
---

# Feature gitea-ci: Gitea CI Verification Report

**Feature Goal:** Gitea Actions workflows that run overnight on a cron schedule to autonomously process GFD features through research and planning, with an orchestrator that discovers eligible features, guards against API overuse, dispatches per-feature work inline, and creates per-feature PRs.
**Acceptance Criteria:** 10 criteria to verify
**Verified:** 2026-02-20
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Orchestrator triggers on cron (0 3 * * *) and workflow_dispatch with slug + type inputs | VERIFIED | `gfd-nightly.yml` lines 4-20: `schedule: cron: '0 3 * * *'` and `workflow_dispatch` with `slug` (string, optional) and `type` (choice: '', research, plan) |
| 2 | Orchestrator auto-detects discussed → research, researched → plan | VERIFIED | `gfd-nightly.yml` lines 67-74: `list-features --status discussed` and `list-features --status researched` build `slug:type` pairs |
| 3 | Sub-workflows run auto-research or auto-plan per feature | VERIFIED | `gfd-nightly.yml` lines 139-144: inline bash `auto-research "$SLUG"` / `auto-plan "$SLUG"` with `timeout 3600`; `gfd-process-feature.yml` lines 52-55: same for workflow_call invocation |
| 4 | Configurable max concurrent sub-workflows (default 1) | VERIFIED | `gfd-nightly.yml` line 125: `MAX_CONCURRENT="${{ vars.GFD_MAX_CONCURRENT || '1' }}"`, line 204-206: concurrency gate, line 210: `process_one "$SLUG" "$TYPE" &` |
| 5 | Results committed to ci/<slug> branch with auto-created PR | VERIFIED | `gfd-nightly.yml` lines 137, 149-155: `git checkout -B "ci/$SLUG"`, `git push origin "ci/$SLUG"`, `tea pulls create --base main --head "ci/$SLUG"` |
| 6 | Features with existing unmerged branches/PRs are skipped | VERIFIED | `gfd-nightly.yml` lines 170-183: `git ls-remote --exit-code origin "refs/heads/ci/$SLUG"` for branch check; `tea pulls list --state open` + `grep -c "ci/$SLUG"` for PR check |
| 7 | Orchestrator monitors Claude API usage and hard-stops on threshold | VERIFIED | `gfd-nightly.yml` lines 80-106 (initial check) and 186-201 (per-feature re-check): Anthropic Admin API query, `HARD_STOP=true; break` when `OUTPUT_TOKENS > THRESHOLD` |
| 8 | Sub-workflow failures don't block other features | VERIFIED | `gfd-nightly.yml` line 123: `set +e`, lines 141-158: `RESULT=$?`, `if [ $RESULT -eq 0 ]` branches to PR creation or failure logging; loop continues to next feature |
| 9 | Workflow setup step installs .NET 10 and gfd-tools | VERIFIED | Both files: `actions/setup-dotnet@v5` with `dotnet-version: '10.0.x'` and `chmod +x get-features-done/bin/gfd-tools` |
| 10 | Nightly summary uploaded as workflow artifact | VERIFIED | `gfd-nightly.yml` lines 218-224: `if: always()` + `uses: https://github.com/christopherhx/gitea-upload-artifact@v4` with `retention-days: 30` |

**Score:** 10/10 truths verified

### Acceptance Criteria Coverage

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Orchestrator runs on cron (configurable, default ~3 AM) and workflow_dispatch with optional slug + type | VERIFIED | `gfd-nightly.yml` line 5: `cron: '0 3 * * *'`; lines 6-20: `workflow_dispatch` with `slug` and `type` inputs |
| 2 | Orchestrator auto-detects eligible features: `discussed` → research, `researched` → plan | VERIFIED | `gfd-nightly.yml` lines 67-74: both statuses queried via `list-features --status` and mapped to research/plan |
| 3 | Sub-workflows run `gfd-tools auto-research` or `gfd-tools auto-plan` for each dispatched feature | VERIFIED | `gfd-nightly.yml` lines 139-143 (orchestrator inline); `gfd-process-feature.yml` lines 52-55 (sub-workflow) |
| 4 | Configurable max concurrent sub-workflows (default 1) | VERIFIED | `GFD_MAX_CONCURRENT` variable with default 1; background jobs + `wait -n` drain |
| 5 | Each feature's results committed to ci/<slug> branch with auto-created PR | VERIFIED | `git checkout -B "ci/$SLUG"`, `git push`, `tea pulls create --base main --head "ci/$SLUG"` in both workflow files |
| 6 | Features with existing unmerged branches/PRs are skipped | VERIFIED | `git ls-remote --exit-code` + `tea pulls list --state open` with `continue` on match |
| 7 | Orchestrator monitors Claude API usage and hard-stops when threshold hit | VERIFIED | Dual guard: initial check (step 6) + per-feature re-check inside loop; `HARD_STOP=true; break` on overage |
| 8 | Sub-workflow failures don't block other features | VERIFIED | `set +e` + result-code-based branching; failed features log and loop continues |
| 9 | Workflow setup step installs .NET 10 and gfd-tools on the `claude` runner | VERIFIED | `actions/setup-dotnet@v5` (dotnet-version: 10.0.x) + `chmod +x get-features-done/bin/gfd-tools` in both files |
| 10 | Nightly run summary uploaded as Gitea Actions workflow artifact | VERIFIED | `gitea-upload-artifact@v4` with `if: always()` and 30-day retention |

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `.gitea/workflows/gfd-process-feature.yml` | Reusable sub-workflow triggered by workflow_call | VERIFIED | 73-line file, valid YAML, `workflow_call` trigger with `slug` + `type` inputs, 10 steps present |
| `.gitea/workflows/gfd-nightly.yml` | Orchestrator with cron + workflow_dispatch triggers | VERIFIED | 224-line file, valid YAML, `schedule` + `workflow_dispatch` triggers, 9-step job `orchestrate` |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `gfd-nightly.yml` | `gfd-tools auto-research / auto-plan` | inline bash with ANTHROPIC_API_KEY env var | WIRED | Lines 118-121: env vars injected; lines 139-143: both commands called conditionally |
| `gfd-nightly.yml` | `gfd-tools list-features` | bash step parsing `feature_slug=` key=value output | WIRED | Lines 67-70: `list-features --status discussed` and `--status researched` with `grep "^feature_slug="` |
| `gfd-nightly.yml` | `gitea-upload-artifact` | `uses: https://github.com/christopherhx/gitea-upload-artifact@v4` | WIRED | Lines 220-224: artifact action used with `name`, `path`, `retention-days` configured |
| `gfd-process-feature.yml` | `gfd-tools auto-research / auto-plan` | bash step with ANTHROPIC_API_KEY env var | WIRED | Lines 48-56: `ANTHROPIC_API_KEY` env + `set -e` + conditional command execution |
| `gfd-process-feature.yml` | `tea pulls create` | bash step after git push | WIRED | Lines 67-72: `tea pulls create` with `--title`, `--base main`, `--head "ci/${{ inputs.slug }}"` |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| None | — | — | — | — |

No TODOs, FIXMEs, placeholder returns, empty handlers, or stub implementations found in either workflow file.

### Human Verification Required

#### 1. Manual workflow_dispatch with slug + type

**Test:** In Gitea, navigate to the GFD Nightly workflow and trigger via workflow_dispatch. Provide a valid feature slug and type "research".
**Expected:** Workflow runs, skips auto-detection, processes only the specified feature, and creates a PR on branch `ci/<slug>`.
**Why human:** Requires live Gitea Actions runner with secrets configured (ANTHROPIC_API_KEY, GITEA_TOKEN, GITEA_URL variable).

#### 2. Cron-triggered nightly run with eligible features

**Test:** Ensure at least one feature has status "discussed" or "researched" in FEATURE.md, wait for or manually force the 3 AM UTC cron, check resulting PRs.
**Expected:** PR created for each eligible feature; features with existing ci/<slug> branches are skipped; summary artifact appears in workflow run.
**Why human:** Requires live execution, real Anthropic API key, and actual features in eligible statuses.

#### 3. Nightly summary artifact presence

**Test:** After any completed run of GFD Nightly, check the workflow run's "Artifacts" section in Gitea.
**Expected:** Artifact named `gfd-nightly-summary-<run_number>` is present and downloadable as markdown.
**Why human:** Artifact upload only verifiable via live Gitea Actions execution.

### Gaps Summary

No gaps found. All 10 acceptance criteria are fully implemented in the workflow files:

- `.gitea/workflows/gfd-nightly.yml` (224 lines) implements criteria 1, 2, 4, 5, 6, 7, 8, 9, 10.
- `.gitea/workflows/gfd-process-feature.yml` (73 lines) implements criteria 3, 5, 9 for the reusable sub-workflow path.

Both YAML files pass Python YAML validation. All key implementation patterns are present: `workflow_call`, cron + `workflow_dispatch`, `list-features --status`, dual usage guard with hard-stop, stale branch + open PR skip, background-job concurrency with `GFD_MAX_CONCURRENT`, `set +e` failure isolation, and `if: always()` artifact upload.

The feature has a known pre-requisite dependency on `auto-skills` being merged before the workflows are functional at runtime, but this is a deployment concern, not an implementation gap — the workflow files themselves are complete.

---

_Verified: 2026-02-20_
_Verifier: Claude (gfd-verifier)_
