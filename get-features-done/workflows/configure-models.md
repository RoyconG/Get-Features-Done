# Configure Models Workflow

Interactive workflow for configuring which Claude model powers each GFD agent role. Presents a per-role selection UI with recommended models and warnings for under-powered choices.

## Overview

This workflow walks through all five GFD agent roles and lets the user select a model for each. It shows the current resolved model and recommended model per role, warns when a weak model is chosen for a demanding role, and persists selections to `docs/features/config.json` under the `model_overrides` key.

## Model Tiers

| Tier   | Model ID              | Input $/MTok | Output $/MTok | Best For                              |
|--------|-----------------------|-------------|---------------|---------------------------------------|
| haiku  | claude-haiku-4-5      | $1          | $5            | Fast structured tasks, pattern matching, verification |
| sonnet | claude-sonnet-4-6     | $3          | $15           | Balanced speed/quality, planning, code generation    |
| opus   | claude-opus-4-6       | $5          | $25           | Complex multi-step reasoning, deep analysis          |

## Agent Role Recommendations

| Role                | Recommended | Minimum | Warn If        |
|---------------------|-------------|---------|----------------|
| gfd-researcher      | sonnet      | haiku   | (no warning)   |
| gfd-planner         | sonnet      | sonnet  | haiku chosen   |
| gfd-executor        | sonnet      | sonnet  | haiku chosen   |
| gfd-verifier        | haiku       | haiku   | (no warning)   |
| gfd-codebase-mapper | haiku       | haiku   | (no warning)   |

**Warning threshold rationale:** gfd-planner and gfd-executor require complex reasoning and code generation — haiku may produce poor-quality plans and implementations. gfd-researcher, gfd-verifier, and gfd-codebase-mapper handle structured/pattern tasks where haiku performs well.

## Step 1: Load Current Configuration

Run the following commands to resolve the currently configured model for each role:

```bash
$HOME/.claude/get-features-done/bin/gfd-tools resolve-model gfd-researcher
$HOME/.claude/get-features-done/bin/gfd-tools resolve-model gfd-planner
$HOME/.claude/get-features-done/bin/gfd-tools resolve-model gfd-executor
$HOME/.claude/get-features-done/bin/gfd-tools resolve-model gfd-verifier
$HOME/.claude/get-features-done/bin/gfd-tools resolve-model gfd-codebase-mapper
```

Extract the resolved model from each output using: `grep "^model=" | cut -d= -f2-`

Also read `docs/features/config.json` to retrieve the current `model_profile` and any existing `model_overrides`.

Store the current resolved model for each role — you'll need these for the Step 5 summary table.

## Step 2: Offer Initial Choice

Use AskUserQuestion to present the initial menu:

**Question:** "GFD Model Configuration\n\nCurrent profile: [model_profile from config.json]\n\nWhat would you like to do?"

**Options:**
- "Configure individual agent roles — set a model for each of the 5 roles"
- "Clear all overrides — remove model_overrides and use profile defaults"
- "View current configuration — show resolved models for all roles"

**If "Clear all overrides":**
1. Read `docs/features/config.json`.
2. Parse the JSON. Remove the `model_overrides` key entirely (or set it to `{}`).
3. Write the complete updated JSON back to `docs/features/config.json` using the Write tool.
4. Run `gfd-tools resolve-model` for each of the 5 roles to confirm the reset.
5. Display: "All model overrides cleared. Roles now use [model_profile] profile defaults."
6. Exit — do not continue to Step 3.

**If "View current configuration":**
1. Display a table of all 5 roles with their current resolved model and the recommended model.
2. Exit — do not continue to Step 3.

**If "Configure individual agent roles":** Proceed to Step 3.

## Step 3: Configure Each Agent Role

Work through each of the 5 roles in order. For each role, use AskUserQuestion to present the selection.

### Role selection prompt format

Use AskUserQuestion with:

**Question (multi-line):**
```
Configure [role display name]
Current: [currently resolved model]  |  Recommended: [recommended model from table above]
```

**Options (exactly 4):**
1. `haiku — claude-haiku-4-5 — fastest, cheapest` (append ` (Recommended)` if recommended=haiku)
2. `sonnet — claude-sonnet-4-6 — balanced speed and quality` (append ` (Recommended)` if recommended=sonnet)
3. `opus — claude-opus-4-6 — most capable, highest cost` (append ` (Recommended)` if recommended=opus)
4. `Custom — enter a custom model ID string`

### Role display names

