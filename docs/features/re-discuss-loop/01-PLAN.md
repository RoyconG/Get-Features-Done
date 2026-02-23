---
feature: re-discuss-loop
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - agents/gfd-researcher.md
  - agents/gfd-planner.md
  - agents/gfd-executor.md
autonomous: true
acceptance_criteria:
  - "Research, planning, and execution agents detect unresolvable blockers and stop with an error box showing the blocker and `/gfd:discuss-feature <slug>` as the fix command"
  - "Blocker details are written to the `## Blockers` section of FEATURE.md so they persist across context windows"
  - "If the same blocker type recurs after a re-discuss, the agent warns the user before stopping"
  - "In auto-advance mode, the agent jumps directly into discuss-feature instead of stopping"
  - "Status rewinds to `discussing` then `discussed` during re-discuss, then the user re-runs the stage that triggered it"

must_haves:
  truths:
    - "When a researcher hits an unresolvable ambiguity, it writes a structured blocker to FEATURE.md ## Blockers and stops with an error box showing `/gfd:discuss-feature <slug>`"
    - "When a planner hits an unresolvable ambiguity, it writes a structured blocker to FEATURE.md ## Blockers and stops with an error box showing `/gfd:discuss-feature <slug>`"
    - "When an executor hits an unresolvable ambiguity, it writes a structured blocker to FEATURE.md ## Blockers and stops with an error box showing `/gfd:discuss-feature <slug>`"
    - "Status is rewound (researching→discussed, planning→researched, in-progress→planned) before the agent stops, so discuss-feature sees a stable pre-stage status"
    - "If the same blocker type previously appeared in ## Decisions as `[re-discuss resolved: <type>]`, a warning is prepended to the error box"
    - "In auto-advance mode, agents write the blocker and rewind status then proceed into discuss-feature instead of stopping"
  artifacts:
    - path: "agents/gfd-researcher.md"
      provides: "Blocker detection + surface pattern for research stage"
      contains: "RESEARCH BLOCKED"
    - path: "agents/gfd-planner.md"
      provides: "Blocker detection + surface pattern for planning stage"
      contains: "PLAN BLOCKED"
    - path: "agents/gfd-executor.md"
      provides: "Blocker detection + surface pattern for execution stage"
      contains: "EXECUTION BLOCKED"
  key_links:
    - from: "agents/gfd-researcher.md"
      to: "docs/features/${SLUG}/FEATURE.md ## Blockers"
      via: "Edit tool surgical insert"
      pattern: "### \\[type:"
    - from: "agents/gfd-executor.md"
      to: "gfd-tools feature-update-status"
      via: "status rewind before stop"
      pattern: "feature-update-status.*planned"
---

<objective>
Add blocker detection and surface patterns to the three downstream agent files (gfd-researcher.md, gfd-planner.md, gfd-executor.md). Each agent gains a new execution path: when it encounters an unresolvable blocker, it writes a structured entry to FEATURE.md ## Blockers, rewinds feature status, emits an error box with the fix command, and stops (or in auto-advance mode, proceeds into discuss-feature).

Purpose: Enable the re-discuss loop by giving agents a consistent way to surface blockers they cannot resolve without user input, keeping context persisted across context windows and pointing users to the exact command to unblock.

Output: Modified agent files with blocker detection steps, structured blocker entry format, repeat-blocker detection, auto-advance path, and ## [STAGE] BLOCKED structured return format.
</objective>

<execution_context>
@$HOME/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/re-discuss-loop/FEATURE.md
@docs/features/re-discuss-loop/RESEARCH.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Add blocker detection to gfd-researcher.md and gfd-planner.md</name>
  <files>agents/gfd-researcher.md
agents/gfd-planner.md</files>
  <action>
Both files need an identical blocker detection pattern added to their execution flow. The pattern is the same for both — only the stage name and status rewind target differ.

**For gfd-researcher.md:**

