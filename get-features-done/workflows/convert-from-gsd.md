<purpose>
Migrate a GSD `.planning/` directory to GFD `docs/features/` structure. Scans phases, maps to slugs, presents mappings for user review, then executes migration (files, frontmatter, cleanup).
</purpose>

<required_reading>
Read all files referenced by the invoking prompt's execution_context before starting.

@/home/conroy/.claude/get-features-done/references/ui-brand.md
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
           If you have a GFD project already, run /gfd:progress instead.
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

## 3. Scan Phase Directories

Scan both `.planning/phases/` and `.planning/milestones/` (for archived phases). Use Node.js via Bash. Collect all phase directories into a JSON array sorted by phase number.

```bash
PHASES_JSON=$(node -e "
const fs = require('fs');
const path = require('path');

function scanPhaseDirs(dir, archived) {
  if (!fs.existsSync(dir)) return [];
  return fs.readdirSync(dir, {withFileTypes: true})
    .filter(e => e.isDirectory() && /^\d/.test(e.name))
    .map(e => ({
      dirName: e.name,
      dirPath: path.join(dir, e.name),
      phaseNum: parseFloat(e.name.match(/^(\d+(?:\.\d+)?)/)?.[1] || '0'),
      phaseName: e.name.replace(/^\d+(?:\.\d+)?-/, ''),
      archived: archived,
    }));
}

function scanMilestonePhaseDirs() {
  const milestonesDir = '.planning/milestones';
  if (!fs.existsSync(milestonesDir)) return [];
  const results = [];
  for (const entry of fs.readdirSync(milestonesDir, {withFileTypes: true})) {
    if (entry.isDirectory()) {
      // Check for archived phases in milestones/vX.Y-phases/ subdirectory
      const phasesSubdir = path.join(milestonesDir, entry.name, 'phases');
      results.push(...scanPhaseDirs(phasesSubdir, true));
    }
  }
  return results;
}

const activePhases = scanPhaseDirs('.planning/phases', false);
const archivedPhases = scanMilestonePhaseDirs();
const allPhases = [...activePhases, ...archivedPhases]
  .sort((a, b) => a.phaseNum - b.phaseNum);

console.log(JSON.stringify(allPhases));
")
```

## 4. Determine Status and Build Mapping Table

For each discovered phase directory, compute the GFD status from disk artifacts (plan/summary counts, verification, research, context files). Extract goal and success criteria from ROADMAP.md for use later in FEATURE.md generation.

