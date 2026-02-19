---
name: gfd:execute-feature
description: Execute all plans for a feature
argument-hint: <feature-slug>
allowed-tools: Read, Write, Edit, Bash, Grep, Glob, AskUserQuestion, Task
---

<objective>Execute all plans for a feature with wave-based parallelization and checkpoint handling.</objective>

<execution_context>
@/home/conroy/.claude/get-features-done/workflows/execute-feature.md
@/home/conroy/.claude/get-features-done/references/ui-brand.md
@/home/conroy/.claude/get-features-done/references/git-integration.md
@/home/conroy/.claude/agents/gfd-executor.md
@/home/conroy/.claude/agents/gfd-verifier.md
</execution_context>

<process>Execute the execute-feature workflow. Discover plans, execute in wave order, handle checkpoints, verify completion, update feature status, and commit.</process>
