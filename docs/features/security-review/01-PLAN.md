---
feature: security-review
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - docs/features/security-review/findings-code.md
autonomous: true
acceptance_criteria:
  - "All GFD source files reviewed (bin/, workflows/, agents/, references/, CI configs)"
  - "OWASP-applicable vulnerability categories checked (injection, path traversal, secrets exposure, privilege escalation, etc.)"

must_haves:
  truths:
    - "All C# source files in get-features-done/GfdTools/ have been read and analyzed for injection, path traversal, process execution, and input validation issues"
    - "Bash files (gfd-tools wrapper) have been read and analyzed for shell injection and symlink risks"
    - "All findings from C# and bash code audit are documented with location, evidence, and severity"
  artifacts:
    - path: "docs/features/security-review/findings-code.md"
      provides: "Working findings notes for C# and bash code audit"
      contains: "## C# Source Audit"
  key_links:
    - from: "docs/features/security-review/findings-code.md"
      to: "docs/features/security-review/SECURITY-REVIEW.md"
      via: "Plan 03 compiles this into final report"
      pattern: "findings-code.md"
---

<objective>
Audit all C# source files and bash wrapper scripts in the GFD codebase for security vulnerabilities, documenting findings with location, evidence, and preliminary severity.

Purpose: The final SECURITY-REVIEW.md (produced in Plan 03) requires complete findings from all file categories. This plan covers the code layer — C# CLI source and bash scripts — which are the highest-complexity files requiring the most careful reading.

Output: docs/features/security-review/findings-code.md containing all findings from C# and bash code audit, structured for later compilation into SECURITY-REVIEW.md.
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
  <name>Task 1: Audit C# source files for injection, path traversal, and process execution vulnerabilities</name>
  <files>docs/features/security-review/findings-code.md</files>
  <action>
Read every C# source file in get-features-done/GfdTools/ and analyze for security vulnerabilities. Use Glob to find all .cs files, then Read each one.

For each file, check:

**Injection checks:**
- ProcessStartInfo usage: confirm ONLY ArgumentList.Add() is used (never string concatenation into Arguments or UseShellExecute=true). Search for any string.Format(), + operator, or $ string interpolation near ProcessStartInfo.
- File path construction: every Path.Combine() that includes user-supplied input (slug, filename, artifact path) — does the result get validated to stay within the expected directory?
- Prompt construction in ClaudeService.cs and Auto*Command.cs: where is file content read from disk and interpolated into Claude prompt strings? What user-controlled inputs flow in?

**Path traversal checks:**
- FeatureService.cs: check Path.Combine(cwd, "docs", "features", slug) — no slug validation before use?
- FeatureUpdateStatusCommand.cs: same slug pattern.
- InitCommands.cs: slug used in path construction?
- VerifyCommands.cs: artifact paths from frontmatter used in Read calls — are they validated to stay within the repo root? Check for Path.IsPathRooted() guards.
- AutoResearchCommand.cs / AutoPlanCommand.cs: path to agent .md file in ~/.claude/ — is this hardcoded or constructed from input?

**Success signal injection in ClaudeService.cs:**
- Find where output is checked for sentinel strings like "## RESEARCH COMPLETE" or "## PLANNING COMPLETE". Could FEATURE.md content containing these strings cause false-positive success detection?

