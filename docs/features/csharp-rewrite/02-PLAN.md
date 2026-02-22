---
feature: csharp-rewrite
plan: 02
type: execute
wave: 2
depends_on: ["01"]
files_modified:
  - get-features-done/GfdTools/Commands/InitCommands.cs
  - get-features-done/GfdTools/Commands/VerifyCommands.cs
  - get-features-done/GfdTools/Commands/HistoryDigestCommand.cs
  - get-features-done/GfdTools/Commands/SummaryExtractCommand.cs
  - get-features-done/GfdTools/Commands/FrontmatterCommands.cs
  - get-features-done/GfdTools/Program.cs
autonomous: true
acceptance_criteria:
  - "All actively-used gfd-tools commands ported (determined by grepping workflows)"
  - "Output uses key=value pairs instead of JSON"
  - "Bugs found during porting are fixed, not replicated"

must_haves:
  truths:
    - "All 5 init subcommands produce correct key=value output (init progress dropped)"
    - "verify plan-structure validates plan XML and frontmatter"
    - "verify commits checks git commit hashes"
    - "verify artifacts checks must_haves artifact paths exist (NEW command)"
    - "verify key-links greps codebase for key link patterns (NEW command)"
    - "frontmatter validate checks required fields per schema (NEW command)"
    - "history-digest returns summary digest as key=value"
    - "summary-extract returns summary fields as key=value"
  artifacts:
    - path: "get-features-done/GfdTools/Commands/InitCommands.cs"
      provides: "All 5 init subcommands"
      min_lines: 150
    - path: "get-features-done/GfdTools/Commands/VerifyCommands.cs"
      provides: "verify plan-structure, commits, artifacts, key-links"
      min_lines: 100
    - path: "get-features-done/GfdTools/Commands/HistoryDigestCommand.cs"
      provides: "history-digest command"
    - path: "get-features-done/GfdTools/Commands/SummaryExtractCommand.cs"
      provides: "summary-extract command"
  key_links:
    - from: "get-features-done/GfdTools/Commands/InitCommands.cs"
      to: "get-features-done/GfdTools/Services/FeatureService.cs"
      via: "FindFeature and ListFeatures calls"
      pattern: "FeatureService"
    - from: "get-features-done/GfdTools/Commands/InitCommands.cs"
      to: "get-features-done/GfdTools/Services/ConfigService.cs"
      via: "Config loading and model resolution"
      pattern: "ConfigService"
    - from: "get-features-done/GfdTools/Commands/VerifyCommands.cs"
      to: "get-features-done/GfdTools/Services/FrontmatterService.cs"
      via: "Reading plan frontmatter for artifact/key-link verification"
      pattern: "FrontmatterService"
---

<objective>
Implement the remaining commands: all 5 init subcommands, verify commands (including 3 NEW commands that are bugs in gfd-tools.cjs), history-digest, summary-extract, and frontmatter validate.

Purpose: Complete the C# tool with full command parity (plus bug fixes). After this plan, every actively-used command works.
Output: All commands implemented and tested. Full parity with gfd-tools.cjs plus 3 new commands.
</objective>

<execution_context>
@$HOME/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/csharp-rewrite/FEATURE.md
@docs/features/csharp-rewrite/RESEARCH.md
@docs/features/csharp-rewrite/01-SUMMARY.md
@get-features-done/bin/gfd-tools.cjs
</context>

<tasks>

<task type="auto">
  <name>Task 1: Implement init commands</name>
  <files>
    get-features-done/GfdTools/Commands/InitCommands.cs
    get-features-done/GfdTools/Program.cs
  </files>
  <action>
Port all 5 init subcommands from gfd-tools.cjs. Each init command returns context for a specific workflow. All output via OutputService as key=value pairs. Do NOT port `init progress` — the progress feature is being dropped.

IMPORTANT: Drop the `--include` flag entirely. The JS version uses `--include feature` to embed file contents in output. The C# port uses key=value which cannot represent multiline values. Per RESEARCH.md recommendation: omit file contents from init output. Workflows will read files separately.

