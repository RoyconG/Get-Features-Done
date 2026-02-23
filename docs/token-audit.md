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

**Token profile:** Medium. Research runs are typically 30-60K input tokens (feature context + web content).

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

**Token profile:** Medium-high. Planning involves reading multiple codebase files + prior SUMMARYs + writing structured plans. Typically 40-80K tokens.

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

**Token profile:** High. Executors read source files, implement changes, run builds, and may iterate on failures. Typical plan: 60-150K tokens.

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

**Token profile:** Low. Verification involves reading files and running grep checks. Typically 20-40K tokens.

**Current assignment:** sonnet (balanced profile) — **overqualified**. Switching verifier to haiku maintains quality while significantly reducing token usage for this role.

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

**Token profile:** Low. Reads files, outputs structured documents. Typically 15-30K tokens.

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

## Estimated Token Usage Per Workflow (balanced profile)

Estimated token ranges based on typical agent task sizes:

| Workflow Stage | Agent Role | Model | Est. Input | Est. Output |
|---|---|---|---|---|
| research | gfd-researcher | sonnet | 30-60K | 5-15K |
| plan (per plan) | gfd-planner | sonnet | 40-80K | 5-20K |
| execute (per plan) | gfd-executor | sonnet | 60-150K | 10-30K |
| verify | gfd-verifier | sonnet | 20-40K | 3-8K |
| map-codebase | gfd-codebase-mapper | haiku | 15-30K | 3-8K |

Actual token counts are recorded per-run in each feature's `## Token Usage` table in FEATURE.md (columns: Input, Output, Cache Read). Headless runs (auto-research, auto-plan) capture exact counts; interactive runs show `—`.

---

## Summary of Findings

1. **Balanced profile is well-calibrated** for the high-complexity roles (planner, executor, researcher). These should remain at sonnet.

2. **gfd-verifier is overqualified at sonnet**. Verification is a structured pattern-matching task with predefined protocols. Haiku handles this well. Recommend updating the `balanced` profile to use `haiku` for `gfd-verifier`.

3. **gfd-codebase-mapper is correctly assigned** at haiku in balanced profile.

4. **The new per-role override system** (implemented in Plan 01) allows users to tune any role independently via `model_overrides` in `docs/features/config.json`, without changing the global profile.

5. **No evidence of redundant work** in the current agent design. Each agent has a distinct role with non-overlapping outputs.

---

*Audit completed: 2026-02-23*
*Evidence sources: agents/gfd-researcher.md, agents/gfd-planner.md, agents/gfd-executor.md, agents/gfd-verifier.md, agents/gfd-codebase-mapper.md, get-features-done/GfdTools/Services/ConfigService.cs, docs/features/review-token-usage/RESEARCH.md*
