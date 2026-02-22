# Security Review — Code Audit Findings

**Audited:** 2026-02-22
**Scope:** C# source files in `get-features-done/GfdTools/`, bash wrapper `get-features-done/bin/gfd-tools`, bash installer `install.sh`
**Auditor:** GFD Plan 01 Executor

---

## C# Source Audit

### [MEDIUM] Finding 3: Prompt Injection via FEATURE.md Content Interpolation

- **File:** `get-features-done/GfdTools/Commands/AutoResearchCommand.cs` (lines 53–82)
- **Evidence:**
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
- **Issue:** User-controlled FEATURE.md content is interpolated directly into the Claude prompt string using C# verbatim string interpolation. If a FEATURE.md contains text such as `## Critical Auto-Run Rules` followed by adversarial instructions, it can override or conflict with the real auto-run rules section. Content containing the sentinel string `## RESEARCH COMPLETE` inserted into FEATURE.md would cause `ClaudeService.cs:77` (`stdout.Contains("## RESEARCH COMPLETE", StringComparison.Ordinal)`) to detect false-positive success, even if the research task was not actually completed.
- **Same pattern in:** `AutoPlanCommand.cs` (lines 55–91): FEATURE.md and RESEARCH.md contents are both interpolated. Sentinel string is `## PLANNING COMPLETE`.
- **Exploitability:** Requires write access to the FEATURE.md file in the repository (local user or collaborator with repo write access). Primarily a risk in shared-repo or CI automation scenarios.

---

### [MEDIUM] Finding 4: Path Traversal via Unvalidated Slug Input

- **File:** `get-features-done/GfdTools/Services/FeatureService.cs` (line 33)
- **Evidence:**
  ```csharp
  var featureDir = Path.Combine(cwd, "docs", "features", slug);
  var featureMd = Path.Combine(featureDir, "FEATURE.md");
  if (!File.Exists(featureMd)) return null;
  ```
- **Issue:** `Path.Combine` on .NET does not prevent directory traversal when a component contains `../`. A slug of `../../etc` would produce a path that normalizes to `{cwd}/etc/FEATURE.md` — outside the feature directory. The only guard is the `File.Exists(featureMd)` check, which only prevents reading a non-existent file; if such a file happened to exist, it would be read and parsed. The same pattern appears in `FeatureUpdateStatusCommand.cs` (line 30):
  ```csharp
  var featureMdPath = Path.Combine(cwd, "docs", "features", slug, "FEATURE.md");
  ```
  This path is written to: `File.WriteAllText(featureMdPath, newContent)` — a path-traversal slug would cause an overwrite of an arbitrary file outside `docs/features/`.
- **Affected commands:** `find-feature`, `feature-update-status`, `init execute-feature`, `init plan-feature`, `init new-feature`, `auto-research`, `auto-plan` — all accept slug as user input and pass it to `FeatureService.FindFeature()` or construct paths directly.
- **CI context:** `gfd-process-feature.yml` passes `inputs.slug` from `workflow_dispatch` directly to these commands, amplifying risk.
- **Validation gap confirmed:** `FeatureService.FindFeature()` checks `string.IsNullOrEmpty(slug)` (line 30) but does NOT validate against `..` sequences or absolute path characters. No slug normalization or containment check exists anywhere in the codebase.

---

### [LOW] Finding 10: Commit Message Injection via Slug (Cosmetic)

- **File:** `get-features-done/GfdTools/Commands/AutoResearchCommand.cs` (lines 108–110), `AutoPlanCommand.cs` (lines 127–129)
- **Evidence:**
  ```csharp
  var commitMessage = result.Success
      ? $"feat({slug}): auto-research complete"
      : $"docs({slug}): auto-research aborted — {result.AbortReason}";
  GitService.ExecGit(cwd, ["commit", "-m", commitMessage]);
  ```
- **Issue:** The `slug` value is interpolated into the commit message string. The `ExecGit` call uses `ArgumentList.Add()` so the full commit message string is passed as a single `-m` argument — there is no shell injection. However, a slug containing newlines (e.g., `\n## injected-header`) would produce a multi-paragraph commit message that could mislead `git log` readers or automated tooling that parses commit messages by line. This is cosmetic but worth noting.
- **Severity rationale:** No code execution possible; impact is limited to unusual git log entries.

---

### [LOW] Finding: Prompt Sentinel String Injection in ClaudeService.cs

- **File:** `get-features-done/GfdTools/Services/ClaudeService.cs` (lines 77–78)
- **Evidence:**
  ```csharp
  bool success = stdout.Contains("## RESEARCH COMPLETE", StringComparison.Ordinal)
              || stdout.Contains("## PLANNING COMPLETE", StringComparison.Ordinal);
  ```
