<purpose>
Orchestrate parallel codebase mapper agents to analyze the codebase and produce structured documents in docs/features/codebase/.

Each agent has fresh context, explores a specific focus area, and writes documents directly. The orchestrator only receives confirmation + line counts, then summarizes.

Output: docs/features/codebase/ folder with 7 structured documents about the codebase state.
</purpose>

<philosophy>
**Why dedicated mapper agents:**
- Fresh context per domain (no token contamination)
- Agents write documents directly (no context transfer back to orchestrator)
- Orchestrator only summarizes what was created (minimal context usage)
- Faster execution (agents run simultaneously)

**Document quality over length:**
Include enough detail to be useful as reference. Prioritize practical examples (especially code patterns) over arbitrary brevity.

**Always include file paths:**
Documents are reference material for Claude when planning/executing. Always include actual file paths formatted with backticks: `src/services/user.ts`.
</philosophy>

<process>

<step name="init_context" priority="first">
Load codebase mapping context:

```bash
INIT=$($HOME/.claude/get-features-done/bin/gfd-tools init map-codebase)
```

Extract from key=value output: `mapper_model`, `codebase_dir`, `has_maps`, `codebase_dir_exists`, `project_exists` (grep "^key=" | cut -d= -f2-).

**If `project_exists` is false AND codebase_dir doesn't exist:**

This is fine — map-codebase can run before project initialization. The output goes to `docs/features/codebase/` which will be created.

**Note:** `codebase_dir` should be `docs/features/codebase`.
</step>

<step name="check_existing">
Check if docs/features/codebase/ already exists using `has_maps` from init context.

If `codebase_dir_exists` is true:

```bash
ls -la docs/features/codebase/
```

**If exists:**

```
docs/features/codebase/ already exists with these documents:
[List files found]

What's next?
1. Refresh — Delete existing and remap codebase
2. Update — Keep existing, only update specific documents
3. Skip — Use existing codebase map as-is
```

Wait for user response.

If "Refresh": Delete docs/features/codebase/, continue to create_structure.
If "Update": Ask which documents to update, continue to spawn_agents (filtered).
If "Skip": Exit workflow.

**If doesn't exist:**
Continue to create_structure.
</step>

<step name="create_structure">
Create docs/features/codebase/ directory:

```bash
mkdir -p docs/features/codebase
```

**Expected output files:**
- STACK.md (from tech mapper)
- INTEGRATIONS.md (from tech mapper)
- ARCHITECTURE.md (from arch mapper)
- STRUCTURE.md (from arch mapper)
- CONVENTIONS.md (from quality mapper)
- TESTING.md (from quality mapper)
- CONCERNS.md (from concerns mapper)

Continue to spawn_agents.
</step>

<step name="spawn_agents">
**Display banner:**

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► MAPPING CODEBASE
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

◆ Spawning 4 codebase mappers in parallel...
  → Tech stack and integrations
  → Architecture and structure
  → Conventions and testing
  → Concerns and technical debt
```

Spawn 4 parallel gfd-codebase-mapper agents.

Use Task tool with `subagent_type="gfd-codebase-mapper"`, `model="{mapper_model}"`, and `run_in_background=true` for parallel execution.

**CRITICAL:** Use the dedicated `gfd-codebase-mapper` agent. The mapper agent writes documents directly.

**Agent 1: Tech Focus**

Task tool parameters:
```
subagent_type: "gfd-codebase-mapper"
model: "{mapper_model}"
run_in_background: true
description: "Map codebase tech stack and integrations"
```

Prompt:
```
Focus: tech

Analyze this codebase for technology stack and external integrations.

Write these documents to docs/features/codebase/:
- STACK.md — Languages, runtime, frameworks, dependencies, build system, configuration
- INTEGRATIONS.md — External APIs, databases, auth providers, webhooks, third-party services

Explore thoroughly. Write documents directly using file tools. Return confirmation only:

## Mapping Complete

**Focus:** tech
**Documents written:**
- `docs/features/codebase/STACK.md` ({N} lines)
- `docs/features/codebase/INTEGRATIONS.md` ({N} lines)

