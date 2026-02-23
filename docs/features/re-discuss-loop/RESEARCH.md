# Feature: Re-Discuss Loop — Research

**Researched:** 2026-02-23
**Domain:** GFD agent orchestration — blocker detection, status state machine, FEATURE.md mutation, focused re-discussion
**Confidence:** HIGH

## User Constraints (from FEATURE.md)

### Locked Decisions

- **Trigger:** User-initiated — agent stops and surfaces the blocker, user runs `/gfd:discuss-feature <slug>` manually (except in auto-advance mode)
- **Auto-advance exception:** In auto-advance mode, the agent jumps directly into `discuss-feature` instead of stopping
- **Re-entry:** Focused update — discuss only the affected area, surgically update FEATURE.md (NOT a full re-discuss from scratch)
- **Blocker context banner:** Shown via "RE-DISCUSSING [slug]" banner + summary of what the agent found
- **Storage:** Blockers written to the existing `## Blockers` section of FEATURE.md
- **Status flow:** Rewinds to `discussing` (e.g., `researching` → `discussing` → `discussed` → `researching`)
- **Loop guard:** Warn on repeat blocker, don't prevent re-discuss
- **Scope:** Research + Planning + Execution stages

### Out of Scope

- Full re-discussion from scratch (focused discussion on affected area only)
- Blocking the user from re-discussing if the same blocker type recurs (warn only)

## Summary

The Re-Discuss Loop feature adds a blocker detection and recovery path to GFD's three downstream stages (research, planning, execution). When an agent hits something it cannot resolve unilaterally — ambiguous scope, conflicting decisions, missing context, technical impossibility — it writes a structured blocker entry to `## Blockers` in FEATURE.md, emits an error box with `/gfd:discuss-feature <slug>` as the fix, and stops. The user then re-discusses the specific affected area. After re-discussion resolves the blocker, the status rewinds back to where the user left off and they re-run the triggering stage.

The implementation touches four places: (1) the three agent files (`gfd-researcher.md`, `gfd-planner.md`, `gfd-executor.md`) each need a blocker detection + surface pattern, (2) the `discuss-feature.md` workflow needs a blocker-detection branch at the start that runs a focused discussion instead of full discovery, (3) FEATURE.md frontmatter/status manipulation via the existing `gfd-tools feature-update-status` command, and (4) the auto-advance path in the executor needs to call `discuss-feature` instead of stopping when a blocker is detected.

The hardest implementation challenge is the `discuss-feature` focused re-discussion path. The current workflow is built entirely around a full from-scratch discussion of gray areas. Adding a blocker-aware branch requires: detecting a populated `## Blockers` section at startup, displaying the blocker context, then running a narrowly scoped discussion of just the blocker area (not presenting full gray-area menus). The repeat-blocker detection requires comparing the current blocker type against resolved blockers from a prior re-discuss round — this needs a consistent blocker type taxonomy written into FEATURE.md.

**Primary recommendation:** Implement blocker detection as a named structured block in FEATURE.md (type + description + source stage), use `gfd-tools feature-update-status` for status rewinds, add a blocker-detection branch in `discuss-feature.md` that reads the `## Blockers` section and runs focused discussion, and add a repeat-blocker check by comparing current blocker type against the `## Decisions` section for prior re-discuss events.

## Standard Stack

### Core

| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| `gfd-tools feature-update-status` | current | Status rewind (e.g., `researching` → `discussing`) | Only status-mutation mechanism in the codebase — confirmed in `FeatureUpdateStatusCommand.cs` |
| `FrontmatterService.Extract / Splice` | current | Read and rewrite FEATURE.md frontmatter + section content | Already used by all status transitions throughout the codebase |
| Agent structured return format | current | Blocker surface pattern — `## RESEARCH BLOCKED` / `## PLAN BLOCKED` / `## EXECUTION BLOCKED` | Follows existing `## RESEARCH BLOCKED`, `## RESEARCH COMPLETE`, `## PLAN COMPLETE` convention already established in agents |
| `discuss-feature.md` workflow | current | Re-discussion entry point — add blocker-aware branch at the top | Existing workflow handles `discussing` status re-entry; blocker path extends it |

### Supporting

