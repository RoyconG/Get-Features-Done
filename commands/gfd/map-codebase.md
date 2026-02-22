---
name: gfd:map-codebase
description: Analyze codebase with parallel mapper agents
allowed-tools: Read, Write, Bash, Grep, Glob, Task
---

<objective>Analyze the codebase and produce structured documentation in docs/features/codebase/.</objective>

<execution_context>
@$HOME/.claude/get-features-done/workflows/map-codebase.md
@$HOME/.claude/get-features-done/references/ui-brand.md
@$HOME/.claude/agents/gfd-codebase-mapper.md
</execution_context>

<process>Execute the map-codebase workflow. Spawn 4 parallel mapper agents (tech, arch, quality, concerns), verify documents, scan for secrets, and commit.</process>
