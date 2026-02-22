---
feature: security-review
plan: 02
type: execute
wave: 1
depends_on: []
files_modified:
  - docs/features/security-review/findings-ci.md
autonomous: true
acceptance_criteria:
  - "OWASP-applicable vulnerability categories checked (injection, path traversal, secrets exposure, privilege escalation, etc.)"
  - "Supply chain risks assessed (external fetches, untrusted downloads, dependency risks)"
  - "Hardcoded user paths flagged as findings"

must_haves:
  truths:
    - "Both CI workflow YAML files have been read and analyzed for secrets, shell injection, supply chain, and token scope"
    - "Hardcoded private IP address in CI workflows is documented as a finding with exact location and value"
    - "Unverified binary download (tea CLI) is documented as a finding with exact download URL"
    - "Third-party action tag-pinning vs SHA-pinning is assessed and documented"
    - "Committed files (config.json, AUTO-RUN.md files, .gitignore) are reviewed for sensitive content and gitignore gaps"
  artifacts:
    - path: "docs/features/security-review/findings-ci.md"
      provides: "Working findings notes for CI workflow and committed files audit"
      contains: "## CI Workflow Audit"
  key_links:
    - from: "docs/features/security-review/findings-ci.md"
      to: "docs/features/security-review/SECURITY-REVIEW.md"
      via: "Plan 03 compiles this into final report"
      pattern: "findings-ci.md"
---

<objective>
Audit all CI workflow YAML files, the install script's external interactions, and committed files that should be reviewed for sensitive content — documenting all findings for inclusion in SECURITY-REVIEW.md.

Purpose: CI workflows and committed files are the primary surface for supply chain risks, secrets exposure, and hardcoded sensitive values. This plan covers those layers independently of the code audit (Plan 01) so both can run in parallel.

Output: docs/features/security-review/findings-ci.md containing findings from CI workflows, supply chain assessment, and committed file review.
</objective>

<execution_context>
@/home/conroy/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/security-review/FEATURE.md
@docs/features/security-review/RESEARCH.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Audit CI workflow files for secrets, shell injection, supply chain, and token scope</name>
  <files>docs/features/security-review/findings-ci.md</files>
  <action>
Read both CI workflow files in full: .gitea/workflows/gfd-nightly.yml and .gitea/workflows/gfd-process-feature.yml. Analyze each for all security concerns.

**Hardcoded sensitive values:**
- Find GITEA_INTERNAL_URL env var. Record the exact value (including the IP address and port). Is it HTTP or HTTPS? Document as finding with exact line reference and full value.
- Are there any other hardcoded hostnames, IPs, usernames, or tokens in either file (not referencing secrets.* but literal values)?

**Shell injection via workflow expression expansion:**
- In gfd-process-feature.yml, find every use of `${{ inputs.slug }}` and `${{ inputs.type }}` inside `run:` blocks. These expression values are expanded by the CI engine before the shell sees them — this is equivalent to unsanitized user input in a shell script.
- Enumerate EVERY location where inputs.slug appears in a run: block: branch name construction, command arguments, file paths in cat/access, JSON payload construction, commit message content.
- For each location, assess: could a malicious slug value (containing `"`, `;`, `\n`, `$(`, backtick) break out of its context and inject shell commands or corrupt JSON?
- Apply the same check to any other workflow_dispatch inputs used in run: blocks.

**Supply chain — unverified binary download:**
- In gfd-nightly.yml, find the curl download of the tea CLI binary. Record: exact URL, whether any checksum/SHA verification step follows, what permissions are granted after download, and what the binary is used to do (repository access with GITEA_TOKEN).
- Are there any other external downloads (curl, wget, apt-get, etc.) in either workflow?

**Supply chain — action pinning:**
- List every `uses:` entry in both workflow files. For each, record: action name and the ref used (tag like @v4, or full SHA).
- Tag refs are mutable; SHA refs are immutable. Classify each action use as tag-pinned (finding) or SHA-pinned (safe).

**Token scope and credential handling:**
- List all secrets referenced in both workflow files (secrets.*). For each, record: what operations use it, what scope it needs, and whether it appears in any contexts where it could be logged (e.g., in command output that flows to a log step).
- Find the ANTHROPIC_API_KEY → CLAUDE_CODE_OAUTH_TOKEN mapping. Is this a standard env var name for Claude Code? (RESEARCH.md notes this may not be standard.)
- Does the debug output step (if: always()) dump any content that would include secret values indirectly (e.g., full Claude output that might echo back prompt content containing sensitive context)?
- Does any step echo the values of secrets or env vars derived from secrets?

**HTTP vs HTTPS:**
- Where GITEA_INTERNAL_URL is used (in env blocks, in tea CLI calls, in API requests), does the http:// URL mean the GITEA_TOKEN is transmitted in plaintext?

Write all findings to docs/features/security-review/findings-ci.md:

```
## CI Workflow Audit

### [SEVERITY] Finding: [Short Name]
- **File:** .gitea/workflows/filename.yml (line N)
- **Evidence:** `yaml snippet`
- **Issue:** explanation

## Clean Passes (CI Workflows)
- [area]: CONFIRMED — explanation
```
  </action>
  <verify>
