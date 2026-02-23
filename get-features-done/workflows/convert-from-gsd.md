<purpose>
Migrate a GSD `.planning/` directory to GFD `docs/features/` structure. Scans phases, maps to slugs, presents mappings for user review, then executes migration (files, frontmatter, cleanup).
</purpose>

<required_reading>
Read all files referenced by the invoking prompt's execution_context before starting.

@$HOME/.claude/get-features-done/references/ui-brand.md
</required_reading>

<process>

## 1. Verify .planning/ Exists

Check for `.planning/` directory using Bash:

```bash
[ -d ".planning" ] && echo "EXISTS" || echo "MISSING"
```

If missing, display error and exit:

```
╔══════════════════════════════════════════════════════════════╗
║  ERROR                                                       ║
╚══════════════════════════════════════════════════════════════╝

No GSD project found. .planning/ directory does not exist.

**To fix:** This command is for projects managed by GSD.
           If you have a GFD project already, run /gfd:status instead.
```

Exit.

Also check if `docs/features/` already exists:

```bash
[ -d "docs/features" ] && echo "EXISTS" || echo "MISSING"
```

If `docs/features/` exists, use AskUserQuestion:
- header: "GFD Project Exists"
- question: "docs/features/ already exists. This migration will ADD new features alongside existing ones. Continue?"
- options:
  - "Continue — add migrated features" — Proceed to next step
  - "Cancel" — Abort migration

If "Cancel": exit.

## 2. Display Banner

Output to user:

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► CONVERT FROM GSD
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Scanning .planning/ for GSD phases...
```

## 3. Scan Phases and Build Mapping

Scan `.planning/phases/` and `.planning/milestones/` for phase directories. Detect status from disk artifacts and extract goals, criteria, and dependencies from ROADMAP.md.

```bash
MAPPING_JSON=$(gfd-tools convert-gsd scan)
```

This outputs a JSON array. Each entry has: `phaseDir`, `dirPath`, `phaseNum`, `phaseName`, `suggestedSlug`, `gfdStatus`, `goal`, `criteria`, `dependsOnPhaseNums`, `archived`.

## 4. Display Mapping Table

Parse `MAPPING_JSON` (JSON array) and display the mapping table to the user:

```
## Suggested Feature Mappings

Found [N] GSD phases.

| # | GSD Phase | Suggested Slug | Status | Notes |
|---|-----------|----------------|--------|-------|
| 1 | 01-foundation-save-system | foundation-save-system | done | |
| 2 | 02-hero-system | hero-system | done | |
| ... | ... | ... | ... | |
| N | 28-death-loot-retrieval | death-loot-retrieval | in-progress | archived |
```

Archived phases show "archived" in the Notes column.

## 5. Interactive Mapping Review

Walk the user through each mapping one at a time. For each phase:

Use AskUserQuestion:
- header: "Phase [phaseNum]: [phaseName]"
- question: "Suggested slug: `[suggestedSlug]` (status: [gfdStatus]). What would you like to do?"
- options:
  - "Accept — use `[suggestedSlug]`"
  - "Rename — I'll provide a different slug"
  - "Skip — don't migrate this phase"

If "Rename": Ask the user (freeform): "Enter the new slug for `[phaseName]` (lowercase, hyphens only):"

Validate slug format: must match `^[a-z0-9]+(-[a-z0-9]+)*$`. If invalid, re-ask with the error:

```
Invalid slug format. Use only lowercase letters, numbers, and hyphens (e.g., hero-system).
```

Build `ACCEPTED_MAPPINGS` array from user choices — include only accepted or renamed entries. Track skipped phases separately in `SKIPPED_PHASES`.

**After all phases reviewed, show confirmation summary:**

```
## Migration Plan

The following [N] phases will be migrated:

| GSD Phase | Feature Slug | Status |
|-----------|--------------|--------|
| 01-foundation-save-system | foundation-save-system | done |
| 03-adventure-system | adventure-system | in-progress |
...

Skipped: [comma-separated list of skipped phase names, or "none"]
```

**Dependency warning check:** For each accepted mapping, look at its `dependsOnPhaseNums`. For each dependency phase number, find the corresponding phase in the full mapping list. If that phase was skipped (in `SKIPPED_PHASES`), warn:

```
Warning: `[slug]` depends on `[skipped-slug]` which was skipped.
         The depends_on field will need manual correction after migration.
