---
name: Security Review
slug: security-review
status: discussed
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

[Populated during planning. Links to plan files.]

## Notes

### Implementation Decisions
- **Threat scope:** Full OWASP-style sweep plus supply chain risks, including hardcoded user paths
- **Review targets:** Everything in the GFD repo (bin/, workflows/, agents/, references/, CI configs)
- **Claude's discretion:** Whether generated output (FEATURE.md, PLAN.md templates) also needs review
- **Output format:** Single SECURITY-REVIEW.md report categorized by severity (critical/high/medium/low)
- **Remediation:** Identify only — no fixes applied, no next-step suggestions in report
- **Motivation:** Pre-publish hygiene before others use GFD

## Decisions

## Blockers

---
*Created: 2026-02-20*
*Last updated: 2026-02-21*
