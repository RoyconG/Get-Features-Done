---
name: Auto Skills
slug: auto-skills
status: done
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

- [x] `gfd-tools auto-research <slug>` runs the research workflow headlessly and produces RESEARCH.md
- [x] `gfd-tools auto-plan <slug>` runs the planning workflow headlessly and produces PLAN.md files
- [x] Both commands abort cleanly on ambiguous decision points (e.g., feature already researched, plans already exist, checker fails 3x) without making destructive choices
- [x] On abort, partial progress is discarded but an AUTO-RUN.md status file is committed explaining what happened
- [x] On success, normal artifacts are committed plus an AUTO-RUN.md summarizing the run (duration, what was produced)
- [x] Max-turns is configurable (with a sensible default) to prevent runaway token spend
- [x] No `AskUserQuestion` calls in auto workflows — all interaction stripped, decisions logged to status file

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

### Plan 02 — AutoResearchCommand + AutoPlanCommand (2026-02-20)

- **Async SetAction pattern:** System.CommandLine beta5 does not accept `async pr => { return int; }` lambdas. Commands use `private static async Task<int> RunAsync(...)` extracted method registered via `cmd.SetAction((ParseResult pr, CancellationToken ct) => RunAsync(...))`.
- **CommitAutoRunMd duplication:** Kept as private static in each command class per existing self-contained command file pattern.
- **Abort cleanup for AutoPlanCommand:** Deletes partial PLAN.md files before committing AUTO-RUN.md on abort — ensures no partial artifacts are committed.
- **FEATURE.md status update inline:** Status updated via Regex.Replace directly in the command, not via subprocess call to gfd-tools feature-update-status.

### Plan 01 — ClaudeService (2026-02-20)

- **Prompt delivery via stdin:** Prompt passed via `process.StandardInput.Write()` + `Close()` rather than as a CLI argument. Avoids shell arg length limits for large research/plan prompts that include full FEATURE.md content and codebase docs.
- **Per-tool --allowedTools:** Each tool added with separate `ArgumentList.Add("--allowedTools")` + `ArgumentList.Add(tool)` pair, not comma-joined. Matches claude CLI's expected argument format.
- **Dual success signals:** `InvokeHeadless()` checks for both `## RESEARCH COMPLETE` and `## PLANNING COMPLETE` — single service handles both auto commands.
- **Max-turns dual detection:** Checks stderr for "max turns" (case-insensitive) AND stdout for "max-turns" to handle both signal paths documented in the research pitfalls.

## Blockers

[Active blockers affecting this feature. Remove when resolved.]

---
*Created: 2026-02-20*
*Last updated: 2026-02-20*
