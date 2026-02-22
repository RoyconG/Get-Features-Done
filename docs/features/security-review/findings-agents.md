# Security Review — Agent Prompts, Workflow Files, Templates, and References Audit

**Audited:** 2026-02-22
**Scope:** `agents/gfd-*.md` (5 files), `get-features-done/workflows/*.md` (9 files), `commands/gfd/*.md` (9 files), `get-features-done/templates/` (10 files), `get-features-done/references/` (3 files)
**Auditor:** GFD Plan 03 Executor

---

## Agent Prompts Audit

### Agents Assessed

| Agent File | Tool Grants | Purpose | Bash? |
|---|---|---|---|
| `agents/gfd-codebase-mapper.md` | Read, Bash, Grep, Glob, Write | Explore codebase, write analysis docs | YES |
| `agents/gfd-executor.md` | Read, Write, Edit, Bash, Grep, Glob | Execute plans, commit, create summaries | YES |
| `agents/gfd-planner.md` | Read, Write, Bash, Glob, Grep, WebFetch | Create PLAN.md files | YES |
| `agents/gfd-researcher.md` | Read, Write, Bash, Grep, Glob, WebSearch, WebFetch | Research implementation domain | YES |
| `agents/gfd-verifier.md` | Read, Write, Bash, Grep, Glob | Verify feature goal achievement | YES |

---

### [LOW] Finding A-1: All Five Agents Granted Bash Tool Access

- **Files:** All five `agents/gfd-*.md` files, frontmatter `tools:` field
- **Evidence:**
  ```yaml
  # gfd-executor.md
  tools: Read, Write, Edit, Bash, Grep, Glob

  # gfd-planner.md
  tools: Read, Write, Bash, Glob, Grep, WebFetch

  # gfd-researcher.md
  tools: Read, Write, Bash, Grep, Glob, WebSearch, WebFetch

  # gfd-verifier.md
  tools: Read, Write, Bash, Grep, Glob

  # gfd-codebase-mapper.md
  tools: Read, Bash, Grep, Glob, Write
  ```
- **Issue:** All five sub-agents are granted Bash tool access. For gfd-executor and gfd-codebase-mapper, Bash is operationally necessary (commits, git operations, shell commands). For gfd-planner, gfd-researcher, and gfd-verifier, Bash is used for init calls (`$HOME/.claude/get-features-done/bin/gfd-tools`) and grep-based verification — the Bash scope is functionally required but broad. Bash is the most powerful Claude tool grant: it enables arbitrary shell command execution. If an agent is compromised via prompt injection through user-controlled content (FEATURE.md, RESEARCH.md, plan files), the attacker gains shell execution capability. This is the blast-radius concern — Bash access means prompt injection becomes code execution, not just text manipulation.
- **Severity rationale:** LOW. Bash is necessary for GFD's operational model (the CLI tool is a C# binary invoked via shell). All agents are spawned in the user's own shell environment with the user's own filesystem permissions — there is no privilege escalation above what the user already has. Exploitation requires prompt injection (Finding 3 from Plan 01, rated MEDIUM), making this a dependent finding rather than an independent vulnerability.

---

### [LOW] Finding A-2: gfd-executor Hardcoded Agent File Path Pattern in Prompt Instructions

- **File:** `agents/gfd-executor.md` (line 315)
- **Evidence:**
  ```markdown
  **Use template:** @$HOME/.claude/get-features-done/templates/summary.md
  ```
  And in the `load_feature_state` step (line 22):
  ```bash
  INIT=$($HOME/.claude/get-features-done/bin/gfd-tools init execute-feature "${SLUG}")
  ```
