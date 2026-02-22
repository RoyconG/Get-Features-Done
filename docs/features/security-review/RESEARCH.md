# Feature: Security Review — Research

**Researched:** 2026-02-22
**Domain:** Static security audit of a shell/C#/markdown/CI codebase prior to public release
**Confidence:** HIGH

## User Constraints (from FEATURE.md)

### Locked Decisions
- **Threat scope:** Full OWASP-style sweep plus supply chain risks, including hardcoded user paths
- **Review targets:** Everything in the GFD repo — `bin/`, `workflows/`, `agents/`, `references/`, CI configs (`get-features-done/GfdTools/`, `agents/`, `commands/`, `.gitea/workflows/`, `install.sh`)
- **Claude's discretion:** Whether generated output (FEATURE.md, PLAN.md templates) also needs review
- **Output format:** Single `SECURITY-REVIEW.md` report categorized by severity (critical/high/medium/low)
- **Remediation:** Identify only — no fixes applied, no next-step suggestions in report
- **Motivation:** Pre-publish hygiene before others use GFD

### Out of Scope
- Applying any fixes
- Suggesting remediation approaches
- Reviewing runtime environments users install GFD into

---

## Summary

GFD is a zero-external-dependency developer toolkit consisting of: a C# CLI (`get-features-done/GfdTools/`), a bash wrapper (`get-features-done/bin/gfd-tools`), a bash installer (`install.sh`), Gitea CI workflow YAMLs (`.gitea/workflows/`), agent prompt markdown files (`agents/`), workflow instruction files (`get-features-done/workflows/`), command definitions (`commands/gfd/`), and templates (`get-features-done/templates/`). The security review must cover all of these layers.

The most significant findings from pre-review codebase analysis fall into four categories: (1) a hardcoded private LAN IP address in both CI workflow files that will expose internal network topology upon public release; (2) `docs/features/config.json` committed to the repository and not gitignored, which can contain user-specific settings and is in the public repo; (3) prompt injection vectors in the auto-research/auto-plan commands where user-controlled FEATURE.md content is interpolated directly into the Claude prompt string without sanitization; (4) path traversal risks from unvalidated slug inputs used to construct filesystem paths. The C# rewrite has addressed the most severe historical issue (command injection via `execSync` string concatenation) by using `ArgumentList.Add()` throughout — this is now secure.

**Primary recommendation:** The audit executor must systematically walk each file category, applying OWASP-applicable checks appropriate to that file type (injection for code, secrets/path exposure for config/CI, supply chain for external actions/downloads), then write findings to `SECURITY-REVIEW.md` sorted by severity.

---

## Standard Stack

This is a pure audit task — no libraries or external tools are needed. The audit is performed by reading and analyzing GFD's own files using the Read, Grep, and Glob tools available to the executor agent.

### File Categories and Security Domains to Check

| File Category | Location | Security Domains |
|---------------|----------|-----------------|
| C# CLI source | `get-features-done/GfdTools/**/*.cs` | Injection, path traversal, input validation, process execution |
| Bash wrapper | `get-features-done/bin/gfd-tools` | Shell injection, symlink traversal, argument handling |
| Bash installer | `install.sh` | Shell injection, symlink attacks, privilege escalation |
| CI workflows | `.gitea/workflows/*.yml` | Secrets exposure, supply chain (pinned actions?), hardcoded values, token scope |
| Agent prompts | `agents/gfd-*.md` | Prompt injection vectors, overly broad tool grants, sensitive data in prompts |
| Workflow files | `get-features-done/workflows/*.md` | Prompt injection, bash snippets with injection risk |
| Command defs | `commands/gfd/*.md` | Overly broad tool grants, privilege concerns |
| Templates | `get-features-done/templates/**` | Hardcoded paths, sensitive data patterns |
| References | `get-features-done/references/*.md` | Sensitive operational data |
| Config | `docs/features/config.json` | Committed secrets, sensitive settings |
| .gitignore | `.gitignore` | Missing exclusions for sensitive files |

---

## Architecture Patterns

### How the Audit Should Be Structured

The security review is an audit task, not a build task. The executor reads files, applies checks, and writes findings. There are no external tools to install or configure.

