# Feature Template

Template for `docs/features/{slug}/FEATURE.md` — the feature definition document.

<template>

```markdown
---
name: [Feature Name]
slug: [feature-slug]
status: backlog
owner: [username]
assignees: []
created: YYYY-MM-DD
priority: medium
depends_on: []
---
# [Feature Name]

## Description

[What this feature does and why it matters. 2-3 sentences.]

## Acceptance Criteria

- [ ] [Observable behavior from user perspective]
- [ ] [Another observable behavior]
- [ ] [Another observable behavior]

## Tasks

[Populated during planning. Links to plan files.]

## Notes

[Design decisions, context, constraints specific to this feature.]

---
*Created: [date]*
*Last updated: [date]*
```

</template>

<guidelines>

**Status Values:**
- `backlog` — Defined but not yet being worked on
- `planning` — Being planned (PLAN.md being created)
- `planned` — Plans exist, ready for execution
- `in-progress` — Actively being executed
- `done` — All acceptance criteria met, verified

**Priority Values:**
- `critical` — Blocking other work
- `high` — Important for current goals
- `medium` — Standard priority
- `low` — Nice to have

**Acceptance Criteria:**
- Must be observable behaviors, not implementation details
- Written from user/system perspective
- Each criterion independently verifiable
- Format: "User can X" or "System does Y when Z"

**depends_on:**
- List feature slugs this feature requires
- Example: `depends_on: [user-auth, database-setup]`

</guidelines>

<evolution>

**During planning:**
- Status: backlog → planning
- Tasks section populated with plan references

**After planning:**
- Status: planning → planned

**During execution:**
- Status: planned → in-progress
- Acceptance criteria checked off as verified

**After execution:**
- Status: in-progress → done
- All acceptance criteria checked

</evolution>
