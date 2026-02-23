---
feature: re-discuss-loop
plan: 02
type: execute
wave: 1
depends_on: []
files_modified:
  - get-features-done/workflows/discuss-feature.md
autonomous: true
acceptance_criteria:
  - "`discuss-feature` detects blockers in FEATURE.md and runs a focused re-discussion on just the affected area (not a full re-discuss)"
  - "After re-discussion resolves the blocker, the blocker is removed from FEATURE.md and the user is shown the next command to run"
  - "Status rewinds to `discussing` then `discussed` during re-discuss, then the user re-runs the stage that triggered it"

must_haves:
  truths:
    - "When discuss-feature is invoked on a feature with an active `### [type:` entry in ## Blockers, it shows the RE-DISCUSSING banner and enters the focused re-discussion path — NOT the full gray-area menu"
    - "The focused re-discussion covers only the specific blocker area described in the entry"
    - "After the user resolves the blocker, the structured entry is removed from ## Blockers and a `[re-discuss resolved: <type>] <date>: <resolution summary>` entry is added to ## Decisions"
    - "Status transitions: discussing (at start of re-discuss) → discussed (after resolution)"
    - "The user is shown the exact command to re-run (`/gfd:research-feature`, `/gfd:plan-feature`, or `/gfd:execute-feature`) based on which stage triggered the blocker"
    - "If no active blocker entries exist (## Blockers is empty or contains only placeholder text), the workflow continues on the standard path — no false positive detection"
  artifacts:
    - path: "get-features-done/workflows/discuss-feature.md"
      provides: "Blocker-detection branch (Step 2.5) in discuss-feature workflow"
      contains: "Step 2.5"
  key_links:
    - from: "get-features-done/workflows/discuss-feature.md Step 2.5"
      to: "docs/features/${SLUG}/FEATURE.md ## Blockers"
      via: "Read tool + detection for ### [type: lines"
      pattern: "### \\[type:"
    - from: "get-features-done/workflows/discuss-feature.md Step 2.5"
      to: "gfd-tools feature-update-status"
      via: "discussing → discussed status transitions"
      pattern: "feature-update-status.*discussed"
---

<objective>
Add a blocker-detection branch (Step 2.5) to get-features-done/workflows/discuss-feature.md. When discuss-feature is invoked on a feature that has active blocker entries in ## Blockers, it skips the full gray-area flow and runs a focused re-discussion targeting only the blocker area. After resolution, it removes the blocker entry, records the resolution in ## Decisions, transitions status to `discussed`, and tells the user which command to re-run.

Purpose: Complete the re-discuss loop by providing the recovery entry point that consumes the blocker entries written by agents (Plan 01). Without this, users have no path to resolve blockers and re-run the blocked stage.

Output: Modified discuss-feature.md workflow with Step 2.5 inserted between Step 2 (Run Init) and Step 3 (Validate Status). The step correctly detects active blockers (not false-positives on placeholder text), handles the focused discussion, and cleans up after resolution.
</objective>

<execution_context>
@$HOME/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/re-discuss-loop/FEATURE.md
@docs/features/re-discuss-loop/RESEARCH.md
@get-features-done/workflows/discuss-feature.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Insert Step 2.5 blocker-detection branch into discuss-feature.md</name>
  <files>get-features-done/workflows/discuss-feature.md</files>
  <action>
Read the full discuss-feature.md file. Insert a new Step 2.5 between Step 2 (Run Init) and Step 3 (Validate Status).

**Detection logic (critical — must not false-positive on placeholder text):**
The `## Blockers` section in every FEATURE.md starts with a placeholder. Active blocker entries start with `### [type:`. Only treat blockers as active if the ## Blockers section contains at least one line starting with `### [type:`. Placeholder text (`*Active blockers affecting this feature.*` or similar) does NOT count.

**Insert the following Step 2.5 immediately after the existing Step 2 content:**

```markdown
## 2.5. Check for Active Blockers

Read the `## Blockers` section of `docs/features/${SLUG}/FEATURE.md`.

**Active blocker detection:** Check if the section contains any lines starting with `### [type:`. The placeholder text in the template does NOT count as an active blocker.

**If no active blocker entries found:** Continue to Step 3 (standard flow — no change to existing behavior).

**If one or more `### [type:` entries exist:** Enter the re-discuss path:

### Re-Discuss Path

**1. Extract blocker information from the first active entry:**
- Blocker type: value between `[type: ` and `]` on the header line
- Detected by: stage name after `Detected by: ` on the header line
- Description: the "What the agent found" paragraph content

**2. Transition to discussing:**
```bash
$HOME/.claude/get-features-done/bin/gfd-tools feature-update-status "${SLUG}" "discussing"
```

**3. Display RE-DISCUSSING banner:**
```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► RE-DISCUSSING [SLUG]
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

**4. Show blocker context:**
```
Blocker found: [type] — detected by [stage] on [date]

