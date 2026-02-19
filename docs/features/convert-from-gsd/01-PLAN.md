---
feature: convert-from-gsd
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - commands/gfd/convert-from-gsd.md
  - get-features-done/workflows/convert-from-gsd.md
autonomous: true
acceptance_criteria:
  - "Scans .planning/ and discovers all GSD phases, milestones, roadmap, research, summaries, and verification docs"
  - "Presents a summary table mapping each GSD phase/milestone to a suggested GFD feature slug with GSD status"
  - "User can accept, rename, or skip each suggested mapping before conversion proceeds"

must_haves:
  truths:
    - "Running /gfd:convert-from-gsd in a GSD project directory discovers all phase directories"
    - "A mapping table is presented showing GSD phase name, suggested slug, and computed GFD status"
    - "User can respond to each mapping with accept, rename, or skip before any files are created"
    - "Phases archived in .planning/milestones/ are included alongside active phases"
    - "Status is derived from disk artifacts (plans, summaries, research, context) and ROADMAP.md goal presence — not ROADMAP.md checkboxes"
  artifacts:
    - path: "commands/gfd/convert-from-gsd.md"
      provides: "Slash command definition with allowed-tools and workflow reference"
      contains: "gfd:convert-from-gsd"
    - path: "get-features-done/workflows/convert-from-gsd.md"
      provides: "Steps 1-5 of migration workflow: verify, scan, read roadmap, detect status, present table, interactive review"
      min_lines: 100
  key_links:
    - from: "commands/gfd/convert-from-gsd.md"
      to: "get-features-done/workflows/convert-from-gsd.md"
      via: "@execution_context reference"
      pattern: "@.*workflows/convert-from-gsd"
    - from: "workflow step: scan phases"
      to: "workflow step: present table"
      via: "Node.js phase scanning logic producing JSON array"
      pattern: "readdirSync.*phases"
---

<objective>
Create the command entry point and the discovery/review half of the migration workflow. The workflow scans .planning/ for all GSD phase directories (including archived ones in milestones/), reads ROADMAP.md for phase goals and status context, computes the actual GFD status from disk (plan/summary counts), presents a mapping table, then walks the user through accepting, renaming, or skipping each suggested feature slug.

Purpose: This is the gating half of the migration — nothing is written until the user approves the mappings. Getting this right prevents destructive mismaps.

Output: Two files — the command stub and the workflow document with steps 1-5 (verify through interactive review).
</objective>

<execution_context>
@/home/conroy/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/convert-from-gsd/FEATURE.md
@docs/features/convert-from-gsd/RESEARCH.md
@commands/gfd/plan-feature.md
@get-features-done/workflows/new-feature.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create the command file</name>
  <files>commands/gfd/convert-from-gsd.md</files>
  <action>
Create `commands/gfd/convert-from-gsd.md` following the exact pattern of existing commands (see `commands/gfd/plan-feature.md` for reference).

The file must have:
- Frontmatter: `name: gfd:convert-from-gsd`, `description: Migrate a GSD .planning/ project to GFD docs/features/ structure`, `argument-hint: (no arguments)`, `allowed-tools: Read, Write, Edit, Bash, Grep, Glob, AskUserQuestion`
- `<objective>` tag: one sentence describing the command
- `<execution_context>` tag with these @-references:
  - `@/home/conroy/.claude/get-features-done/workflows/convert-from-gsd.md`
  - `@/home/conroy/.claude/get-features-done/references/ui-brand.md`
- `<process>` tag: "Execute the convert-from-gsd workflow."

Do NOT include any workflow logic in the command file — that belongs in the workflow. The command is just a stub that loads the workflow.
  </action>
  <verify>
Run: `cat commands/gfd/convert-from-gsd.md` and confirm:
1. Frontmatter has `name: gfd:convert-from-gsd`
2. `allowed-tools` includes `AskUserQuestion`
3. `@execution_context` references `workflows/convert-from-gsd.md`
  </verify>
  <done>File exists at `commands/gfd/convert-from-gsd.md` with correct frontmatter and workflow reference.</done>
</task>

<task type="auto">
  <name>Task 2: Create the workflow — discovery and interactive review (steps 1-5)</name>
  <files>get-features-done/workflows/convert-from-gsd.md</files>
  <action>
Create `get-features-done/workflows/convert-from-gsd.md` with the purpose block and steps 1-5. Steps 6-12 (migration execution) will be added in Plan 02.

Follow existing workflow structure (see `get-features-done/workflows/new-feature.md` for formatting patterns). Use `<purpose>`, `<required_reading>`, and `<process>` tags. Inside `<process>`, each step is a `## N. Step Name` heading.

**`<purpose>` block:**
Migrate a GSD `.planning/` directory to GFD `docs/features/` structure. Scans phases, maps to slugs, presents mappings for user review, then executes migration (files, frontmatter, cleanup).

