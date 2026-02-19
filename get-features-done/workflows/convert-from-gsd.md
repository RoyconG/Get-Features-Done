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

<!-- Steps 6-12 (migration execution) will be appended by Plan 02 -->

</process>