Ready for orchestrator summary.
```

**Agent 2: Architecture Focus**

Task tool parameters:
```
subagent_type: "gfd-codebase-mapper"
model: "{mapper_model}"
run_in_background: true
description: "Map codebase architecture and structure"
```

Prompt:
```
Focus: arch

Analyze this codebase architecture and directory structure.

Write these documents to docs/features/codebase/:
- ARCHITECTURE.md — Pattern (MVC, layered, microservices, etc.), layers, data flow, abstractions, entry points, key modules
- STRUCTURE.md — Directory layout, key file locations, naming conventions, module boundaries

Explore thoroughly. Write documents directly using file tools. Return confirmation only:

## Mapping Complete

**Focus:** arch
**Documents written:**
- `docs/features/codebase/ARCHITECTURE.md` ({N} lines)
- `docs/features/codebase/STRUCTURE.md` ({N} lines)

Ready for orchestrator summary.
```

**Agent 3: Quality Focus**

Task tool parameters:
```
subagent_type: "gfd-codebase-mapper"
model: "{mapper_model}"
run_in_background: true
description: "Map codebase conventions and testing"
```

Prompt:
```
Focus: quality

Analyze this codebase for coding conventions and testing patterns.

Write these documents to docs/features/codebase/:
- CONVENTIONS.md — Code style, naming patterns, error handling patterns, logging patterns, common utilities
- TESTING.md — Test framework, test structure, mocking approach, fixture patterns, coverage approach

Explore thoroughly. Write documents directly using file tools. Return confirmation only:

## Mapping Complete

**Focus:** quality
**Documents written:**
- `docs/features/codebase/CONVENTIONS.md` ({N} lines)
- `docs/features/codebase/TESTING.md` ({N} lines)

Ready for orchestrator summary.
```

**Agent 4: Concerns Focus**

Task tool parameters:
```
subagent_type: "gfd-codebase-mapper"
model: "{mapper_model}"
run_in_background: true
description: "Map codebase concerns and technical debt"
```

Prompt:
```
Focus: concerns

Analyze this codebase for technical debt, known issues, and areas of concern.

Write this document to docs/features/codebase/:
- CONCERNS.md — Tech debt, identified bugs, security concerns, performance issues, fragile areas, missing error handling, TODO comments

Explore thoroughly. Write document directly using file tools. Return confirmation only:

## Mapping Complete

**Focus:** concerns
**Documents written:**
- `docs/features/codebase/CONCERNS.md` ({N} lines)

