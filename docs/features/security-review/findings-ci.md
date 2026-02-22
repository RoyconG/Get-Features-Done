# Security Review: CI Workflow and Committed Files Findings

*Audit date: 2026-02-22*
*Files audited: .gitea/workflows/gfd-nightly.yml, .gitea/workflows/gfd-process-feature.yml*

---

## CI Workflow Audit

### [HIGH] Finding: Hardcoded Private LAN IP Address

- **File:** `.gitea/workflows/gfd-nightly.yml` (line 4) AND `.gitea/workflows/gfd-process-feature.yml` (line 4)
- **Evidence:**
  ```yaml
  env:
    GITEA_INTERNAL_URL: http://192.168.8.109:3000
  ```
- **Issue:** A private RFC 1918 LAN IP address (`192.168.8.109`) and internal port (`3000`) are hardcoded in both CI workflow files. When published publicly, this exposes: (1) internal network topology identifying the author's home LAN subnet, (2) a specific host address on that LAN, (3) the port of the internal Gitea service. This is information an attacker could use to probe or target the internal network if other exposure occurs.

---

### [HIGH] Finding: Unverified Binary Download of tea CLI

- **File:** `.gitea/workflows/gfd-nightly.yml` (lines 36–38)
- **Evidence:**
  ```yaml
  - name: Install tea CLI
    run: |
      curl -sL https://dl.gitea.com/tea/0.10.1/tea-0.10.1-linux-amd64 -o /usr/local/bin/tea
      chmod +x /usr/local/bin/tea
  ```
- **Issue:** The `tea` CLI binary is downloaded from a remote URL without any SHA256 or checksum verification step. No `sha256sum` or `shasum` check follows the download. After download, the binary is immediately granted execute permissions (`chmod +x`) and installed into `/usr/local/bin/`. This binary is subsequently granted access to a `GITEA_TOKEN` secret (via `tea login add --token "${{ secrets.GITEA_TOKEN }}"`). If `dl.gitea.com` is compromised, a CDN poisoning attack occurs, or the download URL is intercepted (especially relevant given the HTTP-based internal URL used elsewhere), a malicious binary would be installed and granted repository access.

---

### [HIGH] Finding: Workflow Expression Injection — inputs.slug in Shell run: Blocks

- **File:** `.gitea/workflows/gfd-process-feature.yml` (lines 54, 60–63, 69, 72, 80–84, 98)
- **Evidence:**
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
- **Issue:** `${{ inputs.slug }}` is a Gitea/GitHub Actions workflow expression that is expanded by the CI engine **before** the shell sees the run: block content. This is a known script injection pattern (GitHub Security Advisory on Expression Injection). The slug flows into six distinct injection contexts:
  1. **Branch name** (`git checkout -B "ci/${{ inputs.slug }}"`) — a slug containing `"; malicious_cmd #` breaks out of the quoted string.
  2. **Command arguments** (passed to `gfd-tools`) — double-quoted but the quotes appear in the expanded YAML string; a slug with `" injection` terminates the argument.
  3. **Commit message** (`-m "ci(${{ inputs.slug }})..."`) — double-quoted; slug with `"` escapes the message.
  4. **Git push ref** (`git push origin "ci/${{ inputs.slug }}"`) — same shell quoting breakout.
  5. **JSON payload (inline)** — the JSON body is a single-quoted heredoc; a slug containing `'` breaks out of single-quote context, and `;` or command substitution `$()` executes arbitrary commands.
  6. **File path** (`cat "docs/features/${{ inputs.slug }}/AUTO-RUN.md"`) — path traversal via `../../` or command execution.

  The `inputs.type` field is slightly mitigated because it is a `choice` type (only `research` or `plan` are valid values), but `inputs.slug` is a free-form string with only `required: true` — no validation. In Gitea's Actions implementation, if expression expansion happens before shell interpretation (as in GitHub Actions), any CI operator or person who can trigger `workflow_dispatch` with an arbitrary slug value could execute arbitrary commands in the CI runner context.

---

### [MEDIUM] Finding: inputs.slug JSON Injection in Nightly Dispatch Payload

