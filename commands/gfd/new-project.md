---
name: gfd:new-project
description: Initialize a new project with docs/features/ and PROJECT.md
allowed-tools: Read, Write, Edit, Bash, Grep, Glob, AskUserQuestion, Task
---

<objective>Initialize a new GFD project with deep context gathering and PROJECT.md creation.</objective>

<execution_context>
@$HOME/.claude/get-features-done/workflows/new-project.md
@$HOME/.claude/get-features-done/references/ui-brand.md
@$HOME/.claude/get-features-done/references/questioning.md
@$HOME/.claude/get-features-done/templates/project.md
@$HOME/.claude/get-features-done/templates/state.md
@$HOME/.claude/get-features-done/templates/config.json
@$HOME/.claude/get-features-done/templates/requirements.md
</execution_context>

<process>Execute the new-project workflow end-to-end. Guide the user through project setup with deep questioning, create all planning artifacts in docs/features/, and commit.</process>
