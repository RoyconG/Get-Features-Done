# GFD — Get Features Done

A structured feature delivery toolkit for [Claude Code](https://docs.anthropic.com/en/docs/claude-code). GFD provides slash commands, specialized agents, and workflow definitions that guide Claude through a disciplined process: define features, research approaches, plan implementation, execute with atomic commits, and verify results.

## What's Inside

| Directory    | Contents                                         |
|-------------|--------------------------------------------------|
| `commands/` | Slash command definitions (`/gfd:*`)             |
| `agents/`   | Specialized agent definitions (planner, executor, researcher, verifier, codebase-mapper) |
| `workflows/`| Detailed workflow instructions for each command   |
| `references/`| Reference docs (git integration, questioning style, UI branding) |
| `templates/` | Document templates (FEATURE.md, PROJECT.md, state, config, codebase analysis) |
| `bin/`      | CLI tool (`gfd-tools.cjs`)                       |

## Installation

```bash
git clone <repo-url> ~/Projects/GFD
cd ~/Projects/GFD
./install.sh
```

The install script creates symlinks from `~/.claude/` into this repo:

- `~/.claude/get-features-done/` -> repo root (bin, templates, workflows, references)
- `~/.claude/commands/gfd/` -> `commands/`
- `~/.claude/agents/gfd-*.md` -> individual agent files

If you already have files in those locations, they'll be backed up with a `.bak` suffix.

## Usage

After installation, the following slash commands are available in Claude Code:

| Command | Description |
|---------|-------------|
| `/gfd:new-project` | Initialize a project with `docs/features/` and `PROJECT.md` |
| `/gfd:new-feature` | Define a new feature with `FEATURE.md` |
| `/gfd:plan-feature` | Create detailed implementation plans for a feature |
| `/gfd:execute-feature` | Execute plans with atomic commits |
| `/gfd:progress` | Check project status and route to next action |
| `/gfd:map-codebase` | Analyze codebase with parallel mapper agents |

## Workflow

1. **`/gfd:new-project`** — Set up project structure and define goals
2. **`/gfd:new-feature`** — Define a feature with acceptance criteria
3. **`/gfd:plan-feature`** — Research the approach, then create a step-by-step plan
4. **`/gfd:execute-feature`** — Execute the plan with atomic commits and verification
5. **`/gfd:progress`** — Track what's done, what's next

## Configuration

Feature-level config lives in `docs/features/config.json` (created per-project from `templates/config.json`). Controls workflow gates, parallelization, and safety settings.

## Version

See `VERSION` for the current release.