- **File:** `.gitea/workflows/gfd-nightly.yml` (lines 78–83)
- **Evidence:**
  ```bash
  HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" \
    -X POST "$GITEA_INTERNAL_URL/api/v1/repos/$REPO/actions/workflows/$WORKFLOW/dispatches" \
    -H "Authorization: token $GITEA_TOKEN" \
    -H "Content-Type: application/json" \
    -d "{\"ref\":\"main\",\"inputs\":{\"slug\":\"$SLUG\",\"type\":\"$TYPE\"}}")
  ```
- **Issue:** The `$SLUG` value (read from `list-features` output, line 94–96) is interpolated into a JSON string via double-quote escaping with bash variable expansion. If a feature slug contains a double-quote or backslash (e.g., `foo","type":"evil`), the JSON structure is broken and the injection could alter the dispatched payload. The value is sourced from the output of `gfd-tools list-features --status discussed` which parses `FEATURE.md` frontmatter — a user-controlled file. A crafted `slug:` value in a FEATURE.md could inject into the JSON API body, potentially dispatching with different inputs than intended.

---

### [MEDIUM] Finding: Third-Party CI Actions Pinned to Tags, Not Commit SHAs

- **File:** `.gitea/workflows/gfd-nightly.yml` (lines 23, 28); `.gitea/workflows/gfd-process-feature.yml` (lines 33, 38)
- **Evidence:**

  | Action | Ref Used | Type |
  |--------|----------|------|
  | `actions/checkout` | `@v4` | Tag — MUTABLE |
  | `actions/setup-dotnet` | `@v5` | Tag — MUTABLE |
  | `actions/checkout` | `@v4` | Tag — MUTABLE |
  | `actions/setup-dotnet` | `@v5` | Tag — MUTABLE |

- **Issue:** All four `uses:` entries reference version tags (`@v4`, `@v5`) rather than pinned commit SHAs (e.g., `actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683`). Version tags are mutable — a compromised upstream repository owner could alter what `@v4` points to. SHA-pinned references are immutable and are the supply chain hardening standard (GitHub security hardening documentation, OpenSSF Scorecard). Both workflow files are equally affected.

---

### [LOW] Finding: GITEA_TOKEN Transmitted Over Plaintext HTTP

- **File:** `.gitea/workflows/gfd-nightly.yml` (line 4), `.gitea/workflows/gfd-process-feature.yml` (line 4); used at lines 42–45 (nightly) and lines 77, 85 (process-feature)
- **Evidence:**
  ```yaml
  GITEA_INTERNAL_URL: http://192.168.8.109:3000
  ```
  Token transmitted via:
  - `tea login add --url "$GITEA_INTERNAL_URL" --token "${{ secrets.GITEA_TOKEN }}"` (nightly, line 42–45)
  - `curl -X POST "$GITEA_INTERNAL_URL/api/v1/repos/$REPO/actions/workflows/$WORKFLOW/dispatches" -H "Authorization: token $GITEA_TOKEN"` (nightly, line 78–82)
  - `curl -X POST ... "$GITEA_INTERNAL_URL/api/v1/repos/${{ github.repository }}/pulls"` (process-feature, line 85)
- **Issue:** All Gitea API calls and the `tea` CLI authentication send the `GITEA_TOKEN` over plaintext HTTP on the LAN. While a private LAN reduces interception risk compared to public internet, HTTP transmits credentials in cleartext visible to any network device on the same subnet. This violates the security best practice of using TLS for credential transmission, even on private networks. Anyone with passive network access on the `192.168.8.0/24` subnet can capture the token.

---

### [LOW] Finding: ANTHROPIC_API_KEY Mapped to Non-Standard Env Var Name

- **File:** `.gitea/workflows/gfd-process-feature.yml` (line 57–58)
- **Evidence:**
  ```yaml
  env:
    CLAUDE_CODE_OAUTH_TOKEN: ${{ secrets.ANTHROPIC_API_KEY }}
  ```
- **Issue:** The `ANTHROPIC_API_KEY` secret is exposed via the non-standard environment variable name `CLAUDE_CODE_OAUTH_TOKEN`. The standard Claude Code credential mechanism uses `ANTHROPIC_API_KEY` as the env var name. This mismatch: (1) suggests the mapping was added as a workaround, (2) may fail silently if Claude Code changes its credential lookup, and (3) creates confusion about what credential type is actually in use (API key vs OAuth token — these are different credential types with different scopes and revocation mechanisms).

---

### [LOW] Finding: Debug Output Step Always Runs and Logs Full Claude Output