- **Issue:** Success detection uses substring matching on the full stdout string. This is the mechanism exploited by Finding 3 above: any content in the FEATURE.md that ends up in Claude's output (e.g., echoed back, summarized, or quoted) containing the exact sentinel strings would trigger false success. The detection also covers `## CHECKPOINT` for abort detection (line 92), which could likewise be falsely triggered by FEATURE.md content containing that string. Severity is LOW here as a standalone finding (it's an enabling mechanism for Finding 3, which is rated MEDIUM).

---

### [LOW] Finding: Silent Exception Swallowing in GitService.cs

- **File:** `get-features-done/GfdTools/Services/GitService.cs` (lines 40–43)
- **Evidence:**
  ```csharp
  catch
  {
      return new GitResult(1, string.Empty, "git not available in this environment");
  }
  ```
- **Issue:** Any exception from `Process.Start(psi)` (including `IOException`, `UnauthorizedAccessException`, and `Win32Exception`) is caught and returns exit code 1 with a generic message. Callers such as `AutoResearchCommand.cs` and `AutoPlanCommand.cs` do not check the return value of `GitService.ExecGit()` when staging and committing files. If git is unavailable or fails silently, the workflow continues without the commit being made — but the code does not detect or report this. Security implication: audit trails (commits) could silently fail without detection.
- **Additional silent-swallow:** `FeatureService.FindFeature()` lines 47–50:
  ```csharp
  catch
  {
      files = [];
  }
  ```
  If `Directory.GetFiles()` throws (e.g., permission denied), the error is swallowed and the feature appears to have no plans/summaries, which could mask state inconsistencies.
- **Also:** `IsCommitObject()` and `PackIndexContains()` catch all exceptions silently (lines 136–138, 193–194).

---

### [LOW] Finding: Silent Config Parse Failure in ConfigService.cs

- **File:** `get-features-done/GfdTools/Services/ConfigService.cs` (lines 105–108)
- **Evidence:**
  ```csharp
  catch
  {
      return defaults;
  }
  ```
- **Issue:** If `config.json` is malformed JSON, or if `JsonDocument.Parse` throws for any reason, the exception is swallowed and default values are returned silently. A user who has customized their config (e.g., changed `mode` from `yolo` to `standard`, or set `auto_advance: true`) would be silently reverted to defaults without any warning. Security implication: a maliciously crafted or corrupted `config.json` could cause the tool to run with `auto_advance: true` (allowing gates to be bypassed) when the user believes they have disabled it. The impact is bounded by the fact that `auto_advance: true` is already a user-opt-in behavior.

---

### [LOW] Finding: FrontmatterService Custom YAML Parser Edge Cases

- **File:** `get-features-done/GfdTools/Services/FrontmatterService.cs` (lines 14–151)
- **Evidence:**
  ```csharp
  var match = SystemRegex.Match(content, @"^---\n([\s\S]*?)\n---", RegexOptions.Multiline);
  ```
- **Issue:** The custom YAML parser handles only a subset of YAML syntax. Specific edge cases:
  1. The frontmatter boundary regex requires `^---\n` — a file with Windows-style `\r\n` line endings would fail to parse (no finding produced, silently returns empty dict).
  2. `int.Parse(value)` (line 133) is called without bounds checking — a YAML value of `99999999999999` (exceeding `int.MaxValue`) would throw an `OverflowException`. However, this exception would propagate up uncaught from `Extract()`, potentially crashing the caller. The plan-structure verifier (`VerifyCommands.cs`) and feature update command call `Extract()` without catching this.
  3. A YAML value of the form `key: "value with 'nested quotes"` or `key: 'value with "nested quotes'` is handled by `UnquoteString` (line 224) which strips only matching outermost quotes — malformed quoting is silently truncated or left unquoted.
- **Security implication:** These are primarily correctness issues. An adversarially crafted FEATURE.md frontmatter with an integer overflow value could crash the CLI process. Severity remains LOW due to the requirement for write access to the FEATURE.md.

---

### [LOW] Finding: `verify-path-exists` Accepts Absolute Paths Without Restriction

- **File:** `get-features-done/GfdTools/Program.cs` (lines 118–130)
- **Evidence:**
  ```csharp
  var fullPath = Path.IsPathRooted(targetPath) ? targetPath : Path.Combine(cwd, targetPath);
  var exists = File.Exists(fullPath) || Directory.Exists(fullPath);
  Output.WriteBool("exists", exists);
  ```
