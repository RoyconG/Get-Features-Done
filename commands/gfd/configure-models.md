---
name: gfd:configure-models
description: Configure which Claude model powers each GFD agent role
allowed-tools: Read, Write, Edit, Bash, AskUserQuestion
---

<objective>Interactively configure per-role model selection for all GFD agent roles. Shows current model, recommended model, and warns on weak choices. Persists to docs/features/config.json.</objective>

<execution_context>
@$HOME/.claude/get-features-done/workflows/configure-models.md
</execution_context>

<process>Execute the configure-models workflow. Load current model config, walk through each agent role with AskUserQuestion, warn on weak choices, and write model_overrides to docs/features/config.json.</process>
