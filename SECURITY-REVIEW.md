# GFD Security Review

**Date:** 2026-02-22
**Scope:** Full codebase audit — bin/, GfdTools/ (C#), .gitea/workflows/, agents/, get-features-done/workflows/, get-features-done/templates/, get-features-done/references/, commands/, install.sh, committed files
**Methodology:** OWASP-applicable static analysis covering injection, path traversal, supply chain, authentication/authorization, privilege escalation, and information disclosure categories

---

## Summary

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 3 |
| Medium | 6 |
| Low | 14 |
| **Total** | **23** |

---

## Critical Findings

No critical severity findings identified.

---

## High Findings

### High-1: Hardcoded Private LAN IP Address

**Location:** `.gitea/workflows/gfd-nightly.yml` (line 4) AND `.gitea/workflows/gfd-process-feature.yml` (line 4)
**Category:** Information Disclosure
**Evidence:**
```yaml
env:
  GITEA_INTERNAL_URL: http://192.168.8.109:3000
```
**Impact:** A private RFC 1918 LAN IP address (`192.168.8.109`) and internal service port (`3000`) are hardcoded in both CI workflow files. When published publicly, this exposes the author's internal network topology: (1) the LAN subnet (`192.168.8.0/24`), (2) the specific host address, (3) the internal Gitea service port. An attacker with knowledge of other exposure vectors (e.g., VPN, leaked credentials, insider access) could use this information to probe or enumerate the internal network.

---

### High-2: Unverified Binary Download of tea CLI

**Location:** `.gitea/workflows/gfd-nightly.yml` (lines 36–38)
**Category:** Supply Chain
**Evidence:**
```yaml
- name: Install tea CLI
  run: |
    curl -sL https://dl.gitea.com/tea/0.10.1/tea-0.10.1-linux-amd64 -o /usr/local/bin/tea
    chmod +x /usr/local/bin/tea
```
**Impact:** The `tea` CLI binary is downloaded from a remote URL without any SHA256 or checksum verification. No integrity check follows the download. After download, the binary is immediately granted execute permissions (`chmod +x`) and installed into `/usr/local/bin/`. This binary is subsequently granted access to a `GITEA_TOKEN` secret (via `tea login add --token "${{ secrets.GITEA_TOKEN }}"`). If `dl.gitea.com` is compromised, a CDN poisoning attack occurs, or the download URL is intercepted (particularly relevant given HTTP-based transmission of the URL used elsewhere), a malicious binary would be installed, granted repository push access, and able to exfiltrate the GITEA_TOKEN secret.

---

### High-3: Workflow Expression Injection — inputs.slug in Shell run: Blocks

**Location:** `.gitea/workflows/gfd-process-feature.yml` (lines 54, 60–63, 69, 72, 80–84, 98)
**Category:** Injection (Shell Injection / Script Injection)
**Evidence:**
```yaml
# Line 54 — branch name construction
run: git checkout -B "ci/${{ inputs.slug }}"

# Lines 60–63 — command arguments
run: |
  if [ "${{ inputs.type }}" = "research" ]; then
    ./get-features-done/bin/gfd-tools auto-research "${{ inputs.slug }}" 2>&1 | tee /tmp/gfd-output.log
  else
    ./get-features-done/bin/gfd-tools auto-plan "${{ inputs.slug }}" 2>&1 | tee /tmp/gfd-output.log
  fi

# Line 69 — commit message
git diff --cached --quiet || git commit -m "ci(${{ inputs.slug }}): auto-${{ inputs.type }} results"

# Line 72 — git push target
run: git push origin "ci/${{ inputs.slug }}"

# Lines 80–84 — JSON API payload (inline JSON construction)
-d '{
  "title": "ci(${{ inputs.slug }}): auto-${{ inputs.type }}",
  "head": "ci/${{ inputs.slug }}",
  "base": "main",
  "body": "Automated ${{ inputs.type }} run for feature: ${{ inputs.slug }}"
}'

# Line 98 — file path in debug step
cat "docs/features/${{ inputs.slug }}/AUTO-RUN.MD" 2>/dev/null
```
**Impact:** `${{ inputs.slug }}` is a Gitea/GitHub Actions workflow expression expanded by the CI engine before the shell sees the run: block. This is a known script injection pattern. The slug flows into six distinct injection contexts: (1) branch name — a slug with `"; malicious_cmd #` breaks out of quoting; (2) command arguments — double-quoted arguments can be escaped with `"`; (3) commit message — `"` terminates the message argument; (4) git push ref — same shell quoting breakout; (5) JSON payload (single-quoted inline) — `'` or `$()` breaks out and executes arbitrary commands; (6) file path — `../../` enables path traversal or `$(cmd)` executes shell commands. Any CI operator who can trigger `workflow_dispatch` with an arbitrary slug value could execute arbitrary commands in the CI runner context, gaining access to `GITEA_TOKEN`, `CREATE_PR_TOKEN`, and `CLAUDE_CODE_OAUTH_TOKEN` secrets stored in the runner environment.

---

## Medium Findings

### Medium-1: Prompt Injection via FEATURE.md Content Interpolation

**Location:** `get-features-done/GfdTools/Commands/AutoResearchCommand.cs` (lines 53–82) and `AutoPlanCommand.cs` (lines 55–91)
**Category:** Injection (Prompt Injection)
**Evidence:**
```csharp
var featureMdContent = File.ReadAllText(featureMdPath);
var prompt = $"""
{agentMd}
...
## FEATURE.md Contents

{featureMdContent}

## Critical Auto-Run Rules

- You are running HEADLESSLY. ...
- On successful completion of research, output exactly:
  `## RESEARCH COMPLETE`
""";
```
**Impact:** User-controlled FEATURE.md content is interpolated directly into the Claude prompt string using C# verbatim string interpolation. If a FEATURE.md contains text mimicking the `## Critical Auto-Run Rules` section or inserting the sentinel string `## RESEARCH COMPLETE`, this can override the auto-run rules or trigger false-positive success detection in `ClaudeService.cs:77` (`stdout.Contains("## RESEARCH COMPLETE")`). A collaborator with repository write access could craft a FEATURE.md that causes the auto-research or auto-plan command to falsely report completion, skip actual research/planning work, and commit a misleading result. In CI automation scenarios (`gfd-process-feature.yml`), this could cause silent failures that appear as successes.

---

### Medium-2: Path Traversal via Unvalidated Slug Input

**Location:** `get-features-done/GfdTools/Services/FeatureService.cs` (line 33); `Commands/FeatureUpdateStatusCommand.cs` (line 30)
**Category:** Path Traversal
**Evidence:**
```csharp
// FeatureService.cs
var featureDir = Path.Combine(cwd, "docs", "features", slug);
var featureMd = Path.Combine(featureDir, "FEATURE.md");
if (!File.Exists(featureMd)) return null;

// FeatureUpdateStatusCommand.cs (write path)
var featureMdPath = Path.Combine(cwd, "docs", "features", slug, "FEATURE.md");
// ...
File.WriteAllText(featureMdPath, newContent);
```
**Impact:** `Path.Combine` on .NET does not prevent directory traversal when a component contains `../`. A slug of `../../etc` produces a path that resolves outside `docs/features/`. The `FeatureUpdateStatusCommand.cs` write path is the most severe: a path-traversal slug would cause `File.WriteAllText()` to overwrite an arbitrary file outside `docs/features/`. This affects all commands that accept slug as user input: `find-feature`, `feature-update-status`, `init execute-feature`, `init plan-feature`, `init new-feature`, `auto-research`, `auto-plan`. Validation gap confirmed: `FeatureService.FindFeature()` checks `string.IsNullOrEmpty(slug)` but does NOT validate against `..` sequences or absolute path characters.

---

### Medium-3: inputs.slug JSON Injection in Nightly Dispatch Payload

**Location:** `.gitea/workflows/gfd-nightly.yml` (lines 78–83)
**Category:** Injection (JSON Injection)
**Evidence:**
```bash
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" \
  -X POST "$GITEA_INTERNAL_URL/api/v1/repos/$REPO/actions/workflows/$WORKFLOW/dispatches" \
  -H "Authorization: token $GITEA_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"ref\":\"main\",\"inputs\":{\"slug\":\"$SLUG\",\"type\":\"$TYPE\"}}")
```
**Impact:** The `$SLUG` value (read from `gfd-tools list-features --status discussed` output, which parses `FEATURE.md` frontmatter) is interpolated into a JSON string via double-quote escaping with bash variable expansion. If a feature slug in a FEATURE.md contains `"` or `\`, the JSON structure is broken. A crafted `slug:` field value like `foo","type":"evil` would inject additional JSON fields into the dispatch payload, potentially altering which workflow inputs are dispatched with. Since `FEATURE.md` files are user-controlled repository files, this creates a path from repository content to CI dispatch manipulation.

---

### Medium-4: Third-Party CI Actions Pinned to Tags, Not Commit SHAs

**Location:** `.gitea/workflows/gfd-nightly.yml` (lines 23, 28); `.gitea/workflows/gfd-process-feature.yml` (lines 33, 38)
**Category:** Supply Chain
**Evidence:**
| Action | Ref Used | Type |
|--------|----------|------|
| `actions/checkout` | `@v4` | Tag — MUTABLE |
| `actions/setup-dotnet` | `@v5` | Tag — MUTABLE |
| `actions/checkout` | `@v4` | Tag — MUTABLE |
| `actions/setup-dotnet` | `@v5` | Tag — MUTABLE |

**Impact:** All four `uses:` entries reference version tags rather than pinned commit SHAs. Version tags are mutable — a compromised upstream repository owner could alter what `@v4` points to. SHA-pinned references are immutable and are the supply chain hardening standard (GitHub security hardening documentation, OpenSSF Scorecard). If `actions/checkout@v4` is modified by a compromised upstream maintainer to exfiltrate secrets, both CI workflows would automatically pull the malicious version on the next run.

---

### Medium-5: config.json Committed Without .gitignore Entry

**Location:** `docs/features/config.json`
**Category:** Information Disclosure / Defense-in-Depth
**Evidence:**
```json
{
  "mode": "yolo",
  "workflow": { "auto_advance": false },
  "team": { "members": [] },
  ...
}
```
**Impact:** `docs/features/config.json` is committed to the repository. The project documentation stated config is not committed to git — but it is, and `.gitignore` does NOT include any entry that would exclude it. Current file contents are benign (operational settings, no credentials). However, the systemic risk is significant: the `team.members` schema field and future config expansions could include email addresses, usernames, or API configuration. The gitignore gap means any future addition of sensitive config values would automatically be committed and exposed. If this repository is published, every future user who adds team members or custom configuration will silently commit those values.

---

### Medium-6: convert-from-gsd Workflow Contains Embedded JavaScript with String Injection Risk

**Location:** `get-features-done/workflows/convert-from-gsd.md` (lines 319–365, ~450)
**Category:** Injection (Template Injection)
**Evidence:**
```javascript
? m.criteria.map(c => `- [${m.gfdStatus === 'done' ? 'x' : ' '}] ${c}`).join('\n')
// ...
`$HOME/.claude/get-features-done/bin/gfd-tools frontmatter merge "${filePath}" --data '${JSON.stringify({feature: m.slug})}'`
```
**Impact:** The `convert-from-gsd.md` workflow contains embedded JavaScript that constructs FEATURE.md file content and shell commands using template literals. Values interpolated include `m.slug`, `m.gfdStatus`, `m.phaseDir`, `m.criteria` — all sourced from GSD migration data (user-controlled files). If a GSD slug contains `'` or backtick characters, the single-quoted shell argument `'${JSON.stringify({feature: m.slug})}'` breaks out of quoting, enabling shell injection into the `gfd-tools frontmatter merge` invocation. Similarly, criteria strings containing backticks or `${...}` sequences could produce malformed YAML content in generated FEATURE.md files.

---

## Low Findings

### Low-1: All Five Agents Granted Bash Tool Access (Blast Radius of Prompt Injection)

**Location:** `agents/gfd-executor.md`, `agents/gfd-planner.md`, `agents/gfd-researcher.md`, `agents/gfd-verifier.md`, `agents/gfd-codebase-mapper.md` — frontmatter `tools:` field
**Category:** Privilege Escalation (Defense-in-Depth)
**Evidence:**
```yaml
# Each agent has:
tools: Read, Write, Edit, Bash, Grep, Glob  # (varies by agent)
```
**Impact:** All five sub-agents are granted Bash tool access, making Bash the universal tool grant. If a prompt injection (Medium-1) succeeds in any agent's context, the attacker gains shell execution capability rather than text manipulation only. This converts a MEDIUM prompt injection finding into a potential code execution scenario. The blast radius is the user's own filesystem permissions (no privilege escalation beyond the user's environment, but all files accessible to the user are reachable).

---

### Low-2: Commit Message Injection via Slug (Cosmetic)

**Location:** `get-features-done/GfdTools/Commands/AutoResearchCommand.cs` (lines 108–110), `AutoPlanCommand.cs` (lines 127–129)
**Category:** Injection (Cosmetic)
**Evidence:**
```csharp
var commitMessage = result.Success
    ? $"feat({slug}): auto-research complete"
    : $"docs({slug}): auto-research aborted — {result.AbortReason}";
GitService.ExecGit(cwd, ["commit", "-m", commitMessage]);
```
**Impact:** The `slug` value is interpolated into the commit message string. No shell injection is possible because `ExecGit` uses `ArgumentList.Add()` (the full message is passed as a single argument). However, a slug containing newlines (e.g., `\n## injected-header`) would produce a multi-paragraph commit message that could mislead `git log` readers or automated tooling that parses commit messages by line.

---

### Low-3: Prompt Sentinel String Injection in ClaudeService.cs

**Location:** `get-features-done/GfdTools/Services/ClaudeService.cs` (lines 77–78)
**Category:** Injection (Enabling Mechanism)
**Evidence:**
```csharp
bool success = stdout.Contains("## RESEARCH COMPLETE", StringComparison.Ordinal)
            || stdout.Contains("## PLANNING COMPLETE", StringComparison.Ordinal);
```
**Impact:** Success detection uses substring matching on the full stdout string. Any content in FEATURE.md or RESEARCH.md that ends up in Claude's output containing the exact sentinel strings would trigger false success. The `## CHECKPOINT` abort detection (line 92) could likewise be falsely triggered by FEATURE.md content containing that string. This is the enabling mechanism for Medium-1 (Prompt Injection via FEATURE.md).

---

### Low-4: Silent Exception Swallowing in GitService.cs

**Location:** `get-features-done/GfdTools/Services/GitService.cs` (lines 40–43)
**Category:** Defense-in-Depth
**Evidence:**
```csharp
catch
{
    return new GitResult(1, string.Empty, "git not available in this environment");
}
```
**Impact:** Any exception from `Process.Start(psi)` is caught and returns exit code 1 with a generic message. Callers such as `AutoResearchCommand.cs` and `AutoPlanCommand.cs` do not check the return value of `GitService.ExecGit()` for staging and commit operations. If git is unavailable or fails silently, the workflow continues without the commit being made. Security implication: audit trails (commits) could silently fail without detection. Additional silent swallows exist in `FeatureService.FindFeature()` (lines 47–50: permission denied on `Directory.GetFiles()` is swallowed), `IsCommitObject()`, and `PackIndexContains()`.

---

### Low-5: Silent Config Parse Failure in ConfigService.cs

**Location:** `get-features-done/GfdTools/Services/ConfigService.cs` (lines 105–108)
**Category:** Defense-in-Depth
**Evidence:**
```csharp
catch
{
    return defaults;
}
```
**Impact:** If `config.json` is malformed JSON, the exception is swallowed and default values are returned silently. A user who has customized their config (e.g., set `auto_advance: true`) would be silently reverted to defaults without warning. A maliciously crafted or corrupted `config.json` could cause the tool to run with `auto_advance: true` (allowing gates to be bypassed) when the user believes they have disabled it.

---

### Low-6: FrontmatterService Custom YAML Parser Edge Cases

**Location:** `get-features-done/GfdTools/Services/FrontmatterService.cs` (lines 14–151)
**Category:** Defense-in-Depth
**Evidence:**
```csharp
var match = SystemRegex.Match(content, @"^---\n([\s\S]*?)\n---", RegexOptions.Multiline);
```
**Impact:** The custom YAML parser handles only a subset of YAML syntax. Key edge cases: (1) Windows-style `\r\n` line endings cause silent parse failure; (2) `int.Parse(value)` without bounds checking — a YAML value exceeding `int.MaxValue` throws uncaught `OverflowException`, potentially crashing the CLI; (3) malformed quoting in YAML values is silently truncated. An adversarially crafted FEATURE.md frontmatter with an integer overflow value could crash the CLI process (requires repository write access to exploit).

---

### Low-7: `verify-path-exists` Command Accepts Absolute Paths Without Restriction

**Location:** `get-features-done/GfdTools/Program.cs` (lines 118–130)
**Category:** Information Disclosure
**Evidence:**
```csharp
var fullPath = Path.IsPathRooted(targetPath) ? targetPath : Path.Combine(cwd, targetPath);
var exists = File.Exists(fullPath) || Directory.Exists(fullPath);
Output.WriteBool("exists", exists);
```
**Impact:** The `verify-path-exists` command accepts any path — including absolute paths like `/etc/passwd` — and confirms whether that path exists on the filesystem. While only a boolean existence check (no content disclosure), this functions as a filesystem oracle that can enumerate the presence of sensitive files and directories. In a CI context where output is logged, this could leak information about the runner's filesystem layout.

---

### Low-8: Hardcoded Agent File Path in AutoResearchCommand / AutoPlanCommand

**Location:** `get-features-done/GfdTools/Commands/AutoResearchCommand.cs` (line 53), `AutoPlanCommand.cs` (line 55)
**Category:** Defense-in-Depth / Supply Chain
**Evidence:**
```csharp
var agentMd = File.ReadAllText(Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".claude/agents/gfd-researcher.md"));
```
**Impact:** The agent prompt file is read from a hardcoded path in the user's home directory. If this path resolves to a symlink (as it does when GFD is installed via `install.sh`, which creates symlinks), a user could replace the symlink with a malicious file to inject arbitrary instructions into the Claude prompt. No integrity verification (hash check, signature) is performed on the agent file content before it is loaded. Additionally, if the file does not exist (`FileNotFoundException`), the command crashes with an unhandled exception rather than providing a useful error message.

---

### Low-9: VerifyCommands.cs Accepts Arbitrary File Paths for Key-Link Verification

**Location:** `get-features-done/GfdTools/Commands/VerifyCommands.cs` (lines 245–251)
**Category:** Path Traversal (Read-Only)
**Evidence:**
```csharp
var fromFullPath = Path.IsPathRooted(from) ? from : Path.Combine(cwd, from);
if (File.Exists(fromFullPath) && !string.IsNullOrEmpty(pattern))
{
    var fromContent = File.ReadAllText(fromFullPath);
    linkVerified = fromContent.Contains(pattern, StringComparison.Ordinal);
}
```
**Impact:** The `verify key-links` subcommand reads `from` paths directly from plan file frontmatter and reads those files' full content into memory. If a plan file contains a `key_links.from` entry pointing to an absolute path (e.g., `/etc/shadow`), the verifier would attempt to read that file. No path containment check is performed. While the output is only a boolean `link_verified`, a timing or error-output side channel could confirm file existence or readability.

---

### Low-10: `generate-slug` Produces No Collision Check

**Location:** `get-features-done/GfdTools/Program.cs` (lines 89–100)
**Category:** Defense-in-Depth
**Evidence:**
```csharp
var slug = System.Text.RegularExpressions.Regex.Replace(
    text.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
```
**Impact:** Two different feature names can produce identical slugs (e.g., "My Feature!" and "My Feature?" both produce `my-feature`). Duplicate slugs cause path collisions in `docs/features/{slug}/` — a new feature with a colliding slug would overwrite or corrupt the existing feature's files. This is a data integrity issue with bounded security impact.

---

### Low-11: install.sh Backup Race Condition (TOCTOU) and Stale Error Message

**Location:** `install.sh` (lines 19–21, 53)
**Category:** Defense-in-Depth
**Evidence:**
```bash
elif [ -d "$TARGET" ]; then
    echo "Backing up existing directory: ${TARGET} → ${TARGET}.bak"
    mv "$TARGET" "${TARGET}.bak"
fi
```
And at line 53:
```bash
echo "Verify with: node ~/.claude/get-features-done/bin/gfd-tools.cjs --help"
```
**Impact:** There is a TOCTOU race between the `[ -d "$TARGET" ]` check and the `mv "$TARGET" "${TARGET}.bak"` operation. On a multi-user system, another process could create or modify `$TARGET` in this window. Additionally, the installer outputs a stale message referencing `gfd-tools.cjs` (a JavaScript file that no longer exists — the codebase was rewritten to C#). Users following this instruction would attempt to invoke a non-existent file.

---

### Low-12: install.sh BASH_SOURCE Symlink Resolution Without Bounds Check

**Location:** `install.sh` (lines 3–6); `get-features-done/bin/gfd-tools` (lines 4–10)
**Category:** Defense-in-Depth
**Evidence:**
```bash
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
```
**Impact:** Both scripts follow symlinks to determine the installation source directory, but neither validates that the resolved directory is within expected bounds. If a user manipulates the symlink chain to point outside the intended directory, the installer would install from an unexpected location. Exploiting this requires the ability to manipulate the symlink chain (implying existing filesystem write access).

---

### Low-13: GITEA_TOKEN Transmitted Over Plaintext HTTP

**Location:** `.gitea/workflows/gfd-nightly.yml` (line 4), `.gitea/workflows/gfd-process-feature.yml` (line 4)
**Category:** Authentication / Information Disclosure
**Evidence:**
```yaml
GITEA_INTERNAL_URL: http://192.168.8.109:3000
```
Token transmitted via `tea login add --url "$GITEA_INTERNAL_URL" --token "${{ secrets.GITEA_TOKEN }}"` and multiple `curl -X POST "$GITEA_INTERNAL_URL/api/v1/..."` calls with `Authorization: token $GITEA_TOKEN`.
**Impact:** All Gitea API calls and `tea` CLI authentication send the `GITEA_TOKEN` over plaintext HTTP on the LAN. While a private LAN reduces interception risk compared to the public internet, HTTP transmits credentials in cleartext visible to any network device on the same subnet. Anyone with passive network access on the `192.168.8.0/24` subnet can capture the token.

---

### Low-14: gfd-researcher Instructed to Fetch External URLs Without Domain Restrictions

**Location:** `agents/gfd-researcher.md` (lines 129–143)
**Category:** Supply Chain / Injection (SSRF)
**Evidence:**
```markdown
| Priority | Tool | Use For | Trust Level |
|----------|------|---------|-------------|
| 1st | WebFetch | Official docs, changelogs, READMEs | HIGH-MEDIUM |
| 2nd | WebSearch | Ecosystem discovery, community patterns, pitfalls | Needs verification |
```
**Impact:** The researcher agent is instructed to use WebFetch and WebSearch against external URLs derived from FEATURE.md content and RESEARCH.md. There is no domain allowlist or URL validation. If a FEATURE.md contains a crafted research URL pointing to an attacker-controlled server, the researcher agent would fetch that URL and incorporate the response content into RESEARCH.md. This RESEARCH.md flows to the planner, which uses it to generate PLAN.md files, which are then executed. This creates a content injection chain from an external URL through the agent pipeline into executable plan files.

---

## Coverage

| File Category | Files Reviewed | Findings |
|---------------|----------------|----------|
| C# source (get-features-done/GfdTools/) | 16 files | 8 findings (Medium-1, Medium-2, Low-2, Low-3, Low-4, Low-5, Low-6, Low-7, Low-8, Low-9, Low-10) |
| Bash scripts | 2 files (bin/gfd-tools, install.sh) | 2 findings (Low-11, Low-12) |
| CI workflows (.gitea/workflows/) | 2 files | 7 findings (High-1, High-2, High-3, Medium-3, Medium-4, Low-13) |
| Agent prompts (agents/) | 5 files | 2 findings (Low-1, Low-14) |
| Workflow files (get-features-done/workflows/) | 9 files | 2 findings (Medium-6, Low in W-2) |
| Command definitions (commands/gfd/) | 9 files | 1 finding (Low-1 contributing) |
| Templates (get-features-done/templates/) | 11 files | 0 findings |
| References (get-features-done/references/) | 3 files | 0 findings |
| Committed files (config.json, .gitignore, AUTO-RUN.md) | 4 files | 2 findings (Medium-5, Low via .gitignore) |

---

## Out of Scope

- Runtime environments users install GFD into
- Remediation approaches (identify-only review)
- Post-installation security configuration
- Test coverage gaps in the C# codebase