1. **`init new-project`** — port `cmdInitNewProject` (lines 1060-1090):
   - Output: `researcher_model=`, `project_exists=`, `has_codebase_map=`, `features_dir_exists=`, `has_existing_code=`, `has_package_file=`, `is_brownfield=`, `needs_codebase_map=`, `has_git=`
   - For `has_existing_code`: use `find` command same as JS, or use Directory.EnumerateFiles with extensions filter.

2. **`init new-feature <slug>`** — port `cmdInitNewFeature` (lines 1092-1108):
   - Output: `slug=`, `feature_exists=`, `existing_status=`, `features_dir_exists=`, `feature_dir=`, `feature_md=`, `project_exists=`

3. **`init plan-feature <slug>`** — port `cmdInitPlanFeature` (lines 1110-1150):
   - Output: `researcher_model=`, `planner_model=`, `checker_model=`, `research_enabled=`, `plan_checker_enabled=`, `feature_found=`, `feature_dir=`, `slug=`, `feature_name=`, `feature_status=`, `has_research=`, `has_plans=`, `plan_count=`, `features_dir_exists=`
   - Do NOT include `feature_content` or `research_content` (dropped `--include`).

4. **`init execute-feature <slug>`** — port `cmdInitExecuteFeature` (lines 1152-1189):
   - Output: `executor_model=`, `verifier_model=`, `parallelization=`, `verifier_enabled=`, `feature_found=`, `feature_dir=`, `slug=`, `feature_name=`, `feature_status=`, `plan_count=`, `incomplete_count=`, `config_exists=`
   - Plans and summaries as repeated keys: `plan=01-PLAN.md`, `plan=02-PLAN.md`, etc. `summary=01-SUMMARY.md`, etc. `incomplete_plan=01`, etc.

5. **`init map-codebase`** — port `cmdInitMapCodebase` (lines 1224-1243):
   - Output: `mapper_model=`, `search_gitignored=`, `parallelization=`, `codebase_dir=`, `has_maps=`, `features_dir_exists=`, `codebase_dir_exists=`
   - Existing maps as repeated key: `existing_map=ARCHITECTURE.md`, etc.

6. Update `Program.cs` to wire init subcommands. Replace any placeholder actions with real implementations.
  </action>
  <verify>
Test each init command:
- `dotnet run --project get-features-done/GfdTools/ -- init plan-feature csharp-rewrite` — should output key=value pairs including `feature_found=true`, `slug=csharp-rewrite`
- `dotnet run --project get-features-done/GfdTools/ -- init execute-feature csharp-rewrite` — should list plans
- `dotnet run --project get-features-done/GfdTools/ -- init new-project` — should detect existing code
- `dotnet run --project get-features-done/GfdTools/ -- init map-codebase` — should show codebase dir status
  </verify>
  <done>All 5 init subcommands produce correct key=value output matching gfd-tools.cjs behavior (minus --include file content embedding which is intentionally dropped). `init progress` intentionally not ported — progress feature is being dropped.</done>
</task>

<task type="auto">
  <name>Task 2: Implement verify, history-digest, summary-extract, frontmatter validate commands</name>
  <files>
    get-features-done/GfdTools/Commands/VerifyCommands.cs
    get-features-done/GfdTools/Commands/HistoryDigestCommand.cs
    get-features-done/GfdTools/Commands/SummaryExtractCommand.cs
    get-features-done/GfdTools/Commands/FrontmatterCommands.cs
    get-features-done/GfdTools/Program.cs
  </files>
  <action>
