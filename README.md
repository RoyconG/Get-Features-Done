# GFD — Get Features Done

A structured feature delivery toolkit for [Claude Code](https://docs.anthropic.com/en/docs/claude-code). GFD provides slash commands, specialized agents, and workflow definitions that guide Claude through a disciplined process: define features, research approaches, plan implementation, execute with atomic commits, and verify results.

## What's Inside

| Directory                      | Contents                                         |
|-------------------------------|--------------------------------------------------|
| `commands/gfd/`               | Slash command definitions (`/gfd:*`)             |
| `agents/`                     | Specialized agent definitions (planner, executor, researcher, verifier, codebase-mapper) |
| `get-features-done/workflows/`| Detailed workflow instructions for each command   |
| `get-features-done/references/`| Reference docs (git integration, questioning style, UI branding) |
| `get-features-done/templates/` | Document templates (FEATURE.md, PROJECT.md, state, config, codebase analysis) |
| `get-features-done/bin/`      | CLI tool (`gfd-tools`)                            |

## Prerequisites

- [Claude Code](https://docs.anthropic.com/en/docs/claude-code) installed and working
- [.NET 10](https://dotnet.microsoft.com/) — used by the `gfd-tools` CLI
- [Git](https://git-scm.com/) — GFD creates atomic commits throughout the workflow

## Installation

```bash
git clone <repo-url> ~/Projects/GFD
cd ~/Projects/GFD
./install.sh
```

The install script creates symlinks from `~/.claude/` into this repo:

- `~/.claude/get-features-done/` -> `get-features-done/` (bin, templates, workflows, references)
- `~/.claude/commands/gfd/` -> `commands/gfd/`
- `~/.claude/agents/gfd-*.md` -> individual agent files

If you already have files in those locations, they'll be backed up with a `.bak` suffix.

Verify the install:

```bash
~/.claude/get-features-done/bin/gfd-tools --help
```

## Quick Start

1. Open Claude Code in your project directory
2. Run `/gfd:new-project` to initialize — this creates `docs/features/` and `PROJECT.md`
3. Run `/gfd:new-feature my-feature` to create your first feature
4. Follow the prompts — each command tells you the next step

At any point, run `/gfd:status` to see where things stand and what to do next.

## Commands

| Command | Description |
|---------|-------------|
| `/gfd:new-project` | Initialize a project with `docs/features/` and `PROJECT.md` |
| `/gfd:new-feature` | Define a new feature (slug + one-liner) |
| `/gfd:discuss-feature` | Refine feature scope through conversation |
| `/gfd:research-feature` | Research implementation approach |
| `/gfd:plan-feature` | Create detailed implementation plans for a feature |
| `/gfd:execute-feature` | Execute plans with atomic commits |
| `/gfd:status` | Show active features with status and next steps |
| `/gfd:map-codebase` | Analyze codebase with parallel mapper agents |
| `/gfd:convert-from-gsd` | Migrate a GSD `.planning/` project to GFD structure |

## Workflow

1. **`/gfd:new-project`** — Set up project structure and define goals
2. **`/gfd:new-feature`** — Create a feature with a slug and one-line description
3. **`/gfd:discuss-feature`** — Refine scope and define acceptance criteria
4. **`/gfd:research-feature`** — Investigate implementation approach
5. **`/gfd:plan-feature`** — Create a step-by-step implementation plan
6. **`/gfd:execute-feature`** — Execute the plan with atomic commits and verification
7. **`/gfd:status`** — See active features and what to do next

## Feature Lifecycle

Each feature progresses through these states:

```
new → discussing → discussed → researching → researched → planning → planned → in-progress → done
```

| State | Set by | Next command |
|-------|--------|--------------|
| `new` | `/gfd:new-feature` | `/gfd:discuss-feature` |
| `discussing` | `/gfd:discuss-feature` | (continue discussion) |
| `discussed` | `/gfd:discuss-feature` | `/gfd:research-feature` |
| `researching` | `/gfd:research-feature` | (research in progress) |
| `researched` | `/gfd:research-feature` | `/gfd:plan-feature` |
| `planning` | `/gfd:plan-feature` | (planning in progress) |
| `planned` | `/gfd:plan-feature` | `/gfd:execute-feature` |
| `in-progress` | `/gfd:execute-feature` | (execution in progress) |
| `done` | `/gfd:execute-feature` | — |

## Configuration

Feature-level config lives in `docs/features/config.json` (created per-project from `templates/config.json`). Controls workflow gates, parallelization, and safety settings.

## Uninstall

Remove the symlinks created by the installer:

```bash
rm ~/.claude/get-features-done
rm ~/.claude/commands/gfd
rm ~/.claude/agents/gfd-*.md
```

If the installer backed up existing files, restore them by renaming `.bak` files back.

## Acknowledgments

GFD is a fork of [Get Shit Done (GSD)](https://github.com/cyanheads/get-shit-done) by [cyanheads](https://github.com/cyanheads). GSD pioneered the structured, agent-driven workflow for Claude Code — phased planning, atomic commits, specialized agents, and the entire discipline that makes AI-assisted development actually reliable. GFD builds on that foundation, reshaping it around a feature-centric model, but the core ideas and architecture come straight from GSD. Huge thanks to the GSD project and its contributors for the inspiration and the groundwork.

## Version

See `get-features-done/VERSION` for the current release.
