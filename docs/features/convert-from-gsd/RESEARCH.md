# Feature: Convert From GSD — Research

**Researched:** 2026-02-20
**Domain:** GSD `.planning/` directory structure, GFD feature structure, file migration, interactive CLI workflow
**Confidence:** HIGH

## Summary

This feature converts a project managed by GSD (Get Shit Done) from the `.planning/` phase-based structure into GFD's `docs/features/<slug>/` feature-based structure. Both systems are fully understood — GSD exists at `~/.claude/get-shit-done/` and both gfd-tools.cjs and gsd-tools.cjs are available and their data models are fully analyzed.

The core mapping is: GSD "phase" (e.g., `01-foundation-save-system`) becomes a GFD "feature" (e.g., `foundation-save-system`). Each GSD phase directory contains a predictable set of artifacts that map directly to GFD feature artifacts. The conversion is a one-shot migration: scan, present mappings, confirm with user, execute, and remove `.planning/`.

The command follows GFD's standard `command → workflow` pattern. It needs no new gfd-tools.cjs subcommand — all needed operations are existing filesystem operations (mkdir, file copy, file write, rm -rf). The workflow is interactive and must handle the most common GSD edge cases: archived milestone phases, decimal insertion phases (e.g., `2.1`), phases with no plans (early/future phases), and the research directory at `.planning/research/`.

**Primary recommendation:** Implement as `commands/gfd/convert-from-gsd.md` + `get-features-done/workflows/convert-from-gsd.md`. No gfd-tools.cjs changes needed. Pure filesystem + string manipulation in the workflow itself using Node.js one-liners via Bash.

## GSD `.planning/` Directory Structure

Source: direct inspection of a live GSD project's `.planning/` directory (HIGH confidence — ground truth).

### Top-Level Files

