---
feature: security-review
plan: 03
type: execute
wave: 2
depends_on: ["01", "02"]
files_modified:
  - SECURITY-REVIEW.md
autonomous: true
acceptance_criteria:
  - "All GFD source files reviewed (bin/, workflows/, agents/, references/, CI configs)"
  - "OWASP-applicable vulnerability categories checked (injection, path traversal, secrets exposure, privilege escalation, etc.)"
  - "Supply chain risks assessed (external fetches, untrusted downloads, dependency risks)"
  - "Hardcoded user paths flagged as findings"
  - "SECURITY-REVIEW.md produced with findings organized by severity (critical/high/medium/low)"

must_haves:
  truths:
    - "Agent prompt files (agents/gfd-*.md) have been read and analyzed for tool grants, hardcoded paths, and prompt injection surfaces"
    - "Workflow files (get-features-done/workflows/*.md) have been read and analyzed for embedded bash injection risks and hardcoded paths"
    - "Template and reference files have been reviewed for sensitive data patterns"
    - "SECURITY-REVIEW.md exists at the repository root"
    - "SECURITY-REVIEW.md contains findings organized under Critical, High, Medium, and Low severity sections"
    - "Every finding has: location, evidence, severity, and impact description"
    - "No remediation suggestions appear in the report (identify-only per feature spec)"
  artifacts:
    - path: "SECURITY-REVIEW.md"
      provides: "Final security audit report"
      contains: "## Critical"
  key_links:
    - from: "docs/features/security-review/findings-code.md"
      to: "SECURITY-REVIEW.md"
      via: "Read and incorporated into report"
      pattern: "findings-code.md"
    - from: "docs/features/security-review/findings-ci.md"
      to: "SECURITY-REVIEW.md"
      via: "Read and incorporated into report"
      pattern: "findings-ci.md"
---

<objective>
Audit agent prompts, workflow files, templates, and references for security issues — then compile all findings from Plans 01 and 02 together with these new findings into the final SECURITY-REVIEW.md report, organized by severity.

Purpose: This is the culminating plan that produces the deliverable specified in the acceptance criteria. It depends on Plans 01 and 02 having completed their audits and written findings files. This plan audits the remaining file categories (the "markdown-as-executable" layer) and then writes the authoritative report.

Output: SECURITY-REVIEW.md at the repository root, organized by severity (critical/high/medium/low), with all findings from all three audit plans included.
</objective>

<execution_context>
@/home/conroy/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/security-review/FEATURE.md
@docs/features/security-review/RESEARCH.md
@docs/features/security-review/01-SUMMARY.md
@docs/features/security-review/02-SUMMARY.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Audit agent prompts, workflow files, templates, and references</name>
  <files>docs/features/security-review/findings-agents.md</files>
  <action>
Read all agent prompt files, workflow instruction files, command definitions, templates, and reference files. This is the "markdown-as-executable" layer — these files are loaded at runtime and executed as instructions by Claude. They are not passive documentation.

