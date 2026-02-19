<purpose>
Create a new feature in the GFD project. A feature is a named, independently deliverable slice of value identified by a slug (e.g., user-auth, payment-flow). This workflow validates the slug, asks for a one-line description, creates the feature directory and FEATURE.md, and routes to discussion.
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

**To fix:** Use a different slug or run /gfd:discuss-feature [SLUG] to discuss it.
```

Exit.

## 3. Gather Feature Description

**Display stage banner:**

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► CREATING FEATURE
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

Ask the user a single question:

"What does **[SLUG]** do? (one sentence)"

Wait for response before continuing.

## 4. Create Feature Directory and FEATURE.md

Create the feature directory:

```bash
mkdir -p docs/features/${SLUG}
```

Write `docs/features/${SLUG}/FEATURE.md` using the template from `@/home/conroy/.claude/get-features-done/templates/feature.md`, filled with the user's answer:

```markdown
---
name: [Derived human-readable name from slug, e.g., "User Authentication" from "user-auth"]
slug: [SLUG]
status: new
owner: [current git user or "unassigned"]
assignees: []
created: [today's date YYYY-MM-DD]
priority: medium
depends_on: []
---
# [Feature Name]

## Description

[One-liner from the user's answer.]

## Acceptance Criteria

- [ ] [To be defined during /gfd:discuss-feature]

## Tasks

[Populated during planning. Links to plan files.]

## Notes

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
Status: new
```

Present next step:

```
───────────────────────────────────────────────────────────────

## ▶ Next Up

**Discuss [SLUG]** — refine scope and define acceptance criteria

`/gfd:discuss-feature [SLUG]`

<sub>`/clear` first → fresh context window</sub>

───────────────────────────────────────────────────────────────

**Also available:**
- `/gfd:new-feature <slug>` — create another feature first
- `/gfd:status` — see all features and their status

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
- [ ] One-line description gathered
- [ ] Feature directory created
- [ ] FEATURE.md written with status: new and placeholder acceptance criteria — **committed**
- [ ] STATE.md updated — **committed**
- [ ] User knows next step is `/gfd:discuss-feature [SLUG]`

</success_criteria>
