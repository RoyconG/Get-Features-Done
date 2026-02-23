---
name: Review Token Usage
slug: review-token-usage
status: in-progress
owner: Conroy
assignees: []
created: 2026-02-20
priority: medium
depends_on: []
---
# Review Token Usage

## Description

Audit all GFD agents for token efficiency — identify overqualified models, redundant work, and other waste — then optimize defaults and add runtime token reporting. Also add an interactive `/gfd:configure-models` command that lets users choose which model powers each agent role, with recommendations and warnings.

## Acceptance Criteria

- [ ] One-time audit document produced in docs/ analyzing token usage across all agent roles with findings and recommendations
- [ ] New `/gfd:configure-models` command walks users through each agent role, showing Claude family models as options plus free text for custom models
- [ ] Each agent role shows the recommended model during selection, with warnings if a potentially too-weak model is chosen
- [ ] Model preferences persisted in GFD config file and respected by all workflows
- [ ] Token usage summary (per agent role) displayed at the end of each major workflow (research, plan, execute)
- [ ] Cumulative `## Token Usage` section maintained in FEATURE.md across workflow runs

## Tasks

[Populated during planning. Links to plan files.]

## Notes

**Implementation Decisions:**
- Audit: Full audit of all agents (orchestrators + spawned agents) for overqualified models, redundant work, and other inefficiencies
- Model selection: New `/gfd:configure-models` command with interactive prompt, per agent role granularity
- Model options: Claude family (haiku/sonnet/opus) as preset options, plus free text for anything else
- Constraints: Warn but allow if user picks a weak model for a demanding role
- Recommendations: Show recommended model per role based on audit findings
- Storage: GFD config file for model preferences
- Reporting: Token summary per agent role at end of workflows, stored as cumulative `## Token Usage` section in FEATURE.md
- Audit output: Document in docs/ with findings and recommendations

## Decisions

- Full audit scope: all agent types including orchestrators
- New `/gfd:configure-models` command (not part of /gfd:settings)
- Per agent role granularity for model selection
- Claude family as preset options with free text for custom models
- Warn but allow weak model choices
- Show recommended defaults based on audit findings
- Token summary at end of workflows + cumulative section in FEATURE.md
- Audit findings documented in docs/

## Blockers

---
*Created: 2026-02-20*
*Last updated: 2026-02-21*