Read docs/features/security-review/findings-ci.md and confirm:
1. File exists with ## CI Workflow Audit section
2. Hardcoded private IP finding is documented with exact IP value and line reference
3. tea CLI binary download without checksum is documented (Finding 5 from research)
4. At least 3 uses of ${{ inputs.slug }} in run: blocks are identified and assessed
5. Action pinning assessment lists the uses: entries and their refs
  </verify>
  <done>findings-ci.md contains CI workflow audit results with all pre-identified findings verified plus any additional discoveries. Shell injection vectors, supply chain risks, and token handling are fully assessed.</done>
</task>

<task type="auto">
  <name>Task 2: Audit committed files and .gitignore for sensitive content and gitignore gaps</name>
  <files>docs/features/security-review/findings-ci.md</files>
  <action>
Review committed files that may contain sensitive content or reveal gitignore policy gaps.

**docs/features/config.json:**
- Read the file. Record its full contents (it is expected to be small). What fields are present? Does it contain any credentials, API keys, usernames, email addresses, or server addresses?
- Check CONCERNS.md which states "Config is not committed to git (assumed in .gitignore)" — verify: is docs/features/config.json actually in .gitignore? If not, document as finding even if current content is benign, because the gitignore gap is a systemic risk.

**.gitignore:**
- Read .gitignore in full. Check for:
  - Is docs/features/config.json listed? (Or docs/features/*.json, or similar pattern covering it?)
  - Are there entries for *.env, .env, .env.*, *.key, *.pem, *.p12, secrets.*, credentials.*?
  - Are there entries for common secret file patterns that a developer might accidentally create?
  - Document any significant missing patterns as LOW findings.

**docs/features/*/AUTO-RUN.md files:**
- Use Glob to find all AUTO-RUN.md files in docs/features/. For each:
  - Read the file. AUTO-RUN.md contains the tail of Claude's stdout output ("Claude Output (tail)").
  - Look for: absolute file paths with usernames (e.g., /home/conroy/..., /var/home/conroy/...), internal hostnames, IP addresses, any content that would be sensitive in a public repository.
  - Document any sensitive content found, with exact file path and the sensitive content quoted.
  - If no sensitive content found in a file, note it as clean.

**Hardcoded user paths in agent files:**
- The acceptance criteria requires "hardcoded user paths flagged as findings."
- Use Grep to search for hardcoded paths across all files in agents/, get-features-done/workflows/, get-features-done/templates/, get-features-done/references/, and commands/:
  - Pattern: `/home/conroy` or `/var/home/conroy` or similar user-specific absolute paths
  - Pattern: `~/.claude` (may be acceptable as a standard install path — note if found)
  - Pattern: any other absolute paths that would be user-specific (not generic /usr/, /etc/, /tmp/)
- For each match: record file, line, the path value, and whether it is user-specific (finding) or generic (clean).

Append findings to docs/features/security-review/findings-ci.md:

```
## Committed Files Audit

### [SEVERITY] Finding: [Short Name]
- **File:** path/to/file (line N if applicable)
- **Evidence:** `content`
- **Issue:** explanation

## Hardcoded User Paths Audit

### [SEVERITY] Finding: [Short Name]
- **File:** path/to/file (line N)
- **Evidence:** `/home/conroy/...`
- **Issue:** explanation

## Clean Passes (Committed Files and Paths)
- AUTO-RUN.md files: [list of files checked, result]
```
  </action>
  <verify>
Read docs/features/security-review/findings-ci.md and confirm:
1. ## Committed Files Audit section exists
2. config.json content is documented (including whether it is in .gitignore)
3. .gitignore assessment is present
4. AUTO-RUN.md files are listed with their assessment results
5. ## Hardcoded User Paths Audit section exists with grep results
6. The acceptance criterion "Hardcoded user paths flagged as findings" is addressed (either findings documented or clear confirmation of none found)
  </verify>
  <done>findings-ci.md contains committed files audit and hardcoded path scan results. The gitignore gap for config.json is assessed. AUTO-RUN.md files are individually checked. Hardcoded user paths are identified across agents, workflows, templates, and references.</done>
</task>

</tasks>

<verification>
Read docs/features/security-review/findings-ci.md and verify:
1. File exists with both ## CI Workflow Audit and ## Committed Files Audit sections
2. All pre-identified CI findings from RESEARCH.md are represented:
   - Finding 1 (hardcoded IP): documented with exact value
   - Finding 5 (tea binary download): documented with exact URL
   - Finding 6 (action tag pinning): all uses: entries listed
   - Finding 7 (token scope): GITEA_TOKEN and CREATE_PR_TOKEN assessed
   - Finding 9 (debug log exposure): assessed
   - Finding 11 (HTTP not HTTPS): documented
3. config.json gitignore gap assessed (Finding 2)
4. Hardcoded user paths scan completed
5. AUTO-RUN.md files reviewed for sensitive content
</verification>

<success_criteria>
- docs/features/security-review/findings-ci.md exists with structured findings
- Both CI workflow files fully assessed across all 6 security domains from research
- Supply chain risks quantified: binary download + action pinning both covered
- Hardcoded user paths: grep complete across all non-code file categories
- Committed files reviewed: config.json content documented, .gitignore gaps identified
- Plan 03 can use this file as authoritative input for the CI and committed files sections of SECURITY-REVIEW.md
</success_criteria>

<output>
After completion, create `docs/features/security-review/02-SUMMARY.md`
</output>
