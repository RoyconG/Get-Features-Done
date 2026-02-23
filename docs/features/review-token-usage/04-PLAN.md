---
feature: review-token-usage
plan: 4
type: execute
wave: 1
depends_on: []
files_modified:
  - get-features-done/workflows/execute-feature.md
  - get-features-done/workflows/research-feature.md
  - get-features-done/workflows/plan-feature.md
autonomous: true
acceptance_criteria:
  - "Token usage summary (per agent role) displayed at the end of each major workflow (research, plan, execute)"
  - "Cumulative ## Token Usage section maintained in FEATURE.md across workflow runs"
must_haves:
  truths:
    - "After research-feature workflow completes, FEATURE.md has a new row in ## Token Usage for gfd-researcher with the model used and 'est.' cost"
    - "After plan-feature workflow completes, FEATURE.md has a new row for gfd-planner"
    - "After execute-feature workflow completes, FEATURE.md has a row for gfd-executor and one for gfd-verifier (if verifier ran)"
    - "If ## Token Usage section does not exist in FEATURE.md, the workflow creates it with the table header before appending the first row"
    - "The token usage writing step is near the end of each workflow, after the main work is committed"
  artifacts:
    - path: "get-features-done/workflows/execute-feature.md"
      provides: "Token usage reporting instructions at end of execute workflow"
      contains: "Token Usage"
    - path: "get-features-done/workflows/research-feature.md"
      provides: "Token usage reporting instructions at end of research workflow"
      contains: "Token Usage"
    - path: "get-features-done/workflows/plan-feature.md"
      provides: "Token usage reporting instructions at end of plan workflow"
      contains: "Token Usage"
  key_links:
    - from: "workflow orchestrator (end-of-workflow step)"
      to: "docs/features/<slug>/FEATURE.md ## Token Usage section"
      via: "Edit tool to read current content and append table row"
      pattern: "## Token Usage"
---

<objective>
Add token usage reporting instructions to the three major interactive workflow files (research, plan, execute). Each workflow orchestrator already knows which model it used (via config). After completing its main work, the orchestrator writes a token row to FEATURE.md.

Purpose: Provides best-effort token visibility for interactive workflow runs (where exact SDK token data is not surfaced to the parent agent). Complements Plan 02's exact data capture for headless auto-run paths.

Output: Three workflow files updated with a "Token Usage Reporting" step at the end.
</objective>

