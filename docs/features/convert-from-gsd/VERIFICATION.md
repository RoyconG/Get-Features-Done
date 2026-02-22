---
feature: convert-from-gsd
verified: 2026-02-20T00:00:00Z
status: passed
score: 7/7 must-haves verified
re_verification: false
---

# Feature convert-from-gsd: Convert From GSD Verification Report

**Feature Goal:** A migration tool that scans a project's GSD `.planning/` directory, maps phases and milestones to GFD features, and creates the corresponding `docs/features/<slug>/` structure. All GSD artifacts are migrated into the feature directory. After conversion, the original `.planning/` directory is removed.
**Acceptance Criteria:** 7 criteria to verify
**Verified:** 2026-02-20T00:00:00Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Running `/gfd:convert-from-gsd` in a GSD project discovers all phase directories | VERIFIED | `scanPhaseDirs` and `scanMilestonePhaseDirs` functions handle `.planning/phases/` and `.planning/milestones/*/phases/` respectively (workflow lines 68-106) |
| 2 | A mapping table is presented showing GSD phase name, suggested slug, and computed GFD status | VERIFIED | Step 4 outputs a `## Suggested Feature Mappings` table with `\| # \| GSD Phase \| Suggested Slug \| Status \| Notes \|` before any writes (workflow lines 204-218) |
| 3 | User can respond to each mapping with accept, rename, or skip before any files are created | VERIFIED | Step 5 uses `AskUserQuestion` per phase with accept/rename/skip options; `ACCEPTED_MAPPINGS` is built and confirmed before Step 6 writes anything (workflow lines 222-276) |
| 4 | Phases archived in `.planning/milestones/` are included alongside active phases | VERIFIED | `scanMilestonePhaseDirs` scans `milestones/*/phases/` subdirectories; archived phases auto-get `done` status (workflow lines 85-97) |
| 5 | Status is derived from disk artifacts (plans, summaries, research, context) not ROADMAP.md checkboxes | VERIFIED | `detectStatus` function checks file counts: plans, summaries, VERIFICATION.md, RESEARCH.md, CONTEXT.md (workflow lines 121-136) |
| 6 | Each accepted phase produces a `docs/features/<slug>/FEATURE.md` populated from ROADMAP.md | VERIFIED | Step 7 creates FEATURE.md with `extractPhaseGoal` (Description) and criteria from ROADMAP.md; `gsd_phase` traceability field included (workflow lines 302-378) |
| 7 | `.planning/` is deleted only after all expected feature directories are verified | VERIFIED | Step 10 checks all slugs' FEATURE.md files exist before Step 11 runs `rm -rf .planning/` (workflow lines 526-565) |

**Score:** 7/7 truths verified

