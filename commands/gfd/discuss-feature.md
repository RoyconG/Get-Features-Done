---
name: gfd:discuss-feature
description: Refine feature scope through conversation
argument-hint: <feature-slug>
allowed-tools: Read, Write, Edit, Bash, Grep, Glob, AskUserQuestion
---

<objective>Deep conversation to refine feature scope. Transitions status from `new` → `discussing` → `discussed` and populates FEATURE.md with acceptance criteria, description, priority, dependencies, and notes.</objective>

<execution_context>
@/home/conroy/.claude/get-features-done/workflows/discuss-feature.md
@/home/conroy/.claude/get-features-done/references/ui-brand.md
@/home/conroy/.claude/get-features-done/references/questioning.md
</execution_context>

<process>Execute the discuss-feature workflow.</process>
