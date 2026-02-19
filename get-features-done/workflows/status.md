<purpose>
Display a plain table of all active features with their current lifecycle status. Excludes features with status "done". Shows a helpful message when no active features exist.
</purpose>

<process>

## 1. Load Features

```bash
FEATURES_RAW=$(node /home/conroy/.claude/get-features-done/bin/gfd-tools.cjs list-features)
```

Parse the JSON array from `features` key. Filter out any feature where `status` is `"done"`.

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
  - `discussing` → (in progress)
  - `discussed` → `/gfd:research-feature <slug>`
  - `researching` → (in progress)
  - `researched` → `/gfd:plan-feature <slug>`
  - `planning` → (in progress)
  - `planned` → `/gfd:execute-feature <slug>`
  - `in-progress` → (in progress)
- For transient statuses (discussing, researching, planning, in-progress), show "(in progress)" since a workflow is already running
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
- [ ] Next Step shows the correct /gfd command (with slug) for actionable statuses, or "(in progress)" for transient ones
- [ ] Empty state message shown when no active features
- [ ] No symbols, no progress bar, no routing in output

</success_criteria>
