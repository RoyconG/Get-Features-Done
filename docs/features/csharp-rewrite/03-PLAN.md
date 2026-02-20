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
  - get-features-done/workflows/progress.md
  - get-features-done/workflows/research-feature.md
  - get-features-done/workflows/status.md
autonomous: true
acceptance_criteria:
  - "All GFD workflow and agent files updated to invoke C# tool and parse key=value output"
  - "gfd-tools.cjs deleted"

must_haves:
  truths:
    - "Every workflow file invokes dotnet run --project instead of node gfd-tools.cjs"
    - "Every workflow parses key=value output instead of JSON"
    - "No workflow references --raw or --include flags"
    - "gfd-tools.cjs no longer exists"
    - "All workflows function correctly with the C# tool"
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
  key_links:
    - from: "get-features-done/workflows/execute-feature.md"
      to: "get-features-done/GfdTools/Program.cs"
      via: "dotnet run --project invocation"
      pattern: "dotnet run --project"
    - from: "get-features-done/workflows/plan-feature.md"
      to: "get-features-done/GfdTools/Program.cs"
      via: "dotnet run --project invocation"
      pattern: "dotnet run --project"
---

<objective>
Update all 10 GFD workflow files to invoke the C# tool instead of gfd-tools.cjs, parse key=value output instead of JSON, and delete gfd-tools.cjs.

Purpose: Complete the migration by switching all consumers to the new tool and removing the old one.
Output: All workflows use the C# tool. gfd-tools.cjs is deleted.
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
    get-features-done/workflows/progress.md
    get-features-done/workflows/research-feature.md
    get-features-done/workflows/status.md
  </files>
  <action>
For each of the 10 workflow files, make these systematic changes:

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

1. **new-project.md** (2 refs): `init new-project`, `commit`
2. **new-feature.md** (2 refs): `init new-feature`, `commit`
3. **discuss-feature.md** (4 refs): `init plan-feature` (used for discuss too), `feature-update-status`, `commit`
4. **research-feature.md** (4 refs): `init plan-feature`, `feature-update-status`, `commit`
5. **plan-feature.md** (3 refs): `init plan-feature`, `feature-update-status`, `commit`
6. **execute-feature.md** (5 refs): `init execute-feature`, `feature-plan-index`, `feature-update-status`, `list-features`, `commit`
7. **progress.md** (3 refs): `init progress`, `progress bar`, `list-features`
8. **status.md** (1 ref): `list-features`
9. **map-codebase.md** (2 refs): `init map-codebase`, `commit`
10. **convert-from-gsd.md** (4 refs): `frontmatter merge`, `feature-update-status`, `commit`

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
  <name>Task 2: Delete gfd-tools.cjs and verify clean state</name>
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
   - `dotnet run --project get-features-done/GfdTools/ -- commit "test" --files /dev/null` — test commit flow (will report nothing to commit, which is correct)
   - `dotnet run --project get-features-done/GfdTools/ -- frontmatter get docs/features/csharp-rewrite/FEATURE.md` — must work

3. Grep entire get-features-done/ directory for any remaining references to `gfd-tools.cjs` (excluding git history). Any remaining references are bugs — fix them.

4. Check if `get-features-done/bin/` directory is now empty. If so, consider removing it (or leave it if other files exist there).
  </action>
  <verify>
`ls get-features-done/bin/gfd-tools.cjs` — must fail (file deleted).
`grep -r "gfd-tools.cjs" get-features-done/` — must return 0 results.
`dotnet run --project get-features-done/GfdTools/ -- init plan-feature csharp-rewrite` — must succeed with key=value output.
  </verify>
  <done>gfd-tools.cjs is deleted. No references to it remain in workflows or any other file. The C# tool is the sole CLI tool for GFD operations.</done>
</task>

</tasks>

<verification>
- Zero occurrences of `gfd-tools.cjs` in any workflow or agent file
- Zero occurrences of `--raw` in workflow files
- Zero occurrences of `--include` in workflow files
- All workflow files contain `dotnet run --project` invocations
- gfd-tools.cjs does not exist on disk
- C# tool handles all commands that workflows invoke
</verification>

<success_criteria>
- All 10 workflow files use `dotnet run --project` with key=value parsing
- gfd-tools.cjs is deleted
- No broken references remain
- End-to-end: a workflow can invoke the C# tool and parse its output
</success_criteria>

<output>
After completion, create `docs/features/csharp-rewrite/03-SUMMARY.md`
</output>