- **Issue:** The `verify-path-exists` command accepts any path — including absolute paths like `/etc/passwd` — and will confirm whether that path exists on the filesystem. While this is a read-only existence check with no file content disclosure, it functions as a filesystem oracle that can enumerate the presence of sensitive files and directories. In a CI context where output is logged, this could leak information about the runner's filesystem layout.
- **Severity rationale:** No file content is read; only boolean existence returned. Impact is informational.

---

### [LOW] Finding: AutoResearchCommand Uses Hardcoded Path to Agent File

- **File:** `get-features-done/GfdTools/Commands/AutoResearchCommand.cs` (line 53)
- **Evidence:**
  ```csharp
  var agentMd = File.ReadAllText(Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
      ".claude/agents/gfd-researcher.md"));
  ```
- **Same pattern in:** `AutoPlanCommand.cs` (line 55): `~/.claude/agents/gfd-planner.md`
- **Issue:** The agent prompt file is read from a hardcoded path in the user's home directory (`~/.claude/agents/`). If this path resolves to a symlink (as it does when GFD is installed via `install.sh`, which creates symlinks), a user could replace the symlink with a malicious file to inject arbitrary instructions into the Claude prompt. There is no integrity verification (hash check, signature) on the agent file content before it is loaded into the prompt.
- **Additional finding:** If the file does not exist (`FileNotFoundException`), the command crashes with an unhandled exception rather than providing a useful error message — this is also a robustness issue.
- **Severity rationale:** Exploiting this requires local filesystem access to modify `~/.claude/agents/`; in single-user local-tool scenarios this is the user attacking themselves. Severity increases in multi-user environments.

---

### [LOW] Finding: VerifyCommands.cs Accepts Arbitrary File Paths for Key-Link Verification

- **File:** `get-features-done/GfdTools/Commands/VerifyCommands.cs` (lines 245–251)
- **Evidence:**
  ```csharp
  var fromFullPath = Path.IsPathRooted(from) ? from : Path.Combine(cwd, from);
  if (File.Exists(fromFullPath) && !string.IsNullOrEmpty(pattern))
  {
      var fromContent = File.ReadAllText(fromFullPath);
      linkVerified = fromContent.Contains(pattern, StringComparison.Ordinal);
  }
  ```
- **Issue:** The `verify key-links` subcommand reads `from` paths directly from the plan file's frontmatter and reads those files' full content into memory. If a plan file contains a `key_links.from` entry pointing to an absolute path (e.g., `/etc/shadow`), the verifier would attempt to read that file. No path containment check is performed. The output is only a boolean `link_verified`, so file content is not directly disclosed — but a timing or error-output side channel could confirm file existence or readability.
- **Artifact path check in `CreateArtifacts()`** has the same pattern (line 185–188): artifact paths from frontmatter are combined with `cwd` or used directly if absolute.

---

### [LOW] Finding: `generate-slug` Regex Produces Predictable Output but No Collision Check

- **File:** `get-features-done/GfdTools/Program.cs` (lines 89–100)
- **Evidence:**
  ```csharp
  var slug = System.Text.RegularExpressions.Regex.Replace(
      text.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
  ```
- **Issue:** The slug generator strips all non-alphanumeric characters and replaces them with `-`. Two different feature names can produce identical slugs (e.g., "My Feature!" and "My Feature?" both produce `my-feature`). While not a direct security issue, duplicate slugs cause path collisions in `docs/features/{slug}/` — a new feature with a colliding slug would overwrite or corrupt the existing feature's files. This is a data integrity issue rather than a security vulnerability.

---

## Clean Passes (C# Source)

