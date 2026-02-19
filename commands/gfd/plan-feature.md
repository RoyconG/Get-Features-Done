---
name: gfd:plan-feature
description: Create detailed plans for a feature
argument-hint: <feature-slug>
allowed-tools: Read, Write, Edit, Bash, Grep, Glob, AskUserQuestion, Task, WebSearch, WebFetch
---

<objective>Create executable plans (PLAN.md files) for a feature with task breakdown and verification criteria.</objective>

<execution_context>
@/home/conroy/.claude/get-features-done/workflows/plan-feature.md
@/home/conroy/.claude/get-features-done/references/ui-brand.md
@/home/conroy/.claude/get-features-done/references/git-integration.md
@/home/conroy/.claude/agents/gfd-planner.md
@/home/conroy/.claude/agents/gfd-researcher.md
</execution_context>

<process>Execute the plan-feature workflow. Load feature context, optionally research, spawn planner agent, verify plan quality, and commit.</process>