| Component | Version | Purpose | When to Use |
|-----------|---------|---------|-------------|
| `config-get workflow.auto_advance` | current | Detect auto-advance mode in agents | Used by `gfd-executor.md` already (`AUTO_CFG`); extend same pattern to researcher and planner |
| `Edit` tool | current | Surgically add/remove blocker entries in FEATURE.md `## Blockers` section | Current pattern for decisions/blockers in executor; avoid full file rewrite for targeted section edits |
| `## Blockers` section | current | Persistent blocker storage across context windows | Already exists in every FEATURE.md (template + all feature files); currently used for execution-time blockers |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Structured blocker entry in `## Blockers` | Separate `BLOCKERS.md` file | `## Blockers` is already in every FEATURE.md, co-located with feature context. Separate file adds complexity with no benefit. |
| Reusing `discuss-feature.md` with a branch | New `/gfd:re-discuss-feature` command | A new command is unnecessary — `discuss-feature` already supports re-entry (status `discussing` re-entry is already handled). A branch keeps the command surface clean. |
| Warn-then-stop for repeat blockers | Hard block on repeat blocker | User Constraints explicitly say "warn, don't prevent" — hard block is ruled out. |

## Architecture Patterns

### Recommended Project Structure

This feature modifies existing files, not new files:

```
agents/
├── gfd-researcher.md        # Add: blocker detection + surface + auto-advance path
├── gfd-planner.md           # Add: blocker detection + surface + auto-advance path
└── gfd-executor.md          # Add: blocker detection + surface (auto-advance already exists)

get-features-done/workflows/
└── discuss-feature.md       # Add: blocker-detection branch at Step 2/3
```

No new commands or GfdTools C# commands are needed. The existing `feature-update-status` command handles all status transitions including rewinds.

### Pattern 1: Blocker Entry Format in FEATURE.md

**What:** A structured text block written to `## Blockers` when a blocker is detected. Must be parseable by the `discuss-feature` workflow to detect and display context.

**When to use:** Any time an agent cannot proceed due to an unresolvable issue.

**Example:**

```markdown
## Blockers

### [type: ambiguous-scope] Detected by: researcher | 2026-02-23

**What the agent found:** The acceptance criterion "support multiple formats" is ambiguous —
it could mean JSON + CSV, or any format the user specifies at runtime.

**Why this blocks progress:** Cannot determine which serialization libraries to research
without knowing the target formats.

**To resolve:** Run `/gfd:discuss-feature re-discuss-loop` to clarify scope.
```

**Block anatomy:**
- `[type: <blocker-type>]` — machine-readable type for repeat-blocker detection
- `Detected by: <stage>` — which stage hit the blocker (`researcher`, `planner`, `executor`)
- `| <date>` — ISO date for ordering
- Human-readable description + resolution hint

**Blocker type taxonomy** (locked set for repeat detection):
- `ambiguous-scope` — scope or feature boundary unclear
- `conflicting-decisions` — two locked decisions contradict each other
- `missing-context` — information needed to proceed is not in FEATURE.md
- `technical-impossibility` — locked decision is technically unachievable

### Pattern 2: Agent Blocker Detection + Surface

**What:** Each agent adds a detection path that: writes the blocker, rewinds status, emits an error box, and returns a structured `## [STAGE] BLOCKED` return.

**When to use:** When an agent determines it cannot proceed without user input.

**Example (researcher):**

```markdown
## RESEARCH BLOCKED

**Feature:** {slug}
**Blocker type:** ambiguous-scope
**Stage:** researcher

╔══════════════════════════════════════════════════════════════╗
║  BLOCKER DETECTED                                            ║
╚══════════════════════════════════════════════════════════════╝

**What's blocking research:**
[Specific description of what the agent found and why it cannot proceed]

**To resolve:**
`/gfd:discuss-feature {slug}` — focused re-discussion on this area

---
Blocker written to: docs/features/{slug}/FEATURE.md ## Blockers
```

**Agent implementation steps:**
1. Detect the blocker (domain-specific — ambiguous acceptance criteria, conflicting notes, etc.)
2. Check for repeat blocker: scan `## Decisions` section for prior `[re-discuss resolved: <type>]` entries matching current type — if found, prepend warning
3. Write structured blocker entry to `## Blockers` section using the Edit tool
4. Rewind status: `gfd-tools feature-update-status "${SLUG}" "discussed"` (return to last stable pre-stage state)
5. Return `## [STAGE] BLOCKED` with error box and fix command
6. Stop — do not attempt further progress

