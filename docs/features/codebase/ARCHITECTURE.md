# Architecture

**Analysis Date:** 2026-02-20

## Pattern Overview

**Overall:** Orchestrator-Agent Workflow Pattern

**Key Characteristics:**
- Agent-based architecture with specialized roles (researcher, planner, executor, verifier, codebase-mapper)
- Orchestrator commands coordinate agent spawning and workflow sequencing
- Declarative command definitions that reference agent and workflow implementations
- Feature-scoped directory structure avoiding cross-artifact conflicts
- Template-driven document generation for consistent structure
- Git-first workflow with atomic commits per task/phase

## Layers

**Command Layer:**
- Purpose: Entry points for user interaction via slash commands (`/gfd:*`)
- Location: `commands/gfd/`
- Contains: Command definition files (`*.md` with frontmatter metadata)
- Depends on: Agent layer (for orchestration), workflow layer (for execution scripts)
- Used by: Claude Code slash command system

**Agent Layer:**
- Purpose: Specialized worker agents that execute workflows with focused context
- Location: `agents/`
- Contains: Agent definitions (`gfd-*.md`) with role description, tools, and process documentation
- Agent types: gfd-researcher, gfd-planner, gfd-executor, gfd-verifier, gfd-codebase-mapper
- Depends on: Workflow definitions, templates, references
- Used by: Orchestrator commands via Task tool spawning

**Workflow Layer:**
- Purpose: Detailed step-by-step execution procedures for complex workflows
- Location: `get-features-done/workflows/`
- Contains: Workflow instructions (`*.md`) defining orchestration logic, parallel execution, state management
- Depends on: Agent specs, tool capabilities, template library
- Used by: Command definitions that reference workflows

**Template Layer:**
- Purpose: Standardized document structures to ensure consistency across projects
- Location: `get-features-done/templates/`
- Contains:
  - Project-level templates: `project.md`, `feature.md`, `state.md`, `config.json`, `requirements.md`, `summary.md`
  - Codebase analysis templates: `codebase/stack.md`, `codebase/integrations.md`, `codebase/architecture.md`, `codebase/structure.md`, `codebase/conventions.md`, `codebase/testing.md`, `codebase/concerns.md`
- Depends on: Nothing (standalone reference documents)
- Used by: Agents when creating project artifacts

**Reference Layer:**
- Purpose: Guidelines and patterns for agent behavior
- Location: `get-features-done/references/`
- Contains: UI branding (`ui-brand.md`), questioning style (`questioning.md`), git integration (`git-integration.md`)
- Depends on: Nothing
- Used by: Agents to maintain consistent tone/approach

**Tools/Utilities Layer:**
- Purpose: Command-line utilities and helper tools
- Location: `get-features-done/bin/`
- Contains: `gfd-tools.cjs` (Node.js CLI for config loading, frontmatter parsing, git operations, model profile management)
- Depends on: Node.js standard library, git
- Used by: Workflow orchestrators for state initialization and commits

## Data Flow

**Feature Definition → Planning → Execution Flow:**

1. User invokes `/gfd:new-feature` command
2. new-feature command references `new-feature` workflow
3. Workflow: user creates `docs/features/<slug>/FEATURE.md` with acceptance criteria
4. User invokes `/gfd:plan-feature <slug>`
5. plan-feature command orchestrator:
   - Loads project context (`PROJECT.md`, `STATE.md`, `REQUIREMENTS.md`)
   - Optionally spawns gfd-researcher agent for discovery
   - Spawns gfd-planner agent with feature context, research findings
   - Planner produces `docs/features/<slug>/PLAN.md`
   - Optionally spawns gfd-verifier to check plan quality
   - Commits plan to git
6. User invokes `/gfd:execute-feature <slug>`
7. execute-feature command orchestrator:
   - Loads plan(s) from `docs/features/<slug>/PLAN.md`
   - Spawns gfd-executor agent with plan
   - Executor creates commits per task, produces `docs/features/<slug>/SUMMARY.md`
   - Updates `docs/features/STATE.md` with progress
   - Commits summary and state

**Codebase Mapping Flow:**

1. User invokes `/gfd:map-codebase`
2. Orchestrator spawns 4 parallel gfd-codebase-mapper agents:
   - Agent 1 (tech focus): analyzes dependencies → writes `docs/features/codebase/STACK.md` and `INTEGRATIONS.md`
   - Agent 2 (arch focus): analyzes structure → writes `docs/features/codebase/ARCHITECTURE.md` and `STRUCTURE.md`
   - Agent 3 (quality focus): analyzes conventions → writes `docs/features/codebase/CONVENTIONS.md` and `TESTING.md`
   - Agent 4 (concerns focus): analyzes debt → writes `docs/features/codebase/CONCERNS.md`
3. Orchestrator: verifies all documents created, scans for secrets, commits to git

**State Management:**

- `docs/features/PROJECT.md`: Project-level context (scope, requirements, constraints, decisions) — updated infrequently (after feature validation)
- `docs/features/STATE.md`: Current execution state (active feature, progress, blockers, decisions) — updated at checkpoint/completion
- `docs/features/<slug>/FEATURE.md`: Feature-level specification (acceptance criteria, locked decisions) — created once, rarely updated
- `docs/features/<slug>/PLAN.md`: Implementation plan with tasks, dependencies, verification — created during planning, may be revised
- `docs/features/<slug>/SUMMARY.md`: Execution results (completed tasks, commits, deviations, outcome) — created after execution
- `docs/features/config.json`: Workflow configuration (model profile, gates, parallelization) — set once at project init