- **File:** `.gitea/workflows/gfd-process-feature.yml` (lines 92–102)
- **Evidence:**
  ```yaml
  - name: Debug output
    if: always()
    run: |
      echo "========== GFD Output =========="
      cat /tmp/gfd-output.log 2>/dev/null || echo "No output captured"
      echo "========== AUTO-RUN.md =========="
      cat "docs/features/${{ inputs.slug }}/AUTO-RUN.MD" 2>/dev/null || echo "No AUTO-RUN.md found"
  ```
- **Issue:** The debug step runs unconditionally (`if: always()`) and dumps: (1) the complete Claude stdout output from the GFD tool run (which may include FEATURE.md contents, RESEARCH.md drafts, internal file paths, and any sensitive context that was fed into the prompt); (2) the full AUTO-RUN.md file for the feature slug. If CI logs are accessible to parties beyond the repository owner (e.g., if the Gitea instance is open, if log access is shared with third parties, or if the repo later becomes public), this verbose logging expands the information disclosure surface. Note: this step also references `AUTO-RUN.MD` (uppercase `.MD`) on line 98, but the actual files are `.md` (lowercase) — this is a bug that means the step silently falls through to the `echo "No AUTO-RUN.md found"` branch, so it currently does not expose AUTO-RUN.md content in practice.

---

### [LOW] Finding: CI Token Scope — GITEA_TOKEN Combines Push and Dispatch Privileges

- **File:** `.gitea/workflows/gfd-nightly.yml` (lines 25, 44, 49); `.gitea/workflows/gfd-process-feature.yml` (lines 35, 72)
- **Evidence:**
  - `GITEA_TOKEN` used for: checkout (read), `tea login` auth, workflow dispatch API calls, and `git push origin` (write)
  - `CREATE_PR_TOKEN` used for: PR creation API only
- **Issue:** `GITEA_TOKEN` combines repository read, write (push), and Actions dispatch capabilities in a single credential. The principle of least privilege would recommend separate tokens for read/checkout, push, and workflow dispatch. The use of a separate `CREATE_PR_TOKEN` for PR creation shows awareness of separation concerns, but the main token's combined push+dispatch scope is broader than minimum necessary. If `GITEA_TOKEN` is leaked, an attacker can both write to the repository and trigger arbitrary workflow dispatches.

---

## Clean Passes (CI Workflows)

- **Nightly `git ls-remote` branch check:** CONFIRMED — uses `"$SLUG"` (properly quoted local variable from gfd-tools output, not a workflow expression) in the branch existence check — no injection risk at this point.
- **`tea pulls list` output handling:** CONFIRMED — the `grep -c "ci/$SLUG"` call uses a local shell variable not a workflow expression; the `|| true` prevents pipeline failure propagation correctly.
- **`workflow_dispatch` type input:** CONFIRMED — `inputs.type` is a `choice` type restricted to `research` or `plan` values, providing server-side validation that limits injection surface for that specific input.
- **`.NET` telemetry opt-out:** CONFIRMED — `DOTNET_CLI_TELEMETRY_OPTOUT: true` prevents telemetry callbacks during CI runs.
- **Concurrency controls:** CONFIRMED — both workflows have `concurrency:` groups that prevent parallel duplicate runs; `gfd-process-feature.yml` correctly uses `cancel-in-progress: false` to avoid interrupting in-flight operations.
- **No plaintext secrets in echo/print statements:** CONFIRMED — no `echo $GITEA_TOKEN` or similar statements that would directly print secret values to logs. Secret masking relies on the CI platform's built-in masking.

---

## Committed Files Audit

### [MEDIUM] Finding: config.json Committed Without .gitignore Entry

- **File:** `docs/features/config.json`
- **Evidence (file contents):**
  ```json
  {
    "mode": "yolo",
    "depth": "standard",
    "workflow": {
      "research": true,
      "plan_check": true,
      "verifier": true,
      "auto_advance": false
    },
    "planning": {
      "commit_docs": true,
      "search_gitignored": false
    },
    "parallelization": {
      "enabled": true,
      "plan_level": true,
      "task_level": false,
      "max_concurrent_agents": 3,
      "min_plans_for_parallel": 2
    },
    "team": {
      "members": []
    },
    "gates": {
      "confirm_project": true,
      "confirm_feature": true,
      "confirm_plan": true,
      "execute_next_plan": true,
      "issues_review": true
    },
    "safety": {
      "always_confirm_destructive": true,
      "always_confirm_external_services": true
    }
  }
  ```
