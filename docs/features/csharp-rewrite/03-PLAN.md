---
feature: csharp-rewrite
plan: 03
type: execute
wave: 3
depends_on: ["01", "02"]
files_modified:
  - get-features-done/workflows/convert-from-gsd.md
  - get-features-done/workflows/discuss-feature.md
  - get-features-done/workflows/execute-feature.md
  - get-features-done/workflows/map-codebase.md
  - get-features-done/workflows/new-feature.md
  - get-features-done/workflows/new-project.md
  - get-features-done/workflows/plan-feature.md
  - get-features-done/workflows/research-feature.md
  - get-features-done/workflows/status.md
  - agents/gfd-executor.md
  - agents/gfd-planner.md
  - agents/gfd-researcher.md
  - agents/gfd-verifier.md
autonomous: true
acceptance_criteria:
  - "All GFD workflow and agent files updated to invoke C# tool and parse key=value output"
  - "gfd-tools.cjs deleted"

must_haves:
  truths:
    - "Every workflow file invokes dotnet run --project instead of node gfd-tools.cjs"
    - "Every agent file invokes dotnet run --project instead of node gfd-tools.cjs"
    - "Every workflow and agent parses key=value output instead of JSON"
    - "No workflow or agent references --raw or --include flags"
    - "gfd-tools.cjs no longer exists"
    - "All workflows and agents function correctly with the C# tool"
  artifacts:
    - path: "get-features-done/workflows/execute-feature.md"
      provides: "Updated execute-feature workflow"
      contains: "dotnet run --project"
    - path: "get-features-done/workflows/plan-feature.md"
      provides: "Updated plan-feature workflow"
      contains: "dotnet run --project"
    - path: "get-features-done/workflows/new-project.md"
      provides: "Updated new-project workflow"
      contains: "dotnet run --project"
    - path: "agents/gfd-executor.md"
      provides: "Updated executor agent"
      contains: "dotnet run --project"
    - path: "agents/gfd-planner.md"
      provides: "Updated planner agent"
      contains: "dotnet run --project"
    - path: "agents/gfd-researcher.md"
      provides: "Updated researcher agent"
      contains: "dotnet run --project"
    - path: "agents/gfd-verifier.md"
      provides: "Updated verifier agent"
      contains: "dotnet run --project"
  key_links:
    - from: "get-features-done/workflows/execute-feature.md"
      to: "get-features-done/GfdTools/Program.cs"
      via: "dotnet run --project invocation"
      pattern: "dotnet run --project"
    - from: "get-features-done/workflows/plan-feature.md"
      to: "get-features-done/GfdTools/Program.cs"
      via: "dotnet run --project invocation"
      pattern: "dotnet run --project"
    - from: "agents/gfd-executor.md"
      to: "get-features-done/GfdTools/Program.cs"
      via: "dotnet run --project invocation"
      pattern: "dotnet run --project"
    - from: "agents/gfd-planner.md"
      to: "get-features-done/GfdTools/Program.cs"
      via: "dotnet run --project invocation"
      pattern: "dotnet run --project"
---

<objective>
Update all 10 GFD workflow files and 4 agent files to invoke the C# tool instead of gfd-tools.cjs, parse key=value output instead of JSON, and delete gfd-tools.cjs.

Purpose: Complete the migration by switching all consumers to the new tool and removing the old one.
Output: All workflows and agents use the C# tool. gfd-tools.cjs is deleted.
</objective>

<execution_context>
@/home/conroy/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/csharp-rewrite/FEATURE.md
@docs/features/csharp-rewrite/RESEARCH.md
@docs/features/csharp-rewrite/01-SUMMARY.md
@docs/features/csharp-rewrite/02-SUMMARY.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Update all workflow files to use C# tool</name>
  <files>
    get-features-done/workflows/convert-from-gsd.md
    get-features-done/workflows/discuss-feature.md
    get-features-done/workflows/execute-feature.md
    get-features-done/workflows/map-codebase.md
    get-features-done/workflows/new-feature.md
    get-features-done/workflows/new-project.md
    get-features-done/workflows/plan-feature.md
    get-features-done/workflows/research-feature.md
    get-features-done/workflows/status.md
  </files>
  <action>
For each of the 9 remaining workflow files (progress.md is deleted, not updated), make these systematic changes:

**A. Replace tool invocation path:**
- Old: `node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs`
- New: `dotnet run --project /home/conroy/.claude/get-features-done/GfdTools/ --`
- The `--` separator is required before command arguments when using `dotnet run`.

**B. Replace JSON parsing instructions with key=value parsing:**