1. **VerifyCommands.cs** — port existing + implement 2 NEW commands:

   a. `verify plan-structure <file>` — port `cmdVerifyPlanStructure` (lines 652-695):
      - Read file, parse frontmatter, check required fields, parse `<task>` XML elements, check for `<name>`, `<action>`, `<verify>`, `<done>`.
      - Output: `valid=true/false`, errors as repeated `error=`, warnings as repeated `warning=`, `task_count=`

   b. `verify commits <hash...>` — port `cmdVerifyCommits` (lines 616-629):
      - Check each hash with `git cat-file -t <hash>`
      - Output: `all_valid=true/false`, `total=`, valid hashes as `valid=<hash>`, invalid as `invalid=<hash>`

   c. **NEW: `verify artifacts <plan-file>`** — NOT in gfd-tools.cjs (bug fix):
      - Read plan file frontmatter, extract `must_haves.artifacts` (array of objects with `path` field)
      - For each artifact, check if `path` exists relative to cwd
      - Output: `all_passed=true/false`, `passed=N`, `total=N`, per-artifact: `artifact_path=<path>`, `artifact_exists=true/false`

   d. **NEW: `verify key-links <plan-file>`** — NOT in gfd-tools.cjs (bug fix):
      - Read plan file frontmatter, extract `must_haves.key_links` (array of objects with `from`, `to`, `pattern` fields)
      - For each key link, read the `from` file and grep for `pattern`
      - Output: `all_verified=true/false`, `verified=N`, `total=N`, per-link: `link_from=`, `link_to=`, `link_verified=true/false`

2. **HistoryDigestCommand.cs** — port `cmdHistoryDigest` (lines 728-767):
   - `history-digest` (no args)
   - Lists all features, reads all summaries, extracts one-liners, decisions, tech-added
   - Output: `summary_count=`, then per-summary: `summary_feature=`, `summary_file=`, `summary_one_liner=`. Decisions as `decision_feature=`, `decision_text=`. Tech as `tech_added=` (repeated keys).

3. **SummaryExtractCommand.cs** — port `cmdSummaryExtract` (lines 699-724):
   - `summary-extract <path> [--fields <comma-separated>]`
   - Output: `path=`, `one_liner=`, `key_files=` (repeated), `tech_added=` (repeated), `decisions=` (repeated)

4. **FrontmatterCommands.cs** — add `frontmatter validate` subcommand:
   - **NEW: `frontmatter validate <file> --schema <name>`** — NOT in gfd-tools.cjs (bug fix):
   - Schema `plan` requires: feature, plan, type, wave, depends_on, files_modified, autonomous
   - Read file frontmatter, check each required field exists
   - Output: `valid=true/false`, `schema=plan`, missing fields as `missing=<field>`, present fields as `present=<field>`

5. Update `Program.cs` to wire all remaining commands. Remove all placeholder actions.

6. Run full build and test suite.
  </action>
  <verify>
Test the commands:
- `dotnet run --project get-features-done/GfdTools/ -- history-digest` — should list summaries
- `dotnet run --project get-features-done/GfdTools/ -- verify plan-structure docs/features/csharp-rewrite/01-PLAN.md` — should show valid=true with task count
- `dotnet run --project get-features-done/GfdTools/ -- frontmatter validate docs/features/csharp-rewrite/01-PLAN.md --schema plan` — should show valid=true
- All output is key=value, no JSON anywhere
  </verify>
  <done>All remaining commands implemented. The 3 new commands (verify artifacts, verify key-links, frontmatter validate) work correctly. Every actively-used command from the grep results is now available in the C# tool.</done>
</task>

</tasks>

<verification>
- `dotnet run --project get-features-done/GfdTools/ -- init plan-feature csharp-rewrite` returns key=value output with correct feature info
- `dotnet run --project get-features-done/GfdTools/ -- init execute-feature csharp-rewrite` returns plan listing
- `dotnet run --project get-features-done/GfdTools/ -- verify plan-structure docs/features/csharp-rewrite/01-PLAN.md` returns valid=true
- `dotnet run --project get-features-done/GfdTools/ -- frontmatter validate docs/features/csharp-rewrite/01-PLAN.md --schema plan` returns valid=true
- `dotnet run --project get-features-done/GfdTools/ -- history-digest` returns summary digest
- No command outputs JSON — all key=value
</verification>

<success_criteria>
- All 5 init subcommands work correctly (init progress dropped)
- All verify subcommands work (including 2 new ones)
- frontmatter validate works (new command)
- history-digest and summary-extract work
- Every command from the actively-used grep list is implemented
- All 3 bug-fix commands (verify artifacts, verify key-links, frontmatter validate) are implemented correctly
</success_criteria>

<output>
After completion, create `docs/features/csharp-rewrite/02-SUMMARY.md`
</output>