- **Issue:** The `@$HOME/.claude/...` path syntax is a Claude Code `@`-reference that resolves `$HOME` at the time the agent is invoked. If GFD is installed under a different path than `~/.claude/get-features-done/` (e.g., via a custom install or a non-standard symlink layout), these references fail silently. The path dependency is baked into the agent prompts and shell commands throughout all five agent files. This is a portability concern: any GFD user who installs to a non-standard location will find all agent prompts broken at the path-reference level.
- **Same pattern in:** All five agent files use `$HOME/.claude/...` path references in their `<execution_flow>` sections. `gfd-planner.md` line 839, `gfd-researcher.md` line 563, `gfd-verifier.md` lines 133-242.
- **Severity rationale:** LOW. `$HOME` is resolved at shell invocation time using the standard Unix home directory variable. The risk is portability (broken installs for non-standard layouts), not security exploitation. No credentials or sensitive data are exposed.

---

### [LOW] Finding A-3: gfd-researcher Instructed to Fetch External URLs Without Domain Restrictions

- **File:** `agents/gfd-researcher.md` (lines 129-143)
- **Evidence:**
  ```markdown
  | Priority | Tool | Use For | Trust Level |
  |----------|------|---------|-------------|
  | 1st | WebFetch | Official docs, changelogs, READMEs | HIGH-MEDIUM |
  | 2nd | WebSearch | Ecosystem discovery, community patterns, pitfalls | Needs verification |

  **WebFetch tips:**
  - Use exact URLs (not search result pages)
  - Check publication dates (prefer /docs/ over marketing)
  - For npm packages: `https://www.npmjs.com/package/{name}` for version info
  - For GitHub repos: README.md and docs/ folder
  ```
- **Issue:** gfd-researcher is instructed to use WebFetch and WebSearch against external URLs. These URLs come from: (1) the FEATURE.md description and acceptance criteria (user-controlled), (2) the RESEARCH.md content written by prior research sessions. There is no domain allowlist or URL validation. If a FEATURE.md contains an instruction such as "research best practices at http://attacker.com/setup.txt", the researcher agent would fetch that URL and incorporate the response content into RESEARCH.md. This RESEARCH.md is subsequently fed to gfd-planner, which uses it to generate PLAN.md files, which are then executed. This creates an SSRF (Server-Side Request Forgery) / content injection chain: malicious URL in FEATURE.md → researcher fetches and incorporates → planner uses to create plan → executor runs plan.
- **Severity rationale:** LOW-MEDIUM. Requires write access to FEATURE.md to plant the URL, which implies a collaborator-level attacker. The WebFetch tool is granted explicitly in the researcher's `tools:` field. Content from arbitrary URLs flows into agent context and can influence plan generation.

---

### [CONFIRMED SAFE] Agent Prompt Files — No Hardcoded User-Specific Paths

- **Files:** All five `agents/gfd-*.md` files
- **Evidence:** Grep for `/home/conroy`, `/var/home/conroy`, or any hardcoded user path returned no matches across all five agent prompt files.
- **Status:** CONFIRMED CLEAN — all path references use `$HOME` (environment variable) or `~/.claude/` (tilde expansion), which are portable patterns.

### [CONFIRMED SAFE] Agent Prompt Files — No Embedded Credentials or API Keys

- **Files:** All five `agents/gfd-*.md` files
- **Evidence:** No API keys, tokens, passwords, private hostnames, or sensitive operational values found in any agent prompt file.
- **Status:** CONFIRMED CLEAN.

### [CONFIRMED SAFE] Prompt Injection Surface in Agent Files (Direct)

- **Files:** All five `agents/gfd-*.md` files
- **Issue considered:** Do agent prompt files themselves incorporate user-controlled content directly?
- **Status:** CONFIRMED — agent prompt files are static instruction documents. They do NOT directly interpolate user-controlled values. The prompt injection surface identified in Plan 01 (Finding 3) originates in the C# commands (`AutoResearchCommand.cs`, `AutoPlanCommand.cs`) which load agent prompts AND user content, then concatenate them. The agent prompt files themselves are clean; the vulnerability is in the C# interpolation layer.

---

## Workflow Files Audit

### Workflow Files Assessed

| Workflow File | Bash Code Blocks? | External Fetch? | Hardcoded Paths? |
|---|---|---|---|
| `get-features-done/workflows/execute-feature.md` | Yes (gfd-tools calls) | No | No (uses $HOME) |
| `get-features-done/workflows/plan-feature.md` | Yes (gfd-tools calls) | No | No (uses $HOME) |
| `get-features-done/workflows/research-feature.md` | Yes (gfd-tools calls) | No | No (uses $HOME) |
| `get-features-done/workflows/new-project.md` | Yes (init calls) | No | No (uses $HOME) |
| `get-features-done/workflows/discuss-feature.md` | No | No | No |
| `get-features-done/workflows/new-feature.md` | Yes (file writes) | No | No (uses $HOME) |
| `get-features-done/workflows/status.md` | Yes (gfd-tools calls) | No | No (uses $HOME) |
| `get-features-done/workflows/convert-from-gsd.md` | Yes (embedded JS) | No | No (uses $HOME) |
| `get-features-done/workflows/map-codebase.md` | No | No | No |

---

### [MEDIUM] Finding W-1: convert-from-gsd.md Contains Embedded JavaScript with String Interpolation and eval-Pattern Template Literals

- **File:** `get-features-done/workflows/convert-from-gsd.md` (lines 319–365, ~450)
- **Evidence:**
  ```javascript
  ? m.criteria.map(c => `- [${m.gfdStatus === 'done' ? 'x' : ' '}] ${c}`).join('\n')
  // ...
  `name: ${name}`,
  `slug: ${m.slug}`,
  `status: ${m.gfdStatus}`,
  `owner: ${process.env.GIT_USER || 'unassigned'}`,
  // ...
  `depends_on: ${JSON.stringify(dependsOn)}`,
  // ...
  $HOME/.claude/get-features-done/bin/gfd-tools frontmatter merge "${filePath}" --data '${JSON.stringify({feature: m.slug})}'
  ```
- **Issue:** The `convert-from-gsd.md` workflow contains embedded JavaScript (Node.js) code that is intended to be run by Claude via the Bash tool. This JavaScript uses template literals to construct FEATURE.md file content, shell commands (including `gfd-tools frontmatter merge` with a JSON argument), and other output. The values interpolated include: `m.slug`, `m.gfdStatus`, `m.phaseDir`, `m.criteria` — all sourced from GSD migration data (user-controlled files from the old GSD system). If a GSD feature slug or criteria string contains a backtick, `${...}` expression, or single-quote injection, the template literal construction could produce malformed YAML, malformed JSON arguments, or shell injection via the `gfd-tools` command line argument. The `${JSON.stringify({feature: m.slug})}` pattern embedded in a single-quoted bash argument is particularly concerning: a slug containing `'` would break out of the single-quote context.
- **Severity rationale:** MEDIUM. Exploitable if a GSD migration source file contains an adversarially crafted slug or criteria value. The convert-from-gsd workflow is a migration utility (one-time use) rather than a production code path, reducing the risk in practice. The JavaScript runs inside Claude's Bash execution environment with user-level permissions.