**Status rewind mapping:**
| Current status | Rewind to |
|----------------|-----------|
| `researching` | `discussed` |
| `planning` | `researched` |
| `in-progress` | `planned` |

### Pattern 3: Discuss-Feature Blocker Branch

**What:** At the start of `discuss-feature.md` (before Step 3 Status Validation), read `## Blockers` and if populated, run a focused re-discussion on the blocker area instead of the full gray-area flow.

**When to use:** Any time `discuss-feature` is invoked on a feature that has active blockers.

**Branching logic (insert between Steps 2 and 3):**

```markdown
## 2.5. Check for Active Blockers

Read the `## Blockers` section of FEATURE.md.

**If `## Blockers` is empty or contains only the placeholder text:**
Continue to Step 3 (standard flow).

**If `## Blockers` has active blocker entries:**

Display RE-DISCUSSING banner:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► RE-DISCUSSING [SLUG]
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Show blocker summary:
**Blocker found:** [type] — detected by [stage] on [date]
[Human-readable description from blocker entry]

Skip Steps 3-6 (status validation, gray area analysis, full discuss).
Proceed directly to focused discussion:
- Discuss only the specific area described in the blocker
- Do NOT present the full gray-area menu
- Focus questions on resolving the specific ambiguity

After resolution:
1. Remove the resolved blocker entry from `## Blockers`
2. Add a `[re-discuss resolved: <type>]` entry to `## Decisions`
3. Surgically update only the affected section of FEATURE.md
4. Transition status to `discussed`
5. Show user the next command: the stage that triggered the blocker
```

**Status rewind in discuss-feature for re-discuss path:**

```bash
# At start of re-discuss (before focused discussion)
gfd-tools feature-update-status "${SLUG}" "discussing"

# After focused discussion resolves blocker
gfd-tools feature-update-status "${SLUG}" "discussed"
```

### Pattern 4: Repeat Blocker Detection

**What:** Before writing a new blocker, check if this blocker type has occurred before on this feature.

**When to use:** Any time an agent is about to write a blocker entry.

**Detection logic:**

```markdown
Check `## Decisions` section of FEATURE.md for lines matching:
`[re-discuss resolved: <current-blocker-type>]`

If found:
Prepend this warning to the error box:

⚠ WARNING: This type of blocker ([type]) occurred before and was
  resolved via re-discuss. If the same issue recurs, the feature scope
  may need a more fundamental rethink.

