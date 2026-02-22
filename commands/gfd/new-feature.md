---
name: gfd:new-feature
description: Create a new feature with FEATURE.md
argument-hint: <feature-slug>
allowed-tools: Read, Write, Edit, Bash, Grep, Glob, AskUserQuestion
---

<objective>Create a new feature definition with FEATURE.md in docs/features/<slug>/.</objective>

<execution_context>
@$HOME/.claude/get-features-done/workflows/new-feature.md
@$HOME/.claude/get-features-done/references/ui-brand.md
@$HOME/.claude/get-features-done/references/questioning.md
@$HOME/.claude/get-features-done/templates/feature.md
</execution_context>

<process>Execute the new-feature workflow. Validate the slug, gather feature details from the user, create FEATURE.md, and commit.</process>