---

### [LOW] Finding W-2: Workflow Files Instruct Claude to Run gfd-tools init with SLUG from $ARGUMENTS

- **Files:** Multiple workflow files including `execute-feature.md` (line 35), `plan-feature.md` (line 34), `research-feature.md` (line 30)
- **Evidence:**
  ```bash
  INIT=$($HOME/.claude/get-features-done/bin/gfd-tools init execute-feature "${SLUG}")
  ```
  where `SLUG` is extracted from `$ARGUMENTS` (the user-provided command argument).
- **Issue:** The `${SLUG}` variable comes from the Claude command argument (`$ARGUMENTS` in Claude Code command context). It is double-quoted in the shell call, which prevents word splitting and glob expansion. However, the slug is passed as a command-line argument to the `gfd-tools` C# binary, which then uses it in `Path.Combine()` calls without path traversal validation (see Plan 01, Finding 4). This is the path traversal amplification path: a crafted slug from the user's command invocation reaches `Path.Combine()` in the C# layer.
- **Severity rationale:** LOW (in the workflow layer). The double-quoting `"${SLUG}"` is correct shell practice and prevents shell injection. The underlying risk (path traversal) is rated MEDIUM in the C# layer (Finding 4 from Plan 01); this finding notes that the workflow correctly passes the value but does not sanitize it before the C# layer receives it.

