---
name: Auto Skills
slug: auto-skills
status: planned
owner: Conroy
assignees: []
created: 2026-02-20
priority: medium
depends_on: []
---
# Auto Skills

## Description

Add `auto-research` and `auto-plan` commands to the gfd-tools C# CLI that run the GFD research and planning workflows fully autonomously via `claude -p`. These commands assemble the workflow prompt, invoke Claude Code in headless mode with appropriate flags (`--allowedTools`, `--max-turns`), and handle the full lifecycle — including status transitions, git commits, and abort handling — without any user interaction.

## Acceptance Criteria

- [ ] `gfd-tools auto-research <slug>` runs the research workflow headlessly and produces RESEARCH.md
- [ ] `gfd-tools auto-plan <slug>` runs the planning workflow headlessly and produces PLAN.md files
- [ ] Both commands abort cleanly on ambiguous decision points (e.g., feature already researched, plans already exist, checker fails 3x) without making destructive choices
- [ ] On abort, partial progress is discarded but an AUTO-RUN.md status file is committed explaining what happened
- [ ] On success, normal artifacts are committed plus an AUTO-RUN.md summarizing the run (duration, what was produced)
- [ ] Max-turns is configurable (with a sensible default) to prevent runaway token spend
- [ ] No `AskUserQuestion` calls in auto workflows — all interaction stripped, decisions logged to status file

## Tasks

- [01-PLAN.md](01-PLAN.md) — ClaudeService: headless subprocess infrastructure (InvokeHeadless, RunResult, BuildAutoRunMd)
- [02-PLAN.md](02-PLAN.md) — AutoResearchCommand + AutoPlanCommand + Program.cs registration

## Notes

### Implementation Decisions
- **Interaction handling:** Abort on ambiguity — never silently choose for the user
- **Abort communication:** Commit an AUTO-RUN.md status file + descriptive commit message
- **Structure:** Standalone commands in gfd-tools C# CLI (`auto-research`, `auto-plan`)
- **Invocation:** gfd-tools handles the full `claude -p` invocation internally
- **Failed plans:** Discard — only AUTO-RUN.md committed on failure
- **Cost guard:** Configurable `--max-turns` with sensible default

### Claude's Discretion
- None — user specified all key decisions

### Deferred Ideas
- None

## Decisions

[Key decisions made during planning and execution of this feature.]

## Blockers

[Active blockers affecting this feature. Remove when resolved.]

---
*Created: 2026-02-20*
*Last updated: 2026-02-20*