Read the full file first. Find the `<structured_returns>` section. The existing `## RESEARCH BLOCKED` return format is there but does NOT yet include: writing to FEATURE.md, status rewind, or the fix command. Replace the existing `## Research Blocked` return format section with the full new pattern. Also add a blocker detection step to the main `<process>` section — insert it at the point where research finds a genuine unresolvable ambiguity (after load_feature_state and context gathering, before writing RESEARCH.md).

**Blocker detection step to add in researcher process (add as a named step before the RESEARCH.md write step):**

```markdown
## Blocker Detection

If at any point during research you encounter an unresolvable issue — ambiguous scope that prevents knowing what to research, conflicting locked decisions, missing context that cannot be inferred, or a technical impossibility in the locked approach — do NOT continue research. Trigger the blocker path:

**Blocker types (use exact strings):**
- `ambiguous-scope` — scope or feature boundary unclear; cannot determine research target
- `conflicting-decisions` — two locked decisions in FEATURE.md contradict each other
- `missing-context` — information needed to proceed is not in FEATURE.md and cannot be inferred
- `technical-impossibility` — a locked decision is technically unachievable

**Blocker path (execute in order):**

1. **Check for repeat blocker:** Scan the `## Decisions` section of FEATURE.md for a line matching `[re-discuss resolved: <current-blocker-type>]`. If found, set REPEAT_WARNING to:
   ```
   ⚠ WARNING: This blocker type ([type]) occurred before and was resolved via re-discuss. If it recurs, the feature scope may need a more fundamental rethink.
   ```
   Otherwise set REPEAT_WARNING to empty string.

2. **Write structured blocker entry** to the `## Blockers` section of `docs/features/${SLUG}/FEATURE.md` using the Edit tool. Detection logic: look for lines starting with `### [type:` to determine if real blockers exist (not just placeholder text). Append the new entry:
   ```markdown
   ### [type: <blocker-type>] Detected by: researcher | <ISO-date>

   **What the agent found:** <specific description of what was found and why it blocks research>

   **Why this blocks progress:** <concrete reason — cannot determine X without knowing Y>

   **To resolve:** Run `/gfd:discuss-feature <slug>` to clarify this area.
   ```

3. **Rewind status:**
   ```bash
   $HOME/.claude/get-features-done/bin/gfd-tools feature-update-status "${SLUG}" "discussed"
   ```
   (researching → discussed: preserves that discuss happened, but un-does the research stage start)

4. **Check auto-advance mode:**
   ```bash
   AUTO_CFG=$($HOME/.claude/get-features-done/bin/gfd-tools config-get workflow.auto_advance 2>/dev/null | grep "^value=" | cut -d= -f2- || echo "false")
   ```
   If `AUTO_CFG` is `"true"`: output "Auto-advancing to discuss-feature for blocker resolution" and return `## RESEARCH BLOCKED (AUTO-ADVANCING)` — the discuss-feature workflow will pick up the active blocker from FEATURE.md.
   If `AUTO_CFG` is not `"true"`: proceed to step 5.

5. **Return `## RESEARCH BLOCKED`** structured return with error box:
   ```markdown
   ## RESEARCH BLOCKED

   **Feature:** {slug}
   **Blocker type:** <blocker-type>
   **Stage:** researcher

   {REPEAT_WARNING if non-empty}

   ╔══════════════════════════════════════════════════════════════╗
   ║  BLOCKER DETECTED                                            ║
   ╚══════════════════════════════════════════════════════════════╝

   **What's blocking research:**
   <Specific description of what the agent found and why it cannot proceed>

   **To resolve:**
   `/gfd:discuss-feature {slug}` — focused re-discussion on this area

   ---
   Blocker written to: docs/features/{slug}/FEATURE.md ## Blockers
   Status rewound to: discussed
   ```

6. **Stop.** Do not attempt further research.
```

**For gfd-planner.md (same pattern, different names):**

Read the full file first. Add an identical blocker detection section to the `<execution_flow>` — insert it after `gather_feature_context` and before `break_into_tasks`. The criteria for triggering are the same but planning-specific:
- `ambiguous-scope` — acceptance criteria too vague to break into tasks
- `conflicting-decisions` — locked decisions contradict each other in a way that prevents planning
- `missing-context` — a required dependency or constraint is not in FEATURE.md
- `technical-impossibility` — a locked decision cannot be planned around

Status rewind for planner: `"researched"` (planning → researched: preserves research, un-does plan stage start).

The structured return format uses `## PLAN BLOCKED` (not RESEARCH BLOCKED). All other mechanics identical to researcher pattern above.