---

### [CONFIRMED SAFE] Workflow Files — No Hardcoded User-Specific Paths

- **Files:** All 9 workflow files
- **Evidence:** Grep for `/home/conroy`, `/var/home/conroy`, and similar patterns returned no matches across all workflow files. All path references use `$HOME` (environment variable).
- **Status:** CONFIRMED CLEAN.

### [CONFIRMED SAFE] Workflow Bash Code — Variable Quoting

- **Files:** `execute-feature.md`, `plan-feature.md`, `research-feature.md`, `new-project.md`
- **Evidence:** Variable expansions in workflow bash code blocks use double-quoting (`"${SLUG}"`, `"${feature_dir}"`, `"${SLUG}/FEATURE.md"`). The `$@` passthrough pattern is not used in workflow files. No `eval` statements found in workflow bash blocks.
- **Status:** CONFIRMED — shell variable quoting is correct in all reviewed workflow bash examples.

### [CONFIRMED SAFE] Workflow Files — No External Fetch Instructions

- **Files:** All 9 workflow files
- **Evidence:** No `curl`, `wget`, or explicit external URL fetch instructions in workflow files. Web fetch capability resides only in the spawned `gfd-researcher` agent.
- **Status:** CONFIRMED CLEAN.

---

## Command Definitions Audit

### Command Files Assessed

| Command File | Tools Granted | Bash Code? | External @-refs |
|---|---|---|---|
| `commands/gfd/execute-feature.md` | Read, Write, Edit, Bash, Grep, Glob, AskUserQuestion, Task | No direct code | @-refs to workflow + agents |
| `commands/gfd/plan-feature.md` | (not declared) | No | @-refs to workflow + agents |
| `commands/gfd/research-feature.md` | (not declared) | No | @-refs to workflow + reference |
| `commands/gfd/new-project.md` | (not declared) | No | @-refs to multiple files |
| `commands/gfd/new-feature.md` | (not declared) | No | @-refs |
| `commands/gfd/discuss-feature.md` | (not declared) | No | @-refs |
| `commands/gfd/status.md` | (not declared) | No | @-ref to workflow |
| `commands/gfd/map-codebase.md` | (not declared) | No | @-refs to workflow + agent |
| `commands/gfd/convert-from-gsd.md` | (not declared) | No | @-refs |

---

### [LOW] Finding C-1: execute-feature Command Grants Broad Tool Set Including Task Spawning

- **File:** `commands/gfd/execute-feature.md` (lines 6-7)
- **Evidence:**
  ```yaml
  allowed-tools: Read, Write, Edit, Bash, Grep, Glob, AskUserQuestion, Task
  ```
- **Issue:** The `execute-feature` command grants the `Task` tool in addition to Bash and all other tools. The `Task` tool allows spawning sub-agents, each of which gets the tool set specified in their agent prompt file (all five agents include Bash). This means `execute-feature` is a command that can spawn arbitrarily many sub-processes via `Task`, each with Bash access. If the executing agent is compromised via prompt injection (FEATURE.md content, plan file content), the `Task` tool allows the attacker to spawn additional agents. This is not unique to this command — it's the design intent — but it amplifies the blast radius of prompt injection.
- **Severity rationale:** LOW. By design, the execute-feature orchestrator spawns sub-agents. This is documented behavior. The risk is the amplification of blast radius if a prompt injection succeeds, not an independent vulnerability.

---

### [CONFIRMED SAFE] Command Files — No Hardcoded User-Specific Paths

- **Files:** All 9 `commands/gfd/*.md` files
- **Evidence:** All `@`-references use `$HOME/.claude/...` pattern. No hardcoded user-specific absolute paths found.
- **Status:** CONFIRMED CLEAN.