**`<required_reading>` block:**
```
@/home/conroy/.claude/get-features-done/references/ui-brand.md
```

**`<process>` block — implement these 5 steps:**

---

**## 1. Verify .planning/ Exists**

Check for `.planning/` directory. If missing, show error:
```
╔══════════════════════════════════════════════════════════════╗
║  ERROR                                                       ║
╚══════════════════════════════════════════════════════════════╝

No GSD project found. .planning/ directory does not exist.

**To fix:** This command is for projects managed by GSD.
           If you have a GFD project already, run /gfd:progress instead.
```
Exit.

Also check if `docs/features/` already exists. If so, warn the user:

Use AskUserQuestion:
- header: "GFD Project Exists"
- question: "docs/features/ already exists. This migration will ADD new features alongside existing ones. Continue?"
- options:
  - "Continue — add migrated features" — Proceed
  - "Cancel" — Abort

If "Cancel": exit.

---

**## 2. Display Banner**

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► CONVERT FROM GSD
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Scanning .planning/ for GSD phases...
```

---

**## 3. Scan Phase Directories**

Scan both `.planning/phases/` and `.planning/milestones/` (for archived phases). Use Node.js one-liners via Bash. Collect all phase directories into a JSON array sorted by phase number.

```bash
PHASES_JSON=$(node -e "
const fs = require('fs');
const path = require('path');

function scanPhaseDirs(dir) {
  if (!fs.existsSync(dir)) return [];
  return fs.readdirSync(dir, {withFileTypes: true})
    .filter(e => e.isDirectory() && /^\d/.test(e.name))
    .map(e => ({
      dirName: e.name,
      dirPath: path.join(dir, e.name),
      phaseNum: parseFloat(e.name.match(/^(\d+(?:\.\d+)?)/)?.[1] || '0'),
      phaseName: e.name.replace(/^\d+(?:\.\d+)?-/, ''),
      archived: dir.includes('milestones'),
    }));
}

// Also check milestones subfolders for archived phase dirs
function scanMilestonePhaseDirs() {
  const milestonesDir = '.planning/milestones';
  if (!fs.existsSync(milestonesDir)) return [];
  const results = [];
  for (const entry of fs.readdirSync(milestonesDir, {withFileTypes: true})) {
    if (entry.isDirectory()) {
      const phasesSubdir = path.join(milestonesDir, entry.name, 'phases');
      results.push(...scanPhaseDirs(phasesSubdir).map(p => ({...p, archived: true})));
    }
  }
  return results;
}

const activePhases = scanPhaseDirs('.planning/phases');
const archivedPhases = scanMilestonePhaseDirs();
const allPhases = [...activePhases, ...archivedPhases]
  .sort((a, b) => a.phaseNum - b.phaseNum);

console.log(JSON.stringify(allPhases));
")
```

---

**## 4. Determine Status and Build Mapping Table**

For each discovered phase directory, compute the GFD status from disk (plan/summary counts). Extract goal and success criteria from ROADMAP.md for use later in FEATURE.md generation.

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

function extractPhaseGoal(phaseNum, phaseName) {
  const numStr = String(Math.floor(phaseNum));
  const pattern = new RegExp(
    '###\\\s+(?:Phase\\\s+)?' + numStr + '[^\\\\d][\\\\s\\\\S]*?\\\\*\\\\*Goal\\\\*\\\\*:\\\\s*(.+)',
    'i'
  );
  const match = roadmap.match(pattern);
  return match?.[1]?.trim() || '';
}

function extractSuccessCriteria(phaseNum) {
  const numStr = String(Math.floor(phaseNum));
  const sectionPattern = new RegExp(
    '###\\\s+(?:Phase\\\s+)?' + numStr + '[^\\\\d]([\\\\s\\\\S]*?)(?=\\\\n###|\\\\n##|\$)'
  );
  const sectionMatch = roadmap.match(sectionPattern);
  if (!sectionMatch) return [];
  const criteriaMatches = [...sectionMatch[1].matchAll(/^\s+\d+\.\s+(.+)\$/gm)];
  return criteriaMatches.map(m => m[1].trim());
}

const mapping = phases.map(p => {
  // Slug: strip numeric prefix (handle decimals like 2.1)
  const slug = p.phaseName.replace(/[^a-z0-9-]/g, '-').replace(/-+/g, '-').replace(/^-|-\$/g, '');
  const goal = extractPhaseGoal(p.phaseNum, p.phaseName);
  const status = detectStatus(p.dirPath, p.archived, !!goal);
  const criteria = extractSuccessCriteria(p.phaseNum);
  return {
    phaseDir: p.dirName,
    dirPath: p.dirPath,
    phaseNum: p.phaseNum,
    phaseName: p.phaseName,
    suggestedSlug: slug,
    gfdStatus: status,
    goal: goal,
    criteria: criteria,
    archived: p.archived,
  };
});

console.log(JSON.stringify(mapping));
")
```

Display the mapping table to the user:

```
## Suggested Feature Mappings

Found [N] GSD phases.

| # | GSD Phase | Suggested Slug | Status | Notes |
|---|-----------|---------------|--------|-------|
| 1 | 01-foundation-save-system | foundation-save-system | done | |
| 2 | 02-hero-system | hero-system | done | |
| ... | ... | ... | ... | |
| N | 28-death-loot-retrieval | death-loot-retrieval | in-progress | archived |
```

(Archived phases show "archived" in Notes column.)

---

**## 5. Interactive Mapping Review**

Walk the user through each mapping one at a time. For each phase:

Use AskUserQuestion:
- header: "Phase [phaseNum]: [phaseName]"
- question: "Suggested slug: `[suggestedSlug]` (status: [gfdStatus]). What would you like to do?"
- options:
  - "Accept — use `[suggestedSlug]`"
  - "Rename — I'll provide a different slug"
  - "Skip — don't migrate this phase"

If "Rename": Ask inline (freeform): "Enter the new slug for `[phaseName]` (lowercase, hyphens only):"
Validate slug format (`^[a-z0-9]+(-[a-z0-9]+)*$`). If invalid, re-ask.

Build `ACCEPTED_MAPPINGS` array from user choices (only accepted/renamed entries).

**After all phases reviewed, show confirmation summary:**

```
## Migration Plan

The following [N] phases will be migrated:

| GSD Phase | Feature Slug | Status |
|-----------|-------------|--------|
| 01-foundation-save-system | foundation-save-system | done |
| 03-adventure-system | adventure-system | in-progress |
...

Skipped: [list of skipped phases, or "none"]
```

**Dependency warning check:** If any accepted phase depends on a skipped phase (from ROADMAP.md `**Depends on**` field), warn:
```
Warning: `combat-system` depends on `adventure-system` which was skipped.
         The depends_on field will need manual correction after migration.
```

Use AskUserQuestion:
- header: "Confirm Migration"
- question: "Ready to migrate [N] features?"
- options:
  - "Proceed — migrate now"
  - "Go back — review mappings again"
  - "Cancel — abort"

If "Go back": return to start of Step 5 (re-run interactive review).
If "Cancel": exit.

Store final `ACCEPTED_MAPPINGS` as a shell variable (JSON array) for use in the next step block (migration execution — added in Plan 02).

---

End the workflow file after Step 5. Add a comment at the bottom:

```
<!-- Steps 6-12 (migration execution) will be appended by Plan 02 -->
```
  </action>
  <verify>
1. `cat get-features-done/workflows/convert-from-gsd.md` — file exists with `<purpose>`, `<required_reading>`, `<process>` structure
2. Confirm 5 numbered steps exist: "Verify .planning/ Exists", "Display Banner", "Scan Phase Directories", "Determine Status and Build Mapping Table", "Interactive Mapping Review"
3. Confirm step 3 includes `scanMilestonePhaseDirs` function for archived phase handling
4. Confirm step 4 includes `detectStatus` function checking plan/summary counts
5. Confirm step 5 uses `AskUserQuestion` per phase with accept/rename/skip options
  </verify>
  <done>
Workflow file exists with all 5 steps implemented. Discovery logic handles both active and archived phases. Status detection uses disk counts (not ROADMAP.md). Interactive review collects ACCEPTED_MAPPINGS. Confirmation step shows final mapping before proceeding.
  </done>
</task>

</tasks>

<verification>
1. `ls commands/gfd/convert-from-gsd.md` — command file exists
2. `ls get-features-done/workflows/convert-from-gsd.md` — workflow file exists
3. `grep "gfd:convert-from-gsd" commands/gfd/convert-from-gsd.md` — command name is correct
4. `grep "workflows/convert-from-gsd" commands/gfd/convert-from-gsd.md` — workflow is referenced
5. `grep "scanMilestonePhaseDirs\|archived" get-features-done/workflows/convert-from-gsd.md` — archived phase handling present
6. `grep "detectStatus\|PLAN\.md\|SUMMARY\.md" get-features-done/workflows/convert-from-gsd.md` — status detection from disk present
7. `grep "AskUserQuestion\|Accept\|Rename\|Skip" get-features-done/workflows/convert-from-gsd.md` — interactive review present
</verification>

<success_criteria>
- Command file exists and follows the pattern of existing commands (plan-feature.md)
- Workflow file has purpose + required_reading + process structure
- Scan discovers ALL phase directories including archived ones in milestones/
- Status computed from disk (plan count vs summary count + verification file)
- Decimal phases (2.1) handled by stripping full numeric prefix
- User sees mapping table before any files are written
- User reviews each phase individually (accept/rename/skip)
- Dependency warnings shown for skipped phases that others depend on
- ACCEPTED_MAPPINGS is available as a shell variable after step 5
</success_criteria>

<output>
After completion, create `docs/features/convert-from-gsd/01-SUMMARY.md`
</output>