- **Issue:** `docs/features/config.json` is committed to the repository. The project documentation (CONCERNS.md) explicitly stated "Config is not committed to git (assumed in .gitignore)" — but the file IS committed and the `.gitignore` does NOT include any entry that would exclude it. Current file contents are benign (operational settings, no credentials). However, the systemic risk is significant: the `team.members` schema field and future config expansions could include email addresses, usernames, or API configuration. The gitignore gap means any future addition of sensitive config values would automatically be committed and exposed. This is a defense-in-depth failure even if the current content is safe.

---

### [LOW] Finding: .gitignore Missing Common Sensitive File Patterns

- **File:** `.gitignore`
- **Evidence (full file contents):**
  ```
  # .NET build output
  get-features-done/GfdTools/bin/
  get-features-done/GfdTools/obj/

  # OS files
  .DS_Store
  Thumbs.db

  # IDE files
  .vs/
  .idea/
  *.user
  *.suo
  ```
- **Issue:** The `.gitignore` is minimal and missing common sensitive file patterns. Specifically absent:
  - `docs/features/config.json` (or `docs/features/*.json`) — the committed config file noted above
  - `.env`, `.env.*`, `*.env` — environment variable files commonly used in development
  - `*.key`, `*.pem`, `*.p12`, `*.pfx` — private key and certificate files
  - `secrets.*`, `credentials.*`, `*.secret` — common credential file naming patterns
  - `*.token` — token storage files
  - `.claude` local config files if any (e.g., `.claude.json` at project root)

  A developer working on this project who follows common patterns (creating `.env` files, storing test certificates) would have those files committed without warning. The absence of these patterns is a safety-net gap.

---

## Hardcoded User Paths Audit

### Scope

Searched for patterns `/home/conroy`, `/var/home/conroy`, `~/.claude`, and general `/home/` or `/var/home/` paths across:
- `agents/` — agent prompt markdown files
- `get-features-done/workflows/` — workflow instruction files
- `get-features-done/templates/` — template files
- `get-features-done/references/` — reference files
- `commands/` — command definition files

### Result

**No hardcoded user-specific paths found** in any of the above directories. Grep across all five directories for `/home/conroy`, `/var/home/conroy`, `~/.claude`, `/home/`, and `/var/home/` returned no matches in the source files checked.

**Note on gfd-process-feature.yml:** Line 45 of the CI workflow contains:
```yaml
mkdir -p "$HOME/.claude/agents"
cp agents/*.md "$HOME/.claude/agents/"
```
This uses the `$HOME` environment variable (not a hardcoded path), which is the correct portable pattern. This is a clean pass.

**Note on RESEARCH.md files and docs:** Hardcoded paths including `/var/home/conroy/Projects/GFD/` appear in research and findings documents (this file, RESEARCH.md). These are documentation artifacts recording audit evidence, not executable source files — they are low-risk as descriptive content but should be reviewed before public release if the author wishes to remove personal path information from committed documentation.

---

## Clean Passes (Committed Files and Paths)

- **AUTO-RUN.md files (cleanup-progress):** CLEAN — `/var/home/conroy/Projects/GFD/docs/features/cleanup-progress/AUTO-RUN.md` contains only feature research summary (commit/file lists, confidence tables). No credentials, IP addresses, personal information, or sensitive internal details beyond the fact that the author uses `/var/home/conroy/` paths (visible in the RESEARCH.md that was committed separately, not in this AUTO-RUN.md).
- **AUTO-RUN.md files (git-worktrees):** CLEAN — `/var/home/conroy/Projects/GFD/docs/features/git-worktrees/AUTO-RUN.md` contains only git worktree research findings. No credentials, IPs, or sensitive content.
- **config.json content:** CONFIRMED benign — contains only operational mode settings (`"mode": "yolo"`, workflow gates). No credentials, API keys, usernames, email addresses, or server addresses in current committed value.
- **Hardcoded user paths in source files (agents/, workflows/, templates/, references/, commands/):** NONE FOUND — all five directories are clean.
- **$HOME usage in CI workflow:** CONFIRMED clean — uses `$HOME` environment variable, not a hardcoded user path.