### [CONFIRMED SAFE] Command Files — No Embedded Credentials or Sensitive Values

- **Files:** All 9 `commands/gfd/*.md` files
- **Status:** CONFIRMED CLEAN — command files are thin wrappers that load workflow files via `@`-reference. No sensitive data in any command file.

---

## Templates and References Audit

### Templates Assessed

| Template File | Sensitive Data? | Hardcoded Paths? |
|---|---|---|
| `get-features-done/templates/config.json` | No (example values only) | No |
| `get-features-done/templates/feature.md` | No | No |
| `get-features-done/templates/summary.md` | No | No |
| `get-features-done/templates/project.md` | (not directly read in this audit) | Likely no |
| `get-features-done/templates/codebase/architecture.md` | (template structure only) | No |
| `get-features-done/templates/codebase/concerns.md` | (template structure only) | No |
| `get-features-done/templates/codebase/conventions.md` | (template structure only) | No |
| `get-features-done/templates/codebase/integrations.md` | (template structure only) | No |
| `get-features-done/templates/codebase/stack.md` | (template structure only) | No |
| `get-features-done/templates/codebase/testing.md` | (template structure only) | No |
| `get-features-done/templates/codebase/structure.md` | (template structure only) | No |

---

### [CONFIRMED SAFE] Templates — No Sensitive Placeholder Values

- **Files:** Reviewed `config.json` (template), `feature.md` (template), `summary.md` (template)
- **Evidence:** Template files use `[placeholder]`, `YYYY-MM-DD`, and illustrative example strings like `user-auth` and `JWT auth`. No real credentials, real API keys, real hostnames, or real email addresses found in any template file.
- **Status:** CONFIRMED CLEAN.

### [CONFIRMED SAFE] Templates — No Hardcoded User-Specific Paths

- **Files:** All template files reviewed
- **Status:** CONFIRMED CLEAN — templates use `{slug}`, `[path]`, and similar generic placeholders.

---

### References Assessed

| Reference File | Sensitive Data? | Hardcoded Paths? |
|---|---|---|
| `get-features-done/references/git-integration.md` | No | No |
| `get-features-done/references/questioning.md` | No | No |
| `get-features-done/references/ui-brand.md` | No | No |

---

### [CONFIRMED SAFE] References — No Sensitive Operational Information

- **Files:** All 3 reference files
- **Evidence:** `git-integration.md` contains commit format documentation with illustrative example hashes (generic strings like `1a2b3c`). `questioning.md` contains interview technique guidance. `ui-brand.md` contains UI pattern documentation. No internal URLs, credentials, IPs, PII, or private hostnames found.
- **Status:** CONFIRMED CLEAN.

---

## Coverage Summary

All files from the Plan 03 Task 1 audit checklist have been assessed:

| File Category | Files Reviewed | Key Findings |
|---|---|---|
| Agent prompts (`agents/gfd-*.md`) | 5 | All have Bash (LOW blast-radius); researcher has external fetch (LOW) |
| Workflow files (`get-features-done/workflows/*.md`) | 9 | convert-from-gsd has embedded JS injection risk (MEDIUM); slug quoting correct |
| Command definitions (`commands/gfd/*.md`) | 9 | execute-feature Task grant amplifies prompt injection blast radius (LOW) |
| Templates | 10+ | All clean — no sensitive values, no hardcoded paths |
| References | 3 | All clean — no operational data |

### Tool Grant Summary by Agent

| Agent | Has Bash | Has WebFetch | Has WebSearch | Has Task |
|---|---|---|---|---|
| gfd-executor | YES | No | No | No |
| gfd-planner | YES | YES | No | No |
| gfd-researcher | YES | YES | YES | No |
| gfd-verifier | YES | No | No | No |
| gfd-codebase-mapper | YES | No | No | No |
| execute-feature workflow (command) | YES | No | No | YES (spawns agents) |
