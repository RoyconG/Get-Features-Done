# Technology Stack

**Analysis Date:** 2026-02-20

## Languages

**Primary:**
- JavaScript (ES2015+) - CLI tooling and configuration parsing
- Markdown - Agent definitions, workflows, commands, templates

**Secondary:**
- YAML - Frontmatter parsing in markdown documents
- JSON - Configuration storage and state management
- Bash - Installation scripts and git integration

## Runtime

**Environment:**
- Node.js v14+ (specified by shebang `#!/usr/bin/env node`)

**Package Manager:**
- No package manager dependencies
- Zero npm dependencies - tool is self-contained
- Lockfile: Not applicable

## Frameworks

**Core:**
- No external frameworks - native Node.js APIs only
- Native modules: `fs`, `path`, `child_process`

**Build/Dev:**
- No build system required - runs directly as Node.js script
- No transpilation needed

## Key Dependencies

**Critical:**
- None - GFD is a zero-dependency CLI tool
- Runs on native Node.js APIs: `fs.readFileSync()`, `path.join()`, `execSync()` for git operations

**Infrastructure:**
- Git (system-level CLI) - for version control operations via `execSync`
- Bash (system shell) - for installation symlinks

## Configuration

**Environment:**
- `docs/features/config.json` - Per-project configuration (see template at `get-features-done/templates/config.json`)
- Configuration keys: `model_profile`, `commit_docs`, `search_gitignored`, `research`, `plan_checker`, `verifier`, `parallelization`, `auto_advance`, `path_prefix`, `team`
- Default model profile: `balanced` (uses Sonnet for planning/execution, Haiku for mapping)

**Build:**
- No build configuration required
- Direct Node.js execution: `node /path/to/gfd-tools.cjs [command] [args]`

## Platform Requirements

**Development:**
- Node.js v14+ (installed system-wide)
- Git (system CLI)
- Bash shell
- Write access to `~/.claude/` directory (for installation symlinks)

**Production:**
- Node.js v14+ runtime
- Read access to project files
- Write access to `docs/features/` directory
- Git repository (`.git/` directory required for version control operations)

## Installation

**Method:** Symlink-based installation via `install.sh` script
- Symlinks `get-features-done/` directory into `~/.claude/get-features-done/`
- Symlinks `commands/gfd/` directory into `~/.claude/commands/gfd/`
- Symlinks individual agent `.md` files into `~/.claude/agents/`
- Backs up any existing files with `.bak` suffix before linking

**Tool Entry Point:**
- `~/.claude/get-features-done/bin/gfd-tools.cjs` - Main CLI tool
- Invoked via `node ~/.claude/get-features-done/bin/gfd-tools.cjs [command]`

## Version

- Current: 1.0.0 (from `get-features-done/VERSION`)

## Code Organization

**CLI Tool:**
- Location: `get-features-done/bin/gfd-tools.cjs` (1726 lines)
- Implements: frontmatter parsing, YAML parsing, git operations, config loading, state management, feature tracking
- Pure CommonJS - compatible with Node.js Edge runtime

**Agent Definitions:**
- Location: `agents/gfd-*.md` (5 agents)
  - `gfd-codebase-mapper.md` - Analyzes codebases, writes STACK/INTEGRATIONS/ARCHITECTURE/CONVENTIONS/CONCERNS docs
  - `gfd-planner.md` - Creates implementation plans with TDD strategies
  - `gfd-executor.md` - Executes plans with atomic git commits
  - `gfd-researcher.md` - Researches approaches and dependencies
  - `gfd-verifier.md` - Verifies implementation against acceptance criteria
- Format: Markdown with embedded instructions and role definitions
- Consumed by: Claude Code as agent prompts

**Workflows:**
- Location: `get-features-done/workflows/` (6 workflows)
- One workflow per command, detailed step-by-step instructions
- Markdown format with bash snippets for tool invocation

**Command Definitions:**
- Location: `commands/gfd/` (6 commands)
- Slash commands for Claude Code: `/gfd:new-project`, `/gfd:new-feature`, `/gfd:plan-feature`, `/gfd:execute-feature`, `/gfd:map-codebase`
- Markdown format with command metadata

**Templates:**
- Location: `get-features-done/templates/`
- Document templates for: `FEATURE.md`, `PROJECT.md`, `codebase/*.md` (STACK, INTEGRATIONS, ARCHITECTURE, CONVENTIONS, CONCERNS, STRUCTURE, TESTING)
- Configuration template: `config.json`
- Frontmatter schema templates for validation

---

*Stack analysis: 2026-02-20*