Old pattern in workflows:
```
INIT=$(node .../gfd-tools.cjs init plan-feature "${SLUG}")
Parse JSON for: planner_model, commit_docs, feature_dir, slug, ...
```

New pattern:
```
INIT=$(dotnet run --project /home/conroy/.claude/get-features-done/GfdTools/ -- init plan-feature "${SLUG}")
```
Then extract values with: `echo "$INIT" | grep "^key=" | cut -d= -f2-`

Where workflows say "Parse JSON for: key1, key2, key3", replace with instructions to extract key=value pairs. Example:
```
Extract from output: planner_model (grep "^planner_model=" | cut -d= -f2-), commit_docs, feature_dir, slug, ...
```

**C. Remove --raw flag usage:**
- Any `--raw` flags should be removed. The C# tool doesn't have --raw because key=value output makes it unnecessary.
- Where JS used `--raw` to get a scalar value, the C# tool outputs `key=value` and you extract with grep/cut.

**D. Remove --include flag usage:**
- Any `--include feature` or `--include config` flags should be removed.
- Add separate file read instructions where content was previously embedded. For example, if a workflow previously did `init plan-feature slug --include feature`, now do:
  ```
  INIT=$(dotnet run --project .../GfdTools/ -- init plan-feature slug)
  # Read feature file separately:
  cat docs/features/$SLUG/FEATURE.md
  ```

**E. Specific workflow updates:**

1. **new-project.md**: `init new-project`, replace `gfd-tools.cjs commit` with plain `git add` + `git commit`
2. **new-feature.md**: `init new-feature`, replace `gfd-tools.cjs commit` with plain `git add` + `git commit`
3. **discuss-feature.md**: `init plan-feature` (used for discuss too), `feature-update-status`, replace `gfd-tools.cjs commit` with plain `git add` + `git commit`
4. **research-feature.md**: `init plan-feature`, `feature-update-status`, replace `gfd-tools.cjs commit` with plain `git add` + `git commit`
5. **plan-feature.md**: `init plan-feature`, `feature-update-status`, replace `gfd-tools.cjs commit` with plain `git add` + `git commit`
6. **execute-feature.md**: `init execute-feature`, `feature-plan-index`, `feature-update-status`, `list-features`, replace `gfd-tools.cjs commit` with plain `git add` + `git commit`
7. **progress.md**: DELETE this file — progress feature is being dropped
8. **status.md**: `list-features`
9. **map-codebase.md**: `init map-codebase`, replace `gfd-tools.cjs commit` with plain `git add` + `git commit`
10. **convert-from-gsd.md**: `frontmatter merge`, `feature-update-status`, replace `gfd-tools.cjs commit` with plain `git add` + `git commit`

**F. Replace `commit` command with plain git:**
The `commit` command is NOT ported to the C# tool. Replace all `gfd-tools.cjs commit "msg" --files file1 file2` invocations with plain git:
```bash
git add file1 file2 && git diff --cached --quiet || git commit -m "msg"
```
Also remove any `commit_docs` config checks — docs are always committed.

Read each file, find all occurrences of `gfd-tools.cjs`, update invocation and parsing patterns.
  </action>
  <verify>
Grep all workflow files for `gfd-tools.cjs` — must return 0 matches.
Grep all workflow files for `dotnet run --project` — must return 30+ matches.
Grep all workflow files for `--raw` — must return 0 matches.
Grep all workflow files for `--include` — must return 0 matches.
  </verify>
  <done>All 10 workflow files updated. Zero references to gfd-tools.cjs remain. All invocations use dotnet run --project. All output parsing uses key=value extraction.</done>
</task>

<task type="auto">
  <name>Task 2: Update all agent files to use C# tool</name>
  <files>
    agents/gfd-executor.md
    agents/gfd-planner.md
    agents/gfd-researcher.md
    agents/gfd-verifier.md
  </files>
  <action>
For each of the 4 agent files, apply the same substitution patterns as the workflow files:

**A. Replace tool invocation path (all 4 files):**
- Old: `node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs`
- New: `dotnet run --project /home/conroy/.claude/get-features-done/GfdTools/ --`

**B. Replace JSON parsing with key=value extraction (all 4 files):**
- Where agents say "Extract from init JSON:" or "Parse JSON for:", replace with key=value extraction using `grep "^key=" | cut -d= -f2-`.

**C. Remove --include flag and add separate file reads:**

CRITICAL — **agents/gfd-executor.md line 22** currently reads:
```
INIT=$(node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs init execute-feature "${SLUG}" --include feature)
```
The `--include feature` flag causes the init command to embed `feature_content` in the output. The executor's line 25 then extracts `feature_content` from the init result.