```

Use AskUserQuestion:
- header: "Confirm Migration"
- question: "Ready to migrate [N] features?"
- options:
  - "Proceed — migrate now"
  - "Go back — review mappings again"
  - "Cancel — abort"

If "Go back": return to the start of Step 5 (re-run interactive review with a fresh pass through all phases).
If "Cancel": exit.

Store final `ACCEPTED_MAPPINGS` as a variable (JSON array) for use in the migration execution steps.

## 6. Initialize GFD Structure (if needed)

If `docs/features/` does not exist, create it and initialize project files:

```bash
mkdir -p docs/features
```

If `.planning/PROJECT.md` exists and `docs/features/PROJECT.md` does not exist:
- Copy `.planning/PROJECT.md` to `docs/features/PROJECT.md`
- Prepend a note at the top: `> Migrated from GSD. Review and update to match GFD format.`

If `docs/features/config.json` does not exist:
- Copy from `get-features-done/templates/config.json` as the default config.

---

## 7. Create Features and Migrate Artifacts

For each entry in `ACCEPTED_MAPPINGS`, create the feature directory, write FEATURE.md, copy and rename GSD artifact files, update frontmatter (`feature:` field), and update the Tasks section with plan file links.

**File rename rules (strip the phase numeric prefix):**
- `NN-MM-PLAN.md` → `MM-PLAN.md`
- `NN-MM-SUMMARY.md` → `MM-SUMMARY.md`
- `NN-RESEARCH.md` → `RESEARCH.md`
- `NN-VERIFICATION.md` → `VERIFICATION.md`
- `NN-CONTEXT.md` → `CONTEXT.md`
- `NN-USER-SETUP.md` → `USER-SETUP.md`
- Any other files: copy as-is

```bash
echo "$ACCEPTED_MAPPINGS" | gfd-tools convert-gsd execute
```

After this step, display:
```
Created [N] feature directories.
```

Note: `frontmatter merge` adds the `feature:` field. The legacy `phase:` field will remain but is harmless — GFD tools read `feature:` and ignore unknown fields.

---

## 8. Migrate Research Directory

If `.planning/research/` exists, copy all its files to `docs/features/research/`:

```bash
if [ -d ".planning/research" ]; then
  mkdir -p docs/features/research
  cp -r .planning/research/. docs/features/research/
  echo "Migrated .planning/research/ to docs/features/research/"
fi
```

---

## 9. Verify Migration Completeness

Before deleting `.planning/`, verify that all expected feature directories and FEATURE.md files were created:

```bash
echo "$ACCEPTED_MAPPINGS" | gfd-tools convert-gsd verify
```

If `all_present=false` in the output, display the missing files and do NOT delete `.planning/`. Otherwise, continue.

---

## 10. Delete .planning/

Only reached if verification passed in Step 10:

```bash
rm -rf .planning/
echo "Deleted .planning/"
```

---

## 11. Commit and Done

Commit all new feature files:

```bash
# Gather all new feature files and commit
FEATURE_FILES=$(find docs/features -name "*.md" -mmin -10 2>/dev/null | head -100)
git add $FEATURE_FILES && git diff --cached --quiet || git commit -m "docs(gfd): migrate from GSD"
```

Display completion banner:

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► MIGRATION COMPLETE ✓
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Migrated [N] GSD phases to GFD features.

| Status | Count |
|--------|-------|
| done   | [N]   |
| in-progress | [N] |
| planned | [N] |
| new     | [N] |

Location: docs/features/
.planning/ has been removed.
```

Show next steps:

```
───────────────────────────────────────────────────────────────

## ▶ Next Up

**Review your features** — check what was migrated

`/gfd:status`

───────────────────────────────────────────────────────────────

**Also available:**
- `/gfd:map-codebase` — analyze your codebase (recommended for brownfield projects)
- `/gfd:execute-feature <slug>` — continue work on an in-progress feature
- `/gfd:plan-feature <slug>` — plan a feature that needs planning

───────────────────────────────────────────────────────────────
```

</process>

<output>
- docs/features/<slug>/FEATURE.md for each accepted phase
- docs/features/<slug>/*.md for all migrated GSD artifacts
- docs/features/research/ (if .planning/research/ existed)
- docs/features/PROJECT.md (if not already present)
- .planning/ is deleted
</output>

<success_criteria>
- [ ] .planning/ found and verified before any writes
- [ ] All phase directories discovered (active + archived)
- [ ] Mapping table presented before any files created
- [ ] User reviewed every phase individually
- [ ] FEATURE.md created for each accepted phase
- [ ] GSD artifacts copied with correct renamed filenames
- [ ] Plan and summary frontmatter updated: phase: → feature:
- [ ] Research directory migrated (if present)
- [ ] All feature dirs verified before .planning/ deletion
- [ ] .planning/ deleted
- [ ] Commit created
</success_criteria>
