---
name: Security Review
slug: security-review
status: in-progress
owner: Conroy
assignees: []
created: 2026-02-20
priority: high
depends_on: []
---
# Security Review

## Description

Comprehensive security audit of the entire GFD codebase — shell scripts, workflow definitions, agent prompts, reference files, and CI configs — to identify vulnerabilities before publishing. Covers all OWASP-applicable categories plus supply chain risks and hardcoded path exposure. Produces a severity-categorized markdown report of findings without applying fixes.

## Acceptance Criteria

- [ ] All GFD source files reviewed (bin/, workflows/, agents/, references/, CI configs)
- [ ] OWASP-applicable vulnerability categories checked (injection, path traversal, secrets exposure, privilege escalation, etc.)
- [ ] Supply chain risks assessed (external fetches, untrusted downloads, dependency risks)
- [ ] Hardcoded user paths flagged as findings
- [ ] SECURITY-REVIEW.md produced with findings organized by severity (critical/high/medium/low)

## Tasks

- [01-PLAN.md](01-PLAN.md) — Audit C# source files and bash scripts for injection, path traversal, and process execution vulnerabilities
- [02-PLAN.md](02-PLAN.md) — Audit CI workflows, committed files, and hardcoded paths for supply chain, secrets, and shell injection risks
- [03-PLAN.md](03-PLAN.md) — Audit agent prompts, workflow files, templates, and references; compile SECURITY-REVIEW.md

## Notes

### Implementation Decisions
- **Threat scope:** Full OWASP-style sweep plus supply chain risks, including hardcoded user paths
- **Review targets:** Everything in the GFD repo (bin/, workflows/, agents/, references/, CI configs)
- **Claude's discretion:** Whether generated output (FEATURE.md, PLAN.md templates) also needs review
- **Output format:** Single SECURITY-REVIEW.md report categorized by severity (critical/high/medium/low)
- **Remediation:** Identify only — no fixes applied, no next-step suggestions in report
- **Motivation:** Pre-publish hygiene before others use GFD

## Decisions

### Plan 01: C# and Bash Code Audit

- **Severity classification for injection findings:** MEDIUM applied to prompt injection (Finding 3) and path traversal (Finding 4) because exploitation requires repository write access (local user or collaborator). These would be HIGH in a server-side API context where user input comes from untrusted external sources.
- **ArgumentList.Add() pattern confirmed safe:** No string concatenation into process arguments exists anywhere in the C# codebase. The C# rewrite fully addressed the execSync injection risk present in the old JavaScript tool.
- **ValidStatuses enforcement confirmed:** Status validation is enforced in `FeatureUpdateStatusCommand.cs` via array membership check before any filesystem operations.
- **Silent exception swallowing rated LOW not MEDIUM:** Git commit failures silently continue execution but the security impact is limited to missing audit trail entries rather than data exfiltration or privilege escalation.
- **Both task outputs written in single commit:** C# audit (Task 1) and bash audit (Task 2) content were both written to findings-code.md in a single atomic file creation. Content coverage is identical to what two separate commits would have produced.

### Plan 02: CI Workflow and Committed Files Audit
- **inputs.slug injection severity:** Classified as HIGH (not MEDIUM). Gitea Actions follows GitHub Actions expression expansion semantics — `${{ inputs.slug }}` is expanded before the shell sees the run: block, making injection reliable across 6+ shell contexts.
- **config.json gitignore gap severity:** Classified as MEDIUM (systemic risk). The schema explicitly supports `team.members` and other fields that could hold sensitive values; the gitignore gap is structural, not just about current content.
- **Hardcoded paths in documentation:** Paths like `/var/home/conroy/Projects/GFD/` appear in RESEARCH.md and audit findings docs. These are documentation artifacts recording audit evidence, not executable source files. Noted as informational, not flagged as HIGH security findings.
- **AUTO-RUN.md case bug:** Line 98 of gfd-process-feature.yml references `AUTO-RUN.MD` (uppercase `.MD`) but actual files use `.md` (lowercase). This incidentally prevents AUTO-RUN.md exposure in the debug step. Documented as a note within the debug log finding rather than a separate finding.

## Blockers

---
*Created: 2026-02-20*
*Last updated: 2026-02-21*