Replace with:
```
INIT=$(dotnet run --project /home/conroy/.claude/get-features-done/GfdTools/ -- init execute-feature "${SLUG}")
```
And immediately after, add a separate read instruction:
```
# Feature content no longer embedded in init output — read separately:
cat docs/features/$SLUG/FEATURE.md
```
Also update line 25 to remove `feature_content` from the "Extract from init" list, since it is now obtained via the separate file read above.

**D. Remove --raw flag usage (if any).**

**E. Specific agent reference counts:**

1. **gfd-executor.md**: `init execute-feature --include feature` → drop `--include`, add separate file read; `config-get`; `feature-update-status`; remove `feature add-decision` refs (Claude uses Edit tool); remove `feature add-blocker` refs (Claude uses Edit tool); replace `commit` with plain `git add` + `git commit`
2. **gfd-planner.md**: replace `commit` refs with plain git; `init plan-feature`; `history-digest`; `frontmatter validate`; `verify plan-structure`; `feature-update-status`
3. **gfd-researcher.md**: `init plan-feature`; replace `commit` refs with plain git
4. **gfd-verifier.md**: `verify artifacts`; `verify key-links`; `summary-extract`; `verify commits`

Read each file, find all occurrences of `gfd-tools.cjs`, update invocation and parsing patterns. The agent files are at `agents/` in the repo root (symlinked from `~/.claude/agents/`).
  </action>
  <verify>
`grep -r "gfd-tools.cjs" agents/` — must return 0 results.
`grep -r "dotnet run --project" agents/` — must return 22+ matches.
`grep -r "\-\-include" agents/` — must return 0 results (for gfd-tools flags specifically).
Confirm agents/gfd-executor.md no longer references `--include feature` and has a separate `cat docs/features/$SLUG/FEATURE.md` instruction after the init call.
  </verify>
  <done>All 4 agent files updated. Zero references to gfd-tools.cjs remain. gfd-executor.md reads FEATURE.md separately instead of using --include. All invocations use dotnet run --project with key=value extraction.</done>
</task>

<task type="auto">
  <name>Task 3: Delete gfd-tools.cjs and verify clean state</name>
  <files>
    get-features-done/bin/gfd-tools.cjs
  </files>
  <action>
1. Delete `get-features-done/bin/gfd-tools.cjs` using `rm`.

2. Run a final verification sweep:
   - `dotnet build --project get-features-done/GfdTools/` — must succeed
   - `dotnet run --project get-features-done/GfdTools/ -- --help` — must show all commands
   - `dotnet run --project get-features-done/GfdTools/ -- init plan-feature csharp-rewrite` — must work
   - `dotnet run --project get-features-done/GfdTools/ -- list-features` — must work
   - `dotnet run --project get-features-done/GfdTools/ -- frontmatter get docs/features/csharp-rewrite/FEATURE.md` — must work

3. Grep BOTH `get-features-done/` AND `agents/` for any remaining references to `gfd-tools.cjs` (excluding git history). Any remaining references are bugs — fix them:
   - `grep -r "gfd-tools.cjs" get-features-done/ agents/` — must return 0 results.

4. Check if `get-features-done/bin/` directory is now empty. If so, consider removing it (or leave it if other files exist there).
  </action>
  <verify>
`ls get-features-done/bin/gfd-tools.cjs` — must fail (file deleted).
`grep -r "gfd-tools.cjs" get-features-done/ agents/` — must return 0 results.
`dotnet run --project get-features-done/GfdTools/ -- init plan-feature csharp-rewrite` — must succeed with key=value output.
  </verify>
  <done>gfd-tools.cjs is deleted. No references to it remain in workflows, agents, or any other file. The C# tool is the sole CLI tool for GFD operations.</done>
</task>

</tasks>

<verification>
- Zero occurrences of `gfd-tools.cjs` in any workflow or agent file
- Zero occurrences of `--raw` in workflow or agent files
- Zero occurrences of `--include` in workflow or agent files (for gfd-tools flags)
- All workflow files contain `dotnet run --project` invocations
- All agent files contain `dotnet run --project` invocations
- gfd-executor.md reads FEATURE.md separately after init (no --include feature)
- gfd-tools.cjs does not exist on disk
- C# tool handles all commands that workflows and agents invoke
</verification>

<success_criteria>
- All 10 workflow files use `dotnet run --project` with key=value parsing
- All 4 agent files use `dotnet run --project` with key=value parsing
- gfd-executor.md obtains feature content via separate file read, not --include
- gfd-tools.cjs is deleted
- No broken references remain in workflows or agents
- End-to-end: a workflow can invoke the C# tool and parse its output
</success_criteria>

<output>
After completion, create `docs/features/csharp-rewrite/03-SUMMARY.md`
</output>