| File | Purpose | Migration Target |
|------|---------|-----------------|
| `ROADMAP.md` | Phase listing with milestone groupings, plan counts, status | Used to determine phase status and milestone groupings |
| `PROJECT.md` | Project context, core value, requirements, decisions | `docs/features/PROJECT.md` (if GFD project doesn't exist yet) |
| `STATE.md` | Current execution position, session log | `docs/features/STATE.md` (if GFD project doesn't exist yet) |
| `REQUIREMENTS.md` | Tracked requirements with IDs | `docs/features/REQUIREMENTS.md` |
| `MILESTONES.md` | Milestone archive with shipping dates | Informational only — used to populate FEATURE.md metadata |
| `config.json` | GSD config (model_profile, commit_docs, etc.) | Read-only — not migrated |
| `INFRASTRUCTURE.md` | Any project-specific infrastructure docs | Migrate to `docs/features/codebase/` or drop |

### Phase Directory Structure

Phase directories follow the pattern: `.planning/phases/NN-phase-name/`

Where `NN` is zero-padded integer (e.g., `01`, `02`) or decimal (e.g., `2.1`).

**Files inside a phase directory:**

| File Pattern | Purpose | GFD Migration Target |
|-------------|---------|---------------------|
| `NN-NN-PLAN.md` | Plan files (numbered per phase) | `docs/features/<slug>/NN-PLAN.md` |
| `NN-NN-SUMMARY.md` | Execution summaries | `docs/features/<slug>/NN-SUMMARY.md` |
| `NN-CONTEXT.md` | Phase context/background | `docs/features/<slug>/CONTEXT.md` |
| `NN-RESEARCH.md` | Phase research | `docs/features/<slug>/RESEARCH.md` |
| `NN-VERIFICATION.md` | Verification report | `docs/features/<slug>/VERIFICATION.md` |
| `NN-USER-SETUP.md` | Manual setup instructions | `docs/features/<slug>/USER-SETUP.md` |

**Plan file frontmatter fields** (from `01-01-PLAN.md` inspection):
```yaml
phase: 01-foundation-save-system
plan: 01
type: execute
wave: 1
depends_on: []
files_modified: [...]
autonomous: true
must_haves:
  truths: [...]
  artifacts: [...]
  key_links: [...]
```

**Summary file frontmatter fields:**
```yaml
phase: 01-foundation-save-system
plan: 01
subsystem: [...]
tags: [...]
requires: [...]
provides: [...]
affects: [...]
tech-stack: {added: [...], patterns: [...]}
key-files: {created: [...], modified: [...]}
key-decisions: [...]
patterns-established: [...]
requirements-completed: []
duration: Xmin
completed: YYYY-MM-DD
```

### Research Directory

`.planning/research/` contains project-level research files (not per-phase):
- `SUMMARY.md`, `STACK.md`, `FEATURES.md`, `ARCHITECTURE.md`, `PITFALLS.md` — standard research output
- Additional ad-hoc files (e.g., `unity6-webgpu.md`, `idle-game-ux.md`)

**Migration target:** These go to `docs/features/research/` (not per-feature).

### Milestones Directory

`.planning/milestones/` contains archived milestone files:
- `vX.Y-ROADMAP.md` — archived roadmap for completed milestone
- `vX.Y-REQUIREMENTS.md` — archived requirements

These are informational. The current `.planning/ROADMAP.md` is the authoritative source.

### GSD Phase Status Values

From ROADMAP.md analysis (sample GSD project, 28 phases inspected):

| ROADMAP indicator | Disk artifacts | Meaning | GFD Status |
|------------------|---------------|---------|------------|
| `- [x] Phase N:` (in completed milestone) | All plans have summaries + VERIFICATION.md | All plans complete | `done` |
| `- [x] Phase N:` with "completed YYYY-MM-DD" | Same | Complete | `done` |
| Phase in progress | Some SUMMARY.md files but not all | Active | `in-progress` |
| Has PLANs but no SUMMARYs | PLAN.md files present | Planned | `planned` |
| No PLANs, has RESEARCH.md | RESEARCH.md in phase dir | Researched | `researched` |
| No PLANs, has CONTEXT.md or ROADMAP Goal | CONTEXT.md or Goal in ROADMAP.md | Discussed | `discussed` |
| No PLANs, no research, no context, no goal | Empty phase dir | Not started | `new` |

**Status detection algorithm:** Check disk artifacts (plan/summary/research/context file presence) for the most reliable status. ROADMAP.md Goal presence indicates the phase was discussed. ROADMAP.md checkbox state corroborates but disk is ground truth.

## GFD Target Structure

Source: direct inspection of `./` (HIGH confidence — ground truth).

### FEATURE.md Frontmatter

```yaml
name: [Human-readable name]
slug: [feature-slug]
status: new|discussing|discussed|researching|researched|planning|planned|in-progress|done
owner: [username]
assignees: []
created: YYYY-MM-DD
priority: medium
depends_on: []
```

### FEATURE.md Body Sections

Required sections:
- `## Description` — 2-3 sentences
- `## Acceptance Criteria` — list of checkboxes
- `## Tasks` — populated later
- `## Notes` — design decisions, constraints

### GFD Plan File Naming Convention

GSD uses `NN-NN-PLAN.md` (phase-plan). GFD uses `NN-PLAN.md` (plan only within feature dir).

**Rename required:** `01-02-PLAN.md` → `02-PLAN.md` (strip the phase prefix).

Same for SUMMARY: `01-02-SUMMARY.md` → `02-SUMMARY.md`.

Plan frontmatter requires updating: remove `phase` field; set `feature` field.

## Architecture Patterns

### Command + Workflow Pattern (from existing GFD commands)

**Command file** (`commands/gfd/convert-from-gsd.md`):
```markdown
---
name: gfd:convert-from-gsd
description: Migrate a GSD .planning/ project to GFD docs/features/ structure
argument-hint: (no arguments)
allowed-tools: Read, Write, Edit, Bash, Grep, Glob, AskUserQuestion
---

<objective>Migrate GSD .planning/ directory to GFD docs/features/ structure.</objective>

<execution_context>
@$HOME/.claude/get-features-done/workflows/convert-from-gsd.md
@$HOME/.claude/get-features-done/references/ui-brand.md
</execution_context>

<process>Execute the convert-from-gsd workflow.</process>
```

**Workflow file** (`get-features-done/workflows/convert-from-gsd.md`): full step-by-step process.

### Workflow Step Sequence

```
1. Verify .planning/ exists
2. Scan .planning/phases/ — discover all phase directories
3. Read ROADMAP.md — extract phase names, goals, status, plan counts
4. Cross-reference disk — count plans/summaries per phase, determine actual status
5. Display mapping table — show suggested GFD slugs and statuses
6. Interactive review — user accepts/renames/skips each mapping
7. Confirm — show final mapping before executing
8. Create docs/features/ structure if needed
9. For each accepted mapping: create feature dir, write FEATURE.md, copy artifacts
10. Migrate .planning/research/ → docs/features/research/ (if exists)
11. Delete .planning/
12. Commit
```

### Phase → Feature Mapping Algorithm

**Slug generation from phase directory name:**
- Input: `01-foundation-save-system`
- Strip numeric prefix: `foundation-save-system`
- Already valid GFD slug format (lowercase, hyphens)

**Human-readable name from slug:**
- `foundation-save-system` → "Foundation Save System"
- Title-case each segment

**Suggested via gfd-tools generate-slug:**
```bash
node $HOME/.claude/get-features-done/bin/gfd-tools.cjs generate-slug "Foundation Save System" --raw
# → foundation-save-system
```

### Reading ROADMAP.md for Phase Goals

ROADMAP.md Phase Details sections contain:
```
### Phase 1: Foundation
**Goal**: [What this phase delivers]
**Depends on**: [Nothing | Phase N]
**Requirements**: [REQ-01, REQ-02]
**Success Criteria**:
  1. [Observable behavior]
  2. [Observable behavior]
```

Extract these sections with regex. The `**Goal**` value becomes FEATURE.md `## Description`. `**Success Criteria**` items become `## Acceptance Criteria` checkboxes.

### Determining Depends_on

GSD phases have numeric dependencies (Phase 3 depends on Phase 2). In GFD, these become slug-to-slug dependencies.

Algorithm:
1. Extract `**Depends on**: Phase N` from ROADMAP.md
2. Look up which feature slug phase N maps to
3. Set `depends_on: [slug]` in target FEATURE.md

### Status Detection Per Phase

```javascript
function detectPhaseStatus(phaseDir, hasGoal) {
  const files = fs.readdirSync(phaseDir);
  const planFiles = files.filter(f => /-PLAN\.md$/.test(f));
  const summaryFiles = files.filter(f => /-SUMMARY\.md$/.test(f));
  const hasVerification = files.some(f => /VERIFICATION\.md$/.test(f));
  const hasResearch = files.some(f => /RESEARCH\.md$/.test(f));
  const hasContext = files.some(f => /CONTEXT\.md$/.test(f));

  if (planFiles.length === summaryFiles.length && planFiles.length > 0 && hasVerification) return 'done';
  if (summaryFiles.length > 0) return 'in-progress';
  if (planFiles.length > 0) return 'planned';
  if (hasResearch) return 'researched';
  if (hasContext || hasGoal) return 'discussed';
  return 'new';
}
```

### Artifact Migration

**File rename rules:**
| GSD filename | GFD filename |
|-------------|-------------|
| `NN-MM-PLAN.md` | `MM-PLAN.md` |
| `NN-MM-SUMMARY.md` | `MM-SUMMARY.md` |
| `NN-RESEARCH.md` | `RESEARCH.md` |
| `NN-VERIFICATION.md` | `VERIFICATION.md` |
| `NN-CONTEXT.md` | `CONTEXT.md` |
| `NN-USER-SETUP.md` | `USER-SETUP.md` |

**Plan frontmatter update on migration:**
- Change `phase: NN-name` → `feature: slug`
- Keep all other fields (type, wave, depends_on, files_modified, autonomous, must_haves)

**Summary frontmatter update:**
- Change `phase: NN-name` → `feature: slug`
- Keep all other fields

### FEATURE.md Generation Template

```markdown
---
name: [Human-readable name]
slug: [slug]
status: [detected-status]
owner: [git user.name from ROADMAP.md or git config]
assignees: []
created: [phase completion date from ROADMAP.md or today]
priority: medium
depends_on: [computed slug dependencies]
gsd_phase: [NN-phase-name]
---
# [Human-readable name]

## Description

[**Goal** value from ROADMAP.md Phase Details section]

## Acceptance Criteria

[**Success Criteria** items from ROADMAP.md, each as `- [x]` if done or `- [ ]` if not]

## Tasks

[If has plans: list of migrated plan file links]
[If no plans: "Populated during planning. Links to plan files."]

## Notes

- Migrated from GSD phase: `[NN-phase-name]`
- Original milestone: [milestone name from ROADMAP.md, e.g., "v0.0 MVP"]
- GSD Requirements: [requirement IDs from ROADMAP.md if present]

---
*Created: [today's date]*
*Last updated: [today's date]*
```

Note: `gsd_phase` is a non-standard frontmatter field for traceability. The GFD workflow and tools will not complain about unknown fields — they use `extractFrontmatter` which is additive.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| YAML frontmatter parsing | Custom regex parser | gfd-tools.cjs `frontmatter get/set/merge` subcommands | Already handles all GSD and GFD frontmatter formats correctly |
| Slug generation | String manipulation inline | `gfd-tools.cjs generate-slug` | Consistent, tested |
| Git commits | Inline `git add && git commit` | `gfd-tools.cjs commit` | Handles commit_docs check, gitignore detection, nothing-to-commit gracefully |
| Phase status | Parsing ROADMAP.md checkbox state | Disk-based detection (plan/summary count) | ROADMAP.md can be stale; disk is ground truth |

**Key insight:** The migration logic is straightforward file operations plus string extraction from ROADMAP.md. The main value is the interactive review step — don't over-engineer the automated parts.

## Common Pitfalls

### Pitfall 1: Decimal Phases (Insertion Phases)
**What goes wrong:** Phase `2.1` (an insertion phase, e.g., `02.1-critical-fix`) doesn't fit the simple `strip prefix` slug algorithm.
**Why it happens:** GSD supports decimal phases for urgent insertions between milestones.
**How to avoid:** Normalize decimal phases: `02.1-critical-fix` → `critical-fix` (strip the `NN.N` prefix). If the slug would collide with an existing slug (unlikely but possible), append a suffix.
**Warning signs:** Phase directories with `.` in the numeric prefix.

### Pitfall 2: Archived Phases in Milestones Directory
**What goes wrong:** Checking only `.planning/phases/` misses phases that were archived to `.planning/milestones/vX.Y-phases/`.
**Why it happens:** The `complete-milestone` command with `--archive-phases` moves phase dirs out of `.planning/phases/` into `.planning/milestones/vX.Y-phases/`.
**How to avoid:** Also check `.planning/milestones/` for archived phase directories. Most GSD projects don't use `--archive-phases` (phases remain in `phases/` despite milestones), but the feature should handle it. Check both directories.
**Warning signs:** Large gap in phase numbers in `phases/` dir (e.g., phases 7-13 missing, milestones dir has a `vX.Y-phases/` subdirectory).

### Pitfall 3: Plan Frontmatter Has Phase-Scoped Plan Numbers
**What goes wrong:** GSD plan files use `plan: 01` within `phase 03`, so multiple phases all have `plan: 01`. After migration, `feature: user-auth` has plan `01` and `feature: hero-system` also has plan `01` — this is correct GFD behavior but the frontmatter field `phase` must be renamed to `feature`.
**Why it happens:** GFD and GSD both use `plan: NN` but GFD keys off `feature:` not `phase:`.
**How to avoid:** When copying plan and summary files, update frontmatter: rename `phase:` → `feature:` (set to the new slug). Use `gfd-tools.cjs frontmatter merge`.
**Warning signs:** Plans with `phase:` field after migration — gfd-tools verify commands don't check for this but other workflows expect `feature:`.

### Pitfall 4: ROADMAP.md Goals May Be Absent for Future Phases
**What goes wrong:** Phases in the "planned" section of ROADMAP.md may have minimal or no `**Goal**` and `**Success Criteria**` content.
**Why it happens:** GSD roadmaps are progressively detailed — future phases are rough sketches.
**How to avoid:** When no `**Goal**` is found, fall back to the phase name as description. When no `**Success Criteria**` found, generate a placeholder: `- [ ] [Phase goal not yet defined — update before planning]`.
**Warning signs:** Acceptance criteria that are just the phase name.

### Pitfall 5: Research Directory Needs Special Handling
**What goes wrong:** `.planning/research/` contains both standard GSD research files (SUMMARY.md, STACK.md, etc.) and ad-hoc files (e.g., `idle-game-ux.md`). A naïve migration copies all to `docs/features/research/`.
**Why it happens:** GSD research output mirrors GFD research output structure (both use SUMMARY.md, STACK.md, etc.).
**How to avoid:** Copy all files. The GFD `docs/features/research/` directory is the correct location. This is a straightforward copy, not a transformation.
**Warning signs:** None — this case works cleanly.

### Pitfall 6: .planning/ Deletion Must Be Last
**What goes wrong:** Deleting `.planning/` before confirming all feature directories were successfully created.
**Why it happens:** User expects cleanup to be atomic.
**How to avoid:** Use a two-phase approach: (1) create all GFD artifacts, (2) verify all expected files exist, (3) only then delete `.planning/`. If verification fails, stop and report what's missing.
**Warning signs:** Partial migration state where some features exist but not all.

### Pitfall 7: Skipped Phases Leave Dependency Chains Broken
**What goes wrong:** User skips phase `03-adventure-system` but phase `04-combat-system` depends on it. The resulting `04-combat-system` FEATURE.md has `depends_on: [adventure-system]` which is a slug that doesn't exist.
**Why it happens:** User skips some phases as too granular or outdated.
**How to avoid:** Detect missing dependency slugs during the confirm step and warn the user. Don't block migration — just warn: "Feature `combat-system` depends on `adventure-system` which was skipped. You'll need to update depends_on manually."

## Code Examples

Verified patterns from existing codebase inspection:

### Scanning Phase Directories

```bash
# Source: direct inspection of a sample GSD .planning/ structure
ls /path/to/project/.planning/phases/
# Output: 01-foundation-save-system  02-hero-system  03-adventure-system ...

# Node.js equivalent in workflow bash block:
node -e "
const fs = require('fs');
const path = require('path');
const phasesDir = '.planning/phases';
if (fs.existsSync(phasesDir)) {
  const dirs = fs.readdirSync(phasesDir, {withFileTypes: true})
    .filter(e => e.isDirectory())
    .map(e => ({
      dirName: e.name,
      phaseNum: e.name.match(/^(\d+(?:\.\d+)?)/)?.[1],
      phaseName: e.name.replace(/^\d+(?:\.\d+)?-/, ''),
    }))
    .sort((a, b) => parseFloat(a.phaseNum) - parseFloat(b.phaseNum));
  console.log(JSON.stringify(dirs));
}
"
```

### Extracting Phase Info from ROADMAP.md

```bash
# Extract goal and success criteria for a phase
node -e "
const fs = require('fs');
const roadmap = fs.readFileSync('.planning/ROADMAP.md', 'utf-8');
const phaseNum = '23';

// Find phase section by number
const phasePattern = new RegExp(
  '###\\\\s+(?:Phase\\\\s+)?' + phaseNum + ':\\\\s+([^\\\\n]+)([\\\\s\\\\S]*?)(?=\\\\n###|\\\\n##|$)'
);
const match = roadmap.match(phasePattern);
if (match) {
  const phaseTitle = match[1].trim();
  const body = match[2];
  const goalMatch = body.match(/\*\*Goal\*\*:\\s*(.+)/);
  const criteriaMatches = [...body.matchAll(/^\\s+\\d+\\.\\s+(.+)\$/gm)];
  console.log(JSON.stringify({
    title: phaseTitle,
    goal: goalMatch?.[1]?.trim(),
    criteria: criteriaMatches.map(m => m[1].trim()),
  }));
}
"
```

### Updating Plan Frontmatter on Migration

```bash
# Source: gfd-tools.cjs frontmatter merge pattern
node $HOME/.claude/get-features-done/bin/gfd-tools.cjs \
  frontmatter merge "docs/features/foundation-save-system/01-PLAN.md" \
  --data '{"feature": "foundation-save-system"}'
# This replaces the phase field or adds feature field — does not remove phase field
# Must follow with frontmatter set to remove phase field if needed
```

### Detecting Phase Status from Disk

```bash
node -e "
const fs = require('fs');
const dir = '.planning/phases/01-foundation-save-system';
const files = fs.readdirSync(dir);
const plans = files.filter(f => /-PLAN\.md$/.test(f));
const summaries = files.filter(f => /-SUMMARY\.md$/.test(f));
const hasVerification = files.some(f => /VERIFICATION\.md$/.test(f));

const hasResearch = files.some(f => /RESEARCH\.md$/.test(f));
const hasContext = files.some(f => /CONTEXT\.md$/.test(f));

let status = 'new';
if (plans.length > 0 && plans.length === summaries.length && hasVerification) status = 'done';
else if (summaries.length > 0) status = 'in-progress';
else if (plans.length > 0) status = 'planned';
else if (hasResearch) status = 'researched';
else if (hasContext) status = 'discussed';

console.log(JSON.stringify({status, plans: plans.length, summaries: summaries.length, hasVerification, hasResearch, hasContext}));
"
```

### Renaming Plan Files

```bash
# GSD: 01-02-PLAN.md → GFD: 02-PLAN.md
# Strip the NN- prefix from the phase number, keep the plan number
# Input pattern: {phase_num_padded}-{plan_num}-PLAN.md
# Output pattern: {plan_num}-PLAN.md

node -e "
const files = ['01-01-PLAN.md', '01-02-PLAN.md', '01-03-PLAN.md'];
const phasePrefix = '01';
for (const f of files) {
  // Remove phase prefix: '01-02-PLAN.md' -> '02-PLAN.md'
  const renamed = f.replace(new RegExp('^' + phasePrefix + '-'), '');
  console.log(f + ' -> ' + renamed);
}
"
# Output:
# 01-01-PLAN.md -> 01-PLAN.md
# 01-02-PLAN.md -> 02-PLAN.md
# 01-03-PLAN.md -> 03-PLAN.md
```

### Interactive Mapping Display

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► CONVERT FROM GSD
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Found 28 GSD phases in .planning/phases/

## Suggested Feature Mappings

| GSD Phase | Suggested Slug | GSD Status | GFD Status |
|-----------|---------------|------------|------------|
| 01-foundation-save-system | foundation-save-system | complete | done |
| 02-hero-system | hero-system | complete | done |
| ...
| 28-death-loot-retrieval | death-loot-retrieval | in progress | in-progress |
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| GSD phase-scoped planning | GFD feature-scoped planning | This migration | Phases become independently-addressable features |
| `.planning/phases/NN-name/` | `docs/features/<slug>/` | This migration | Named slugs instead of numbered phases |
| ROADMAP.md for phase ordering | `depends_on:` in FEATURE.md frontmatter | This migration | Explicit dependency graph vs implicit ordering |

## Open Questions

1. **Should PROJECT.md and STATE.md be migrated or created fresh?**
   - What we know: GSD has `PROJECT.md` and `STATE.md` in `.planning/`. GFD has different templates in `docs/features/`.
   - What's unclear: GSD PROJECT.md content structure is similar but not identical to GFD PROJECT.md. Should we copy as-is or transform?
   - Recommendation: If `docs/features/PROJECT.md` doesn't exist, copy GSD `PROJECT.md` as-is with a header note. User can refine later. If it already exists (GFD project already initialized), skip.

2. **How to handle the codebase analysis directory?**
   - What we know: GSD has `~/.claude/get-shit-done/templates/codebase/` with same templates as GFD. GFD maps to `docs/features/codebase/`.
   - What's unclear: GSD doesn't use `docs/features/codebase/` — it may have separate codebase docs elsewhere.
   - Recommendation: Don't migrate codebase docs. If user wants them, they should run `/gfd:map-codebase` post-migration.

3. **How to handle `.planning/milestones/vX.Y-phases/` (archived phase directories)?**
   - What we know: The `complete-milestone --archive-phases` flag moves phase dirs to milestones. Most projects don't use this flag.
   - What's unclear: How common is `--archive-phases` usage in practice? No real example to inspect.
   - Recommendation: Check for `milestones/*/phases/` subdirectories and offer to include archived phases in migration. Default to including them. Mark archived phases as `done`.

## Sources

### Primary (HIGH confidence)
- A live GSD project's `.planning/` directory — 28 phases inspected directly
- `./get-features-done/bin/gfd-tools.cjs` — GFD CLI source inspected
- `$HOME/.claude/get-shit-done/bin/gsd-tools.cjs` — GSD CLI source inspected
- `$HOME/.claude/get-shit-done/templates/summary.md` — GSD SUMMARY.md frontmatter schema
- `$HOME/.claude/get-shit-done/templates/roadmap.md` — GSD ROADMAP.md format
- `./get-features-done/templates/feature.md` — GFD FEATURE.md template
- `./docs/features/codebase/STRUCTURE.md` — GFD directory layout

### Secondary (MEDIUM confidence)
- `$HOME/.claude/get-shit-done/references/planning-config.md` — GSD config format and branching strategies

## Metadata

**Confidence breakdown:**
- GSD directory structure: HIGH — inspected live GSD project with 28 phases
- GFD target structure: HIGH — inspected GFD codebase and templates directly
- Mapping algorithm: HIGH — both formats fully understood, no ambiguity
- Pitfalls: HIGH (for listed ones) — derived from actual GSD project inspection
- Archived phases pitfall: MEDIUM — no live example of `--archive-phases` usage found

**Research date:** 2026-02-20
**Valid until:** 2026-04-20 (stable — both GSD and GFD are internal tools under active development, but structure unlikely to change dramatically)
