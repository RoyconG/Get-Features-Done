---
name: C# Rewrite
slug: csharp-rewrite
status: done
owner: Conroy
assignees: []
created: 2026-02-20
priority: critical
depends_on: []
---
# C# Rewrite

## Description

Rewrite gfd-tools.cjs as a C# dotnet console app using System.CommandLine 2.0, targeting .NET 10. Output uses key=value pairs (one per line) instead of JSON to minimize token usage. Only actively-used commands are ported — determined by grepping workflow/agent files. The .cjs file is deleted once parity is confirmed and all workflow/agent files are updated to invoke the C# tool.

## Acceptance Criteria

- [x] C# console app builds and runs via `dotnet run`
- [x] All actively-used gfd-tools commands ported (determined by grepping workflows)
- [x] Output uses key=value pairs instead of JSON
- [x] All GFD workflow and agent files updated to invoke C# tool and parse key=value output
- [x] gfd-tools.cjs deleted
- [x] Bugs found during porting are fixed, not replicated

## Tasks

- [01-PLAN.md](01-PLAN.md) — Project scaffold, core services, first batch of commands
- [02-PLAN.md](02-PLAN.md) — Init commands, verify commands, history-digest, summary-extract, frontmatter validate
- [03-PLAN.md](03-PLAN.md) — Update all workflow and agent files, delete gfd-tools.cjs

## Notes

**Implementation Decisions:**
- Command parity: Grep workflows/agents to find active commands; skip dead code
- Verification: Claude's discretion on approach
- Project location: get-features-done/GfdTools/ (alongside current .cjs)
- Code layout: Single project — Commands/, Models/, Services/ folders
- .NET version: .NET 10
- Invocation: `dotnet run --project` (caches build after first run)
- Wrapper script: Claude's discretion on whether to use a shell wrapper for shorter invocations
- Output format: key=value pairs, one per line (not JSON — saves tokens)
- Lists in output: Claude's discretion (repeated keys vs comma-separated)
- Error reporting: Claude's discretion (stderr+exit code vs error= key)
- Bug handling: Fix bugs found during porting rather than replicating them

## Decisions

- Drop `commit` command — workflows use plain `git add` + `git commit` instead
- Drop `commit_docs` config option — docs are always committed
- Drop `feature add-decision` / `feature add-blocker` — Claude uses Edit tool directly
- Drop `frontmatter set` — `feature-update-status` covers the common case, Edit tool handles rest
- Drop `progress bar` and `init progress` — progress feature being dropped; delete progress.md workflow

## Blockers

---
*Created: 2026-02-20*
*Last updated: 2026-02-20 (completed)*
