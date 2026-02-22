---
feature: cleanup-progress
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - commands/gfd/progress.md
  - get-features-done/workflows/convert-from-gsd.md
  - get-features-done/workflows/new-project.md
  - get-features-done/workflows/map-codebase.md
  - docs/features/codebase/ARCHITECTURE.md
  - docs/features/codebase/STACK.md
  - docs/features/codebase/STRUCTURE.md
autonomous: true
acceptance_criteria:
  - "Progress command handler deleted from C# tool (and JS if still present)"
  - "/gfd:progress skill and workflow file removed"
  - "All references to progress command removed from agent prompts and workflow files"
  - "Tests for the progress command removed"
  - "Any utilities used exclusively by the progress command removed"
  - "No remaining dead code or broken references after removal"

must_haves:
  truths:
    - "No /gfd:progress skill file exists in commands/gfd/"
    - "No /gfd:progress references remain in active workflow files"
    - "Codebase documentation no longer mentions progress.md or /gfd:progress"
    - "grep for gfd:progress across commands/ and workflows/ returns zero results"
  artifacts:
    - path: "commands/gfd/progress.md"
      provides: "DELETED — must not exist"
      absent: true
    - path: "get-features-done/workflows/convert-from-gsd.md"
      provides: "Updated workflow with /gfd:status replacing /gfd:progress"
      contains: "/gfd:status"
    - path: "get-features-done/workflows/new-project.md"
      provides: "Updated workflow with /gfd:status replacing /gfd:progress"
      contains: "/gfd:status"
    - path: "get-features-done/workflows/map-codebase.md"
      provides: "Updated workflow with /gfd:status replacing /gfd:progress"
      contains: "/gfd:status"
  key_links:
    - from: "commands/gfd/"
      to: "filesystem"
      via: "file deletion"
      pattern: "progress\\.md must not exist"
    - from: "workflow files"
      to: "/gfd:status"
      via: "text replacement"
      pattern: "gfd:progress replaced with gfd:status"
---

<objective>
Remove /gfd:progress entirely from the codebase: delete the slash command skill file, replace all live workflow references with /gfd:status, and update codebase documentation to remove stale entries.

Purpose: The progress command is redundant with /gfd:status. Full removal reduces surface area and eliminates broken references after the workflow file was already deleted in the csharp-rewrite feature.

Output: No skill file, no workflow references, no documentation entries for /gfd:progress or progress.md.
</objective>

<execution_context>
@/home/conroy/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/cleanup-progress/FEATURE.md
@docs/features/cleanup-progress/RESEARCH.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Delete /gfd:progress skill file</name>
  <files>commands/gfd/progress.md</files>
  <action>
    Delete the file `commands/gfd/progress.md`. This is the Claude Code slash command skill definition — the entry point for /gfd:progress. It still exists even though the backing workflow file (get-features-done/workflows/progress.md) and the C# ProgressCommand.cs were already deleted during the csharp-rewrite feature.

    Run: `rm commands/gfd/progress.md`

    Confirm deletion: `ls commands/gfd/progress.md 2>/dev/null && echo "EXISTS - ERROR" || echo "DELETED - OK"`

    Note: The workflow file get-features-done/workflows/progress.md is ALREADY deleted (confirmed in research). No action needed there.
  </action>
  <verify>
    `ls /var/home/conroy/Projects/GFD/commands/gfd/progress.md 2>/dev/null && echo "EXISTS - ERROR" || echo "DELETED - OK"` outputs "DELETED - OK"
  </verify>
  <done>commands/gfd/progress.md does not exist. ls of commands/gfd/ shows no progress.md entry.</done>
</task>

<task type="auto">
  <name>Task 2: Replace /gfd:progress with /gfd:status in active workflow files</name>
  <files>
    get-features-done/workflows/convert-from-gsd.md
    get-features-done/workflows/new-project.md
    get-features-done/workflows/map-codebase.md
  </files>
  <action>
    Replace all five occurrences of /gfd:progress references across three active workflow files. Use exact replacements as specified:

    **convert-from-gsd.md** (2 replacements):
    - Line ~31: Change `run /gfd:progress instead.` to `run /gfd:status instead.`
    - Line ~602: Change `` `/gfd:progress` `` to `` `/gfd:status` ``

    **new-project.md** (2 replacements):
    - Line ~32: Change `Run /gfd:progress to see current status.` to `Run /gfd:status to see current status.`
    - Line ~415: Change `- \`/gfd:progress\` — see overall project status` to `- \`/gfd:status\` — see overall project status`

    **map-codebase.md** (1 replacement):
    - Line ~390: Change `` `/gfd:progress` `` to `` `/gfd:status` ``

    Use the Edit tool (or sed) for each replacement. Do NOT change any other occurrences of the word "progress" that refer to things like "in-progress status" or "progress tracking" — only replace the slash command references `/gfd:progress`.

    After edits, verify no /gfd:progress remains:
    `grep -rn "gfd:progress" get-features-done/workflows/`
  </action>
  <verify>
    `grep -rn "gfd:progress" /var/home/conroy/Projects/GFD/get-features-done/workflows/` returns zero results.
  </verify>
  <done>All five /gfd:progress slash command references in convert-from-gsd.md, new-project.md, and map-codebase.md are replaced with /gfd:status. No /gfd:progress text remains in any workflow file.</done>