Add to `<structured_returns>` section a new `## Plan Blocked` subsection with the error box template (matching the RESEARCH BLOCKED format but with "planner" stage name and `## PLAN BLOCKED` header).
  </action>
  <verify>
Read agents/gfd-researcher.md and confirm:
- A "Blocker Detection" named step exists in the process
- The step contains the four blocker type strings exactly: `ambiguous-scope`, `conflicting-decisions`, `missing-context`, `technical-impossibility`
- The step references `feature-update-status "${SLUG}" "discussed"` for status rewind
- The step includes AUTO_CFG check with `config-get workflow.auto_advance` pattern
- `## RESEARCH BLOCKED` return format includes the error box with `╔` border characters
- `## RESEARCH BLOCKED` return format mentions `Blocker written to: docs/features/{slug}/FEATURE.md ## Blockers`

Read agents/gfd-planner.md and confirm:
- A blocker detection section exists in the execution flow (after gather_feature_context)
- Status rewind uses `"researched"` (not `"discussed"`)
- `## PLAN BLOCKED` return format exists in structured_returns
  </verify>
  <done>
Both agent files contain the blocker detection path. The researcher rewinds to `discussed`; the planner rewinds to `researched`. Both check for repeat blockers in ## Decisions, write structured entries to ## Blockers, check auto-advance mode, and return a ## [STAGE] BLOCKED structured return with the error box and fix command.
  </done>
</task>

<task type="auto">
  <name>Task 2: Add blocker detection to gfd-executor.md</name>
  <files>agents/gfd-executor.md</files>
  <action>
The executor already has `AUTO_CFG` detection and `state_updates` that use the Edit tool for blockers. This task extends the executor with the same blocker detection pattern — but the executor's blocker threshold is higher (it has Rule 1-3 auto-fix rules). The blocker path only triggers for Rule 4-equivalent issues: architectural decisions that genuinely require user input.

Read the full gfd-executor.md. Find the `<state_updates>` section and the deviation/checkpoint rules.

**Add a new `<blocker_detection>` section** (insert after `<self_check>` and before `<state_updates>`):

```markdown
<blocker_detection>

## When to Trigger a Blocker

Blockers are ONLY for issues that genuinely require user input — not for things Claude can decide with reasonable judgment (those are deviations). Before triggering a blocker, apply the Rule 1-3 check:
- Can this be auto-fixed and documented as a deviation? → Fix it, document it, continue.
- Is it "Claude's discretion" per FEATURE.md? → Make a reasonable choice, document it, continue.
- Only if "this decision requires the user's input and proceeding without it would produce the wrong outcome" → trigger blocker path.

**Blocker types (use exact strings):**
- `ambiguous-scope` — the plan scope is fundamentally unclear in a way that prevents knowing what to implement
- `conflicting-decisions` — FEATURE.md has locked decisions that directly contradict each other
- `missing-context` — a required piece of context is not in FEATURE.md and cannot be inferred
- `technical-impossibility` — a locked decision in FEATURE.md or the plan is technically unachievable

**Blocker path (execute in order):**

1. **Check for repeat blocker:** Scan the `## Decisions` section of FEATURE.md for a line matching `[re-discuss resolved: <current-blocker-type>]`. If found, set REPEAT_WARNING to:
   ```
   ⚠ WARNING: This blocker type ([type]) occurred before and was resolved via re-discuss. If it recurs, the feature scope may need a more fundamental rethink.
   ```
   Otherwise REPEAT_WARNING is empty.

2. **Write structured blocker entry** to the `## Blockers` section of `docs/features/${SLUG}/FEATURE.md` using the Edit tool. Append the new entry:
   ```markdown
   ### [type: <blocker-type>] Detected by: executor | <ISO-date>

   **What the agent found:** <specific description of what was found and why it blocks execution>

   **Why this blocks progress:** <concrete reason>

   **To resolve:** Run `/gfd:discuss-feature <slug>` to clarify this area.
   ```

