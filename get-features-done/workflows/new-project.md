<purpose>
Initialize a new GFD project through a unified flow: questioning, requirements, and feature scaffolding. This is the most leveraged moment in any project — deep questioning here means better features, better plans, better outcomes. One workflow takes you from idea to ready-for-feature-planning.
</purpose>

<required_reading>
Read all files referenced by the invoking prompt's execution_context before starting.

@$HOME/.claude/get-features-done/references/ui-brand.md
</required_reading>

<process>

## 1. Setup

**MANDATORY FIRST STEP — Execute these checks before ANY user interaction:**

```bash
INIT=$($HOME/.claude/get-features-done/bin/gfd-tools init new-project)
```

Extract from key=value output: `project_exists`, `has_codebase_map`, `has_existing_code`, `is_brownfield`, `needs_codebase_map`, `has_git`, `planner_model`, `researcher_model`, `mapper_model` (grep "^key=" | cut -d= -f2-).

**If `project_exists` is true:** Show error:

```
╔══════════════════════════════════════════════════════════════╗
║  ERROR                                                       ║
╚══════════════════════════════════════════════════════════════╝

Project already initialized. docs/features/ already exists.

**To fix:** Run /gfd:status to see current status.
```

Exit.

**If `has_git` is false:** Initialize git:
```bash
git init
```

## 2. Brownfield Detection

**If `needs_codebase_map` is true** (existing code detected but no codebase map):

Use AskUserQuestion:
- header: "Codebase"
- question: "I detected existing code in this directory. Would you like to map the codebase first to understand the architecture?"
- options:
  - "Map codebase first (Recommended)" — Run /gfd:map-codebase to understand existing architecture
  - "Skip mapping" — Proceed with project initialization

**If "Map codebase first":**

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► REDIRECTING TO MAP-CODEBASE
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Mapping codebase first. After mapping completes, run:

  /gfd:new-project
```

Exit command — user will re-run `/gfd:new-project` after mapping.

**If "Skip mapping" OR `needs_codebase_map` is false:** Continue to Step 3.

## 3. Deep Questioning

**Display stage banner:**

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► QUESTIONING
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

**Open the conversation:**

Ask inline (freeform, NOT AskUserQuestion):

"What do you want to build?"

Wait for their response. This gives you the context needed to ask intelligent follow-up questions.

**Follow the thread:**

Based on what they said, ask follow-up questions that dig into their response. Use AskUserQuestion with options that probe what they mentioned — interpretations, clarifications, concrete examples.

Keep following threads. Each answer opens new threads to explore. Ask about:
- What excited them
- What problem sparked this
- What they mean by vague terms
- What it would actually look like
- What's already decided

Consult `@$HOME/.claude/get-features-done/references/questioning.md` for techniques:
- Challenge vagueness
- Make abstract concrete
- Surface assumptions
- Find edges
- Reveal motivation

**Check context (background, not out loud):**

As you go, mentally check the context checklist from questioning.md:
- [ ] What they're building (concrete enough to explain to a stranger)
- [ ] Why it needs to exist (the problem or desire driving it)
- [ ] Who it's for (even if just themselves)
- [ ] What "done" looks like (observable outcomes)

If gaps remain, weave questions naturally. Don't suddenly switch to checklist mode.

**Decision gate:**

When you could write a clear PROJECT.md, use AskUserQuestion:

- header: "Ready?"
- question: "I think I understand what you're after. Ready to create PROJECT.md?"
- options:
  - "Create PROJECT.md" — Let's move forward
  - "Keep exploring" — I want to share more / ask me more

If "Keep exploring" — ask what they want to add, or identify gaps and probe naturally.

Loop until "Create PROJECT.md" selected.

## 4. Workflow Preferences

**Check for global defaults** at `~/.gfd/defaults.json`. If the file exists, offer to use saved defaults:

Use AskUserQuestion:
- header: "Defaults"
- question: "Use your saved default settings? (from ~/.gfd/defaults.json)"
- options:
  - "Yes (Recommended)" — Use saved defaults, skip settings questions
  - "No" — Configure settings manually

If "Yes": read `~/.gfd/defaults.json`, use those values for config.json, and skip directly to **Commit config.json** below.

If "No" or `~/.gfd/defaults.json` doesn't exist: proceed with the questions below.

**Round 1 — Core workflow settings:**

```
AskUserQuestion([
  {
    header: "Mode",
    question: "How do you want to work?",
    multiSelect: false,
    options: [
      { label: "YOLO (Recommended)", description: "Auto-approve, just execute" },
      { label: "Interactive", description: "Confirm at each step" }
    ]
  },
  {
    header: "Depth",
    question: "How thorough should planning be?",
    multiSelect: false,
    options: [
      { label: "Quick", description: "Ship fast (1-3 plans per feature)" },
      { label: "Standard", description: "Balanced scope and speed (3-5 plans per feature)" },
      { label: "Comprehensive", description: "Thorough coverage (5-10 plans per feature)" }
    ]
  },
  {
    header: "Execution",
    question: "Run plans in parallel?",
    multiSelect: false,
    options: [
      { label: "Parallel (Recommended)", description: "Independent plans run simultaneously" },
      { label: "Sequential", description: "One plan at a time" }
    ]
  },
  {
    header: "Git Tracking",
    question: "Commit planning docs to git?",
    multiSelect: false,
    options: [
      { label: "Yes (Recommended)", description: "Planning docs tracked in version control" },
      { label: "No", description: "Keep docs/features/ local-only (add to .gitignore)" }
    ]
  }
])
```

**Round 2 — Workflow agents:**

These spawn additional agents during planning/execution. They add tokens and time but improve quality.

| Agent | When it runs | What it does |
|-------|--------------|--------------|
| **Researcher** | Before planning each feature | Investigates domain, finds patterns, surfaces gotchas |
| **Plan Checker** | After plan is created | Verifies plan actually achieves the feature goal |
| **Verifier** | After feature execution | Confirms acceptance criteria were met |

All recommended for important projects. Skip for quick experiments.

```
AskUserQuestion([
  {
    header: "Research",
    question: "Research before planning each feature? (adds tokens/time)",
    multiSelect: false,
    options: [
      { label: "Yes (Recommended)", description: "Investigate domain, find patterns, surface gotchas" },
      { label: "No", description: "Plan directly from feature description" }
    ]
  },
  {
    header: "Plan Check",
    question: "Verify plans will achieve their goals? (adds tokens/time)",
    multiSelect: false,
    options: [
      { label: "Yes (Recommended)", description: "Catch gaps before execution starts" },
      { label: "No", description: "Execute plans without verification" }
    ]
  },
  {
    header: "Verifier",
    question: "Verify work satisfies acceptance criteria after each feature? (adds tokens/time)",
    multiSelect: false,
    options: [
      { label: "Yes (Recommended)", description: "Confirm deliverables match acceptance criteria" },
      { label: "No", description: "Trust execution, skip verification" }
    ]
  },
  {
    header: "AI Models",
    question: "Which AI models for planning agents?",
    multiSelect: false,
    options: [
      { label: "Balanced (Recommended)", description: "Sonnet for most agents — good quality/cost ratio" },
      { label: "Quality", description: "Opus for research/planning — higher cost, deeper analysis" },
      { label: "Budget", description: "Haiku where possible — fastest, lowest cost" }
    ]
  }
])
```

## 5. Create Directory Structure

**Display stage banner:**

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► CREATING PROJECT STRUCTURE
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

Create the directory structure:

```bash
mkdir -p docs/features
```

**If commit_docs = No:**
- Set `planning.commit_docs: false` in config.json
- Add `docs/features/` to `.gitignore` (create if needed)

## 6. Write PROJECT.md

Synthesize all context from the questioning session into `docs/features/PROJECT.md` using the template from `@$HOME/.claude/get-features-done/templates/project.md`.

**For greenfield projects:**

Initialize requirements as hypotheses:

```markdown
## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] [Requirement 1]
- [ ] [Requirement 2]
- [ ] [Requirement 3]