[Human-readable "What the agent found" description from the blocker entry]
```

**5. Skip Steps 3–7 entirely.** Do NOT run status validation, do NOT present the full gray-area menu. The feature status is now `discussing` — the standard status guard in Step 3 would allow this, but the full discuss flow is not needed here.

**6. Run focused re-discussion:**

Ask only questions that resolve the specific blocker area. Scope strictly to what the blocker describes. Do NOT expand into adjacent gray areas or present the full feature domain.

Use AskUserQuestion for each question:
- header: "Re-discuss" (or the blocker area name, max 12 chars)
- question: targeted at the specific ambiguity in the blocker entry
- options: 2-3 concrete choices

Ask up to 4 questions, then check:
- header: "Re-discuss"
- question: "Is the blocker resolved?"
- options: "Yes — resolved" / "Need to discuss more"

If "Need to discuss more" → ask up to 4 more questions, then check again.

**7. After "Yes — resolved":**

a. **Remove the resolved blocker entry** from `## Blockers` in FEATURE.md using the Edit tool. Remove the entire `### [type: <type>] ...` block including all content until the next `###` header or end of section.

b. **Add resolution to ## Decisions** using the Edit tool. Append to the `## Decisions` section:
```markdown
[re-discuss resolved: <blocker-type>] <ISO-date>: <one-sentence summary of what was decided>
```

c. **Surgically update the relevant FEATURE.md section** based on the blocker area (e.g., if the blocker was `ambiguous-scope`, update `## Acceptance Criteria` or `## Description` with the clarification). Use the Edit tool — do NOT rewrite the entire file.

d. **Transition to discussed:**
```bash
$HOME/.claude/get-features-done/bin/gfd-tools feature-update-status "${SLUG}" "discussed"
```

e. **Commit:**
```bash
git add "docs/features/${SLUG}/FEATURE.md" && git diff --cached --quiet || git commit -m "docs(${SLUG}): resolve blocker via re-discuss [<blocker-type>]"
```

f. **Show re-discuss done banner and next command:**
```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► [SLUG] RE-DISCUSSED ✓
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Blocker resolved: [type]
Stage that triggered it: [stage]
```

Then show the next command based on which stage detected the blocker:
- `researcher` → `/gfd:research-feature [SLUG]`
- `planner` → `/gfd:plan-feature [SLUG]`
- `executor` → `/gfd:execute-feature [SLUG]`

```
───────────────────────────────────────────────────────────────

## ▶ Next Up

**Re-run [stage] for [SLUG]**

`/gfd:[stage-command] [SLUG]`

<sub>`/clear` first → fresh context window</sub>
───────────────────────────────────────────────────────────────
```

g. **Exit.** Do NOT continue to Step 3 or later steps.
```

**Important implementation note on Step 3 status guard:** The status guard in Step 3 currently errors on `researching`, `planning`, `in-progress`. After Step 2.5, if no active blockers were found, the guard runs normally. If active blockers were found, Step 2.5 completes and exits before Step 3 runs — so the guard is never a problem for the re-discuss path. No changes to Step 3 are needed.
  </action>
  <verify>
Read get-features-done/workflows/discuss-feature.md and confirm:

1. Step 2.5 exists between "## 2. Run Init" and "## 3. Validate Status"
2. Detection logic checks for `### [type:` lines (not just any content in ## Blockers)
3. RE-DISCUSSING banner uses the same `━` border style as the existing DISCUSSING banner
4. Steps 3-7 are explicitly skipped in the re-discuss path
5. After resolution: blocker entry removed from ## Blockers, `[re-discuss resolved: <type>]` added to ## Decisions
6. Status transitions: `discussing` at start, `discussed` after resolution
7. Next command is determined by the `Detected by:` field in the blocker entry (researcher/planner/executor → research-feature/plan-feature/execute-feature)
8. Step 3 and later steps are unchanged (the existing flow is unmodified)
  </verify>
  <done>
discuss-feature.md has Step 2.5 that: detects active blockers via `### [type:` pattern (not placeholder text), shows RE-DISCUSSING banner, runs focused discussion on only the blocker area (not full gray-area menu), removes the resolved blocker from ## Blockers, adds `[re-discuss resolved: <type>]` to ## Decisions, transitions status discussing → discussed, and shows the user the command to re-run the blocking stage.
  </done>
</task>

</tasks>

<verification>
After task completes:

1. discuss-feature.md has Step 2.5 inserted in the correct position
2. Standard discuss path (no active blockers) is completely unchanged — no false positives
3. Re-discuss path is self-contained: enters at Step 2.5, exits before Step 3
4. Blocker cleanup is complete: ## Blockers cleared, ## Decisions updated, FEATURE.md surgically updated
5. Status flow is correct: discussing (at start) → discussed (after resolution)
6. Next command shown matches the stage that detected the blocker
7. The commit message includes the blocker type: `docs(${SLUG}): resolve blocker via re-discuss [<blocker-type>]`
</verification>

<success_criteria>
- get-features-done/workflows/discuss-feature.md has Step 2.5 between Steps 2 and 3
- Step 2.5 detects active blockers via `### [type:` pattern (false-positive safe)
- Re-discuss path: banner → focused discussion (not full gray-area menu) → resolution → cleanup → next command
- Status transitions match: discussing → discussed
- Resolved blocker removed from ## Blockers; `[re-discuss resolved: <type>]` added to ## Decisions
- Standard path (no blockers) is unmodified
</success_criteria>

<output>
After completion, create `docs/features/re-discuss-loop/02-SUMMARY.md`
</output>
