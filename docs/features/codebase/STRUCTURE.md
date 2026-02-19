# Codebase Structure

**Analysis Date:** 2026-02-20

## Directory Layout

```
/var/home/conroy/Projects/GFD/
├── agents/                              # Agent definitions with role, process, templates
│   ├── gfd-codebase-mapper.md          # Analyzes codebase, writes STACK/ARCHITECTURE/CONVENTIONS/CONCERNS
│   ├── gfd-executor.md                 # Executes PLAN.md files, creates SUMMARY.md
│   ├── gfd-planner.md                  # Creates PLAN.md files from FEATURE.md
│   ├── gfd-researcher.md               # Discovers approach/approach via web search
│   └── gfd-verifier.md                 # Checks plan quality and execution completeness
├── commands/
│   └── gfd/                            # Slash command definitions
│       ├── execute-feature.md          # Run plans, produce execution summary
│       ├── map-codebase.md             # Analyze codebase, output structured docs
│       ├── new-feature.md              # Define new feature with acceptance criteria
│       ├── new-project.md              # Initialize project with docs/features/ structure
│       ├── plan-feature.md             # Create implementation plans
│       └── progress.md                 # Check project status, determine next action
├── docs/
│   └── features/                       # Project planning artifacts (per-project)
│       ├── codebase/                   # Codebase analysis output (created by map-codebase)
│       │   ├── ARCHITECTURE.md         # System patterns, layers, data flow
│       │   ├── CONVENTIONS.md          # Code style, naming, patterns
│       │   ├── CONCERNS.md             # Tech debt, bugs, issues
│       │   ├── INTEGRATIONS.md         # External services, APIs, auth
│       │   ├── STACK.md                # Languages, frameworks, dependencies
│       │   ├── STRUCTURE.md            # Directory layout, file locations
│       │   └── TESTING.md              # Test frameworks, patterns, coverage
│       ├── PROJECT.md                  # Project context (what, core value, requirements, constraints)
│       ├── REQUIREMENTS.md             # Validated, active, out-of-scope requirements
│       ├── STATE.md                    # Current execution state, blockers, decisions
│       └── config.json                 # Workflow configuration (model profile, gates)
├── get-features-done/                  # GFD toolkit (shared across projects)
│   ├── bin/
│   │   └── gfd-tools.cjs               # Node.js CLI (config loading, git ops, model profiles)
│   ├── references/                     # Guidelines for agent behavior
│   │   ├── git-integration.md          # Git workflow patterns, commit conventions
│   │   ├── questioning.md              # User questioning style and approach
│   │   └── ui-brand.md                 # UI/visual presentation guidelines
│   ├── templates/                      # Document templates (copy to projects)
│   │   ├── codebase/                   # Templates for codebase analysis
│   │   │   ├── architecture.md
│   │   │   ├── concerns.md
│   │   │   ├── conventions.md
│   │   │   ├── integrations.md
│   │   │   ├── stack.md
│   │   │   ├── structure.md
│   │   │   └── testing.md
│   │   ├── config.json                 # Project config template
│   │   ├── feature.md                  # Feature definition template
│   │   ├── project.md                  # Project context template
│   │   ├── requirements.md             # Requirements template
│   │   ├── state.md                    # State template
│   │   └── summary.md                  # Execution summary template
│   ├── workflows/                      # Orchestration procedures
│   │   ├── execute-feature.md          # Step-by-step executor orchestration
│   │   ├── map-codebase.md             # Parallel agent spawning for codebase analysis
│   │   ├── new-feature.md              # Feature creation workflow
│   │   ├── new-project.md              # Project initialization workflow
│   │   ├── plan-feature.md             # Planning workflow with research/checking/verification
│   │   └── progress.md                 # Status check workflow
│   └── VERSION                         # Release version number
├── install.sh                          # Installation script (creates symlinks to ~/.claude/)
└── README.md                           # Project overview and usage
```

## Directory Purposes