### Acceptance Criteria Coverage

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Scans `.planning/` and discovers all GSD phases, milestones, roadmap, research, summaries, and verification docs | VERIFIED | Steps 1-4 scan both active phases and archived milestone phases. Step 4 reads ROADMAP.md for goals/criteria. Step 9 migrates `research/`. Status detection checks summaries and verification docs. |
| 2 | Presents a summary table mapping each GSD phase/milestone to a suggested GFD feature slug with GSD status | VERIFIED | Step 4 produces `MAPPING_JSON` and displays `## Suggested Feature Mappings` table with columns: #, GSD Phase, Suggested Slug, Status, Notes. Archived phases shown with "archived" note. |
| 3 | User can accept, rename, or skip each suggested mapping before conversion proceeds | VERIFIED | Step 5 uses `AskUserQuestion` per phase with three options (Accept/Rename/Skip). Rename validates slug format. Confirmation step at end of Step 5 with "Go back" option. No files written until Step 6 begins. |
| 4 | Creates `docs/features/<slug>/FEATURE.md` for each accepted mapping, populated with context from the GSD plans | VERIFIED | Step 7 Node.js script creates FEATURE.md with frontmatter (name, slug, status, owner, gsd_phase), Description from ROADMAP.md `**Goal**`, Acceptance Criteria from ROADMAP.md success criteria. Step 8 updates Tasks section with links to migrated plan files. |
| 5 | Migrates all related GSD documents (RESEARCH.md, PLAN.md, SUMMARY.md, VERIFICATION.md, etc.) into the feature directory | VERIFIED | Step 8 copies all `.md` files from GSD phase dir to GFD feature dir with rename rules: `NN-MM-PLAN.md` → `MM-PLAN.md`, `NN-MM-SUMMARY.md` → `MM-SUMMARY.md`, `NN-RESEARCH.md` → `RESEARCH.md`, `NN-VERIFICATION.md` → `VERIFICATION.md`, etc. Frontmatter updated via `gfd-tools.cjs frontmatter merge`. |
| 6 | GSD statuses are mapped to GFD statuses (complete → done, in-progress → in-progress, etc.) | VERIFIED | `detectStatus` function maps disk artifacts to all 6 GFD statuses: done (plans=summaries+VERIFICATION.md or archived), in-progress (summaries>0), planned (plans>0), researched (RESEARCH.md), discussed (CONTEXT.md or ROADMAP.md goal), new (nothing). Covers the full GSD→GFD status mapping defined in FEATURE.md notes. |
| 7 | Deletes `.planning/` directory after successful conversion | VERIFIED | Step 10 verifies all expected FEATURE.md files exist. Step 11 runs `rm -rf .planning/` only after Step 10 passes (delete-last pattern). Step 10 failure outputs error and exits without deleting. |

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `commands/gfd/convert-from-gsd.md` | Slash command definition with allowed-tools and workflow reference | VERIFIED | 16 lines. Frontmatter: `name: gfd:convert-from-gsd`, `allowed-tools: Read, Write, Edit, Bash, Grep, Glob, AskUserQuestion`. References `workflows/convert-from-gsd.md` in `<execution_context>`. |
| `get-features-done/workflows/convert-from-gsd.md` | Steps 1-12 of migration workflow | VERIFIED | 642 lines. Contains all 12 numbered steps. `<purpose>`, `<required_reading>`, `<process>`, `<output>`, `<success_criteria>` tags all present and properly closed. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `commands/gfd/convert-from-gsd.md` | `get-features-done/workflows/convert-from-gsd.md` | `@execution_context` reference | WIRED | `@$HOME/.claude/get-features-done/workflows/convert-from-gsd.md` present in command file |
| Workflow step 3: scan phases | Workflow step 4: present table | Node.js `PHASES_JSON` → `MAPPING_JSON` pipeline | WIRED | `PHASES_JSON` produced in Step 3, consumed in Step 4's `node -e` via `$(echo $PHASES_JSON)` |
| Workflow step 7: create FEATURE.md | ROADMAP.md `**Goal**` field | `extractPhaseGoal` regex | WIRED | `\*\*Goal\*\*` pattern present in regex; function called during FEATURE.md generation |
| Workflow step 8: migrate artifacts | `gfd-tools.cjs frontmatter merge` | `node gfd-tools.cjs frontmatter merge --data` | WIRED | Full command present in step 8 with `{feature: m.slug}` data payload |
| Workflow step 10: verify | Workflow step 11: delete `.planning/` | Pre-deletion FEATURE.md existence check | WIRED | `MISSING` variable checked; `exit 1` prevents reaching `rm -rf .planning/` on failure |

### Anti-Patterns Found

None. No TODOs, FIXMEs, placeholders, empty implementations, or stub handlers found in either artifact.

### Human Verification Required

#### 1. End-to-end migration on a real GSD project

**Test:** Run `/gfd:convert-from-gsd` in a project with a `.planning/` directory containing at least 3 phases (including at least one archived in `milestones/*/phases/`).
**Expected:** Banner appears, mapping table shown with correct slugs and statuses, user walks through accept/rename/skip per phase, FEATURE.md files created with ROADMAP.md goal/criteria, artifacts migrated and renamed, `.planning/` deleted after migration.
**Why human:** Requires a real GSD project fixture. Cannot verify interactive `AskUserQuestion` prompts or actual Claude tool execution programmatically.

#### 2. Slug rename validation

**Test:** At Step 5, choose "Rename" and enter an invalid slug (e.g., `My Feature!`), then enter a valid one (e.g., `my-feature`).
**Expected:** Error message displayed for invalid slug; re-prompts until valid slug entered.
**Why human:** Slug validation regex behavior requires interactive testing.

#### 3. Dependency warning for skipped phases

**Test:** Skip a phase that another accepted phase depends on (per ROADMAP.md `**Depends on**` field).
**Expected:** Warning shown: "Warning: `[slug]` depends on `[skipped-slug]` which was skipped."
**Why human:** Requires a ROADMAP.md with `**Depends on**` entries and specific skip choices during interactive review.

### Gaps Summary

No gaps found. All 7 acceptance criteria are substantively implemented in the workflow file (`get-features-done/workflows/convert-from-gsd.md`, 642 lines) and the command file (`commands/gfd/convert-from-gsd.md`). All three task commits exist in git history (ef291be, 6b5ae8e, e814a98). The feature is complete as a workflow/command tool.

Three items require human verification because they involve interactive CLI flows that cannot be verified programmatically, but the underlying implementation for all three is demonstrably present in the workflow.

---

_Verified: 2026-02-20T00:00:00Z_
_Verifier: Claude (gfd-verifier)_
