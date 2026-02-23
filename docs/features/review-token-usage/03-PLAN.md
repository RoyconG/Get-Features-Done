---
feature: review-token-usage
plan: 3
type: execute
wave: 2
depends_on: [1]
files_modified:
  - commands/gfd/configure-models.md
  - get-features-done/workflows/configure-models.md
autonomous: true
acceptance_criteria:
  - "New /gfd:configure-models command walks users through each agent role, showing Claude family models as options plus free text for custom models"
  - "Each agent role shows the recommended model during selection, with warnings if a potentially too-weak model is chosen"
  - "Model preferences persisted in GFD config file and respected by all workflows"
must_haves:
  truths:
    - "Running /gfd:configure-models presents selection for each of the 5 agent roles (researcher, planner, executor, verifier, codebase-mapper)"
    - "Each role selection shows the current model, the recommended model, and 4 options (haiku/sonnet/opus/custom)"
    - "Selecting haiku for gfd-executor or gfd-planner triggers a visible warning before accepting the choice"
    - "After configure-models completes, docs/features/config.json has a model_overrides object with user selections"
    - "gfd-tools resolve-model <role> returns the configured model after configure-models writes config.json"
    - "User can clear all overrides to reset to profile defaults"
  artifacts:
    - path: "commands/gfd/configure-models.md"
      provides: "Command entry point — /gfd:configure-models slash command"
      contains: "name: gfd:configure-models"
    - path: "get-features-done/workflows/configure-models.md"
      provides: "Interactive workflow for per-role model selection"
      min_lines: 80
  key_links:
    - from: "configure-models workflow"
      to: "docs/features/config.json model_overrides"
      via: "Write tool with full merged JSON"
      pattern: "model_overrides"
    - from: "configure-models workflow"
      to: "gfd-tools resolve-model"
      via: "Bash verification after writing config.json"
      pattern: "resolve-model"
---

<objective>
Create the /gfd:configure-models command and its workflow. This provides an interactive UI for users to configure which Claude model powers each GFD agent role, with recommendations drawn from the audit document and warnings for under-powered choices.

Purpose: Gives users direct control over model selection without editing config.json manually. Uses the ModelOverrides config extension from Plan 01.

Output: commands/gfd/configure-models.md and get-features-done/workflows/configure-models.md.
</objective>

<execution_context>
@$HOME/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/review-token-usage/FEATURE.md
@docs/features/review-token-usage/RESEARCH.md
@docs/features/PROJECT.md
@commands/gfd/plan-feature.md
@commands/gfd/new-feature.md
@get-features-done/workflows/new-feature.md
@docs/token-audit.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create commands/gfd/configure-models.md command entry point</name>
  <files>
    commands/gfd/configure-models.md
  </files>
  <action>
    Read commands/gfd/plan-feature.md and commands/gfd/new-feature.md to understand the exact command file pattern used in this project.

    Create commands/gfd/configure-models.md following that pattern:

    ```markdown
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
    ```

    The allowed-tools list must include AskUserQuestion (for interactive prompts) and Write (to update config.json). Read and Bash are needed to read config and run gfd-tools. Do NOT include Task tool — this workflow is fully interactive and does not spawn subagents.
  </action>
  <verify>
    ```bash
    head -10 commands/gfd/configure-models.md
    # Shows YAML frontmatter with name: gfd:configure-models
    grep "AskUserQuestion" commands/gfd/configure-models.md
    # Confirms tool is listed
    grep "configure-models.md" commands/gfd/configure-models.md
    # Confirms workflow reference
    ```
  </verify>
  <done>
    commands/gfd/configure-models.md exists with correct frontmatter (name: gfd:configure-models), allowed-tools including AskUserQuestion and Write, and an execution_context referencing the workflow file.
  </done>
</task>