- **GitService.cs ArgumentList.Add() usage:** CONFIRMED SAFE — `ExecGit()` uses `foreach (var arg in args) { psi.ArgumentList.Add(arg); }` throughout. `UseShellExecute = false` is set. No string concatenation into process arguments found anywhere in the codebase.
- **ClaudeService.cs ArgumentList.Add() usage:** CONFIRMED SAFE — all claude arguments (`-p`, `--max-turns`, `--model`, `--allowedTools`, `--output-format`, and all tool strings) are added individually via `psi.ArgumentList.Add()`. No string interpolation into argument strings.
- **FeatureUpdateStatusCommand.cs status validation:** CONFIRMED ENFORCED — `ValidStatuses` array is defined and checked at line 27: `if (!ValidStatuses.Contains(newStatus)) return Output.Fail(...)`. The status is validated against the allowed list before any filesystem operations.
- **Program.cs entry point validation:** The slug argument in `find-feature` is passed directly to `FeatureService.FindFeature()`, which checks `string.IsNullOrEmpty(slug)` but does NOT validate for path traversal sequences (see Finding 4). No other input validation at the entry point level. Commands using `System.CommandLine` framework benefit from type-safe argument parsing (typed as `string`, not arbitrary shell input), which prevents shell metacharacter injection at the process level — but does not prevent filesystem path traversal.
- **ConfigService.cs JSON injection:** CONFIRMED NOT APPLICABLE — `JsonDocument.Parse` is used (System.Text.Json), which is a safe parser. Config values are read and mapped to typed fields; there is no JSON injection risk from the config file into the application's behavior beyond the configured values themselves.
- **FrontmatterService.cs regex safety:** CONFIRMED — the regex `@"^---\n([\s\S]*?)\n---"` uses non-greedy matching (`*?`) and has no catastrophic backtracking risk with well-formed input. Pathological inputs (deeply nested alternations) are not present.
- **InitCommands.cs `new-feature` and `plan-feature`:** Slug input used in `Output.Write("feature_dir", $"docs/features/{slug}")` — this writes the traversal path to output but does not perform filesystem operations with the unvalidated slug at this point (only `FeatureService.FindFeature` is called, which has the path traversal issue covered in Finding 4).
- **AutoResearchCommand.cs / AutoPlanCommand.cs `AbortReason` in commit message:** The `AbortReason` field in `RunResult` is derived from internal string constants and stderr content — not directly from user-supplied slug input. No injection risk via this path.
- **HistoryDigestCommand.cs and SummaryExtractCommand.cs:** Read file paths constructed from `FeatureService.ListFeatures()` output (directory-enumerated paths, not user input). No direct user-controlled path injection. The `Directory.GetFiles()` enumeration is constrained to `docs/features/` subdirectories.
- **FrontmatterCommands.cs `frontmatter merge` JSON input:** `ParseJsonToDictionary` uses `System.Text.Json` safely. The `--data` JSON option is parsed through the typed command-line framework. No shell injection risk.

---

## Bash Script Audit

### [LOW] Finding 8: install.sh Backup Race Condition (TOCTOU)

- **File:** `install.sh` (lines 19–21)
- **Evidence:**
  ```bash
  elif [ -d "$TARGET" ]; then
      echo "Backing up existing directory: ${TARGET} → ${TARGET}.bak"
      mv "$TARGET" "${TARGET}.bak"
  fi
  ```
- **Issue:** There is a time-of-check to time-of-use (TOCTOU) race between the `[ -d "$TARGET" ]` check and the `mv "$TARGET" "${TARGET}.bak"` operation. On a multi-user system, another process could create or modify `$TARGET` in this window. In practice this is very low risk for a local developer tool, but the pattern is worth noting. The backup mechanism is also non-atomic: if `mv` fails partway through, `$TARGET` may be in an undefined state.
- **Additional finding:** The installer outputs a stale message at line 53: `echo "Verify with: node ~/.claude/get-features-done/bin/gfd-tools.cjs --help"`. The JavaScript tool (`gfd-tools.cjs`) no longer exists — the codebase was rewritten to C#. This outdated instruction references the old tool that has been removed. While not a security issue, it may cause users to install an environment expecting a non-existent file and attempt to execute it.

---

### [LOW] Finding 8b: install.sh BASH_SOURCE Symlink Resolution Without Bounds Check

- **File:** `install.sh` (lines 3–6, also `gfd-tools` wrapper lines 4–10)
- **Evidence (install.sh):**
  ```bash
  SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  ```
  **Evidence (gfd-tools wrapper):**
  ```bash
  REAL_SOURCE="${BASH_SOURCE[0]}"
  while [ -L "$REAL_SOURCE" ]; do
    LINK_DIR="$(cd "$(dirname "$REAL_SOURCE")" && pwd)"
    REAL_SOURCE="$(readlink "$REAL_SOURCE")"
    [[ "$REAL_SOURCE" != /* ]] && REAL_SOURCE="$LINK_DIR/$REAL_SOURCE"
  done
  SCRIPT_DIR="$(cd "$(dirname "$REAL_SOURCE")" && pwd)"
  ```
- **Issue:** Both scripts follow symlinks to determine the installation source directory, but neither validates that the resolved directory is within expected bounds (e.g., within the repository or within `~/.claude/`). If a user manipulates the symlink chain to point outside the intended directory (e.g., to a directory controlled by a different user or process), the installer would install from an unexpected location. The `gfd-tools` wrapper's while-loop correctly handles relative symlinks (converting them to absolute paths), which is the correct defensive pattern. However, there is no check that `SCRIPT_DIR` resolves to a path ending in `.../get-features-done/bin/`.
- **Severity rationale:** Exploiting this requires the ability to manipulate the symlink chain, which implies the attacker already has filesystem write access. Low risk in practice.

