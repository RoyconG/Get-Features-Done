# Codebase Concerns

**Analysis Date:** 2026-02-20

## Tech Debt

**YAML parsing in frontmatter extraction:**
- Issue: Custom YAML parser in `gfd-tools.cjs` with manual state machine instead of using yaml library
- Files: `get-features-done/bin/gfd-tools.cjs` (lines 79-180)
- Why: Likely avoided npm dependency for CLI tool, wanted zero external dependencies
- Impact: Parser has edge cases with nested objects and arrays, difficult to maintain, prone to bugs with unusual YAML structures
- Fix approach: Replace with npm `yaml` package or `js-yaml` library, or add comprehensive test suite for parser

**Synchronous git operations blocking main thread:**
- Issue: All git operations use `execSync` instead of async alternatives
- Files: `get-features-done/bin/gfd-tools.cjs` (lines 68-75)
- Why: Simpler synchronous implementation, tool designed for CLI not concurrent operations
- Impact: Large repos with slow git operations block the entire process, no parallelization possible
- Fix approach: Use `simple-git` npm package with async/await for non-blocking git operations, especially for `isGitIgnored()` and `execGit()` functions

**Path construction without validation:**
- Issue: File paths constructed from user input without sanitization in several places
- Files: `get-features-done/bin/gfd-tools.cjs` (path joins throughout, especially lines 249-253, 300-310)
- Why: Assumes FEATURE.md and config.json are always in expected locations, no path traversal checks
- Impact: Could allow path traversal attacks if processing untrusted feature slugs or config paths
- Fix approach: Add `path.normalize()` and directory containment checks before file operations, validate slug format

**Large JSON objects written to console:**
- Issue: Output function attempts to handle large JSON by writing to temp files, but threshold is 50KB which is easily exceeded
- Files: `get-features-done/bin/gfd-tools.cjs` (lines 222-239)
- Why: Avoids context window overflow from massive JSON responses
- Impact: When output exceeds 50KB, tool writes to `/tmp/gfd-tools/` creating temporary files that may not be cleaned up, potential disk bloat
- Fix approach: Add cleanup of old temp files, make threshold configurable, consider streaming output

**No input validation on command arguments:**
- Issue: Command routing switch statement takes user arguments without type validation
- Files: `get-features-done/bin/gfd-tools.cjs` (lines 1459-1700+)
- Why: Designed for trusted internal use only
- Impact: Malformed arguments could cause unexpected behavior or errors, no helpful error messages
- Fix approach: Add argument validator for each command, provide usage hints on invalid input

## Known Bugs

**Frontmatter parser doesn't handle quoted values with special characters:**
- Symptoms: YAML values containing colons, commas, or quotes are not properly escaped/parsed
- Trigger: Create FEATURE.md with description containing text like "foo: bar" or "test, value"
- Files: `get-features-done/bin/gfd-tools.cjs` (lines 165-167, regex for quoted strings)
- Workaround: Manually escape quotes and colons in frontmatter values
- Root cause: Quote handling at lines 165-167 only checks start/end, doesn't handle escaped quotes within the string

**Feature status transitions not validated:**
- Symptoms: Can set feature status to invalid values, no enum validation
- Trigger: `gfd-tools feature-update-status slug invalid-status`
- Files: `get-features-done/bin/gfd-tools.cjs` (lines 1547-1549, feature-update-status command)
- Workaround: Only use valid statuses: backlog, planning, planned, in-progress, done
- Root cause: No validation of status value against allowed states

**Config.json with invalid JSON silently falls back to defaults:**
- Symptoms: Configuration changes lost if JSON is malformed, no user notification
- Trigger: Manual edit of docs/features/config.json with invalid JSON syntax
- Files: `get-features-done/bin/gfd-tools.cjs` (lines 51-56, loadConfig function)
- Workaround: Validate JSON syntax with `jq` before committing config changes
- Root cause: Try/catch at lines 54-55 catches JSON.parse errors but silently returns defaults

## Security Considerations

**execSync with user input in git commands:**
- Risk: Git command arguments passed directly to execSync without escaping could allow command injection
- Files: `get-features-done/bin/gfd-tools.cjs` (lines 68-75, execGit function)
- Current mitigation: Assumes Claude-generated arguments only, not external user input
- Recommendations: Use execSync with array form (const args syntax) instead of string concatenation, or use `simple-git` library with proper escaping

