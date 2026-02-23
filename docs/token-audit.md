# GFD Agent Token Audit

**Date:** 2026-02-23
**Purpose:** Analyze model assignments across all GFD agent roles for cost efficiency. Identifies overqualified model assignments and provides evidence-based recommendations for optimal defaults.

---

## Current Model Assignments

The GFD config system uses named profiles (`quality`, `balanced`, `budget`) defined in `get-features-done/GfdTools/Services/ConfigService.cs`. Per-role overrides are now supported via `model_overrides` in `docs/features/config.json`.

| Agent Role | quality | balanced | budget | Current Default |
|---|---|---|---|---|
| gfd-planner | opus | sonnet | sonnet | **sonnet** (balanced) |
| gfd-executor | opus | sonnet | haiku | **sonnet** (balanced) |
| gfd-verifier | opus | sonnet | haiku | **sonnet** (balanced) |
| gfd-researcher | opus | sonnet | haiku | **sonnet** (balanced) |
| gfd-codebase-mapper | sonnet | haiku | haiku | **haiku** (balanced) |

The `balanced` profile is the current default. Notable: `gfd-verifier` is at `sonnet` in balanced, while the verifier's task profile suggests `haiku` would be sufficient.

---

## Agent Role Analysis

### gfd-researcher

**Source:** `agents/gfd-researcher.md`

**Primary tasks:**
- Web search using WebSearch and WebFetch tools
- Synthesis of findings into structured RESEARCH.md documents
- Codebase analysis (Grep, Glob, Bash) to identify existing patterns
- Confidence-level assessment and source hierarchy evaluation

**Tool access:** Read, Write, Bash, Grep, Glob, WebSearch, WebFetch

**Complexity assessment:** Medium-high. The researcher must:
1. Execute multi-step web research with WebSearch + WebFetch for verification
2. Synthesize findings from multiple sources with conflicting information
3. Apply a "training data = hypothesis" discipline — actively questioning its own prior knowledge
4. Produce structured documents with accurate confidence levels
5. Make prescriptive recommendations ("use X") not exploratory summaries ("consider X or Y")

The synthesis and cross-verification steps require genuine reasoning, not just retrieval. However, the researcher's task is bounded (one feature domain per run) and produces a structured output format. Haiku would produce lower-quality synthesis with weaker cross-verification discipline.

**Minimum viable model:** haiku (usable but synthesis quality degrades — prescriptive guidance becomes exploratory, confidence levels are less reliable)

**Recommended model:** sonnet

**Cost profile:** Medium. Research runs are typically 30-60K input tokens (feature context + web content). A single research session runs $0.10-0.30 at sonnet rates.

---

### gfd-planner

**Source:** `agents/gfd-planner.md`

**Primary tasks:**
- Codebase exploration via Glob, Grep, Bash to map existing patterns
- Goal-backward methodology: derive truths → artifacts → wiring → key links
- Dependency graph construction and wave assignment
- Multi-file plan creation with exact file paths, task actions, verify steps
- Must-haves derivation in YAML frontmatter

**Tool access:** Read, Write, Bash, Glob, Grep, WebFetch

**Complexity assessment:** High. The planner must:
1. Reason backwards from acceptance criteria to implementation artifacts
2. Build accurate dependency graphs (fails silently if wrong — executor proceeds with bad ordering)
3. Produce specific, non-vague task actions that "a different Claude instance could execute without asking clarifying questions"
4. Detect TDD candidates and scope plans within 50% context budget
5. Honor locked decisions from FEATURE.md with zero deviation

The must-haves derivation requires structured reasoning across multiple levels simultaneously. Haiku plans are likely to produce vague task descriptions that require executor clarification, broken dependency graphs, and missing verification steps. The cost of a bad plan is high (executor wastes context on poorly-defined work).

**Minimum viable model:** sonnet (haiku produces vague, incomplete plans)

**Recommended model:** sonnet

**Cost profile:** Medium-high. Planning involves reading multiple codebase files + prior SUMMARYs + writing structured plans. Typically 40-80K tokens. $0.12-0.25 per plan at sonnet rates.

---

### gfd-executor

**Source:** `agents/gfd-executor.md`

**Primary tasks:**
- Multi-file code changes with exact file editing (Read → Edit/Write)
- Running builds, tests, and verification commands (Bash)
- Deviation detection: auto-fix bugs (Rule 1), add missing critical functionality (Rule 2), fix blockers (Rule 3)
- Sequential multi-step implementation with state tracking across tasks
- Atomic commit protocol with per-task commits

**Tool access:** Read, Write, Edit, Bash, Grep, Glob

