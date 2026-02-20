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

Rewrite gfd-tools.cjs as a C# dotnet console app using System.CommandLine 2.0. The tool is invoked by GFD agents and outputs JSON to stdout. Only actively-used commands will be ported — dead code in the .cjs is skipped. Once the C# version has full parity with active commands, the .cjs file is deleted and all workflow files are updated to use dotnet run.

## Acceptance Criteria

- [ ] C# console app builds and runs via `dotnet run`
- [ ] All actively-used gfd-tools commands are ported with identical JSON output
- [ ] All GFD workflow and agent files updated to invoke the C# tool instead of node
- [ ] gfd-tools.cjs is deleted
- [ ] Agents can invoke the tool and parse its JSON output without changes to their logic

## Tasks

[Populated during planning. Links to plan files.]

## Notes

Using System.CommandLine 2.0 (Microsoft first-party, GA Nov 2025). stdout = JSON for agent consumption, stderr = diagnostics. Target latest stable .NET.

## Decisions

## Blockers

---
*Created: 2026-02-20*
*Last updated: 2026-02-20*