### Out of Scope

- [Exclusion 1] — [why]
- [Exclusion 2] — [why]
```

All Active requirements are hypotheses until shipped and validated.

**For brownfield projects (codebase map exists):**

Infer Validated requirements from existing code:

1. Read `docs/features/codebase/ARCHITECTURE.md` and `STACK.md`
2. Identify what the codebase already does
3. These become the initial Validated set

```markdown
## Requirements

### Validated

- ✓ [Existing capability 1] — existing
- ✓ [Existing capability 2] — existing

### Active

- [ ] [New requirement 1]
- [ ] [New requirement 2]

### Out of Scope

- [Exclusion 1] — [why]
```

**Key Decisions:**

Initialize with any decisions made during questioning:

```markdown
## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| [Choice from questioning] | [Why] | — Pending |
```

Do not compress. Capture everything gathered.

## 7. Write config.json

Write `docs/features/config.json` using the workflow preferences gathered in Step 4.

```json
{
  "mode": "yolo|interactive",
  "depth": "quick|standard|comprehensive",
  "workflow": {
    "research": true|false,
    "plan_check": true|false,
    "verifier": true|false,
    "auto_advance": false
  },
  "planning": {
    "commit_docs": true|false,
    "search_gitignored": false
  },
  "parallelization": {
    "enabled": true|false,
    "plan_level": true,
    "task_level": false,
    "max_concurrent_agents": 3,
    "min_plans_for_parallel": 2
  },
  "team": {
    "members": []
  },
  "gates": {
    "confirm_project": true,
    "confirm_feature": true,
    "confirm_plan": true,
    "execute_next_plan": true,
    "issues_review": true
  },
  "safety": {
    "always_confirm_destructive": true,
    "always_confirm_external_services": true
  }
}
```

## 8. Commit

Commit all artifacts:

```bash
git add docs/features/PROJECT.md docs/features/config.json && git diff --cached --quiet || git commit -m "docs(gfd): initialize project"
```

## 9. Done

**Display stage banner:**

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 GFD ► PROJECT INITIALIZED ✓
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

**[Project Name]**

| Artifact       | Location                          |
|----------------|-----------------------------------|
| Project        | docs/features/PROJECT.md          |
| Config         | docs/features/config.json         |
```

Present completion summary and next step:

```
───────────────────────────────────────────────────────────────

## ▶ Next Up

**Create your first feature** — define what to build

`/gfd:new-feature <feature-slug>`

<sub>`/clear` first → fresh context window</sub>

───────────────────────────────────────────────────────────────

**Also available:**
- `/gfd:map-codebase` — map existing codebase first (brownfield)
- `/gfd:status` — see overall project status

───────────────────────────────────────────────────────────────
```

</process>

<output>

- `docs/features/PROJECT.md`
- `docs/features/config.json`

</output>

<success_criteria>

- [ ] docs/features/ directory created
- [ ] Git repo initialized (if needed)
- [ ] Brownfield detection completed
- [ ] Deep questioning completed (threads followed, not rushed)
- [ ] PROJECT.md captures full context — **committed**
- [ ] config.json has mode, depth, parallelization, workflow agents — **committed**
- [ ] User knows next step is `/gfd:new-feature <slug>`

**Atomic commits:** Each stage commits its artifacts immediately. If context is lost, artifacts persist.

</success_criteria>