<task type="auto">
  <name>Task 2: Create get-features-done/workflows/configure-models.md workflow</name>
  <files>
    get-features-done/workflows/configure-models.md
  </files>
  <action>
    Create the workflow file. It is a markdown prompt that instructs the Claude agent (which has AskUserQuestion access) to walk through model configuration interactively. Write it as a complete, self-contained workflow document.

    The workflow must include these sections in order:

    **## Overview**
    Brief description of what this workflow does.

    **## Model Tiers**
    Table showing the 3 Claude model tiers with IDs and use cases:
    | Tier | Model ID | Input $/MTok | Output $/MTok | Best For |
    - haiku: claude-haiku-4-5, $1/$5, fast structured tasks
    - sonnet: claude-sonnet-4-6, $3/$15, balanced speed/quality
    - opus: claude-opus-4-6, $5/$25, complex multi-step reasoning

    **## Agent Role Recommendations**
    Table with recommended and minimum model per role:
    | Role | Recommended | Minimum | Warning Threshold |
    - gfd-researcher: recommended=sonnet, minimum=haiku, warn-if=anything weaker than haiku (no warning for haiku, warn for nothing — haiku is the minimum)
    - gfd-planner: recommended=sonnet, minimum=sonnet, warn-if=haiku
    - gfd-executor: recommended=sonnet, minimum=sonnet, warn-if=haiku
    - gfd-verifier: recommended=haiku, minimum=haiku, warn-if=nothing (haiku is fine)
    - gfd-codebase-mapper: recommended=haiku, minimum=haiku, warn-if=nothing

    **## Step 1: Load Current Configuration**
    Instructions to run:
    ```bash
    gfd-tools resolve-model gfd-researcher
    gfd-tools resolve-model gfd-planner
    gfd-tools resolve-model gfd-executor
    gfd-tools resolve-model gfd-verifier
    gfd-tools resolve-model gfd-codebase-mapper
    ```
    Extract the `model=` value from each output (grep "^model=" | cut -d= -f2-).
    Also read docs/features/config.json to get the current model_profile.

    **## Step 2: Offer Initial Choice**
    Use AskUserQuestion to ask:
    "What would you like to do?"
    Options:
    - "Configure individual agent roles" — proceed to role-by-role selection
    - "Clear all overrides (use profile defaults)" — remove model_overrides from config.json and exit
    - "View current configuration" — show current resolved models and exit

    If "Clear all overrides": read config.json, remove or empty the model_overrides key, write config.json back, confirm with message, exit.
    If "View current": show a table of all 5 roles with their current resolved model and recommended model, exit.

    **## Step 3: Configure Each Agent Role**
    For each of the 5 roles in order (researcher, planner, executor, verifier, codebase-mapper):

    Use AskUserQuestion with:
    - Question: "Configure [role name]\nCurrent: [current model] | Recommended: [recommended]"
    - Options (4 max):
      - "haiku — claude-haiku-4-5 — fastest, cheapest" (add "(Recommended)" if recommended=haiku)
      - "sonnet — claude-sonnet-4-6 — balanced" (add "(Recommended)" if recommended=sonnet)
      - "opus — claude-opus-4-6 — most capable, most expensive" (add "(Recommended)" if recommended=opus)
      - "Custom — enter a custom model string"

    If user selects "Custom", use a follow-up AskUserQuestion asking for the custom model ID (free text via "Other" option which the UI provides automatically, OR by presenting a single-option question that prompts for text).

    **Warning logic**: After the user makes a selection, check if the chosen model is weaker than the role's minimum:
    - Model strength order (weakest to strongest): haiku < sonnet < opus
    - If chosen model is below minimum: use AskUserQuestion to warn:
      "Warning: [role] minimum is [minimum]. [chosen model] may produce poor results for this role's task complexity. Proceed with [chosen model]?"
      Options: "Yes, use [chosen model] anyway" | "No, let me choose again"
      If "No": repeat the role selection for this role.

    Store the final selection for each role.

    **## Step 4: Write Configuration**
    After all 5 roles are configured:
    1. Read current docs/features/config.json content.
    2. Parse as JSON. Update or create the `model_overrides` key with all 5 role selections. Only include roles where the user's selection differs from what they started with (to keep config clean) — OR include all 5 for clarity. Prefer including all 5 so the config is self-documenting.
    3. Write the complete updated JSON back to docs/features/config.json. Preserve all existing keys (model_profile, workflow, planning, parallelization, team, gates, safety). Only update/add model_overrides.
    4. Show confirmation: run `gfd-tools resolve-model <role>` for each role and display the results in a table.

    **## Step 5: Show Summary**
    Display a markdown table:
    | Agent Role | Previous | New | Change |
    |------------|----------|-----|--------|
    (for each role, show old resolved model, new model, and whether it changed)

    Tell user: "Model overrides saved to docs/features/config.json. Run /gfd:configure-models again to change."

    **## Important Notes**
    - The Write tool writes the complete file, not a patch. Always read config.json first, parse the full JSON structure, update only model_overrides, then write the full content back.
    - Do not use Bash to write config.json (avoid shell quoting issues with JSON). Use the Write tool.
    - model_overrides keys must be exact agent names: gfd-researcher, gfd-planner, gfd-executor, gfd-verifier, gfd-codebase-mapper.
    - Values must be the tier alias (haiku/sonnet/opus) or a full model ID string for custom models.
  </action>
  <verify>
    ```bash
    wc -l get-features-done/workflows/configure-models.md
    # 80+ lines
    grep -c "AskUserQuestion" get-features-done/workflows/configure-models.md
    # 3+ (one per major interactive step)
    grep "model_overrides" get-features-done/workflows/configure-models.md
    # Shows config write instructions
    grep "gfd-executor\|gfd-planner\|gfd-researcher\|gfd-verifier\|gfd-codebase-mapper" \
      get-features-done/workflows/configure-models.md | wc -l
    # 5+ lines mentioning all agent roles
    ```
  </verify>
  <done>
    get-features-done/workflows/configure-models.md exists, is 80+ lines, includes instructions for all 5 agent roles, AskUserQuestion prompts, warning logic for weak model choices, config.json write instructions, and a clear-all-overrides option.
  </done>
</task>

</tasks>

<verification>
After both tasks complete:
1. commands/gfd/configure-models.md exists with correct frontmatter
2. get-features-done/workflows/configure-models.md exists with 80+ lines
3. All 5 agent roles are covered in the workflow
4. Warning logic is present for gfd-executor and gfd-planner (haiku threshold)
5. model_overrides write step is included
6. Clear-all-overrides option is included
</verification>

<success_criteria>
- /gfd:configure-models slash command is available (command file exists with correct name)
- Workflow presents all 5 roles with current + recommended model info
- Weak model selection triggers warning but allows user to proceed
- Config write step persists model_overrides to docs/features/config.json
- Workflow is self-contained — no spawned subagents, no external dependencies beyond gfd-tools CLI
</success_criteria>

<output>
After completion, create `docs/features/review-token-usage/03-SUMMARY.md` following the summary template.

Also copy the installed workflow file:
```bash
cp get-features-done/workflows/configure-models.md \
   $HOME/.claude/get-features-done/workflows/configure-models.md
```
</output>