**Agent prompt files (agents/gfd-*.md):**
Use Glob to find all files matching agents/gfd-*.md. For each file, check:
- **Tool grants:** What tools does the agent have access to? Is Bash granted? If so, is this necessary? Bash is the most powerful grant — it allows arbitrary shell command execution. Document every agent with Bash access as a finding if Bash is broader than needed for that agent's purpose.
- **Hardcoded absolute paths:** Any `/home/username/`, `/var/home/username/`, or similar user-specific paths hardcoded in instructions? (These would fail for any user other than the original author.)
- **Embedded credentials or sensitive values:** API keys, tokens, private hostnames?
- **Prompt injection surface:** Does the agent instruct Claude to read external files and incorporate their content into further prompts? (This propagates the Finding 3 prompt injection surface from the C# layer.)

**Workflow instruction files (get-features-done/workflows/*.md):**
Use Glob to find all .md files in get-features-done/workflows/. For each file, check:
- **Embedded bash code blocks:** Are there shell commands with unquoted variables, string concatenation into command arguments, or use of eval? Check for patterns like: `git commit -m "$(...)"`with unquoted variables, commands using `$SLUG` or similar without double-quoting.
- **Hardcoded absolute paths:** Same check as agents — user-specific paths.
- **External fetch instructions:** Do workflows instruct Claude to fetch external URLs that could be attacker-controlled?

**Command definitions (commands/gfd/*.md):**
Use Glob to find all .md files in commands/gfd/. Check:
- **Overly broad tool grants:** Does any command grant tools that are broader than its stated purpose?
- **Hardcoded paths or sensitive values.**

**Templates (get-features-done/templates/**):**
Use Glob to find all files in get-features-done/templates/. Check:
- **Sensitive data in example values:** Do templates include placeholder values that happen to be real credentials, paths, or hostnames?
- **Hardcoded user-specific paths.**

**References (get-features-done/references/*.md):**
Use Glob to find reference files. Check:
- **Operational data:** Do reference files contain sensitive operational information (internal URLs, credentials, PII)?

Write findings to docs/features/security-review/findings-agents.md:

```
## Agent Prompts Audit

### [SEVERITY] Finding: [Short Name]
- **File:** agents/gfd-name.md (approx line N)
- **Evidence:** `excerpt`
- **Issue:** explanation

## Workflow Files Audit
[same structure]

## Templates and References Audit
[same structure]

## Clean Passes (Agents/Workflows/Templates)
- [area]: CONFIRMED — explanation
```

**Note on severity calibration for agents:**
- Bash tool grant to an agent that doesn't need shell access: LOW (increases blast radius of prompt injection but requires a compromised FEATURE.md to exploit)
- Hardcoded user-specific path in agent/workflow: LOW-MEDIUM (breaks portability; affects users who install GFD under a different username)
- External URL fetch in workflow without validation: MEDIUM (if URL is user-controlled input)
  </action>
  <verify>
Read docs/features/security-review/findings-agents.md and confirm:
1. File exists with ## Agent Prompts Audit section
2. All agents/gfd-*.md files are assessed (list them in findings file)
3. get-features-done/workflows/*.md files are assessed
4. Tool grants for each agent are documented
5. Hardcoded path findings (if any) are documented with exact paths
  </verify>
  <done>findings-agents.md contains audit results for all agent prompts, workflow files, command definitions, templates, and references. Tool grants, hardcoded paths, and embedded bash injection risks are assessed for each file category.</done>
</task>

<task type="auto">
  <name>Task 2: Compile SECURITY-REVIEW.md from all findings across Plans 01, 02, and this plan</name>
  <files>SECURITY-REVIEW.md</files>
  <action>
Read all three findings files: docs/features/security-review/findings-code.md, docs/features/security-review/findings-ci.md, docs/features/security-review/findings-agents.md.

Compile all findings into a single SECURITY-REVIEW.md file at the repository root, organized strictly by severity.

**Report structure:**

```markdown
# GFD Security Review

**Date:** 2026-02-22
**Scope:** Full codebase audit — bin/, GfdTools/ (C#), .gitea/workflows/, agents/, get-features-done/workflows/, get-features-done/templates/, get-features-done/references/, commands/, install.sh, committed files
**Methodology:** OWASP-applicable static analysis covering injection, path traversal, supply chain, authentication/authorization, privilege escalation, and information disclosure categories

---

## Summary

| Severity | Count |
|----------|-------|
| Critical | N |
| High | N |
| Medium | N |
| Low | N |
| **Total** | **N** |

---

## Critical Findings

[If none: "No critical severity findings identified."]

### [Finding Name]

**Location:** file path, line range
**Category:** [OWASP category: injection / path traversal / secrets exposure / supply chain / etc.]
**Evidence:**
```
exact code or config snippet
```
**Impact:** [what an attacker could do if this is exploited; no remediation suggestions]

---

## High Findings

[repeat finding format]

---

## Medium Findings

[repeat finding format]

---

## Low Findings

[repeat finding format]

---

## Coverage

| File Category | Files Reviewed | Findings |
|---------------|----------------|----------|
| C# source (get-features-done/GfdTools/) | N files | N findings |
| Bash scripts | 2 files | N findings |
| CI workflows (.gitea/workflows/) | 2 files | N findings |
| Agent prompts (agents/) | N files | N findings |
| Workflow files (get-features-done/workflows/) | N files | N findings |
| Command definitions (commands/gfd/) | N files | N findings |
| Templates (get-features-done/templates/) | N files | N findings |
| References (get-features-done/references/) | N files | N findings |
| Committed files (config.json, AUTO-RUN.md) | N files | N findings |

---

## Out of Scope

- Runtime environments users install GFD into
- Remediation approaches (identify-only review)
- Post-installation security configuration
```

**Critical rules for report compilation:**
- DO NOT include any remediation suggestions, fix recommendations, or "how to resolve" guidance. The feature spec explicitly requires identify-only. No "consider using X instead", no "to fix this, do Y."
- DO include the exact evidence (code/config snippet) for each finding so the reader can locate it without Claude's help.
- DO use the OWASP category labels from the research: injection, path traversal, secrets exposure, supply chain, privilege escalation, information disclosure, authentication/authorization.
- Assign severity using the classifications from RESEARCH.md: Critical (exploitable without user interaction, direct secret exposure), High (minimal attacker preconditions, command injection with user-controlled input), Medium (requires attacker to control some input), Low (defense-in-depth, best-practice deviations).
- If the findings files document any disagreements with the pre-assigned severities from the research, use the executor's assessment based on actual code inspection.
- Number each finding within its severity section for easy reference (e.g., "Critical-1", "High-1", "Medium-1", etc.).
  </action>
  <verify>
1. Run: `ls -la SECURITY-REVIEW.md` — file exists at repo root
2. Run: `grep -c "^### " SECURITY-REVIEW.md` — at least 8 findings sections (matching the 12 pre-identified findings, some may be merged or split)
3. Run: `grep "## Critical\|## High\|## Medium\|## Low" SECURITY-REVIEW.md` — all four severity sections exist
4. Run: `grep -i "remediat\|fix\|resolve\|suggest\|recommend" SECURITY-REVIEW.md` — should return nothing (no remediation language per feature spec)
5. Run: `grep "## Coverage" SECURITY-REVIEW.md` — coverage table exists
  </verify>
  <done>SECURITY-REVIEW.md exists at the repository root with all findings from the three-plan audit organized by severity. The file contains no remediation suggestions. All four severity sections are present. Coverage table shows all file categories reviewed.</done>
</task>

</tasks>

<verification>
Final acceptance verification for the complete security-review feature:

1. `ls SECURITY-REVIEW.md` — report exists at repo root
2. `grep "## Critical\|## High\|## Medium\|## Low" SECURITY-REVIEW.md` — all four severity sections present
3. `grep "^## Coverage" SECURITY-REVIEW.md` — coverage table present showing all file categories
4. `grep -i "to fix\|remediat\|recommend" SECURITY-REVIEW.md` — zero matches (no remediation language)
5. Visually confirm: Hardcoded private IP (192.168.8.109) appears in the report
6. Visually confirm: tea CLI binary download without checksum appears in the report
7. Visually confirm: Prompt injection via FEATURE.md interpolation appears in the report
8. Visually confirm: Path traversal via unvalidated slug appears in the report

All five acceptance criteria from FEATURE.md are satisfied:
- [x] All GFD source files reviewed: Coverage table confirms all categories
- [x] OWASP-applicable categories checked: injection, path traversal, secrets, supply chain, privilege, disclosure all assessed
- [x] Supply chain risks assessed: binary download + action pinning documented
- [x] Hardcoded user paths flagged: grep results from Plans 01 and 02 included
- [x] SECURITY-REVIEW.md produced with severity organization: critical/high/medium/low sections present
</verification>

<success_criteria>
- SECURITY-REVIEW.md exists at the repository root (not in docs/features/)
- All four severity levels (Critical, High, Medium, Low) are present as sections
- At minimum 8 distinct findings documented (12 pre-identified in research; final count may vary)
- Coverage table shows all 9 file categories were reviewed
- Zero remediation language in the report
- Every finding has: location, OWASP category, evidence snippet, impact description
- The three working findings files (findings-code.md, findings-ci.md, findings-agents.md) remain in docs/features/security-review/ as audit trail
</success_criteria>

<output>
After completion, create `docs/features/security-review/03-SUMMARY.md`
</output>