```bash
MAPPING_JSON=$(node -e "
const fs = require('fs');
const path = require('path');
const phases = $(echo \$PHASES_JSON);

const roadmap = fs.existsSync('.planning/ROADMAP.md')
  ? fs.readFileSync('.planning/ROADMAP.md', 'utf-8') : '';

function detectStatus(phaseDir, archived, hasGoal) {
  if (archived) return 'done'; // archived phases are complete by definition
  if (!fs.existsSync(phaseDir)) return hasGoal ? 'discussed' : 'new';
  const files = fs.readdirSync(phaseDir);
  const plans = files.filter(f => /-PLAN\.md\$/.test(f));
  const summaries = files.filter(f => /-SUMMARY\.md\$/.test(f));
  const hasVerification = files.some(f => /VERIFICATION\.md\$/.test(f));
  const hasResearch = files.some(f => /RESEARCH\.md\$/.test(f));
  const hasContext = files.some(f => /CONTEXT\.md\$/.test(f));
  if (plans.length === summaries.length && plans.length > 0 && hasVerification) return 'done';
  if (summaries.length > 0) return 'in-progress';
  if (plans.length > 0) return 'planned';
  if (hasResearch) return 'researched';
  if (hasContext || hasGoal) return 'discussed';
  return 'new';
}

function extractPhaseGoal(phaseNum) {
  const numStr = String(Math.floor(phaseNum));
  const sectionPattern = new RegExp(
    '###\\\\s+(?:Phase\\\\s+)?' + numStr + '[^\\\\d][\\\\s\\\\S]*?(?=\\\\n###|\\\\n##|\$)'
  );
  const sectionMatch = roadmap.match(sectionPattern);
  if (!sectionMatch) return '';
  const goalMatch = sectionMatch[0].match(/\*\*Goal\*\*:\\s*(.+)/);
  return goalMatch?.[1]?.trim() || '';
}

function extractSuccessCriteria(phaseNum) {
  const numStr = String(Math.floor(phaseNum));
  const sectionPattern = new RegExp(
    '###\\\\s+(?:Phase\\\\s+)?' + numStr + '[^\\\\d][\\\\s\\\\S]*?(?=\\\\n###|\\\\n##|\$)'
  );
  const sectionMatch = roadmap.match(sectionPattern);
  if (!sectionMatch) return [];
  const criteriaMatches = [...sectionMatch[0].matchAll(/^\\s+\\d+\\.\\s+(.+)\$/gm)];
  return criteriaMatches.map(m => m[1].trim());
}

function extractDependsOn(phaseNum) {
  const numStr = String(Math.floor(phaseNum));
  const sectionPattern = new RegExp(
    '###\\\\s+(?:Phase\\\\s+)?' + numStr + '[^\\\\d][\\\\s\\\\S]*?(?=\\\\n###|\\\\n##|\$)'
  );
  const sectionMatch = roadmap.match(sectionPattern);
  if (!sectionMatch) return [];
  const depMatch = sectionMatch[0].match(/\*\*Depends on\*\*:\\s*(.+)/);
  if (!depMatch) return [];
  // Extract phase numbers from the depends-on line
  const depNums = [...depMatch[1].matchAll(/Phase\\s+(\\d+(?:\\.\\d+)?)/gi)].map(m => parseFloat(m[1]));
  return depNums;
}

const mapping = phases.map(p => {
  // Slug: strip numeric prefix (handles decimals like 2.1)
  const slug = p.phaseName
    .toLowerCase()
    .replace(/[^a-z0-9-]/g, '-')
    .replace(/-+/g, '-')
    .replace(/^-|-\$/g, '');
  const goal = extractPhaseGoal(p.phaseNum);
  const status = detectStatus(p.dirPath, p.archived, !!goal);
  const criteria = extractSuccessCriteria(p.phaseNum);
  const dependsOnNums = extractDependsOn(p.phaseNum);
  return {
    phaseDir: p.dirName,
    dirPath: p.dirPath,
    phaseNum: p.phaseNum,
    phaseName: p.phaseName,
    suggestedSlug: slug,
    gfdStatus: status,
    goal: goal,
    criteria: criteria,
    dependsOnPhaseNums: dependsOnNums,
    archived: p.archived,
  };
});

console.log(JSON.stringify(mapping));
")
```

Parse `MAPPING_JSON` and display the mapping table to the user:

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

If `.planning/STATE.md` exists and `docs/features/STATE.md` does not exist:
- Copy `.planning/STATE.md` to `docs/features/STATE.md`

If `.planning/REQUIREMENTS.md` exists and `docs/features/REQUIREMENTS.md` does not exist:
- Copy `.planning/REQUIREMENTS.md` to `docs/features/REQUIREMENTS.md`

If `docs/features/config.json` does not exist:
- Copy from `get-features-done/templates/config.json` as the default config.

---

## 7. Create Feature Directories and FEATURE.md Files

For each entry in `ACCEPTED_MAPPINGS`, create the feature directory and write FEATURE.md:

```bash
node -e "
const fs = require('fs');
const path = require('path');
const mapping = JSON.parse(process.env.ACCEPTED_MAPPINGS);
const today = new Date().toISOString().split('T')[0];

for (const m of mapping) {
  const featureDir = path.join('docs/features', m.slug);
  fs.mkdirSync(featureDir, {recursive: true});

  // Build human-readable name from slug
  const name = m.slug.split('-').map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ');

  // Build depends_on: map GSD phase number dependencies to slugs
  // m.dependsOnSlugs is pre-computed from ROADMAP.md in step 4
  const dependsOn = m.dependsOnSlugs || [];

  // Build acceptance criteria: use ROADMAP.md success criteria if available
  const criteriaLines = m.criteria && m.criteria.length > 0
    ? m.criteria.map(c => \`- [\${m.gfdStatus === 'done' ? 'x' : ' '}] \${c}\`).join('\n')
    : \`- [ ] [Phase goal not yet defined — update before planning]\`;

  // Build tasks section
  let tasksSection = '[Populated during planning. Links to plan files.]';

  // Build description
  const description = m.goal
    || \`\${name} — migrated from GSD phase \${m.phaseDir}. Update description before planning.\`;

  const frontmatter = [
    '---',
    \`name: \${name}\`,
    \`slug: \${m.slug}\`,
    \`status: \${m.gfdStatus}\`,
    \`owner: \${process.env.GIT_USER || 'unassigned'}\`,
    'assignees: []',
    \`created: \${today}\`,
    'priority: medium',
    \`depends_on: \${JSON.stringify(dependsOn)}\`,
    \`gsd_phase: \${m.phaseDir}\`,
    '---',
  ].join('\n');

  const body = [
    \`# \${name}\`,
    '',
    '## Description',
    '',
    description,
    '',
    '## Acceptance Criteria',
    '',
    criteriaLines,
    '',
    '## Tasks',
    '',
    tasksSection,
    '',
    '## Notes',
    '',
    \`- Migrated from GSD phase: \\\`\${m.phaseDir}\\\`\`,
    \`- GSD status at migration: \${m.gfdStatus}\`,
    '',
    '---',
    \`*Created: \${today}\`,
    \`*Last updated: \${today}\`,
  ].join('\n');

  fs.writeFileSync(path.join(featureDir, 'FEATURE.md'), frontmatter + '\n' + body + '\n');
  console.log('Created: ' + path.join(featureDir, 'FEATURE.md'));
}
" ACCEPTED_MAPPINGS="$ACCEPTED_MAPPINGS" GIT_USER="$(git config user.name 2>/dev/null || echo unassigned)"
```

After this step, display:
```
Created [N] feature directories.
```

---

## 8. Migrate GSD Artifacts

For each entry in `ACCEPTED_MAPPINGS`, copy all files from the GSD phase directory to the GFD feature directory, applying rename rules and frontmatter updates.

**File rename rules (strip the phase numeric prefix):**
- `NN-MM-PLAN.md` → `MM-PLAN.md`
- `NN-MM-SUMMARY.md` → `MM-SUMMARY.md`
- `NN-RESEARCH.md` → `RESEARCH.md`
- `NN-VERIFICATION.md` → `VERIFICATION.md`
- `NN-CONTEXT.md` → `CONTEXT.md`
- `NN-USER-SETUP.md` → `USER-SETUP.md`
- Any other files: copy as-is

```bash
node -e "
const fs = require('fs');
const path = require('path');
const mapping = JSON.parse(process.env.ACCEPTED_MAPPINGS);

function renameGsdFile(filename, phaseNumStr) {
  // Strip phase numeric prefix from files like NN-MM-PLAN.md or NN-RESEARCH.md
  const planPattern = new RegExp('^' + phaseNumStr + '-([0-9]+-(?:PLAN|SUMMARY)\.md)$');
  const planMatch = filename.match(planPattern);
  if (planMatch) return planMatch[1];

  const singlePattern = new RegExp('^' + phaseNumStr + '-(.+\.md)$');
  const singleMatch = filename.match(singlePattern);
  if (singleMatch) return singleMatch[1].toUpperCase();

  return filename;
}

for (const m of mapping) {
  const srcDir = m.dirPath;
  const dstDir = path.join('docs/features', m.slug);
  if (!fs.existsSync(srcDir)) continue;

  const phaseNumStr = m.phaseDir.match(/^(\d+(?:\.\d+)?)/)?.[1] || '';
  const padded = phaseNumStr.replace('.', '-').padStart(2, '0');

  const files = fs.readdirSync(srcDir).filter(f => f.endsWith('.md'));
  for (const file of files) {
    const newName = renameGsdFile(file, padded);
    const src = path.join(srcDir, file);
    const dst = path.join(dstDir, newName);
    fs.copyFileSync(src, dst);
    console.log('Copied: ' + file + ' -> ' + newName);
  }
}
" ACCEPTED_MAPPINGS="$ACCEPTED_MAPPINGS"
```

**Update frontmatter in all migrated PLAN.md and SUMMARY.md files:**

After copying, update the `phase:` → `feature:` field in each migrated plan and summary file using gfd-tools.cjs:

