---
feature: status
plan: 02
type: execute
wave: 2
depends_on: ["01"]
files_modified:
  - commands/gfd/status.md
  - get-features-done/workflows/status.md
autonomous: true
acceptance_criteria:
  - Running `/gfd:status` displays a Feature Name | Status table, excluding `done` features
  - When no active features exist, a helpful message is shown with a hint to create one
must_haves:
  truths:
    - "Running /gfd:status shows a table with Feature Name and Status columns"
    - "Done features are excluded from the status table"
    - "When no non-done features exist, a helpful empty-state message appears with /gfd:new-feature hint"
    - "Status values in the table are raw strings — no symbols, no formatting"
  artifacts:
    - path: "commands/gfd/status.md"
      provides: "Thin command file that delegates to status workflow"
      contains: "name: gfd:status"
    - path: "get-features-done/workflows/status.md"
      provides: "Status table rendering logic"
      contains: "list-features"
  key_links:
    - from: "commands/gfd/status.md"
      to: "get-features-done/workflows/status.md"
      via: "@reference in execution_context"
      pattern: "@.*workflows/status\\.md"
    - from: "status workflow"
      to: "gfd-tools.cjs list-features"
      via: "bash call"
      pattern: "list-features"
---

<objective>
Create the `/gfd:status` command that replaces `/gfd:progress` with a simple Feature Name | Status table.

Purpose: Users need a quick way to see what features are active. The new status command shows a plain table — no symbols, no progress bars, no routing suggestions. Excludes done features. Shows a helpful hint when empty.

Output: `commands/gfd/status.md` (thin command) and `get-features-done/workflows/status.md` (full workflow logic).
</objective>

<execution_context>
@/home/conroy/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/status/FEATURE.md
@docs/features/status/RESEARCH.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create the status command file</name>
  <files>commands/gfd/status.md</files>
  <action>
Create /var/home/conroy/Projects/GFD/commands/gfd/status.md as a thin command file following the existing GFD command pattern (same structure as commands/gfd/progress.md but updated):

```markdown
---
name: gfd:status
description: Show active feature status table
allowed-tools: Read, Bash, Grep, Glob
---

<objective>Show a plain table of all active features (excluding done) with their current status.</objective>

<execution_context>
@/home/conroy/.claude/get-features-done/workflows/status.md
</execution_context>

<process>Execute the status workflow.</process>
```

Note: No `argument-hint` needed (no arguments). `allowed-tools` is minimal since this is read-only display.
  </action>
  <verify>
```bash
cat /var/home/conroy/Projects/GFD/commands/gfd/status.md
```
File exists and contains `name: gfd:status` in frontmatter and an `@` reference to the status workflow.
  </verify>
  <done>
commands/gfd/status.md exists with correct frontmatter name and execution_context pointing to workflows/status.md
  </done>
</task>

<task type="auto">
  <name>Task 2: Create the status workflow</name>
  <files>get-features-done/workflows/status.md</files>
  <action>
Create /var/home/conroy/Projects/GFD/get-features-done/workflows/status.md with the following content:

```markdown
<purpose>
Display a plain table of all active features with their current lifecycle status. Excludes features with status "done". Shows a helpful message when no active features exist.
</purpose>

<process>

## 1. Load Features

```bash
FEATURES_RAW=$(node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs list-features)
```

Parse the JSON array from `features` key. Filter out any feature where `status` is `"done"`.

## 2. Render Status Table

**If active features exist (count > 0 after filtering):**

Output a plain markdown table:

```
| Feature Name | Status |
|--------------|--------|
| [name] | [status] |
| [name] | [status] |
```

Rules:
- `name` field from each feature object (human-readable name, not slug)
- `status` field as a raw string — no symbols, no emoji, no formatting
- Sort order: use the order returned by list-features (already sorted by priority + status)
- Do NOT include done features

**If no active features exist (all features done, or no features at all):**

```
No active features.

Run /gfd:new-feature <slug> to create your first feature.
```

## 3. Done

No routing, no next-step suggestions, no progress bars. Display only.

</process>

<success_criteria>

- [ ] list-features called to get feature data
- [ ] done features excluded from table
- [ ] Table shows Feature Name and Status columns with raw values
- [ ] Empty state message shown when no active features
- [ ] No symbols, no progress bar, no routing in output

</success_criteria>
```

Important: The code blocks within the workflow markdown must use triple backticks. Ensure the bash block at step 1 is properly formatted as a fenced code block.
  </action>
  <verify>
```bash
cat /var/home/conroy/Projects/GFD/get-features-done/workflows/status.md
```
File exists and contains: the list-features bash call, filtering logic for done features, a table rendering section, and the empty-state message with `/gfd:new-feature` hint.
  </verify>
  <done>
get-features-done/workflows/status.md exists with list-features call, done-filtering logic, plain table rendering, and empty-state message
  </done>
</task>

</tasks>

<verification>
```bash
# Confirm both files exist
ls /var/home/conroy/Projects/GFD/commands/gfd/status.md
ls /var/home/conroy/Projects/GFD/get-features-done/workflows/status.md

# Confirm command name
grep "name: gfd:status" /var/home/conroy/Projects/GFD/commands/gfd/status.md

# Confirm workflow references list-features
grep "list-features" /var/home/conroy/Projects/GFD/get-features-done/workflows/status.md

# Confirm no symbols in the table format description
grep -v "✓\|◆\|○\|⚠\|progress bar" /var/home/conroy/Projects/GFD/get-features-done/workflows/status.md | grep -c "Status"
```
</verification>

<success_criteria>
- commands/gfd/status.md exists with name "gfd:status"
- get-features-done/workflows/status.md exists and references list-features
- Workflow excludes done features from output
- Empty state shows /gfd:new-feature hint
- No symbols or formatting in the table — raw status values only
</success_criteria>

<output>
After completion, create `docs/features/status/02-SUMMARY.md` with what was built and any deviations.
</output>