**agents/**
- Purpose: Agent definitions that Claude Code loads as subagent types
- Contains: Markdown files defining role, responsibilities, process steps, templates
- Key files: Each agent is one file with frontmatter + instructions
- Installed to: `~/.claude/agents/` (via install.sh)

**commands/gfd/**
- Purpose: Slash command definitions (`/gfd:*`)
- Contains: Command metadata (name, description, allowed tools) + workflow references
- Key files: Each command is one file referencing a workflow
- Installed to: `~/.claude/commands/gfd/` (via install.sh)

**docs/features/**
- Purpose: Per-project planning and analysis artifacts
- Contains: PROJECT.md (scope), STATE.md (status), feature directories, config
- Key files: `PROJECT.md`, `STATE.md`, `config.json` (project-level); `{slug}/FEATURE.md`, `{slug}/PLAN.md`, `{slug}/SUMMARY.md` (per-feature)
- Generated: Newly created by commands, not pre-existing
- NOT installed: Lives in user's project, committed to git

**docs/features/codebase/**
- Purpose: Codebase analysis output (created once via `/gfd:map-codebase`)
- Contains: 7 structured documents analyzing tech stack, architecture, conventions, testing, concerns
- Key files: STACK.md, ARCHITECTURE.md, STRUCTURE.md, CONVENTIONS.md, TESTING.md, INTEGRATIONS.md, CONCERNS.md
- Generated: By gfd-codebase-mapper agents
- Updated: User can re-run map-codebase to refresh if codebase changes significantly

**get-features-done/bin/**
- Purpose: CLI utilities for workflow orchestration
- Contains: gfd-tools.cjs (Node.js tool for config/state management)
- Key functions: `init`, `commit`, `task-checkpoint`, frontmatter parsing, git operations
- Installed to: `~/.claude/get-features-done/bin/` (via install.sh)

**get-features-done/references/**
- Purpose: Style and behavior guidelines for agents
- Contains: UI branding, questioning approach, git integration patterns
- Used by: Agents to maintain consistent user experience
- Installed to: `~/.claude/get-features-done/references/` (via install.sh)

**get-features-done/templates/**
- Purpose: Boilerplate documents for consistency
- Contains: Markdown templates with placeholders for PROJECT.md, FEATURE.md, STATE.md, analysis docs
- Used by: Agents when creating project artifacts
- Installed to: `~/.claude/get-features-done/templates/` (via install.sh)

**get-features-done/workflows/**
- Purpose: Detailed orchestration procedures for multi-step workflows
- Contains: Step-by-step instructions for command execution (agent spawning, state transitions, verification)
- Key patterns: Sequential steps with decision logic, parallel task execution via Task tool
- Installed to: `~/.claude/get-features-done/workflows/` (via install.sh)

## Key File Locations

**Project Initialization:**
- `docs/features/PROJECT.md`: Project context (what it is, core value, requirements, constraints, decisions)
- `docs/features/STATE.md`: Current execution state (active feature, progress, blockers)
- `docs/features/config.json`: Configuration (model profile, workflow gates, parallelization settings)

**Codebase Analysis:**
- `docs/features/codebase/STACK.md`: Technologies, languages, frameworks, dependencies
- `docs/features/codebase/ARCHITECTURE.md`: System patterns, layers, entry points, data flow
- `docs/features/codebase/STRUCTURE.md`: Directory layout, naming conventions, file locations
- `docs/features/codebase/CONVENTIONS.md`: Code style, naming patterns, error handling, logging
- `docs/features/codebase/TESTING.md`: Test frameworks, structure, mocking, coverage patterns
- `docs/features/codebase/INTEGRATIONS.md`: External APIs, databases, auth providers, webhooks
- `docs/features/codebase/CONCERNS.md`: Tech debt, known bugs, security issues, performance bottlenecks

**Feature Execution:**
- `docs/features/{slug}/FEATURE.md`: Feature definition (acceptance criteria, locked decisions)
- `docs/features/{slug}/PLAN.md`: Implementation plan (tasks, dependencies, verification criteria)
- `docs/features/{slug}/SUMMARY.md`: Execution results (completed tasks, commits, deviations)

**Toolkit Configuration:**
- `get-features-done/templates/*/`: Document templates (reference only, copied to projects)
- `get-features-done/workflows/*/`: Orchestration procedures
- `get-features-done/bin/gfd-tools.cjs`: CLI utilities
- `get-features-done/references/*/`: Style guidelines

## Naming Conventions

**Files:**
- Project-level docs: `PROJECT.md`, `STATE.md`, `REQUIREMENTS.md`, `config.json` (UPPERCASE for markdown)
- Feature-level docs: `FEATURE.md`, `PLAN.md`, `SUMMARY.md` (UPPERCASE)
- Codebase analysis: `STACK.md`, `ARCHITECTURE.md`, `STRUCTURE.md`, `CONVENTIONS.md`, `TESTING.md`, `INTEGRATIONS.md`, `CONCERNS.md` (UPPERCASE)
- Commands: lowercase dash-separated (e.g., `new-project.md`, `plan-feature.md`)
- Agents: lowercase dash-separated with `gfd-` prefix (e.g., `gfd-planner.md`)
- Workflows: lowercase dash-separated matching command names (e.g., `plan-feature.md`)

**Directories:**
- Feature directories: lowercase slug format matching feature name (e.g., `user-auth/`, `dark-mode-theme/`)
- Tool directories: lowercase (e.g., `bin/`, `templates/`, `workflows/`, `references/`)
- Analysis directories: lowercase scoped (e.g., `docs/features/codebase/`)

**Frontmatter Fields:**
- Status values: `backlog`, `planning`, `planned`, `in-progress`, `done`
- Priority values: `critical`, `high`, `medium`, `low`
- Agent names: `gfd-planner`, `gfd-executor`, `gfd-researcher`, `gfd-verifier`, `gfd-codebase-mapper`
- Command names: `gfd:new-project`, `gfd:new-feature`, `gfd:plan-feature`, `gfd:execute-feature`, `gfd:progress`, `gfd:map-codebase`

## Where to Add New Code

**New Feature:**
1. Create `docs/features/{slug}/FEATURE.md` (using template from `get-features-done/templates/feature.md`)
2. Create `docs/features/{slug}/` directory
3. During planning: add `docs/features/{slug}/PLAN.md`
4. During execution: add `docs/features/{slug}/SUMMARY.md`

**New Workflow/Command:**
1. Create `commands/gfd/{name}.md` with command definition
2. Create `get-features-done/workflows/{name}.md` with orchestration procedure
3. Update references in agent definitions if needed
4. Run `install.sh` to create symlinks

**New Agent Type:**
1. Create `agents/gfd-{name}.md` with agent definition
2. Include role, responsibilities, process steps
3. Reference in command definitions that use this agent
4. Add model profile mappings in `get-features-done/bin/gfd-tools.cjs`

**New Project:**
1. Run `/gfd:new-project` command to initialize
2. Automatically creates `docs/features/PROJECT.md`, `STATE.md`, `config.json`
3. Optional: Run `/gfd:map-codebase` to analyze existing codebase

**Codebase Analysis Updates:**
1. Run `/gfd:map-codebase` (spawns 4 parallel agents, creates 7 documents in `docs/features/codebase/`)
2. Documents automatically overwritten if re-running
3. No manual editing needed unless documenting non-code concerns

**New Reference/Guideline:**
1. Create `get-features-done/references/{name}.md`
2. Include in agent context via @-references in their definitions
3. Run `install.sh` to propagate to `~/.claude/`

## Special Directories

**docs/features/codebase/**
- Purpose: Codebase analysis output
- Generated: Yes (by map-codebase command)
- Committed: Yes (to version control)
- User edits: No (regenerated each mapping)
- Consumed by: plan-feature and execute-feature commands (loads relevant docs for feature planning)

**~/.claude/commands/gfd/**
- Purpose: Symlinked command definitions (operational)
- Generated: No (created by install.sh from commands/gfd/)
- Committed: No (symlink target, not contents)
- User edits: No (edit in source repo, reinstall)
- Installed by: `install.sh` script

**~/.claude/agents/**
- Purpose: Symlinked agent definitions (operational)
- Generated: No (created by install.sh from agents/)
- Committed: No (symlink target, not contents)
- User edits: No (edit in source repo, reinstall)
- Installed by: `install.sh` script

**~/.claude/get-features-done/**
- Purpose: Symlinked toolkit (operational)
- Generated: No (created by install.sh from get-features-done/)
- Committed: No (symlink target, not contents)
- User edits: No (edit in source repo, reinstall)
- Installed by: `install.sh` script

**docs/features/ (project-specific)**
- Purpose: Project planning artifacts
- Generated: Yes (by new-project, new-feature, plan-feature, execute-feature, map-codebase commands)
- Committed: Yes (to project git)
- User edits: Yes (e.g., updating PROJECT.md, reviewing FEATURE.md)
- Conflicts: Minimal because each feature is feature-scoped (different slugs = different directories)