---

### [CONFIRMED SAFE] install.sh `set -euo pipefail`

- **File:** `install.sh` (line 2)
- **Evidence:** `set -euo pipefail`
- **Status:** CONFIRMED PRESENT — the script exits immediately on any unset variable reference (`-u`), command failure (`-e`), or pipe failure (`-o pipefail`). This is the correct defensive posture.

---

### [CONFIRMED SAFE] gfd-tools Argument Passthrough

- **File:** `get-features-done/bin/gfd-tools` (line 12)
- **Evidence:** `exec dotnet run --project "$PROJECT_DIR" -- "$@"`
- **Status:** CONFIRMED SAFE — arguments are passed via `"$@"` (double-quoted), which prevents word splitting and glob expansion of individual arguments. The `--` separator ensures arguments following it are treated as positional arguments to the `dotnet run` process, not as `dotnet run` options. The project path (`$PROJECT_DIR`) is quoted with double quotes. No `eval` usage, no command substitution with user-controlled input, no unquoted variable interpolation.

---

### [CONFIRMED SAFE] gfd-tools Symlink Resolution Loop

- **File:** `get-features-done/bin/gfd-tools` (lines 4–10)
- **Status:** CONFIRMED — the while loop correctly handles the case where `readlink` returns a relative path (the `[[ "$REAL_SOURCE" != /* ]]` guard). The loop terminates naturally when `REAL_SOURCE` is no longer a symlink (`-L` test fails). No infinite loop risk (symlink cycles are bounded by filesystem limits on Linux). No `eval` or subshell injection risk in the loop body.

---

### [CONFIRMED SAFE] install.sh External Downloads

- **File:** `install.sh`
- **Status:** CONFIRMED NOT PRESENT — `install.sh` contains no `curl`, `wget`, `fetch`, or any external download commands. All installation is performed via local symlink creation (`ln -s`). No external binary downloads, no network access.

---

### [CONFIRMED SAFE] install.sh File Permissions

- **File:** `install.sh`
- **Status:** CONFIRMED — the installer creates symlinks only; it does not `chmod` any files. Permissions of the installed files are inherited from the repository source files. The installer does not use `sudo` or elevate privileges. No setuid/setgid bits are set.

---

## Coverage Summary

All files from the Plan 01 audit checklist have been assessed:

| File | Status | Key Finding |
|------|--------|-------------|
| `Program.cs` | REVIEWED | No slug validation at entry point; `verify-path-exists` is a filesystem oracle |
| `Services/FeatureService.cs` | REVIEWED | Path traversal via slug (Finding 4) |
| `Services/GitService.cs` | REVIEWED | ArgumentList safe; silent exception swallowing |
| `Services/ClaudeService.cs` | REVIEWED | Sentinel string injection enabling mechanism; ArgumentList safe |
| `Services/ConfigService.cs` | REVIEWED | Silent config parse failure; JSON parsing safe |
| `Services/FrontmatterService.cs` | REVIEWED | Custom parser edge cases (int overflow, CRLF); no regex ReDoS |
| `Commands/AutoResearchCommand.cs` | REVIEWED | Prompt injection (Finding 3); hardcoded agent path |
| `Commands/AutoPlanCommand.cs` | REVIEWED | Prompt injection (Finding 3); hardcoded agent path |
| `Commands/FeatureUpdateStatusCommand.cs` | REVIEWED | Path traversal (Finding 4); status validation CONFIRMED SAFE |
| `Commands/VerifyCommands.cs` | REVIEWED | Arbitrary file path reads via plan frontmatter |
| `Commands/InitCommands.cs` | REVIEWED | Slug used in output string but not filesystem ops at this level |
| `Commands/FrontmatterCommands.cs` | REVIEWED | JSON parsing safe; file paths from user input not validated |
| `Commands/HistoryDigestCommand.cs` | REVIEWED | Paths from enumeration, not user input; safe |
| `Commands/SummaryExtractCommand.cs` | REVIEWED | Path from user input; no traversal check but only read |
| `Commands/ConfigGetCommand.cs` | REVIEWED | Reads config only; no path injection risk |
| `get-features-done/bin/gfd-tools` | REVIEWED | Argument passthrough safe; symlink loop safe |
| `install.sh` | REVIEWED | TOCTOU backup race (low); stale error message; no external downloads |
