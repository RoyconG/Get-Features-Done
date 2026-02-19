<purpose>
Create a new feature in the GFD project. A feature is a named, independently deliverable slice of value identified by a slug (e.g., user-auth, payment-flow). This workflow validates the slug, gathers feature context through conversation, creates the feature directory and FEATURE.md, and routes to planning.
</purpose>

<required_reading>
Read all files referenced by the invoking prompt's execution_context before starting.

@/home/conroy/.claude/get-features-done/references/ui-brand.md
</required_reading>

<process>

## 1. Parse and Validate Slug

Extract the feature slug from $ARGUMENTS (first positional argument).

**If no slug provided:**

```
╔══════════════════════════════════════════════════════════════╗
║  ERROR                                                       ║
╚══════════════════════════════════════════════════════════════╝

No feature slug provided.

**To fix:** Run with a slug: /gfd:new-feature user-auth

Slug rules:
  - Lowercase letters, numbers, and hyphens only
  - No spaces, underscores, or special characters
  - Examples: user-auth, payment-flow, email-notifications
```

Exit.

**Validate slug format:**

A valid slug matches: `^[a-z0-9]+(-[a-z0-9]+)*$`

Invalid examples: `UserAuth`, `user_auth`, `user auth`, `user-Auth-123`

**If invalid slug:**

```
╔══════════════════════════════════════════════════════════════╗
║  ERROR                                                       ║
╚══════════════════════════════════════════════════════════════╝

Invalid slug: "[provided slug]"

Slugs must be lowercase alphanumeric with hyphens only.

**To fix:** Try: /gfd:new-feature [corrected-slug]
```

Exit.

## 2. Run Init

```bash
INIT=$(node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs init new-feature "${SLUG}")
```

Parse JSON for: `project_exists`, `feature_exists`, `feature_dir`, `commit_docs`, `planner_model`.

**If `project_exists` is false:**

```
╔══════════════════════════════════════════════════════════════╗
║  ERROR                                                       ║
╚══════════════════════════════════════════════════════════════╝

No GFD project found. docs/features/ does not exist.

**To fix:** Run /gfd:new-project first.
```

Exit.

**If `feature_exists` is true:**

```
╔══════════════════════════════════════════════════════════════╗
║  ERROR                                                       ║
╚══════════════════════════════════════════════════════════════╝

Feature already exists: docs/features/[SLUG]/

**To fix:** Use a different slug or run /gfd:plan-feature [SLUG] to plan it.
```

Exit.

## 3. Gather Feature Context

**Display stage banner:**

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► CREATING FEATURE
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

Ask the user about this feature. Use a conversational approach — follow threads, don't interrogate.

**Question 1 — Description:**

Ask inline (freeform):

"What does the **[SLUG]** feature do, and why does it matter?"

Wait for response before continuing.

**Question 2 — Acceptance Criteria:**

Use AskUserQuestion:
- header: "Done Looks Like"
- question: "How will you know [SLUG] is complete? What can a user do when it works?"
- options:
  - "Walk me through the user experience" — Describe it conversationally
  - "I have specific criteria" — I'll list them
  - "Let me think about this" — I'll describe my goals and you help me derive criteria

Follow up with clarifying questions until you have 3-5 concrete, observable behaviors. Each should be independently verifiable from a user or system perspective.

**Question 3 — Priority:**

Use AskUserQuestion:
- header: "Priority"
- question: "How important is [SLUG] relative to other work?"
- options:
  - "Critical — blocking other work"
  - "High — important for current goals"
  - "Medium — standard priority"
  - "Low — nice to have"

**Question 4 — Dependencies:**

Use AskUserQuestion:
- header: "Dependencies"
- question: "Does [SLUG] depend on other features being done first?"
- options:
  - "No dependencies" — This can be worked on independently
  - "Yes, has dependencies" — I'll name the feature slugs it needs
  - "Not sure yet" — Leave this empty for now

If "Yes, has dependencies": ask for a comma-separated list of feature slugs.

**Summarize and confirm:**

Present what you captured:

```
## Feature: [SLUG]

**Description:** [2-3 sentence summary from conversation]

**Acceptance Criteria:**
- [ ] [Criterion 1]
- [ ] [Criterion 2]
- [ ] [Criterion 3]

**Priority:** [high/medium/low/critical]
**Depends on:** [slugs or "none"]
```

Use AskUserQuestion:
- header: "Confirm"
- question: "Does this capture the [SLUG] feature correctly?"
- options:
  - "Looks good — create it" — Proceed
  - "Adjust description" — Let me refine the description
  - "Adjust acceptance criteria" — Let me refine the criteria

Loop until "Looks good — create it" selected.

## 4. Create Feature Directory and FEATURE.md

Create the feature directory:

```bash
mkdir -p docs/features/${SLUG}
```

Write `docs/features/${SLUG}/FEATURE.md` using the template from `@/home/conroy/.claude/get-features-done/templates/feature.md`, filled with the user's answers:

```markdown
---
name: [Derived human-readable name from slug, e.g., "User Authentication" from "user-auth"]
slug: [SLUG]
status: backlog
owner: [current git user or "unassigned"]
assignees: []
created: [today's date YYYY-MM-DD]
priority: [critical|high|medium|low]
depends_on: [array of dependency slugs, or empty array]
---
# [Feature Name]

## Description

[Description from conversation — 2-3 sentences. What this feature does and why it matters.]

## Acceptance Criteria

- [ ] [Criterion 1 — observable behavior]
- [ ] [Criterion 2 — observable behavior]
- [ ] [Criterion 3 — observable behavior]

## Tasks

[Populated during planning. Links to plan files.]

## Notes

[Any design decisions, constraints, or context captured during this conversation.]

---
*Created: [today's date]*
*Last updated: [today's date]*
```

**Derive the human-readable name from the slug:**
- `user-auth` → "User Authentication"
- `payment-flow` → "Payment Flow"
- `email-notifications` → "Email Notifications"

Title-case each word, expand common abbreviations naturally.

## 5. Update STATE.md

Read `docs/features/STATE.md` and update:
- Last activity: today's date — "Created feature [SLUG]"
- Total feature count (increment)

## 6. Commit

```bash
node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs commit "docs(gfd): create feature ${SLUG}" --files docs/features/${SLUG}/FEATURE.md docs/features/STATE.md
```

## 7. Done

**Display stage banner:**

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► FEATURE CREATED ✓
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Feature: [SLUG]
Location: docs/features/[SLUG]/FEATURE.md
Status: backlog
```

Present next step:

```
───────────────────────────────────────────────────────────────

## ▶ Next Up

**Plan [SLUG]** — create implementation plans

`/gfd:plan-feature [SLUG]`

<sub>`/clear` first → fresh context window</sub>

───────────────────────────────────────────────────────────────

**Also available:**
- `/gfd:new-feature <slug>` — create another feature first
- `/gfd:progress` — see all features and their status

───────────────────────────────────────────────────────────────
```

</process>

<output>

- `docs/features/{slug}/FEATURE.md`
- Updated `docs/features/STATE.md`

</output>

<success_criteria>

- [ ] Slug validated (lowercase, alphanumeric, hyphens only)
- [ ] Project existence verified
- [ ] Feature uniqueness verified (no duplicate slugs)
- [ ] Feature description gathered conversationally
- [ ] 3-5 concrete, observable acceptance criteria captured
- [ ] Priority captured
- [ ] Dependencies captured
- [ ] Feature directory created
- [ ] FEATURE.md written with all fields populated — **committed**
- [ ] STATE.md updated — **committed**
- [ ] User knows next step is `/gfd:plan-feature [SLUG]`

</success_criteria>