## Key Abstractions

**Feature:**
- Purpose: Represents a single unit of deliverable work
- Examples: `docs/features/user-auth/FEATURE.md`, `docs/features/dark-mode/FEATURE.md`
- Pattern: Feature-scoped directory with frontmatter metadata (name, slug, status, priority, acceptance criteria)
- Artifacts: FEATURE.md, PLAN.md, SUMMARY.md, supporting files/analysis

**Plan:**
- Purpose: Executable breakdown of a feature into parallel-optimized tasks
- Examples: `docs/features/user-auth/PLAN.md`
- Pattern: Frontmatter (feature, plan type, dependencies, execution wave) + objectives + context references + tasks with verification
- Task types: `auto` (autonomous execution), `checkpoint:*` (execution pause point)
- Key decision: Plans are prompts (executed directly by executor agents), not documents that become prompts

**Workflow:**
- Purpose: Orchestration script for complex multi-agent operations
- Examples: `get-features-done/workflows/plan-feature.md`, `get-features-done/workflows/execute-feature.md`
- Pattern: Sequential steps with decision points, agent spawning instructions, checkpoint logic
- Key responsibility: Manage parallel execution (via Task tool with `run_in_background=true`), state transitions, verification

**Agent:**
- Purpose: Specialized worker with focus area and tool set
- Examples: gfd-planner (creates plans), gfd-executor (runs plans), gfd-researcher (discovers approach)
- Pattern: Role description + context fidelity rules + process with numbered steps + templates for output
- Execution: Spawned by orchestrator with fresh context, produces output directly (no return to orchestrator)

**Model Profile:**
- Purpose: Configure which Claude model runs each agent type
- Examples: `quality` (all agents use opus), `balanced` (planner/executor use sonnet, mapper uses haiku), `budget` (executor/mapper use haiku)
- Location: `get-features-done/bin/gfd-tools.cjs` (MODEL_PROFILES constant)
- Used by: Config loading to determine `executor_model`, `planner_model`, etc. for agent spawning

## Entry Points

**Slash Commands:**
- Location: `commands/gfd/`
- Files: `new-project.md`, `new-feature.md`, `plan-feature.md`, `execute-feature.md`, `progress.md`, `map-codebase.md`
- Each defines: name (command path), description, allowed-tools, execution_context (references), process (orchestration steps)

**Key command sequence:**
1. `/gfd:new-project` — Initialize project with PROJECT.md, STATE.md, config.json
2. `/gfd:new-feature` — Create FEATURE.md with acceptance criteria
3. `/gfd:plan-feature <slug>` — Generate PLAN.md
4. `/gfd:execute-feature <slug>` — Run PLAN.md, produce SUMMARY.md
5. `/gfd:progress` — Check status, determine next action
6. `/gfd:map-codebase` — Analyze codebase, produce STACK/ARCHITECTURE/CONVENTIONS/CONCERNS docs

**Initialization Entry:**
- Location: `get-features-done/bin/gfd-tools.cjs`
- Function: `gfd-tools.cjs init <command-type> [slug]`
- Returns: JSON with config, state, paths, available agents/plans
- Used by: Orchestrator workflows to determine execution context

**Execution Hooks:**
- Location: `get-features-done/bin/gfd-tools.cjs`
- Functions:
  - `commit <message> --files <patterns>` — Create git commit with specified files
  - `task-checkpoint <checkpoint-name>` — Signal execution pause point
- Used by: Workflows to persist state and manage checkpoints

## Error Handling

**Strategy:** Graceful degradation with manual intervention points (checkpoints)

**Patterns:**

**Pattern 1: Agent Failures**
- Caught at orchestrator level (workflow step)
- If optional (research, verification): skip and continue
- If critical (planning, execution): surface error and stop

**Pattern 2: Execution Deviations**
- Executor applies auto-fix rules (RULE 1: bug fixes, RULE 2: missing critical features)
- Other deviations: pause at checkpoint, wait for user decision
- All deviations tracked in SUMMARY.md

**Pattern 3: Plan Divergence**
- Executor detects work not in plan
- Small issues: auto-fix, track as deviation, continue
- Scope change: checkpoint, document decision, resume
- Safety: executor never commits beyond plan without checkpoint

**Pattern 4: State Inconsistency**
- If STATE.md missing but docs/features/ exists: offer reconstruct or continue
- If PLAN.md missing at execution start: error and stop (requires planning first)
- If commits don't match state: executor pauses and surfaces mismatch

## Cross-Cutting Concerns

**Logging:**
- Approach: Minimal structured output from agents (return confirmation + line counts only)
- Orchestrator displays user-facing banners and status updates
- All work logged via git commits (message + diff = full audit trail)

**Validation:**
- Agent-level: Each agent validates its inputs (FEATURE.md acceptance criteria, PLAN.md completeness, etc.)
- Orchestrator-level: Verifier agent checks plan quality before committing; executor verifies task completion before moving on
- Document-level: Frontmatter schema validated when loaded; content templates ensure consistency

**Authentication:**
- Approach: Handled at execution time, not planning time
- Pattern: Auth errors treated as "authentication gates" — executor pauses, user re-authenticates, resumes
- No credentials stored in planning artifacts

**Configuration:**
- Single source of truth: `docs/features/config.json` (loaded at project init)
- Profiles: Model selection via `model_profile` (quality/balanced/budget)
- Gates: Workflow gates (enable/disable research, plan-checking, verification)
- Team: Member definitions for assignment/visibility