**Input validation:**
- Program.cs / command registration: are slug/path arguments validated at entry point before being passed to services?
- FeatureUpdateStatusCommand.cs: is status validated against an allowed list? (RESEARCH.md indicates ValidStatuses array exists — confirm it's actually enforced)

**Silent error handling in GitService.cs:**
- What exceptions are caught and swallowed? Could silent failures mask security-relevant events?

**ConfigService.cs:**
- What does silent config parse failure return? Could a malformed config.json cause unexpected behavior that has security implications?

For each finding, record: file path, line range (approximate), vulnerable pattern (quoted code snippet), severity (critical/high/medium/low), and brief explanation of impact.

Write all findings to docs/features/security-review/findings-code.md using this structure:

```
## C# Source Audit

### [SEVERITY] Finding: [Short Name]
- **File:** path/to/file.cs (approx lines N-M)
- **Evidence:** `code snippet`
- **Issue:** explanation
```

Also record CLEAN PASSES (areas checked with no finding) so Plan 03 knows coverage is complete:
```
## Clean Passes (C# Source)
- GitService.cs ArgumentList.Add() usage: CONFIRMED SAFE — no string concatenation into process arguments found
```
  </action>
  <verify>
Read docs/features/security-review/findings-code.md and confirm:
1. File exists and has ## C# Source Audit section
2. At minimum, Finding 3 (prompt injection in AutoResearchCommand.cs/AutoPlanCommand.cs) and Finding 4 (path traversal via slug in FeatureService.cs) are documented — these are the highest-confidence C# findings from research
3. Clean passes section exists showing ArgumentList.Add() pattern was verified
  </verify>
  <done>findings-code.md contains documented findings for all C# files, with evidence quoted from source and severity assigned. Clean passes confirm areas checked with no issue found.</done>
</task>

<task type="auto">
  <name>Task 2: Audit bash wrapper and installer for shell injection, symlink risks, and privilege patterns</name>
  <files>docs/features/security-review/findings-code.md</files>
  <action>
Read get-features-done/bin/gfd-tools and install.sh and analyze for shell security vulnerabilities.

**get-features-done/bin/gfd-tools:**
- Is `exec dotnet run --project "$PROJECT_DIR" -- "$@"` safe? Arguments passed via "$@" are not split by shell — confirm this is the actual pattern used, not some variation with unquoted variables.
- Symlink resolution loop: does the while loop using readlink safely resolve to an absolute path? Is the resolved directory validated to be within expected bounds?
- Any variable substitutions in unquoted context? Any `eval` usage? Any command substitution (`$(...)`) with user-controlled input?
- Does the script handle edge cases: missing PROJECT_DIR, dotnet not found, invalid arguments?

**install.sh:**
- Check the backup mechanism: is there a TOCTOU race between directory existence check and mv operation? (low-risk but document)
- SCRIPT_DIR derivation via BASH_SOURCE[0] and readlink loop: if BASH_SOURCE[0] is a symlink pointing outside the repo, does the installer install from an unexpected location?
- Are symlinks created with absolute or relative paths? Could existing symlinks at target paths be exploited before backup?
- `set -euo pipefail` confirmed? (protective measure)
- Any curl/wget calls? Any external downloads?
- File permission grants: what permissions are set on installed files?

Append findings to docs/features/security-review/findings-code.md under:
```
## Bash Script Audit

### [SEVERITY] Finding: [Short Name]
- **File:** path/to/script (approx lines N-M)
- **Evidence:** `code snippet`
- **Issue:** explanation

## Clean Passes (Bash Scripts)
- install.sh set -euo pipefail: CONFIRMED PRESENT
```
  </action>
  <verify>
Read docs/features/security-review/findings-code.md and confirm:
1. ## Bash Script Audit section exists
2. install.sh TOCTOU backup race (Finding 8 from research) is documented or explicitly noted as not found
3. gfd-tools argument passthrough ("$@") is assessed and result documented
  </verify>
  <done>findings-code.md contains bash script audit results appended to the C# findings. All key areas from the research checklist are assessed with findings or clean-pass confirmations.</done>
</task>

</tasks>

<verification>
Read docs/features/security-review/findings-code.md and verify:
1. File exists at the correct path
2. ## C# Source Audit section is present with multiple subsections
3. ## Bash Script Audit section is present
4. At least one finding per severity level is present, or clear statements that no findings in a given severity were found
5. Clean passes section exists showing what was verified as safe
6. All C# files from the research checklist are mentioned (either as finding or clean pass):
   - Program.cs, FeatureService.cs, GitService.cs, ClaudeService.cs, ConfigService.cs, FrontmatterService.cs, AutoResearchCommand.cs, AutoPlanCommand.cs, FeatureUpdateStatusCommand.cs, VerifyCommands.cs, InitCommands.cs
</verification>

<success_criteria>
- docs/features/security-review/findings-code.md exists with structured findings
- All 11 C# source files from the research checklist are assessed
- Both bash scripts (gfd-tools wrapper, install.sh) are assessed
- Each finding has: file location, code evidence, severity, impact explanation
- ArgumentList.Add() pattern confirmed as safe (clean pass documented)
- Plan 03 can use this file as authoritative input for the C# and bash sections of SECURITY-REVIEW.md
</success_criteria>

<output>
After completion, create `docs/features/security-review/01-SUMMARY.md`
</output>