</task>

<task type="auto">
  <name>Task 3: Update codebase documentation to remove progress.md entries</name>
  <files>
    docs/features/codebase/ARCHITECTURE.md
    docs/features/codebase/STACK.md
    docs/features/codebase/STRUCTURE.md
  </files>
  <action>
    Update three codebase map documentation files to remove stale references to progress.md and /gfd:progress:

    **ARCHITECTURE.md** (2 changes):
    - Line ~143: Remove `progress.md` from the file list. The current line reads: `- Files: \`new-project.md\`, \`new-feature.md\`, \`plan-feature.md\`, \`execute-feature.md\`, \`progress.md\`, \`map-codebase.md\`` — remove the `, \`progress.md\`` portion.
    - Line ~151: Step 5 reads `5. \`/gfd:progress\` — Check status, determine next action` — replace with `5. \`/gfd:status\` — Check status, determine next action`

    **STACK.md** (1 change):
    - Line ~111: Remove `/gfd:progress` from the slash commands list. Current text: `Slash commands for Claude Code: \`/gfd:new-project\`, \`/gfd:new-feature\`, \`/gfd:plan-feature\`, \`/gfd:execute-feature\`, \`/gfd:map-codebase\`, \`/gfd:progress\`` — remove `, \`/gfd:progress\`` from the end.

    **STRUCTURE.md** (3 changes):
    - Line ~22: Remove `│       └── progress.md                 # Check project status, determine next action` — delete the entire line.
    - Line ~65: Remove `│   │   └── progress.md                 # Status check workflow` — delete the entire line.
    - Line ~169: Remove `gfd:progress` from the command names list. Current text lists command names including `gfd:progress` — remove it from the list.

    After edits, verify:
    `grep -n "gfd:progress\|progress\.md" docs/features/codebase/`
  </action>
  <verify>
    `grep -rn "gfd:progress\|progress\.md" /var/home/conroy/Projects/GFD/docs/features/codebase/` returns zero results (or only results about STATE.md progress tracking, which is unrelated).
  </verify>
  <done>ARCHITECTURE.md, STACK.md, and STRUCTURE.md contain no references to progress.md or /gfd:progress. The codebase documentation accurately reflects the current command set.</done>
</task>

</tasks>

<verification>
Run the full grep sweep to confirm zero remaining references:

```bash
# Must return zero results for the slash command
grep -rn "gfd:progress" /var/home/conroy/Projects/GFD/get-features-done/ /var/home/conroy/Projects/GFD/commands/ /var/home/conroy/Projects/GFD/docs/features/codebase/

# Must not exist
ls /var/home/conroy/Projects/GFD/commands/gfd/progress.md 2>/dev/null && echo "EXISTS - ERROR" || echo "DELETED - OK"

# Workflow file already gone — confirm
ls /var/home/conroy/Projects/GFD/get-features-done/workflows/progress.md 2>/dev/null && echo "EXISTS - ERROR" || echo "ALREADY DELETED - OK"

# No broken references to progress.md in codebase docs
grep -rn "progress\.md" /var/home/conroy/Projects/GFD/docs/features/codebase/
```

All checks must pass with zero results (except "DELETED - OK" messages).
</verification>

<success_criteria>
- commands/gfd/progress.md does not exist
- grep for "gfd:progress" across commands/, get-features-done/workflows/, and docs/features/codebase/ returns zero results
- grep for "progress.md" across docs/features/codebase/ returns zero results
- /gfd:status appears in each location where /gfd:progress was replaced
- No other files reference gfd:progress (spot-check with grep -rn "gfd:progress" across the whole repo)
</success_criteria>

<output>
After completion, create `docs/features/cleanup-progress/01-SUMMARY.md`
</output>
