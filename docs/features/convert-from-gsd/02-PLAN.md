---
feature: convert-from-gsd
plan: 02
type: execute
wave: 2
depends_on: ["01"]
files_modified:
  - get-features-done/workflows/convert-from-gsd.md
autonomous: true
acceptance_criteria:
  - "Creates docs/features/<slug>/FEATURE.md for each accepted mapping, populated with context from the GSD plans"
  - "Migrates all related GSD documents (RESEARCH.md, PLAN.md, SUMMARY.md, VERIFICATION.md, etc.) into the feature directory"
  - "GSD statuses are mapped to GFD statuses (complete → done, in-progress → in-progress, etc.)"
  - "Deletes .planning/ directory after successful conversion"

must_haves:
  truths:
    - "Each accepted phase produces a docs/features/<slug>/ directory with FEATURE.md"
    - "FEATURE.md is populated with Description from ROADMAP.md **Goal** and Acceptance Criteria from **Success Criteria**"
    - "Plan files are renamed from NN-MM-PLAN.md to MM-PLAN.md with frontmatter field phase: → feature:"
    - "Summary files are renamed from NN-MM-SUMMARY.md to MM-SUMMARY.md with frontmatter field phase: → feature:"
    - ".planning/research/ is copied to docs/features/research/ if it exists"
    - ".planning/ is deleted only after all feature directories are verified to exist"
    - "Commit is created with all migrated files"
  artifacts:
    - path: "get-features-done/workflows/convert-from-gsd.md"
      provides: "Steps 6-12 of migration workflow: GFD init, FEATURE.md creation, artifact migration, research dir copy, deletion, commit, done screen"
      min_lines: 200
  key_links:
    - from: "workflow step: create FEATURE.md"
      to: "ROADMAP.md **Goal** field"
      via: "Node.js regex extraction of phase goal text"
      pattern: "\\*\\*Goal\\*\\*"
    - from: "workflow step: migrate artifacts"
      to: "gfd-tools.cjs frontmatter merge"
      via: "node gfd-tools.cjs frontmatter merge --data '{\"feature\": \"slug\"}'"
      pattern: "frontmatter merge"
    - from: "workflow step: delete .planning/"
      to: "verification of all feature dirs"
      via: "check each expected slug exists before rm -rf"
      pattern: "rm -rf .planning"
---

<objective>
Append steps 6-12 to the workflow created in Plan 01, implementing the actual migration: initializing GFD project structure if needed, creating FEATURE.md for each accepted mapping, copying and renaming all GSD artifacts, updating plan/summary frontmatter, migrating the research directory, deleting .planning/ only after verification, and committing.

Purpose: This is the execution half — all writes happen here. The delete-last approach prevents data loss if migration fails partway.

Output: Workflow file updated with steps 6-12. Running the full command on a GSD project will produce a complete GFD docs/features/ structure and remove .planning/.
</objective>

<execution_context>
@$HOME/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/convert-from-gsd/FEATURE.md
@docs/features/convert-from-gsd/RESEARCH.md
@docs/features/convert-from-gsd/01-SUMMARY.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Append migration execution steps 6-12 to the workflow</name>
  <files>get-features-done/workflows/convert-from-gsd.md</files>
  <action>
Remove the placeholder comment at the bottom of `get-features-done/workflows/convert-from-gsd.md` and append steps 6-12.

**## 6. Initialize GFD Structure (if needed)**

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

**## 7. Create Feature Directories and FEATURE.md Files**

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