<execution_context>
@$HOME/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/review-token-usage/FEATURE.md
@docs/features/review-token-usage/RESEARCH.md
@docs/features/PROJECT.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Add token reporting to research-feature.md and plan-feature.md</name>
  <files>
    get-features-done/workflows/research-feature.md
    get-features-done/workflows/plan-feature.md
  </files>
  <action>
    Read both workflow files in full to understand their structure and identify where the final output/return step is. Then add a token reporting section.

    **Token reporting section to add** (adapt the wording to match each file's style):

    For research-feature.md, add a new section near the end — after the main research completion and commit step, but before the final structured return/output section:

    ```markdown
    ## Token Usage Reporting

    After research is complete and committed, append a token usage row to FEATURE.md:

    1. Determine the model used for the researcher agent:
       ```bash
       gfd-tools resolve-model gfd-researcher
       ```
       Extract: `grep "^model=" | cut -d= -f2-`

    2. Get today's date: `date +%Y-%m-%d`

    3. Read the current FEATURE.md content (docs/features/<slug>/FEATURE.md).

    4. Check if a `## Token Usage` section exists in FEATURE.md:
       - **If it exists:** append a new row to the table. Find the last row of the table and insert after it (before any next `##` section or end of file).
       - **If it does not exist:** append the full section at the end of the file.

    5. Row format:
       ```
       | research | <YYYY-MM-DD> | gfd-researcher | <model> | est. |
       ```

    6. New section format (when creating for the first time):
       ```markdown
       ## Token Usage

       | Workflow | Date | Agent Role | Model | Cost |
       |----------|------|------------|-------|------|
       | research | <YYYY-MM-DD> | gfd-researcher | <model> | est. |
       ```
       Note: Interactive workflow runs mark cost as `est.` (estimated) because exact token counts are not available from the Task tool return value. For headless auto-research runs, the C# AutoResearchCommand writes exact cost data.

    7. Use the Edit tool (preferred) or Write tool to update FEATURE.md with the new row.

    8. Commit the FEATURE.md update:
       ```bash
       git add docs/features/<slug>/FEATURE.md
       git commit -m "docs(<slug>): add research token usage"
       ```
    ```

    For plan-feature.md, add the equivalent section after planning is complete and committed. The row uses:
    - workflow: `plan`
    - agentRole: `gfd-planner`
    - model: resolved from `gfd-tools resolve-model gfd-planner`
    - cost: `est.`

    **Placement guidance:** In each workflow file, find the section that says something like "## Structured Return" or "## Output" or "## Next Steps" and insert the Token Usage Reporting section immediately BEFORE it. If the final section is a return-to-orchestrator statement, the token reporting should happen just before that return.

    If the workflow already has numbered steps (Step 1, Step 2, ...), add the token reporting as a new step with the next number, before the final "return" or "output" step.
  </action>
  <verify>
    ```bash
    grep -n "Token Usage" get-features-done/workflows/research-feature.md
    # Shows line number where section was added
    grep -n "Token Usage" get-features-done/workflows/plan-feature.md
    # Shows line number where section was added
    grep "gfd-researcher" get-features-done/workflows/research-feature.md | tail -5
    # Confirms researcher role referenced in token section
    grep "gfd-planner" get-features-done/workflows/plan-feature.md | tail -5
    # Confirms planner role referenced in token section
    ```
  </verify>
  <done>
    Both research-feature.md and plan-feature.md have a Token Usage Reporting section with instructions to resolve the model, build the row, check for existing section, append or create the ## Token Usage table, and commit. The section is placed after main work is committed, before any final output/return section.
  </done>
</task>

<task type="auto">
  <name>Task 2: Add token reporting to execute-feature.md</name>
  <files>
    get-features-done/workflows/execute-feature.md
  </files>
  <action>
    Read execute-feature.md in full to understand its structure. The execute workflow is more complex — it runs executor and optionally verifier agents. The token reporting step must account for both.

    Add a "Token Usage Reporting" section near the end of execute-feature.md, after all plans have been executed and before the final output/structured return section:

    ```markdown
    ## Token Usage Reporting

    After all plans have executed (and verifier has run, if enabled), append token usage rows to FEATURE.md.

    1. Resolve models for the agents that ran:
       ```bash
       gfd-tools resolve-model gfd-executor
       gfd-tools resolve-model gfd-verifier
       ```
       Extract each model: `grep "^model=" | cut -d= -f2-`

    2. Get today's date: `date +%Y-%m-%d`

    3. Read the current FEATURE.md content.

    4. Check if `## Token Usage` section exists:
       - If yes: append rows to the existing table.
       - If no: create the section with header.

    5. Rows to append (one per agent role that ran):
       ```
       | execute | <YYYY-MM-DD> | gfd-executor | <executor-model> | est. |
       ```
       If verifier ran (check workflow config — verifier is enabled unless explicitly disabled):
       ```
       | execute | <YYYY-MM-DD> | gfd-verifier | <verifier-model> | est. |
       ```

    6. Table format when creating new section:
       ```markdown
       ## Token Usage

       | Workflow | Date | Agent Role | Model | Cost |
       |----------|------|------------|-------|------|
       | execute | <YYYY-MM-DD> | gfd-executor | <model> | est. |
       | execute | <YYYY-MM-DD> | gfd-verifier | <model> | est. |
       ```

    7. Update FEATURE.md using Edit or Write tool.

    8. Commit:
       ```bash
       git add docs/features/<slug>/FEATURE.md
       git commit -m "docs(<slug>): add execute token usage"
       ```

    Note: These are `est.` (estimated) costs because interactive Task() tool calls do not reliably surface token counts to the parent orchestrator. For auto-plan runs (headless), the C# AutoPlanCommand writes exact cost data separately.
    ```

    **Placement:** Add this section after the verifier section and before the final "Structured Return" or "Next Steps" section. If execute-feature.md has a section called something like "## Completion" or "## Done", add Token Usage Reporting immediately before it.
  </action>
  <verify>
    ```bash
    grep -n "Token Usage" get-features-done/workflows/execute-feature.md
    # Shows line where section was added
    grep "gfd-executor" get-features-done/workflows/execute-feature.md | tail -3
    # Confirms executor role in token section
    grep "gfd-verifier" get-features-done/workflows/execute-feature.md | tail -3
    # Confirms verifier role in token section
    ```
  </verify>
  <done>
    execute-feature.md has a Token Usage Reporting section that covers both gfd-executor and gfd-verifier, placed after execution completes and before final output. The section includes resolve-model commands, row format for both roles, create-or-append logic, and a git commit step.
  </done>
</task>

</tasks>

<verification>
After both tasks complete:
1. All three workflow files have `## Token Usage` in their content (grep confirms)
2. research-feature.md references gfd-researcher in its token section
3. plan-feature.md references gfd-planner in its token section
4. execute-feature.md references both gfd-executor and gfd-verifier
5. All three have a commit step after writing the token row

Install the updated workflows so they take effect in the current Claude Code session:
```bash
cp get-features-done/workflows/execute-feature.md $HOME/.claude/get-features-done/workflows/execute-feature.md
cp get-features-done/workflows/research-feature.md $HOME/.claude/get-features-done/workflows/research-feature.md
cp get-features-done/workflows/plan-feature.md $HOME/.claude/get-features-done/workflows/plan-feature.md
```
</verification>

<success_criteria>
- execute-feature.md, research-feature.md, and plan-feature.md all contain Token Usage Reporting sections
- Each section instructs the orchestrator to resolve the appropriate model(s), build the row(s), and update FEATURE.md
- Create-if-missing logic is described for the ## Token Usage section
- Sections are positioned after main work (not before), so the token row is written only on success
- Updated files are copied to ~/.claude/get-features-done/workflows/ so they take effect immediately
</success_criteria>

<output>
After completion, create `docs/features/review-token-usage/04-SUMMARY.md` following the summary template.
</output>