**No authentication for gfd-tools operations:**
- Risk: Anyone with filesystem access can modify feature state, plans, and summaries via gfd-tools
- Files: `get-features-done/bin/gfd-tools.cjs` (entire file - all commands read/write without auth)
- Current mitigation: Tool intended for local use in Claude Code environment only
- Recommendations: Add optional team/user tracking if shared repos become common, document filesystem permission requirements

**Environment variables and secrets in config.json:**
- Risk: If user_setup lists env vars or API keys, they could be exposed in plaintext in docs/features/config.json
- Files: `get-features-done/bin/gfd-tools.cjs` (lines 36-56, config loading - no secret filtering)
- Current mitigation: Config is not committed to git (assumed in .gitignore)
- Recommendations: Add guidance to .gitignore template, never print config.json contents in logs, mask secrets in output

## Performance Bottlenecks

**Feature listing with readdirSync on every status check:**
- Problem: `listFeaturesInternal()` does synchronous filesystem scan of all features
- Files: `get-features-done/bin/gfd-tools.cjs` (lines 298-329)
- Measurement: ~100ms per call with 20+ features (scales linearly)
- Cause: Full directory scan + file read for each feature to extract frontmatter
- Improvement path: Cache feature list with mtime-based invalidation, lazy-load frontmatter on demand only when needed

**Recursive grep operations in health check:**
- Problem: `cmdHealthCheck` runs grep recursively on entire codebase
- Files: `get-features-done/bin/gfd-tools.cjs` (around line 1263, find + grep in healthcheck)
- Measurement: Can take 2-5+ seconds in large repos (500+ files)
- Cause: Scans entire directory tree without excluding common non-code directories
- Improvement path: Add exclude patterns for vendor/, node_modules/, .git/, build/, dist/ directories; consider using single-pass logic

**Feature state updates read/modify/write entire STATE.md file:**
- Problem: State operations load entire file, merge object, write back (no atomic updates)
- Files: `get-features-done/bin/gfd-tools.cjs` (state commands throughout)
- Measurement: State files typically 5-50KB, but waterfall of reads/writes in batch operations
- Cause: No transaction mechanism, each state operation is separate read-modify-write cycle
- Improvement path: Batch state updates, or use append-only log format, or switch to database (SQLite)

## Fragile Areas

**Agent prompt definitions as standalone markdown files:**
- Files: `agents/gfd-*.md` (all 5 agent definition files)
- Why fragile: Agents are spawned with entire markdown file as system prompt, any syntax change breaks parsing
- Common failures: Editing agent descriptions or adding sections can break XML structure that orchestrator expects, inconsistent section names break parsing
- Safe modification: Always validate XML structure using `xmllint` or similar, test orchestrator routing after changes, maintain strict format (no deviations from templates)
- Test coverage: No automated tests for agent structure or orchestrator parsing - manual verification only

**Frontmatter in markdown files critical for routing:**
- Files: All PLAN.md, SUMMARY.md, FEATURE.md, VERIFICATION.md files created by tool
- Why fragile: Frontmatter format is YAML but parser is custom (not standard), orchestrator depends on exact field names and types
- Common failures: Typos in frontmatter keys (e.g., `plan` vs `plan_id`), missing required fields, wrong value types (string vs int)
- Safe modification: Always validate with `gfd-tools frontmatter validate` before using plan files, regenerate from templates rather than hand-editing
- Test coverage: No automated validation of generated frontmatter - gfd-tools has some validation but not comprehensive

**Wave dependency graph calculation:**
- Files: `agents/gfd-planner.md` (lines 214-281, dependency graph section)
- Why fragile: Wave assignment is computed once during planning, if dependencies are wrong, entire execution order fails
- Common failures: Circular dependencies not detected, missing transitive dependencies, file conflicts not caught
- Safe modification: Add explicit cycle detection before assigning waves, validate file_modified sets for overlaps
- Test coverage: No end-to-end tests verifying wave structure produces correct execution order

## Scaling Limits

**gfd-tools JSON output to console:**
- Current capacity: ~50KB JSON output before switching to temp files
- Limit: Large feature analysis with many files or plans can exceed limit, temp files created
- Symptoms at limit: Command output goes to `/tmp/gfd-tools/` instead of stdout, orchestrator must fetch from temp file
- Scaling path: Stream output in chunks, implement paginated responses, or use dedicated output format for large datasets

**Feature state file concatenation:**
- Current capacity: STATE.md grows with decisions, blockers, metrics, session history
- Limit: File approaches 100KB+ with long project history (20+ features, 50+ decisions)
- Symptoms at limit: Slow YAML parsing, difficult to edit manually, git diffs become large
- Scaling path: Implement history rotation (archive old decisions), use database instead of file-based state, or split state into multiple files per project