Then continue with normal blocker surface (don't prevent).
```

### Pattern 5: Auto-Advance Mode Integration

**What:** When `AUTO_CFG` is `"true"`, instead of stopping and emitting an error box, the agent invokes `discuss-feature` directly.

**When to use:** Auto-advance mode only (`config-get workflow.auto_advance` returns `"true"`).

**Example (researcher auto-advance path):**

```bash
AUTO_CFG=$($HOME/.claude/get-features-done/bin/gfd-tools config-get workflow.auto_advance 2>/dev/null | grep "^value=" | cut -d= -f2- || echo "false")

# ... blocker detected ...

if [ "$AUTO_CFG" = "true" ]; then
  # Write blocker, rewind status, then jump into discuss-feature
  # (discuss-feature will detect the blocker and run focused re-discussion)
  # Return: "Auto-advancing to discuss-feature for blocker resolution"
else
  # Standard path: write blocker, rewind status, emit error box, stop
fi
```

### Anti-Patterns to Avoid

- **Full re-discuss on blocker:** The FEATURE.md Notes explicitly require focused re-discussion on only the affected area. Do not reuse the full gray-area menu when blockers exist.
- **Blocking on repeat blocker:** Notes say "warn, don't prevent." A hard block would violate the locked decision.
- **Inventing a new gfd-tools command:** `feature-update-status` already handles all status transitions including rewinds. No new C# command is needed.
- **Writing blockers to a separate file:** The `## Blockers` section is the canonical storage location (locked decision). Do not create `BLOCKERS.md` or similar.
- **Rewinding all the way to `discussed` from `in-progress`:** The rewind should go to `planned` from `in-progress`, not `discussed` — otherwise the execution plans are implicitly invalidated. The correct rewind is one step back to the last stable state before the blocking stage.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Status transitions | Custom status-update logic editing FEATURE.md frontmatter directly | `gfd-tools feature-update-status <slug> <status>` | The C# command handles frontmatter parsing, validation against the valid status list, and file write atomically |
| Blocker storage | New file, new field, custom format | `## Blockers` section in FEATURE.md + Edit tool | Already exists in every FEATURE.md; consistent with executor's existing pattern for blocker entries |
| Auto-advance detection | Parse config.json manually | `config-get workflow.auto_advance` | Existing pattern used in `gfd-executor.md` (`AUTO_CFG` variable) |
| Focused vs full discuss | New command `/gfd:re-discuss-feature` | Branch in `discuss-feature.md` at blocker detection step | `discuss-feature` already supports re-entry for `discussing` status; a branch keeps the command surface clean |

**Key insight:** Every piece of infrastructure needed already exists. This feature is purely additive: new code paths in existing agents, a new branch in an existing workflow.

## Common Pitfalls

### Pitfall 1: Status Rewind Goes Too Far

**What goes wrong:** When `in-progress` hits a blocker, the agent rewinds to `discussed` (skipping `planned`). This implicitly suggests the existing plans are invalid, forcing a full re-plan after re-discuss.

**Why it happens:** Confusing the "discussing → discussed" pattern with the recovery path. The Notes say "rewinds to discussing" but this is describing the re-discuss process itself, not the final landing status for every stage.

**How to avoid:** Use the mapping: `researching → discussed`, `planning → researched`, `in-progress → planned`. This preserves the execution plans so the user can re-run the blocking stage without replanning.

**Warning signs:** If the re-discuss output tells the user to re-plan before re-executing, the rewind went too far.

### Pitfall 2: Blocker Detection is Too Aggressive

**What goes wrong:** Agents write blockers for issues they could reasonably resolve themselves (Rule 1-3 deviations in executor) or for things that are explicitly "Claude's discretion."

**Why it happens:** Overly conservative agent behavior misclassifies resolvable uncertainty as a blocker.

**How to avoid:** Blockers are only for issues that genuinely require user input to resolve. The criteria mirror the executor's Rule 4 (architectural decisions): "Does this require a decision only the user can make?" If Claude can make a reasonable choice and document it, it should — not block.

**Warning signs:** A blocker that says "I wasn't sure which approach to take" when FEATURE.md already says "Claude's discretion."

### Pitfall 3: Focused Re-Discussion Drifts Into Full Re-Discuss

**What goes wrong:** The `discuss-feature` blocker branch presents the full gray-area menu (Step 5 Analyze Feature + Step 6 Present Gray Areas), leading to a complete re-scope of the feature instead of targeted resolution.

**Why it happens:** Reusing the full discussion path without branching past Steps 5-6.

**How to avoid:** When a blocker is detected, skip Steps 5-6 entirely. Present only the specific blocker area and ask questions targeted at resolving it. The user chose to re-discuss because of this specific blocker — respect that scope.

**Warning signs:** User is presented with 4+ gray-area checkboxes for a re-discuss that should have been a 1-2 question conversation.

### Pitfall 4: Repeat Blocker Check Fails to Match Types

**What goes wrong:** The repeat blocker check looks for `[re-discuss resolved: ambiguous-scope]` but the original blocker was written as `[type: ambiguous scope]` (space vs hyphen) — no match, warning suppressed.

**Why it happens:** Inconsistent blocker type formatting between the write path (agent writes blocker) and read path (agent checks for repeats).

**How to avoid:** Establish the blocker type taxonomy as a fixed list with exact strings (listed in Pattern 1 above). Agents must use the exact strings from the taxonomy. Both write and read paths reference the same list.

**Warning signs:** A user reports "I've re-discussed this three times" but the repeat warning never appeared.

### Pitfall 5: `## Blockers` Section Detection is Fragile

**What goes wrong:** The `discuss-feature` workflow checks for blockers by looking for any content in the `## Blockers` section, but the template/placeholder text `[Active blockers affecting this feature. Remove when resolved.]` is still there — false positive.

**Why it happens:** The template creates `## Blockers` with placeholder text. If the section parser looks for any non-empty content, it fires on the placeholder.

**How to avoid:** Detection should look for structured blocker entries (lines starting with `### [type:`) rather than any content. Or: agents should clear the placeholder text when writing the first real blocker.

**Warning signs:** `discuss-feature` always enters the re-discuss path even for features that were never blocked.

## Code Examples

Verified patterns from this codebase:

### Existing Auto-Advance Detection (from gfd-executor.md)

```bash
# Source: agents/gfd-executor.md
AUTO_CFG=$($HOME/.claude/get-features-done/bin/gfd-tools config-get workflow.auto_advance 2>/dev/null | grep "^value=" | cut -d= -f2- || echo "false")

# Auto-mode checkpoint behavior
if [ "$AUTO_CFG" = "true" ]; then
  # checkpoint:human-verify → auto-approve
  # checkpoint:decision → auto-select first option
  # checkpoint:human-action → STOP normally (cannot automate)
fi
```

### Existing Status Update Pattern (from all workflows)

```bash
# Source: get-features-done/workflows/discuss-feature.md
$HOME/.claude/get-features-done/bin/gfd-tools feature-update-status "${SLUG}" "discussing"
# ... work ...
$HOME/.claude/get-features-done/bin/gfd-tools feature-update-status "${SLUG}" "discussed"
```

### Existing Error Box Pattern (from execute-feature.md)

```markdown
╔══════════════════════════════════════════════════════════════╗
║  ERROR                                                       ║
╚══════════════════════════════════════════════════════════════╝

[Specific error message]

**To fix:** [Command to run]
```

### Existing Decisions/Blockers Edit Pattern (from gfd-executor.md)

The executor uses the Edit tool directly on FEATURE.md to add blocker entries (not a gfd-tools command — that was dropped in the C# rewrite). This is the correct pattern:

```markdown
# Source: agents/gfd-executor.md state_updates section
# "For decisions and blockers: Use the Edit tool to add entries directly to the
#  ## Decisions and ## Blockers sections of docs/features/${SLUG}/FEATURE.md."
```

### Existing RESEARCH BLOCKED Return Format (from gfd-researcher.md)

```markdown
## RESEARCH BLOCKED

**Feature/Project:** {name}
**Blocked by:** [what's preventing progress]

### Attempted
[What was tried]

### Options
1. [Option to resolve]
2. [Alternative approach]

### Awaiting
[What's needed to continue]
```

The new blocker pattern for re-discuss-loop extends this with: a structured blocker entry already written to FEATURE.md, a specific fix command (`/gfd:discuss-feature`), and a repeat-blocker warning if applicable.

### Discuss-Feature Status Guard (from discuss-feature.md workflow)

The current status guard (Step 3) allows re-entry for `discussing` and prompts for `discussed`. The blocker-detection branch (Step 2.5) must run BEFORE this guard, because a feature with an active blocker may be in `researching`, `planning`, or `in-progress` status — not `new`/`discussing`/`discussed`. The guard must be relaxed to allow `researching`, `planning`, and `in-progress` when blockers are present.

```markdown
# Existing guard in discuss-feature.md Step 3:
# If status is `researching`, `researched`, `planning`, `planned`, `in-progress`, or `done`:
# → Error: "Feature is already past the discussion phase"

# New behavior when blockers detected (Step 2.5 branch):
# Allow these statuses — blocker path handles the status rewind itself
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `feature add-blocker` gfd-tools command | Edit tool directly on FEATURE.md `## Blockers` section | C# rewrite (2026-02) | Blocker writes use Edit tool, not a gfd-tools subcommand |
| `feature add-decision` gfd-tools command | Edit tool directly on FEATURE.md `## Decisions` section | C# rewrite (2026-02) | Same pattern — direct Edit for decisions too |
| Node.js gfd-tools.cjs | C# gfd-tools dotnet binary | C# rewrite (2026-02) | All status/frontmatter operations now go through the dotnet binary |

**Deprecated/outdated:**
- `feature add-blocker` command: Not ported to the C# rewrite. Confirmed in `csharp-rewrite/03-SUMMARY.md`: "Decisions/blockers added directly via Edit tool on FEATURE.md (feature add-decision/add-blocker not ported)."

## Open Questions

1. **Status rewind semantics for `in-progress`**
   - What we know: The executor records blockers in FEATURE.md. The Notes say status rewinds to `discussing`.
   - What's unclear: If a feature is `in-progress` and hits a blocker, should it rewind to `planned` (preserving the plans) or `discussed` (invalidating plans and requiring re-plan)?
   - Recommendation: Rewind to `planned` to preserve the execution plans. Re-discuss resolves the ambiguity, user re-runs `/gfd:execute-feature`. Only rewind to `discussed` if the blocker invalidates the existing plans (agent judgment call).

2. **Blocker persistence after re-discuss**
   - What we know: After re-discussion, the blocker is removed from `## Blockers` and a resolved entry goes to `## Decisions`.
   - What's unclear: If the user re-runs the blocking stage and hits the same blocker again, the `## Decisions` entry serves as the repeat-blocker signal. But what's the exact string format for this decision entry?
   - Recommendation: Use `[re-discuss resolved: <type>] <date>: <one-liner of resolution>` as the Decisions entry format.

3. **Discuss-feature status guard relaxation**
   - What we know: The current guard in `discuss-feature.md` errors on `researching`, `planning`, `in-progress` statuses.
   - What's unclear: Should the guard be modified to allow any status when blockers are present, or should the rewind happen before calling `discuss-feature` (making the status `discussed` by the time `discuss-feature` is invoked)?
   - Recommendation: The agent rewinds status before stopping (so by the time the user runs `discuss-feature`, the status is `discussed`). This avoids modifying the status guard and keeps the blocker-detection branch at Step 2.5 as purely a content check (not a status check).

## Sources

### Primary (HIGH confidence)

- `/workspace/conroy-gitea/Get-Features-Done/agents/gfd-researcher.md` — Agent RESEARCH BLOCKED return format, structured returns
- `/workspace/conroy-gitea/Get-Features-Done/agents/gfd-executor.md` — Auto-advance detection pattern (AUTO_CFG), Edit tool for blockers/decisions, deviation rules
- `/workspace/conroy-gitea/Get-Features-Done/agents/gfd-planner.md` — Planner structured returns, decision fidelity patterns
- `/workspace/conroy-gitea/Get-Features-Done/get-features-done/workflows/discuss-feature.md` — Full discuss flow, status transitions, status guard logic
- `/workspace/conroy-gitea/Get-Features-Done/get-features-done/GfdTools/Commands/FeatureUpdateStatusCommand.cs` — Valid statuses, frontmatter splice mechanism
- `/workspace/conroy-gitea/Get-Features-Done/get-features-done/GfdTools/Services/FrontmatterService.cs` — Frontmatter Extract/Splice implementation
- `/workspace/conroy-gitea/Get-Features-Done/get-features-done/GfdTools/Services/ConfigService.cs` — auto_advance config resolution
- `/workspace/conroy-gitea/Get-Features-Done/get-features-done/templates/feature.md` — FEATURE.md template with `## Blockers` section
- `/workspace/conroy-gitea/Get-Features-Done/docs/features/csharp-rewrite/03-SUMMARY.md` — Confirmed `feature add-blocker` command was dropped in C# rewrite
- `/workspace/conroy-gitea/Get-Features-Done/docs/features/auto-run-feature-toggle/RESEARCH.md` — Auto-advance architecture patterns, auto_advance config field details

### Secondary (MEDIUM confidence)

- `/workspace/conroy-gitea/Get-Features-Done/docs/features/codebase/ARCHITECTURE.md` — Overall GFD architecture and error handling patterns
- `/workspace/conroy-gitea/Get-Features-Done/docs/features/codebase/STACK.md` — Config key inventory including `auto_advance`

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — All components confirmed by direct codebase inspection
- Architecture patterns: HIGH — Patterns derived from existing implementations in agents and workflows
- Pitfalls: HIGH — Derived from careful reading of existing code paths and locked decisions in FEATURE.md

**Research date:** 2026-02-23
**Valid until:** 2026-03-23 (30 days — codebase is stable, no external dependencies)