**Recommended audit pass structure:**

1. **Pass 1 — Secrets and Hardcoded Sensitive Values** (across all files)
   - Hardcoded IPs, usernames, hostnames
   - API keys, tokens, passwords in plaintext
   - Private URLs exposed in public files

2. **Pass 2 — Injection Vulnerabilities** (code files: .cs, .sh, bash snippets in .md)
   - Command injection (string concatenation into process arguments)
   - Shell injection in bash scripts
   - Path traversal via unvalidated slugs/filenames
   - Prompt injection (user-controlled content interpolated into LLM prompts)

3. **Pass 3 — Supply Chain Risks** (CI workflow files, install scripts)
   - External downloads without integrity verification (checksums, pinned hashes)
   - Third-party CI actions without pinned SHA references
   - Fetches from mutable URLs (using version tags instead of SHAs)

4. **Pass 4 — Authentication and Authorization**
   - Token scope minimization (are tokens used with least privilege?)
   - Token exposure in logs (are secrets masked?)
   - Unauthenticated command surfaces

5. **Pass 5 — Privilege Escalation and File System**
   - Symlink attacks in installer
   - Path containment (do file operations stay within expected directories?)
   - File permission issues

6. **Pass 6 — Information Disclosure**
   - Sensitive data in committed files
   - Sensitive data in log/output that could leak
   - Files that should be gitignored but aren't

### Severity Classification

Use standard severity classification:

| Severity | Definition |
|----------|-----------|
| **Critical** | Exploitable without user interaction; direct data exfiltration, remote code execution, secret exposure in public repo |
| **High** | Requires minimal attacker preconditions; command injection with user-controlled input, path traversal outside repo |
| **Medium** | Requires attacker to control some input or have partial access; prompt injection risks, weak validation |
| **Low** | Defense-in-depth gaps, information disclosure that aids attackers, best-practice deviations |

---

## Known Findings from Pre-Review Codebase Analysis

These are confirmed issues identified during research. The audit executor must independently verify each and may discover additional findings.

### Finding 1: Hardcoded Private IP in CI Workflows (HIGH confidence)