Ready for orchestrator summary.
```

Continue to collect_confirmations.
</step>

<step name="collect_confirmations">
Wait for all 4 agents to complete.

**What you receive:** Just file paths and line counts. NOT document contents.

If any agent failed, note the failure and continue with successful documents.

Continue to verify_output.
</step>

<step name="verify_output">
Verify all documents created successfully:

```bash
ls -la docs/features/codebase/
wc -l docs/features/codebase/*.md
```

**Verification checklist:**
- All 7 documents exist (STACK, INTEGRATIONS, ARCHITECTURE, STRUCTURE, CONVENTIONS, TESTING, CONCERNS)
- No empty documents (each should have >20 lines)

If any documents missing or empty, note which agents may have failed.

Continue to scan_for_secrets.
</step>

<step name="scan_for_secrets">
**CRITICAL SECURITY CHECK:** Scan output files for accidentally leaked secrets before committing.

Run secret pattern detection:

```bash
# Check for common API key patterns in generated docs
grep -E '(sk-[a-zA-Z0-9]{20,}|sk_live_[a-zA-Z0-9]+|sk_test_[a-zA-Z0-9]+|ghp_[a-zA-Z0-9]{36}|gho_[a-zA-Z0-9]{36}|glpat-[a-zA-Z0-9_-]+|AKIA[A-Z0-9]{16}|xox[baprs]-[a-zA-Z0-9-]+|-----BEGIN.*PRIVATE KEY|eyJ[a-zA-Z0-9_-]+\.eyJ[a-zA-Z0-9_-]+\.)' docs/features/codebase/*.md 2>/dev/null && SECRETS_FOUND=true || SECRETS_FOUND=false
```

**If SECRETS_FOUND=true:**

```
╔══════════════════════════════════════════════════════════════╗
║  ERROR                                                       ║
╚══════════════════════════════════════════════════════════════╝

Potential secrets detected in codebase documents!

Found patterns that look like API keys or tokens in:
[show grep output]

This would expose credentials if committed.

**Action required:**
1. Review the flagged content above
2. If these are real secrets, remove them before committing
3. Consider adding sensitive files to Claude Code "Deny" permissions

Pausing before commit. Reply "safe to proceed" if the flagged content
is not actually sensitive, or edit the files first.
```

Wait for user confirmation before continuing to commit_codebase_map.

**If SECRETS_FOUND=false:**

Continue to commit_codebase_map.
</step>

<step name="commit_codebase_map">
Commit the codebase map:

```bash
git add docs/features/codebase/*.md && git diff --cached --quiet || git commit -m "docs(gfd): map codebase"
```

Continue to offer_next.
</step>

<step name="offer_next">
**Display banner:**

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► CODEBASE MAPPED ✓
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

**Get line counts:**
```bash
wc -l docs/features/codebase/*.md
```

**Output format:**

```
Codebase mapping complete.

Created docs/features/codebase/:
- STACK.md ([N] lines) — Technologies and dependencies
- ARCHITECTURE.md ([N] lines) — System design and patterns
- STRUCTURE.md ([N] lines) — Directory layout and organization
- CONVENTIONS.md ([N] lines) — Code style and patterns
- TESTING.md ([N] lines) — Test structure and practices
- INTEGRATIONS.md ([N] lines) — External services and APIs
- CONCERNS.md ([N] lines) — Technical debt and issues
```

**Determine next action:**

Check if project is already initialized (docs/features/PROJECT.md exists):

```bash
if [ -f "docs/features/PROJECT.md" ]; then
  PROJECT_EXISTS=true
else
  PROJECT_EXISTS=false
fi
```

**If PROJECT_EXISTS=false (pre-initialization mapping):**

```
───────────────────────────────────────────────────────────────

## ▶ Next Up

**Initialize project** — use codebase context for planning

`/gfd:new-project`

<sub>`/clear` first → fresh context window</sub>

───────────────────────────────────────────────────────────────

**Also available:**
- Re-run mapping: `/gfd:map-codebase`
- Review a document: `cat docs/features/codebase/STACK.md`

───────────────────────────────────────────────────────────────
```

**If PROJECT_EXISTS=true (re-mapping existing project):**

```
───────────────────────────────────────────────────────────────

## ▶ Next Up

**Continue feature planning** — codebase context now available

`/gfd:progress`

<sub>`/clear` first → fresh context window</sub>
```

**After displaying the primary Next Up command, render the active features status table:**

Call `list-features` to get all features. Filter out done features. If there are 2+ active features, render:

```
| Feature Name | Status | Next Step |
|--------------|--------|-----------|
| [name] | [status] | [next command] |
| [name] | [status] | [next command] |
```

- No feature is bolded (map-codebase is not feature-specific)
- Features in default sort order
- Next Step uses the same status→command mapping as /gfd:status
- Skip this table if only 1 or 0 active features remain

```
───────────────────────────────────────────────────────────────

**Also available:**
- Re-run mapping: `/gfd:map-codebase`
- Review a document: `cat docs/features/codebase/ARCHITECTURE.md`
- Plan a feature with codebase context: `/gfd:plan-feature <slug>`

───────────────────────────────────────────────────────────────
```

End workflow.
</step>

</process>

<success_criteria>

- [ ] docs/features/codebase/ directory created
- [ ] 4 parallel gfd-codebase-mapper agents spawned with run_in_background=true
- [ ] Agents write documents directly (orchestrator doesn't receive document contents)
- [ ] All 7 codebase documents exist after agents complete
- [ ] No empty documents (each >20 lines)
- [ ] Secret scan run before committing
- [ ] Committed with message "docs(gfd): map codebase"
- [ ] Clear completion summary with line counts
- [ ] User offered clear next steps (new-project or progress depending on state)

</success_criteria>