```bash
node -e "
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');
const mapping = JSON.parse(process.env.ACCEPTED_MAPPINGS);

for (const m of mapping) {
  const featureDir = path.join('docs/features', m.slug);
  if (!fs.existsSync(featureDir)) continue;

  const files = fs.readdirSync(featureDir)
    .filter(f => f.endsWith('-PLAN.md') || f.endsWith('-SUMMARY.md'));

  for (const file of files) {
    const filePath = path.join(featureDir, file);
    try {
      execSync(
        \`node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs frontmatter merge \"\${filePath}\" --data '\${JSON.stringify({feature: m.slug})}'\`,
        {stdio: 'inherit'}
      );
      console.log('Updated frontmatter: ' + filePath);
    } catch(e) {
      console.error('Warning: frontmatter update failed for ' + filePath + ': ' + e.message);
    }
  }
}
" ACCEPTED_MAPPINGS="$ACCEPTED_MAPPINGS"
```

Note: `gfd-tools.cjs frontmatter merge` adds the `feature:` field. The legacy `phase:` field will remain but is harmless — GFD tools read `feature:` and ignore unknown fields.

**Update Tasks section in FEATURE.md for features that have plans:**

For each feature that has migrated plan files, update the Tasks section in FEATURE.md:

```bash
node -e "
const fs = require('fs');
const path = require('path');
const mapping = JSON.parse(process.env.ACCEPTED_MAPPINGS);

for (const m of mapping) {
  const featureDir = path.join('docs/features', m.slug);
  if (!fs.existsSync(featureDir)) continue;

  const planFiles = fs.readdirSync(featureDir).filter(f => /^\d+-PLAN\.md$/.test(f)).sort();
  if (planFiles.length === 0) continue;

  const taskLinks = planFiles.map(f => {
    const planNum = f.replace('-PLAN.md', '');
    return \`- [\${f}](\${f}) — Plan \${planNum}\`;
  }).join('\n');

  const featurePath = path.join(featureDir, 'FEATURE.md');
  let content = fs.readFileSync(featurePath, 'utf-8');
  content = content.replace(
    /## Tasks\n\n\[Populated during planning.*?\]/,
    \`## Tasks\n\n\${taskLinks}\`
  );
  fs.writeFileSync(featurePath, content);
  console.log('Updated Tasks in ' + featurePath);
}
" ACCEPTED_MAPPINGS="$ACCEPTED_MAPPINGS"
```

---

## 9. Migrate Research Directory

If `.planning/research/` exists, copy all its files to `docs/features/research/`:

```bash
if [ -d ".planning/research" ]; then
  mkdir -p docs/features/research
  cp -r .planning/research/. docs/features/research/
  echo "Migrated .planning/research/ to docs/features/research/"
fi
```

---

## 10. Verify Migration Completeness

Before deleting `.planning/`, verify that all expected feature directories and FEATURE.md files were created:

```bash
MISSING=$(node -e "
const fs = require('fs');
const path = require('path');
const mapping = JSON.parse(process.env.ACCEPTED_MAPPINGS);
const missing = [];
for (const m of mapping) {
  const featurePath = path.join('docs/features', m.slug, 'FEATURE.md');
  if (!fs.existsSync(featurePath)) {
    missing.push(featurePath);
  }
}
console.log(missing.join('\n'));
" ACCEPTED_MAPPINGS="$ACCEPTED_MAPPINGS")

if [ -n "$MISSING" ]; then
  echo ""
  echo "ERROR: Migration incomplete. The following files were not created:"
  echo "$MISSING"
  echo ""
  echo "NOT deleting .planning/ — please investigate and retry."
  exit 1
fi

echo "Verification passed: all [N] feature directories created."
```

---

## 11. Delete .planning/

Only reached if verification passed in Step 10:

```bash
rm -rf .planning/
echo "Deleted .planning/"
```

---

## 12. Commit and Done

Commit all new feature files:

```bash
# Gather all new feature files
FEATURE_FILES=$(find docs/features -name "*.md" -newer docs/features/STATE.md 2>/dev/null | head -100)
node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs commit "docs(gfd): migrate from GSD" --files $FEATURE_FILES docs/features/STATE.md
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

`/gfd:progress`

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
- docs/features/PROJECT.md, STATE.md, REQUIREMENTS.md (if not already present)
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
