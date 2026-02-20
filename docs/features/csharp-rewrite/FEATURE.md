---
name: C# Rewrite
slug: csharp-rewrite
status: discussed
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

- [ ] C# console app builds and runs via `dotnet run`
- [ ] All actively-used gfd-tools commands ported (determined by grepping workflows)
- [ ] Output uses key=value pairs instead of JSON
- [ ] All GFD workflow and agent files updated to invoke C# tool and parse key=value output
- [ ] gfd-tools.cjs deleted
- [ ] Bugs found during porting are fixed, not replicated

## Tasks

[Populated during planning. Links to plan files.]

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

## Blockers

---
*Created: 2026-02-20*
*Last updated: 2026-02-20*