**Location:** `.gitea/workflows/gfd-nightly.yml:4` and `.gitea/workflows/gfd-process-feature.yml:4`
**Value:** `GITEA_INTERNAL_URL: http://192.168.8.109:3000`
**Issue:** A private LAN IP address is hardcoded in both workflow files. When the repository is published publicly, this exposes:
- Internal network topology (a host at 192.168.8.109 on the author's LAN)
- That the Gitea instance is on a private RFC 1918 address
- The port number of the Gitea service

**Additional issue:** The URL uses `http://` (not `https://`), meaning the GITEA_TOKEN secret is transmitted in plaintext on this private network segment.

### Finding 2: config.json Committed to Repository (MEDIUM confidence)

**Location:** `docs/features/config.json`
**Issue:** The per-project configuration file is committed to the repository. CONCERNS.md explicitly notes:
> "Config is not committed to git (assumed in .gitignore)"

But the file IS committed and `.gitignore` does NOT include `docs/features/config.json`. The file contains operational settings (`"mode": "yolo"`) and while it contains no credentials currently, the config schema supports a `team.members` field and the tool supports environment variable and API key content being added to config. The gitignore gap is a systemic risk even if the current committed value is benign.

### Finding 3: Prompt Injection via FEATURE.md Content Interpolation (MEDIUM confidence)

**Location:** `get-features-done/GfdTools/Commands/AutoResearchCommand.cs:57-82` and `AutoPlanCommand.cs:65-91`
**Issue:** User-controlled FEATURE.md content is read from disk and interpolated directly into the Claude prompt string using C# string interpolation (`$"""`):

```csharp
var featureMdContent = File.ReadAllText(featureMdPath);
var prompt = $"""
{agentMd}
...
{featureMdContent}
...
## Critical Auto-Run Rules
...
""";
```

If a FEATURE.md file contains text like `## Critical Auto-Run Rules` followed by malicious instructions, it could override or conflict with the actual auto-run rules section. Similarly, content containing the sentinel strings `## RESEARCH COMPLETE` or `## PLANNING COMPLETE` could cause premature success detection in `ClaudeService.cs:77-78`.

**Scope:** This is a local-use-only tool, so the attacker model is someone who can write a FEATURE.md file (i.e., the user themselves, or a collaborator with repo write access). The risk is primarily in a shared-repo scenario where a malicious collaborator crafts a FEATURE.md to manipulate auto-run behavior.

### Finding 4: Path Traversal via Unvalidated Slug Input (MEDIUM confidence)

**Location:** `get-features-done/GfdTools/Services/FeatureService.cs:33` and `FeatureUpdateStatusCommand.cs:30`
**Issue:** The `slug` argument from user input is used directly to construct filesystem paths without validation:

```csharp
var featureDir = Path.Combine(cwd, "docs", "features", slug);
// Also:
var featureMdPath = Path.Combine(cwd, "docs", "features", slug, "FEATURE.md");
```

`Path.Combine` on .NET does not prevent directory traversal. If `slug` contains `../`, the resulting path escapes `docs/features/`. Example: slug `../../etc` would produce `docs/features/../../etc` which normalizes to `etc/` at the repo root. The `gfd-tools find-feature` and `feature-update-status` commands both accept slug from external input (the CI workflow passes `inputs.slug` directly).

**CI context:** In `.gitea/workflows/gfd-process-feature.yml:61-63`, the `inputs.slug` value flows from `workflow_dispatch` inputs into shell variable `${{ inputs.slug }}` and then into the `auto-research`/`auto-plan` command as an argument. A malformed slug in a CI dispatch could cause the tool to read/write files outside the feature directory.

### Finding 5: Supply Chain Risk — Unverified Binary Download in CI (HIGH confidence)

**Location:** `.gitea/workflows/gfd-nightly.yml:36-38`
**Issue:** The `tea` CLI binary is downloaded without integrity verification:

```yaml
run: |
  curl -sL https://dl.gitea.com/tea/0.10.1/tea-0.10.1-linux-amd64 -o /usr/local/bin/tea
  chmod +x /usr/local/bin/tea
```

No SHA256 checksum verification is performed. If the download URL is compromised or the CDN is poisoned, a malicious binary is installed and immediately granted execute permissions. The binary is then used to interact with the repository's Gitea instance using a `GITEA_TOKEN` secret.

### Finding 6: Third-Party CI Actions Pinned to Tags, Not SHAs (MEDIUM confidence)

**Location:** `.gitea/workflows/gfd-nightly.yml` and `.gitea/workflows/gfd-process-feature.yml`
**Issue:** External actions are pinned to version tags (`@v4`, `@v5`) rather than immutable commit SHAs:

```yaml
uses: actions/checkout@v4
uses: actions/setup-dotnet@v5
```

Tags are mutable — a compromised upstream repository could alter what `@v4` points to. SHA pinning (`uses: actions/checkout@<full-40-char-sha>`) is the supply chain hardening standard.

**Note:** GitHub Actions-style third-party action resolution behavior in Gitea may differ from GitHub; verify Gitea's caching/trust model for external actions.

### Finding 7: CI Token Scope Ambiguity — Two Separate Tokens (LOW-MEDIUM confidence)

**Location:** `.gitea/workflows/gfd-process-feature.yml`
**Issue:** Two separate tokens are used: `secrets.GITEA_TOKEN` (for checkout, push, and nightly dispatch) and `secrets.CREATE_PR_TOKEN` (for PR creation). Using a separate token for PR creation suggests the main token may lack PR creation scope, which is a positive separation-of-concerns pattern. However:
- The `GITEA_TOKEN` also has push rights (used in `git push origin`) AND is used for the Gitea Actions API dispatch. Combining push + dispatch in one token is broader than needed.
- The `ANTHROPIC_API_KEY` secret is exposed as env var `CLAUDE_CODE_OAUTH_TOKEN` — this works but is a non-standard env var name that may not match future Claude Code credential mechanisms.

### Finding 8: install.sh Symlink-Based Installation Without Path Validation (LOW confidence)

**Location:** `install.sh`
**Issue:** The installer creates symlinks in `~/.claude/` using `SCRIPT_DIR` derived from `BASH_SOURCE[0]`. The script uses `set -euo pipefail` which is good. However:
- The backup mechanism (`mv "$TARGET" "${TARGET}.bak"`) is not atomic — a race condition between the `[ -d "$TARGET" ]` check and the `mv` could be exploited on multi-user systems, though this is low-risk in practice (local developer tool).
- If `BASH_SOURCE[0]` is manipulated (via symlink), the installation source directory could be non-standard. The script follows symlinks via a while loop (`readlink "$REAL_SOURCE"`) but does not validate that the resolved directory is within expected bounds.

### Finding 9: Information Disclosure — Debug Output Includes Full Claude Response (LOW confidence)

**Location:** `.gitea/workflows/gfd-process-feature.yml:92-103`
**Issue:** The "Debug output" step runs `if: always()` and dumps:
```yaml
cat /tmp/gfd-output.log 2>/dev/null || echo "No output captured"
cat "docs/features/${{ inputs.slug }}/AUTO-RUN.md" 2>/dev/null
```
This means the full Claude output (including FEATURE.md content, RESEARCH.md drafts, and any error messages) is always written to CI logs. If CI logs are publicly accessible (e.g., public Gitea repo), sensitive feature content could be exposed. Even in private repos, unnecessarily verbose logs expand the blast radius of any log access breach.

### Finding 10: Commit Message Injection via Slug (LOW confidence)

**Location:** `get-features-done/GfdTools/Commands/AutoResearchCommand.cs:109-110` and `AutoPlanCommand.cs:128-129`
**Issue:** The `slug` value is interpolated into git commit messages:
```csharp
var commitMessage = result.Success
    ? $"feat({slug}): auto-research complete"
    : $"docs({slug}): auto-research aborted — {result.AbortReason}";
GitService.ExecGit(cwd, ["commit", "-m", commitMessage]);
```

The `ExecGit` call uses `ArgumentList.Add()` so the commit message is passed as a single argument (no shell injection). However, a slug containing newlines or special characters could produce unusual commit messages. This is cosmetic but worth flagging.

### Finding 11: HTTP (Not HTTPS) for Internal Gitea URL (LOW confidence)

**Location:** `.gitea/workflows/gfd-nightly.yml:4` and `gfd-process-feature.yml:4`
**Issue:** `GITEA_INTERNAL_URL: http://192.168.8.109:3000` uses plain HTTP. All Gitea API calls (workflow dispatch, PR creation) and `tea` CLI auth transmit the `GITEA_TOKEN` secret over cleartext HTTP on the LAN. On a private LAN this is lower risk than public internet exposure, but is a security best-practice violation.

### Finding 12: Generated Content Files in Review Scope (Claude's Discretion)

The FEATURE.md notes this is "at Claude's discretion." From a security review perspective:

**PLAN.md and FEATURE.md templates** contain no executable code or credentials. They are markdown templates with frontmatter. Security risk: LOW (no actionable findings expected).

**AUTO-RUN.md files** contain the tail of Claude's stdout output. If Claude's output included any sensitive content (API keys echoed during research, internal paths), AUTO-RUN.md would capture and commit it. This is a data-at-rest risk if the repo is public. Current committed AUTO-RUN.md files should be spot-checked.

---

## Audit Checklist for Executor

Use this checklist to ensure complete coverage. Each item maps to a file or code location to examine.

### C# Source Files (`get-features-done/GfdTools/`)

- [ ] `Program.cs` — verify all commands validate slug/path arguments before filesystem ops
- [ ] `Services/FeatureService.cs` — path traversal via slug (Finding 4 above)
- [ ] `Services/GitService.cs` — ArgumentList used (confirmed safe), catch-all exceptions silent (what errors are swallowed?)
- [ ] `Services/ClaudeService.cs` — prompt construction (Finding 3 above), success signal detection via string contains (sentinel string injection)
- [ ] `Services/ConfigService.cs` — silent config parse failure (known bug from CONCERNS.md), JSON injection?
- [ ] `Services/FrontmatterService.cs` — custom YAML parser edge cases, regex pattern safety
- [ ] `Commands/AutoResearchCommand.cs` — prompt injection (Finding 3), path construction for agent .md file (`~/.claude/agents/`)
- [ ] `Commands/AutoPlanCommand.cs` — same as above, partial plan file deletion on abort
- [ ] `Commands/FeatureUpdateStatusCommand.cs` — slug path traversal (Finding 4), status enum validation (present in C# version)
- [ ] `Commands/VerifyCommands.cs` — artifact path traversal (Path.IsPathRooted check present but verify), file read of arbitrary paths in `key-links`
- [ ] `Commands/InitCommands.cs` — slug used in path construction without traversal check

### Bash Files

- [ ] `get-features-done/bin/gfd-tools` — symlink resolution loop (any TOCTOU?), `exec dotnet run --project "$PROJECT_DIR" -- "$@"` (argument passthrough safe because no shell interpolation at this point)
- [ ] `install.sh` — symlink TOCTOU, backup atomicity (Finding 8), unvalidated `SCRIPT_DIR`

### CI Workflow Files

- [ ] `.gitea/workflows/gfd-nightly.yml` — hardcoded IP (Finding 1), binary download without checksum (Finding 5), action pinning (Finding 6), slug injection in shell (the `dispatch()` function uses `"$SLUG"` in JSON payload — check for JSON injection), HTTP not HTTPS (Finding 11)
- [ ] `.gitea/workflows/gfd-process-feature.yml` — hardcoded IP (Finding 1), action pinning (Finding 6), token scope (Finding 7), debug log exposure (Finding 9), `inputs.slug` used in shell without sanitization (shell injection via `${{ inputs.slug }}`), commit message injection (Finding 10)

### Committed Files That Should Be Assessed for Sensitive Content

- [ ] `docs/features/config.json` — content review (Finding 2), gitignore gap
- [ ] `docs/features/*/AUTO-RUN.md` — does any contain unexpected sensitive output?
- [ ] `.gitignore` — missing entries for `docs/features/config.json`, any `*.env`, credentials

### Agent Prompts and Workflows (Prompt Injection Surface)

- [ ] `agents/gfd-researcher.md` — tool grants (Read, Write, Bash, Grep, Glob, WebSearch, WebFetch — is Bash overly broad?)
- [ ] `agents/gfd-executor.md` — tool grants review, any hardcoded paths?
- [ ] `agents/gfd-planner.md` — tool grants, hardcoded path patterns in instructions
- [ ] `agents/gfd-verifier.md` — bash snippets embedded in agent instructions (are they injection-safe?)
- [ ] `agents/gfd-codebase-mapper.md` — notes "scans for secrets" — does it log/output what it finds?
- [ ] `get-features-done/workflows/*.md` — bash code snippets, any string concatenation into commands, hardcoded paths

### Templates and References

- [ ] `get-features-done/templates/**` — any hardcoded paths, credentials, or sensitive data in example values
- [ ] `get-features-done/references/*.md` — any sensitive operational data

---

## Don't Hand-Roll

This is an audit task, not a software build. There are no library choices.

| Problem | Don't Build | Use Instead |
|---------|-------------|-------------|
| Severity classification | Custom scoring | Standard OWASP/CVSSv3 severity labels (critical/high/medium/low) |
| Finding format | Free-form notes | Structured: finding name, location, evidence, severity |

---

## Common Pitfalls

### Pitfall 1: Confusing the JS Tool with the C# Tool

**What goes wrong:** CONCERNS.md documents the old JavaScript `gfd-tools.cjs` with its `execSync` command injection issue. The codebase was rewritten to C# (see `docs/features/csharp-rewrite/`). The C# version uses `ArgumentList.Add()` and does NOT have the `execSync` string concatenation vulnerability.

**How to avoid:** Read the C# source files in `get-features-done/GfdTools/`, not the CONCERNS.md which describes the old JS tool. Confirm the JS tool no longer exists in the active codebase (`get-features-done/bin/` only contains the bash wrapper, not a `.cjs` file).

**Warning signs:** Finding "execSync command injection" in the current codebase without first verifying `gfd-tools.cjs` exists.

### Pitfall 2: Missing the Workflow YAML Bash Injection Vectors

**What goes wrong:** The CI workflow YAML files contain inline bash scripts. The `${{ inputs.slug }}` value from `workflow_dispatch` is interpolated directly into shell commands. This is a GitHub Actions-style script injection pattern where the expression is substituted before the shell sees it.

**How to avoid:** In `.gitea/workflows/gfd-process-feature.yml`, check every use of `${{ inputs.slug }}` in `run:` blocks. The slug flows into: branch name (`git checkout -B "ci/${{ inputs.slug }}"`), command arguments (`./get-features-done/bin/gfd-tools auto-research "${{ inputs.slug }}"`), commit messages, file paths (`cat "docs/features/${{ inputs.slug }}/AUTO-RUN.md"`), and JSON API request body.

**Warning signs:** Any `${{ inputs.* }}` or `${{ github.* }}` used inside `run:` blocks without being assigned to an intermediate env var first.

### Pitfall 3: Treating Markdown Files as Inert

**What goes wrong:** The agent `.md` files and workflow `.md` files contain embedded bash code snippets and are loaded at runtime and executed as instructions by Claude. These are not passive documentation — they are the executable behavior of the system.

**How to avoid:** When reviewing agent and workflow markdown, check embedded bash blocks for shell injection patterns, hardcoded paths, and sensitive data, just as you would a `.sh` file.

### Pitfall 4: Missing the Prompt Injection Attack Surface

**What goes wrong:** In `AutoResearchCommand.cs` and `AutoPlanCommand.cs`, the FEATURE.md contents are interpolated into the Claude prompt string. Standard code security scanners would not flag this as it looks like normal string formatting, but from an LLM security perspective, user-controlled input inside LLM prompts without sandboxing is a prompt injection surface.

**How to avoid:** Check every location where file content read from disk (especially user-editable files like FEATURE.md, RESEARCH.md) is interpolated into a prompt string passed to `ClaudeService.InvokeHeadless`.

---

## Code Examples

### How Process Execution Is Done (C# — SAFE pattern)

The C# codebase correctly uses ArgumentList:

```csharp
// Source: get-features-done/GfdTools/Services/GitService.cs
var psi = new ProcessStartInfo("git")
{
    WorkingDirectory = cwd,
    UseShellExecute = false,
};
foreach (var arg in args)
{
    psi.ArgumentList.Add(arg);  // Each arg added individually — no shell injection
}
```

```csharp
// Source: get-features-done/GfdTools/Services/ClaudeService.cs
psi.ArgumentList.Add("-p");
psi.ArgumentList.Add("--max-turns");
psi.ArgumentList.Add(maxTurns.ToString());
// ...
psi.ArgumentList.Add("--allowedTools");
psi.ArgumentList.Add(tool);  // Tool string added individually
```

**Verify** that no string concatenation exists anywhere that builds command strings (search for `string.Format`, `+` operator with variable interpolation near `ProcessStartInfo` usage).

### How Paths Are Constructed (C# — NEEDS VALIDATION)

```csharp
// Source: get-features-done/GfdTools/Services/FeatureService.cs:33
var featureDir = Path.Combine(cwd, "docs", "features", slug);
// slug is unvalidated — "../.." would escape docs/features/
```

Pattern to look for (path traversal risk):
- `Path.Combine(cwd, ..., slug, ...)` without `slug` validation
- `Path.Combine(cwd, ..., userInput, ...)` generally

### How CI Dispatches the Slug (YAML — INJECTION RISK)

```yaml
# Source: .gitea/workflows/gfd-process-feature.yml:61-63
if [ "${{ inputs.type }}" = "research" ]; then
  ./get-features-done/bin/gfd-tools auto-research "${{ inputs.slug }}" 2>&1 | tee /tmp/gfd-output.log
```

The `${{ inputs.slug }}` is expanded by the CI engine before the shell sees it. A slug of `foo"; malicious_command; echo "` would break out of the quoted context depending on the Gitea Actions expression expansion behavior.

---

## State of the Art

| Old Approach | Current Approach | When Changed |
|--------------|------------------|--------------|
| JS `gfd-tools.cjs` with `execSync` string concat (injection risk) | C# `GfdTools` with `ArgumentList.Add()` (injection-safe) | 2026-02 (csharp-rewrite feature) |
| No status validation in JS version | C# `ValidStatuses` array check before update | 2026-02 (csharp-rewrite feature) |

**Known open issues from CONCERNS.md that are security-relevant:**
- Custom YAML parser in FrontmatterService has edge cases with special characters (not a direct security issue but a correctness issue)
- Config.json parse failures silently return defaults (no security impact currently, but could mask misconfigurations)
- Path construction without validation (Finding 4 above — confirmed not fixed in C# rewrite)

---

## Open Questions

1. **Does Gitea Actions expand `${{ inputs.slug }}` before shell sees it?**
   - What we know: GitHub Actions does this expansion and it is the source of script injection vulnerabilities (see GitHub Security Advisory on expression injection). Gitea Actions aims for compatibility with GitHub Actions syntax.
   - What's unclear: Gitea's exact expansion semantics and whether `workflow_dispatch` inputs have any sanitization.
   - Recommendation: Treat as HIGH risk (assume expansion before shell, flag as injection risk) until confirmed otherwise.

2. **Are AUTO-RUN.md files committed to the public repo and do they contain sensitive content?**
   - What we know: AUTO-RUN.md files are committed by the auto-research/auto-plan commands. The commit includes "Claude Output (tail)" — the last 50 lines of Claude's stdout.
   - What's unclear: Whether any of the committed AUTO-RUN.md files contain paths, usernames, or operational details that shouldn't be public.
   - Recommendation: Spot-check each committed AUTO-RUN.md file during the audit.

3. **What is the threat model for the prompt injection finding?**
   - What we know: FEATURE.md is user-controlled; auto-run commands inline its content into prompts.
   - What's unclear: Whether a sufficiently adversarial FEATURE.md can cause the CI auto-run to produce outputs that exfiltrate data or bypass safety checks in the context of Claude's sandbox.
   - Recommendation: Flag as medium severity; note that exploitation requires write access to the repository.

---

## Sources

### Primary (HIGH confidence — direct codebase inspection)
- `/var/home/conroy/Projects/GFD/get-features-done/GfdTools/Services/ClaudeService.cs` — prompt construction, success signal detection
- `/var/home/conroy/Projects/GFD/get-features-done/GfdTools/Services/GitService.cs` — process execution patterns
- `/var/home/conroy/Projects/GFD/get-features-done/GfdTools/Services/FeatureService.cs` — path construction from slug
- `/var/home/conroy/Projects/GFD/get-features-done/GfdTools/Commands/AutoResearchCommand.cs` — prompt interpolation of FEATURE.md
- `/var/home/conroy/Projects/GFD/get-features-done/GfdTools/Commands/AutoPlanCommand.cs` — prompt interpolation of FEATURE.md
- `/var/home/conroy/Projects/GFD/.gitea/workflows/gfd-nightly.yml` — hardcoded IP, binary download, action pinning
- `/var/home/conroy/Projects/GFD/.gitea/workflows/gfd-process-feature.yml` — slug injection, token handling, debug log exposure
- `/var/home/conroy/Projects/GFD/install.sh` — installer patterns
- `/var/home/conroy/Projects/GFD/docs/features/config.json` — committed config file
- `/var/home/conroy/Projects/GFD/.gitignore` — missing exclusions

### Secondary (MEDIUM confidence — project documentation)
- `docs/features/codebase/CONCERNS.md` — known security considerations (describes JS version; verify against C# source)
- `docs/features/csharp-rewrite/RESEARCH.md` — documents ArgumentList.Add() as the fix for shell injection
- `docs/features/auto-skills/RESEARCH.md` — documents prompt injection risk acknowledgment in comments

---

## Metadata

**Confidence breakdown:**
- Finding inventory: HIGH — all findings based on direct codebase inspection
- Severity ratings: MEDIUM — follow standard OWASP conventions but severity in context depends on deployment model (local tool vs. public CI)
- Completeness: MEDIUM — thorough for known categories; unknown unknowns remain until executor performs full file-by-file pass

**Research date:** 2026-02-22
**Valid until:** 2026-03-22 (stable codebase; findings unlikely to change unless code changes)