| Role key            | Display name          |
|---------------------|-----------------------|
| gfd-researcher      | GFD Researcher        |
| gfd-planner         | GFD Planner           |
| gfd-executor        | GFD Executor          |
| gfd-verifier        | GFD Verifier          |
| gfd-codebase-mapper | GFD Codebase Mapper   |

### Custom model entry

If user selects "Custom", use a follow-up AskUserQuestion:

**Question:** "Enter the custom model ID for [role display name]:\n(Example: claude-3-5-haiku-20241022, anthropic/claude-opus-4-6)"

**Options:** Provide at least one example option (e.g., `claude-haiku-4-5`) so the user understands the format. The "Other" free-text field in AskUserQuestion captures the actual custom value.

Store the custom model ID the user provides.

### Warning logic

After the user's selection is confirmed, check if it falls below the role's minimum:

**Strength ordering (weakest to strongest):** haiku < sonnet < opus

**Warning applies when:**
- Role is `gfd-planner` or `gfd-executor` AND chosen model resolves to `haiku` tier
- (gfd-researcher, gfd-verifier, gfd-codebase-mapper never warn)

**For custom models:** If the model string contains "haiku" and the role is gfd-planner or gfd-executor, apply the warning. Otherwise trust the user's choice without warning.

**Warning prompt (use AskUserQuestion):**

**Question:**
```
Warning: [role display name] minimum recommended model is sonnet.
Choosing haiku may result in poor-quality [plans/code] for this role's task complexity.

Proceed with [chosen model]?
```

**Options:**
- `Yes, use [chosen model] anyway`
- `No, let me choose again`

If "No, let me choose again": repeat the role selection for this role from the beginning (go back to the role selection prompt).

Store the final confirmed selection for each role.

## Step 4: Write Configuration

After all 5 roles are configured:

1. Read the current content of `docs/features/config.json`.
2. Parse it as JSON.
3. Set the `model_overrides` key to an object with all 5 role selections:
   ```json
   {
     "gfd-researcher": "[chosen model]",
     "gfd-planner": "[chosen model]",
     "gfd-executor": "[chosen model]",
     "gfd-verifier": "[chosen model]",
     "gfd-codebase-mapper": "[chosen model]"
   }
   ```
   Use tier aliases (`haiku`, `sonnet`, `opus`) for preset selections. Use the raw model ID string for custom selections.
4. Preserve all other existing keys in config.json (`model_profile`, `workflow`, `planning`, `parallelization`, `team`, `gates`, `safety`).
5. Write the complete updated JSON back to `docs/features/config.json` using the **Write tool** (not Bash — avoids shell quoting issues with JSON).
6. Verify by running:
   ```bash
   $HOME/.claude/get-features-done/bin/gfd-tools resolve-model gfd-researcher
   $HOME/.claude/get-features-done/bin/gfd-tools resolve-model gfd-planner
   $HOME/.claude/get-features-done/bin/gfd-tools resolve-model gfd-executor
   $HOME/.claude/get-features-done/bin/gfd-tools resolve-model gfd-verifier
   $HOME/.claude/get-features-done/bin/gfd-tools resolve-model gfd-codebase-mapper
   ```
   Each should return the model you just wrote.

## Step 5: Show Summary

Display a markdown summary table showing what changed:

```
Model configuration saved to docs/features/config.json

| Agent Role          | Previous Model | New Model | Changed |
|---------------------|----------------|-----------|---------|
| gfd-researcher      | [old]          | [new]     | [Yes/No] |
| gfd-planner         | [old]          | [new]     | [Yes/No] |
| gfd-executor        | [old]          | [new]     | [Yes/No] |
| gfd-verifier        | [old]          | [new]     | [Yes/No] |
| gfd-codebase-mapper | [old]          | [new]     | [Yes/No] |
```

Close with: "Run /gfd:configure-models again to update your model preferences."

## Important Notes

- **Write tool for config.json**: Always use the Write tool (not Bash echo/cat heredoc) to write JSON. Shell quoting is unreliable with nested JSON.
- **Always read before writing**: Read `docs/features/config.json` first, parse the full structure, update only `model_overrides`, then write the full content back. Never write a partial config.
- **model_overrides key names**: Use exact agent names — `gfd-researcher`, `gfd-planner`, `gfd-executor`, `gfd-verifier`, `gfd-codebase-mapper`.
- **Value format**: Tier alias (`haiku`, `sonnet`, `opus`) for preset choices; full model ID string for custom choices.
- **No subagents**: This workflow is fully interactive. Do not spawn Task agents. Use AskUserQuestion for all interactive prompts.
- **gfd-tools path**: Use `$HOME/.claude/get-features-done/bin/gfd-tools` (full path) or just `gfd-tools` if the binary is on PATH.
