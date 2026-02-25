<purpose>
Display a plain table of all active features with their current lifecycle status. Excludes features with status "done". Shows a helpful message when no active features exist.
</purpose>

<process>

## 1. Load Features

```bash
FEATURES_RAW=$($HOME/.claude/get-features-done/bin/gfd-tools list-features)
```

Extract from key=value output: each feature appears as a group of repeated keys — `feature_slug=`, `feature_name=`, `feature_status=`, `feature_owner=`, `feature_priority=` — one group per feature. Filter out any feature where `feature_status` is `done`.

## 2. Render Status Table

**If active features exist (count > 0 after filtering):**

Output a plain markdown table:

```
| Feature Name | Status | Next Step |
|--------------|--------|-----------|
| [name] | [status] | [next] |
| [name] | [status] | [next] |
```

Rules:
- `name` field from each feature object (human-readable name, not slug)
- `status` field as a raw string — no symbols, no emoji, no formatting
- `next` is the next /gfd command based on current status:
  - `new` → `/gfd:discuss-feature <slug>`
  - `discussing` → `/gfd:discuss-feature <slug>`
  - `discussed` → `/gfd:research-feature <slug>`
  - `researching` → `/gfd:research-feature <slug>`
  - `researched` → `/gfd:plan-feature <slug>`
  - `planning` → `/gfd:plan-feature <slug>`
  - `planned` → `/gfd:execute-feature <slug>`
  - `in-progress` → `/gfd:execute-feature <slug>`
- Sort order: use the order returned by list-features (already sorted by priority + status)
- Do NOT include done features

**If no active features exist (all features done, or no features at all):**

```
No active features.

Run /gfd:new-feature <slug> to create your first feature.
```

## 3. Done

No routing, no next-step suggestions, no progress bars. Display only.

</process>

<success_criteria>

- [ ] list-features called to get feature data
- [ ] done features excluded from table
- [ ] Table shows Feature Name, Status, and Next Step columns
- [ ] Next Step shows the correct /gfd command (with slug) for every status
- [ ] Empty state message shown when no active features
- [ ] No symbols, no progress bar, no routing in output

</success_criteria>
