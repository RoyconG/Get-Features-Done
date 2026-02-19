# External Integrations

**Analysis Date:** 2026-02-20

## APIs & External Services

**Claude Code (Anthropic):**
- Purpose: Host environment for slash commands and agent execution
- SDK/Client: Native integration via Claude Code platform
- Auth: Implicit via Claude Code session (no API keys required in GFD itself)
- Integration points:
  - Slash commands: `/gfd:*` commands defined in `commands/gfd/`
  - Agent execution: Agents run as Claude Code agents
  - Model selection: Via model profile configuration (`quality`/`balanced`/`budget`)

## Data Storage

**File-Based State:**
- Location: `docs/features/` directory (created per-project)
- Format: Markdown documents with YAML frontmatter
- Storage mechanism: Local filesystem only
- No external databases used

**Configuration Storage:**
- File: `docs/features/config.json`
- Format: JSON
- Lifecycle: Per-project (created from template on `/gfd:new-project`)
- Not version-controlled (added to `.gitignore`)

**Project Metadata:**
- File: `docs/features/PROJECT.md`
- Contains: Project goals, scope, context
- Created by: `/gfd:new-project` command
- Versioned: Yes (committed to git)

**Feature Definitions:**
- File: `docs/features/<slug>/FEATURE.md`
- Contains: Feature acceptance criteria, scope, type classification
- Created by: `/gfd:new-feature` command
- Versioned: Yes (committed to git)

**Implementation Plans:**
- File pattern: `docs/features/<slug>/*-PLAN.md`
- Contains: Task breakdown, complexity estimation, dependencies
- Created by: `/gfd:plan-feature` command
- Versioned: Yes (committed to git)

**Execution State:**
- File: `docs/features/STATE.md`
- Contains: Progress tracking, plan advancement, metrics
- Updated during: `/gfd:execute-feature` execution
- Versioned: Yes (committed to git)

**Codebase Analysis Docs:**
- Location: `docs/features/codebase/`
- Files: `STACK.md`, `INTEGRATIONS.md`, `ARCHITECTURE.md`, `STRUCTURE.md`, `CONVENTIONS.md`, `TESTING.md`, `CONCERNS.md`
- Created by: `/gfd:map-codebase` command
- Purpose: Reference documents for planning and execution
- Versioned: Yes (committed to git)

**File Storage:**
- Type: Local filesystem only
- No cloud storage integration
- No S3, GCS, or Blob storage used

**Caching:**
- Type: None currently implemented
- State is ephemeral during agent execution
- No Redis, Memcached, or in-process caching

## Version Control

**Git Integration:**
- Purpose: Atomic feature commits, history tracking, collaboration
- Implementation: Direct `git` CLI invocation via Node.js `execSync()`
- Location: `get-features-done/bin/gfd-tools.cjs` - `execGit()` function
- Operations used:
  - `git status` - Check working tree status
  - `git diff` - View staged/unstaged changes
  - `git add` - Stage files
  - `git commit` - Create atomic commits
  - `git log` - View commit history
  - `git check-ignore` - Check .gitignore status
- Commit messages: Structured format with type prefix (`feat`, `fix`, `docs`, `refactor`, etc.)
- Co-authored commits: Include `Co-Authored-By: Claude <model>` trailer

**Ignored Paths:**
- `docs/features/config.json` - Project-specific configuration
- `docs/features/STATE.md` - Can be ignored per project config
- Configurable via `search_gitignored` option in config

## Authentication & Identity

**Auth Provider:**
- Type: None (GFD is a local CLI tool)
- User identity: Implicit via Git author configuration (`git config user.name`, `git config user.email`)
- Session: Implicit via Claude Code session (no login required)

**Git Author Configuration:**
- Used for: Git commit authorship
- Source: System git config or Claude Code environment
- Format: Standard git author metadata

## Monitoring & Observability

**Error Tracking:**
- Type: None (errors surface directly to Claude Code console)
- Logging: Direct stderr output via `execSync()`

**Logs:**
- Approach: Console output (stdout/stderr)
- Format: Plain text or JSON for structured output
- No persistent logging system
- No log aggregation service

**Metrics:**
- Type: Feature-level metrics only
- Storage: `docs/features/STATE.md` frontmatter
- Tracked metrics: `duration`, `completed_date`, execution timestamps
- No analytics or telemetry service

## CI/CD & Deployment

**Hosting:**
- Type: Local execution only
- No cloud deployment required
- Runs in Claude Code environment on user's machine

**CI Pipeline:**
- Type: None (GFD is not deployed, distributed as repo)
- Installation: Via `install.sh` symlink script
- No automated testing pipeline
- No release automation

## Environment Configuration

**Required Environment Variables:**
- None required by GFD itself
- Git config used instead (`user.name`, `user.email`)
- Project-specific vars can be specified in feature `user_setup` frontmatter (documentation only, not enforced by GFD)

**Optional Environment Variables:**
- `HOME` - Used for symlink installation target
- `GIT_AUTHOR_NAME`, `GIT_AUTHOR_EMAIL` - Git author overrides (standard git conventions)

**Secrets Location:**
- Type: Not managed by GFD
- Reference: Documented in feature `user_setup` sections
- External secret management: User responsibility (1Password, AWS Secrets Manager, etc.)
- Convention: Store in project `.env.local` (gitignored) or external vault

**Configuration Sourcing:**
- Per-project: `docs/features/config.json`
- Defaults: Hardcoded in `gfd-tools.cjs` `loadConfig()` function (lines 36-57)
- Mode precedence: Project config overrides hardcoded defaults

## Webhooks & Callbacks

**Incoming:**
- Type: None (GFD is not a server)
- Use case: Not applicable

**Outgoing:**
- Type: None (GFD does not make external HTTP requests)
- Git operations: Local filesystem only
- Claude API: Handled by Claude Code platform (transparent to GFD)

## Tool-Specific Integrations

**Anthropic Claude API:**
- Indirect usage via Claude Code platform
- Model selection: Configurable per role via model profiles
  - Profile `quality`: Opus for all agents except mapper (Sonnet)
  - Profile `balanced`: Sonnet for planning/exec/research/verify, Haiku for mapper
  - Profile `budget`: Sonnet for planner, Haiku for executor/verifier/researcher/mapper
- Configuration: `model_profile` in `docs/features/config.json`
- Tokens: Managed transparently by Claude Code (not exposed to GFD)

**Installation Framework:**
- Symlink-based discovery
- Location: `~/.claude/` directory (Claude Code standard)
- Mechanism: Creates symlinks to repo directories so Claude Code discovers agents/commands/workflows
- No package registry dependencies (npm, PyPI, etc.)

---

*Integration audit: 2026-02-20*