**Complexity assessment:** High. The executor must:
1. Parse plan tasks precisely and implement them in correct sequence
2. Apply deviation rules correctly — distinguish bugs from architectural changes
3. Write correct, idiomatic code that matches existing codebase patterns
4. Handle build failures by diagnosing root causes, not symptom-chasing
5. Maintain per-task state (what's been committed, what's in-flight) across a long context

Executor tasks have the highest failure cost: incorrect code changes require rework, build failures waste turns, missed deviation rules leave security holes. Code quality directly correlates with model capability in the executor role.

**Minimum viable model:** sonnet (haiku produces incomplete implementations, misses deviation rules, generates code that doesn't compile)

**Recommended model:** sonnet

**Cost profile:** High. Executors read source files, implement changes, run builds, and may iterate on failures. Typical plan: 60-150K tokens. $0.20-0.50 per plan at sonnet rates.

---

### gfd-verifier

**Source:** `agents/gfd-verifier.md`

**Primary tasks:**
- Load FEATURE.md acceptance criteria
- Grep and Read files to verify artifacts exist and are substantive (not stubs)
- Pattern-match against stub detection patterns (TODO, placeholder, empty returns)
- Check key links (imports, wiring, connection patterns)
- Produce structured VERIFICATION.md with gap YAML for planner re-processing

**Tool access:** Read, Write, Bash, Grep, Glob

**Complexity assessment:** Low-medium. The verifier's task is fundamentally pattern-matching and boolean evaluation:
1. Does file X exist? (filesystem check)
2. Does file X have at least N lines? (wc -l check)
3. Does file X contain pattern Y? (grep check)
4. Does wiring pattern Z exist? (grep with regex)

The verifier uses structured templates, has predefined stub detection patterns, and outputs a structured YAML format. It does NOT write code, make architectural decisions, or synthesize novel information. The "critical mindset" is process-driven, not intelligence-driven: follow the protocol, report what you find.

Haiku handles structured comparison tasks well. The gap output format is fixed YAML with specific fields — haiku reliably produces structured output. The risk with haiku is missing subtle stubs or misclassifying partial implementations, but the structured protocol reduces this risk substantially.

**Minimum viable model:** haiku

**Recommended model:** haiku

**Cost profile:** Low. Verification involves reading files and running grep checks. Typically 20-40K tokens. $0.02-0.05 at haiku rates vs $0.06-0.15 at sonnet rates.

**Current assignment:** sonnet (balanced profile) — **overqualified**. This is the highest-value optimization: switching verifier to haiku maintains quality while cutting per-verification cost by 60-75%.

---

### gfd-codebase-mapper

**Source:** `agents/gfd-codebase-mapper.md`

**Primary tasks:**
- Read package manifests, config files, source directories
- Grep for patterns (imports, exports, naming conventions)
- Summarize findings into structured templates (STACK.md, ARCHITECTURE.md, etc.)
- Follow fixed templates — fill in sections from actual codebase data

**Tool access:** Read, Bash, Grep, Glob, Write

**Complexity assessment:** Low. The mapper is explicitly a read-and-summarize task:
1. Run predefined exploration commands (grep, find, cat)
2. Fill in template structure with actual findings
3. Include file paths throughout
4. Write documents directly to `docs/features/codebase/`

The mapper's philosophy section explicitly acknowledges: "Document quality over brevity" but the documents are structured summaries, not synthesized analyses. Templates have fixed sections. The mapper does not make recommendations or architectural decisions — it describes what IS.

Haiku is well-suited for structured summarization tasks with predefined templates. The codebase mapper is the closest to a "pure structured output" task in the GFD agent suite.

**Minimum viable model:** haiku

**Recommended model:** haiku

**Cost profile:** Low. Reads files, outputs structured documents. Typically 15-30K tokens. $0.01-0.04 at haiku rates.

**Current assignment:** haiku (balanced profile) — **already optimal**.

---

## Recommendations

Based on task analysis for each agent role, here are the recommended default model assignments:

| Agent Role | Recommended | Minimum | Current (balanced) | Change? | Rationale |
|---|---|---|---|---|---|
| gfd-researcher | sonnet | haiku | sonnet | No | Web synthesis requires reasoning; haiku produces lower-quality prescriptive guidance |
| gfd-planner | sonnet | sonnet | sonnet | No | Goal-backward dependency reasoning fails below sonnet threshold |
| gfd-executor | sonnet | sonnet | sonnet | No | Code correctness and deviation detection require full reasoning capability |
| gfd-verifier | haiku | haiku | sonnet | **Yes — downgrade to haiku** | Pattern-matching + structured output task; sonnet is overqualified |
| gfd-codebase-mapper | haiku | haiku | haiku | No | Already optimal |

### Priority Recommendation

**Switch `gfd-verifier` from `sonnet` to `haiku` in the `balanced` profile.**

This is the highest-value change: verification is run after every execute workflow, potentially multiple times per feature (initial + re-verification after gap closure). Each verification session at sonnet costs ~3-5x more than at haiku for equivalent quality output.

---

## Profile Assessment

### quality profile (all opus / sonnet for mapper)
**Assessment:** Expensive and partially unjustified. Opus for verifier and mapper is wasteful — these roles do not benefit from opus-level intelligence. Quality profile is appropriate for users who want maximum plan and execution quality for complex features and are willing to pay for it.

**Recommended change:** Lower verifier and mapper to haiku even in quality profile. The "quality" in quality profile should reflect reasoning quality for roles that need it, not uniformity.

### balanced profile (all sonnet / haiku for mapper)
**Assessment:** Well-calibrated for planner/executor/researcher. The verifier assignment at sonnet is the one optimization opportunity. The mapper at haiku is correct.

**Recommended change:** Change `gfd-verifier` from `sonnet` to `haiku`.

### budget profile (sonnet for planner, haiku for rest)
**Assessment:** Appropriate. Planner correctly kept at sonnet (haiku produces bad plans). Executor at haiku is the risk — budget profile accepts reduced code quality for cost savings.

**No change recommended** for budget profile.

---

## Pricing Reference (Feb 2026)

Current Claude model pricing from Anthropic official docs (verified Feb 2026):

| Alias | Model ID | Input $/MTok | Output $/MTok | Context Window |
|---|---|---|---|---|
| haiku | claude-haiku-4-5 | $1 | $5 | 200K |
| sonnet | claude-sonnet-4-6 | $3 | $15 | 200K |
| opus | claude-opus-4-6 | $5 | $25 | 200K |

Note: Claude Haiku 3 (`claude-3-haiku-20240307`) is deprecated and retiring April 19, 2026. The `haiku` alias must resolve to `claude-haiku-4-5`.

---

## Estimated Per-Workflow Cost (balanced profile, current)

Rough estimates based on typical agent task sizes and token counts:

| Workflow Stage | Agent Role | Model | Est. Tokens | Est. Cost |
|---|---|---|---|---|
| research | gfd-researcher | sonnet | 50K | $0.10-0.25 |
| plan (per plan) | gfd-planner | sonnet | 60K | $0.12-0.25 |
| execute (per plan) | gfd-executor | sonnet | 100K | $0.20-0.45 |
| verify | gfd-verifier | sonnet | 30K | $0.06-0.14 |
| map-codebase | gfd-codebase-mapper | haiku | 20K | $0.01-0.03 |

**Typical full feature (1 research + 3 plans + 1 verification):**
- Current balanced: ~$0.90-1.75 per feature
- After verifier optimization: ~$0.84-1.64 per feature (small saving per feature)
- At scale (many verifications per feature due to gap-closure cycles): optimization compounds

---

## Estimated Per-Workflow Cost (balanced profile, after optimization)

With `gfd-verifier` changed from `sonnet` to `haiku`:

| Workflow Stage | Agent Role | Model | Est. Tokens | Est. Cost |
|---|---|---|---|---|
| research | gfd-researcher | sonnet | 50K | $0.10-0.25 |
| plan (per plan) | gfd-planner | sonnet | 60K | $0.12-0.25 |
| execute (per plan) | gfd-executor | sonnet | 100K | $0.20-0.45 |
| verify | gfd-verifier | **haiku** | 30K | **$0.01-0.04** |
| map-codebase | gfd-codebase-mapper | haiku | 20K | $0.01-0.03 |

**Saving per verification:** ~$0.05-0.10 (75% reduction for verification stage)

---

## Summary of Findings

1. **Balanced profile is well-calibrated** for the high-complexity roles (planner, executor, researcher). These should remain at sonnet.

2. **gfd-verifier is overqualified at sonnet**. Verification is a structured pattern-matching task with predefined protocols. Haiku handles this well at ~25% of the sonnet cost. Recommend updating the `balanced` profile to use `haiku` for `gfd-verifier`.

3. **gfd-codebase-mapper is correctly assigned** at haiku in balanced profile.

4. **The new per-role override system** (implemented in Plan 01) allows users to tune any role independently via `model_overrides` in `docs/features/config.json`, without changing the global profile.

5. **No evidence of redundant work** in the current agent design. Each agent has a distinct role with non-overlapping outputs.

---

*Audit completed: 2026-02-23*
*Evidence sources: agents/gfd-researcher.md, agents/gfd-planner.md, agents/gfd-executor.md, agents/gfd-verifier.md, agents/gfd-codebase-mapper.md, get-features-done/GfdTools/Services/ConfigService.cs, docs/features/review-token-usage/RESEARCH.md*