**Agent markdown files as prompts:**
- Current capacity: Agent files are 400-1150 lines, fit in context but consume ~20-30% of available context per spawn
- Limit: If agent definitions grow beyond ~1500 lines, may approach context limits for downstream tasks
- Symptoms at limit: Agent spawned with insufficient remaining context for actual work, quality degradation
- Scaling path: Modularize agent definitions (split concerns into separate sections), compress repetitive guidance, use references instead of inline examples

## Dependencies at Risk

**No external dependencies in gfd-tools.cjs:**
- Risk: Zero-dependency approach means everything is implemented manually (YAML parser, git wrapper, file operations)
- Impact: Security bugs and edge cases that npm packages handle are reimplemented from scratch
- Migration plan: Consider adding `yaml`, `simple-git`, and `minimist` for robustness, or document custom implementations as acceptable tech debt

**Markdown parsing for frontmatter:**
- Risk: Custom regex-based parser for YAML frontmatter is fragile, could break with valid YAML
- Impact: Agent spawning fails if generated file has unexpected YAML format, no graceful degradation
- Migration plan: Use `gray-matter` or `front-matter` npm package to handle YAML robustly

## Missing Critical Features

**No plan validation before execution:**
- Problem: Plans can be generated with missing elements (empty tasks, missing verifications, incomplete acceptance_criteria list)
- Current workaround: Executor discovers issues at runtime, has to handle gracefully
- Blocks: Can't guarantee plan quality before expensive Claude execution starts
- Implementation complexity: Low - add schema validation using JSON Schema and validation library

**No dry-run mode for feature execution:**
- Problem: Can't preview what a plan will do before executing it, no way to estimate time/cost
- Current workaround: User has to read PLAN.md manually and estimate
- Blocks: Can't safely test plans on production-like scenarios
- Implementation complexity: Medium - would need to trace task flow without actually creating files/commits

**No rollback mechanism for executed plans:**
- Problem: If execution goes wrong partway through, no automatic rollback to clean state
- Current workaround: Manual git reset, manual file deletion, restart from checkpoint
- Blocks: Can't safely recover from mid-plan failures
- Implementation complexity: High - requires transactional semantics, git branch isolation per plan

**No inter-feature dependency tracking:**
- Problem: FEATURE.md has depends_on field but planner doesn't validate dependencies exist or are done
- Current workaround: Manual tracking, risk of deadlocks if circular dependencies occur
- Blocks: Can't automatically determine safe execution order across features
- Implementation complexity: Medium - add dependency graph validation, cycle detection

## Test Coverage Gaps

**gfd-tools command routing and argument parsing:**
- What's not tested: Most commands lack unit tests, argument validation is missing
- Files: `get-features-done/bin/gfd-tools.cjs` (main() function and all command handlers)
- Risk: Command argument bugs go undetected, wrong output format could break orchestrator
- Priority: High - gfd-tools is critical infrastructure
- Difficulty to test: Requires mocking filesystem operations and git calls, or using test fixtures

**Frontmatter parsing with edge cases:**
- What's not tested: Custom YAML parser not tested with nested objects, quoted special characters, arrays with complex values
- Files: `get-features-done/bin/gfd-tools.cjs` (lines 79-180, extractFrontmatter and related functions)
- Risk: Malformed frontmatter causes silent parsing failures, generated documents may be invalid
- Priority: High - affects reliability of all generated FEATURE/PLAN/SUMMARY documents
- Difficulty to test: Need comprehensive YAML test suite with edge cases

**Agent spawning and orchestrator routing:**
- What's not tested: No end-to-end tests that spawn agents and verify they produce correct output
- Files: All agent definitions + orchestrator routing logic (not visible in this codebase)
- Risk: Orchestrator changes could silently break agent routing, no verification until user reports
- Priority: Medium - affects all GFD workflows
- Difficulty to test: Requires mocking Claude API, complex orchestration logic

**Feature execution wave structure:**
- What's not tested: No tests verifying wave assignment produces correct parallelization
- Files: `agents/gfd-planner.md` (dependency graph and wave assignment logic)
- Risk: Circular dependencies or missing transitive dependencies cause incorrect wave ordering
- Priority: Medium - affects plan execution correctness
- Difficulty to test: Requires simulating planner logic, validating topological sort

---

*Concerns audit: 2026-02-20*
*Update as issues are fixed or new ones discovered*
