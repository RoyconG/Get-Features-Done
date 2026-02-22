---
name: gfd:plan-feature
description: Create detailed plans for a feature
argument-hint: <feature-slug>
allowed-tools: Read, Write, Edit, Bash, Grep, Glob, AskUserQuestion, Task, WebSearch, WebFetch
---

<objective>Create executable plans (PLAN.md files) for a feature with task breakdown and verification criteria.</objective>

<execution_context>
@$HOME/.claude/get-features-done/workflows/plan-feature.md
@$HOME/.claude/get-features-done/references/ui-brand.md
@$HOME/.claude/get-features-done/references/git-integration.md
@$HOME/.claude/agents/gfd-planner.md
@$HOME/.claude/agents/gfd-researcher.md
</execution_context>

<process>Execute the plan-feature workflow. Load feature context, optionally research, spawn planner agent, verify plan quality, and commit.</process>