3. **Rewind status:**
   ```bash
   $HOME/.claude/get-features-done/bin/gfd-tools feature-update-status "${SLUG}" "planned"
   ```
   (in-progress → planned: preserves execution plans, un-does execution stage start)

4. **Check auto-advance mode** (use existing `AUTO_CFG` variable):
   If `AUTO_CFG` is `"true"`: output "Auto-advancing to discuss-feature for blocker resolution" and return `## EXECUTION BLOCKED (AUTO-ADVANCING)`.
   If `AUTO_CFG` is not `"true"`: proceed to step 5.

5. **Return `## EXECUTION BLOCKED`** structured return:
   ```markdown
   ## EXECUTION BLOCKED

   **Feature:** {slug}
   **Blocker type:** <blocker-type>
   **Stage:** executor

   {REPEAT_WARNING if non-empty}

   ╔══════════════════════════════════════════════════════════════╗
   ║  BLOCKER DETECTED                                            ║
   ╚══════════════════════════════════════════════════════════════╝

   **What's blocking execution:**
   <Specific description of what the agent found and why it cannot proceed>

   **To resolve:**
   `/gfd:discuss-feature {slug}` — focused re-discussion on this area

   ---
   Blocker written to: docs/features/{slug}/FEATURE.md ## Blockers
   Status rewound to: planned
   ```

6. **Stop.** Do not attempt further execution. Do NOT create a partial SUMMARY.md.

</blocker_detection>
```

Also add `## EXECUTION BLOCKED` return format to the `<structured_returns>` section of the executor (alongside the existing completion format). Match the same format as the researcher/planner blocked returns.
  </action>
  <verify>
Read agents/gfd-executor.md and confirm:
- A `<blocker_detection>` section exists in the file
- The section explicitly states blockers are ONLY for Rule-4-equivalent issues (requires user input, not Claude's discretion)
- Status rewind uses `"planned"` (not `"discussed"` or `"researched"`)
- The section reuses the existing `AUTO_CFG` variable (does not re-declare it)
- `## EXECUTION BLOCKED` return format exists in the file
- The blocker entry format uses `Detected by: executor`
  </verify>
  <done>
gfd-executor.md has a `<blocker_detection>` section that: enforces a high threshold (Rule 4 only), writes structured blocker entries to ## Blockers using the Edit tool, rewinds to `planned`, checks the existing AUTO_CFG variable for auto-advance behavior, and returns `## EXECUTION BLOCKED` with error box and fix command.
  </done>
</task>

</tasks>

<verification>
After both tasks complete:

1. All three agent files contain the blocker detection pattern
2. Status rewind mapping is correct:
   - researcher → `discussed`
   - planner → `researched`
   - executor → `planned`
3. All three use the same four blocker type strings (exact match for repeat detection)
4. All three check `## Decisions` for `[re-discuss resolved: <type>]` before writing a new blocker
5. Auto-advance path exists in all three (researcher/planner add AUTO_CFG check; executor reuses existing)
6. The executor's blocker path clearly distinguishes "needs user input" from "Claude's discretion" to prevent over-triggering
</verification>

<success_criteria>
- agents/gfd-researcher.md contains: Blocker Detection step, status rewind to `discussed`, REPEAT_WARNING check, AUTO_CFG check, `## RESEARCH BLOCKED` return with error box
- agents/gfd-planner.md contains: blocker detection section, status rewind to `researched`, REPEAT_WARNING check, AUTO_CFG check, `## PLAN BLOCKED` return with error box
- agents/gfd-executor.md contains: `<blocker_detection>` section with Rule-4 threshold guidance, status rewind to `planned`, reuse of existing AUTO_CFG, `## EXECUTION BLOCKED` return with error box
- All three write structured blocker entries to FEATURE.md ## Blockers using the Edit tool with `### [type: <blocker-type>]` header format
</success_criteria>

<output>
After completion, create `docs/features/re-discuss-loop/01-SUMMARY.md`
</output>