**## 8. Migrate GSD Artifacts**

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
  const planPattern = new RegExp('^' + phaseNumStr + '-([0-9]+-(?:PLAN|SUMMARY)\\.md)$');
  const planMatch = filename.match(planPattern);
  if (planMatch) return planMatch[1];

  const singlePattern = new RegExp('^' + phaseNumStr + '-(.+\\.md)$');
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
        \`node $HOME/.claude/get-features-done/bin/gfd-tools.cjs frontmatter merge \"\${filePath}\" --data '\${JSON.stringify({feature: m.slug})}'\`,
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

**## 9. Migrate Research Directory**

If `.planning/research/` exists, copy all its files to `docs/features/research/`:

```bash
if [ -d ".planning/research" ]; then
  mkdir -p docs/features/research
  cp -r .planning/research/. docs/features/research/
  echo "Migrated .planning/research/ to docs/features/research/"
fi
```

---

**## 10. Verify Migration Completeness**

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

**## 11. Delete .planning/**

Only reached if verification passed in Step 10:

```bash
rm -rf .planning/
echo "Deleted .planning/"
```

---

**## 12. Commit and Done**

Commit all new feature files:

```bash
# Gather all new feature files
FEATURE_FILES=$(find docs/features -name "*.md" -newer docs/features/STATE.md 2>/dev/null | head -100)
node $HOME/.claude/get-features-done/bin/gfd-tools.cjs commit "docs(gfd): migrate from GSD" --files $FEATURE_FILES docs/features/STATE.md
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

Close the `</process>` and `</output>` tags at the end of the workflow file. Add an `<output>` section listing what the workflow creates:

```
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
```
  </action>
  <verify>
1. `grep -c "## [0-9]*\." get-features-done/workflows/convert-from-gsd.md` — should show 12 numbered steps total
2. `grep "rm -rf .planning" get-features-done/workflows/convert-from-gsd.md` — deletion step exists
3. `grep "MISSING\|Verification passed" get-features-done/workflows/convert-from-gsd.md` — pre-deletion verification exists
4. `grep "frontmatter merge\|feature:" get-features-done/workflows/convert-from-gsd.md` — frontmatter update logic exists
5. `grep "planning/research\|docs/features/research" get-features-done/workflows/convert-from-gsd.md` — research dir migration exists
6. `grep "gfd-tools.cjs commit" get-features-done/workflows/convert-from-gsd.md` — commit step exists
7. `grep "</process>\|</output>\|<success_criteria>" get-features-done/workflows/convert-from-gsd.md` — workflow properly closed
  </verify>
  <done>
Workflow file contains all 12 steps. Steps 6-12 implement: GFD init, FEATURE.md creation (with ROADMAP.md goal + criteria), artifact migration with rename rules, frontmatter update (phase → feature), research dir copy, pre-deletion verification, .planning/ deletion, commit, and done screen. The delete-last pattern is implemented.
  </done>
</task>

</tasks>

<verification>
1. `wc -l get-features-done/workflows/convert-from-gsd.md` — file should be >200 lines
2. `grep "## 6\.\|## 7\.\|## 8\.\|## 9\.\|## 10\.\|## 11\.\|## 12\." get-features-done/workflows/convert-from-gsd.md` — all 7 new steps present
3. `grep "done\|in-progress\|planned\|new" get-features-done/workflows/convert-from-gsd.md` — GFD status values present
4. `grep "gsd_phase" get-features-done/workflows/convert-from-gsd.md` — traceability field included in FEATURE.md template
5. Manually read the workflow end: confirm `</process>`, `<output>`, `<success_criteria>` tags are properly closed
</verification>

<success_criteria>
- Workflow file has all 12 steps complete
- FEATURE.md generation uses ROADMAP.md **Goal** as Description
- FEATURE.md uses ROADMAP.md **Success Criteria** as Acceptance Criteria checkboxes (checked if done)
- Plan/summary files renamed by stripping phase numeric prefix
- frontmatter merge updates phase: field to feature: in all migrated plan/summary files
- research/ directory migration is a simple cp -r
- Verification step checks all expected FEATURE.md files exist before running rm -rf
- gfd-tools.cjs commit used (not raw git commands)
- Done screen shows migration stats and next steps
</success_criteria>

<output>
After completion, create `docs/features/convert-from-gsd/02-SUMMARY.md`
</output>
