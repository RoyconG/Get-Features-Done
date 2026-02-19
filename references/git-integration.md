<overview>
Git integration for GFD framework.
</overview>

<core_principle>

**Commit outcomes, not process.**

The git log should read like a changelog of what shipped, not a diary of planning activity.
</core_principle>

<commit_points>

| Event                   | Commit? | Why                                              |
| ----------------------- | ------- | ------------------------------------------------ |
| PROJECT.md created      | YES     | Project initialization                           |
| PLAN.md created         | NO      | Intermediate - commit with plan completion       |
| RESEARCH.md created     | NO      | Intermediate                                     |
| DISCOVERY.md created    | NO      | Intermediate                                     |
| **Task completed**      | YES     | Atomic unit of work (1 commit per task)         |
| **Plan completed**      | YES     | Metadata commit (SUMMARY + docs)                |
| Handoff created         | YES     | WIP state preserved                              |

</commit_points>

<git_check>

```bash
[ -d .git ] && echo "GIT_EXISTS" || echo "NO_GIT"
```

If NO_GIT: Run `git init` silently. GFD projects always get their own repo.
</git_check>

<commit_formats>

<format name="initialization">
## Project Initialization

```
docs(gfd): initialize [project-name]

[One-liner from PROJECT.md]

Features:
- [feature-slug]: [goal]
- [feature-slug]: [goal]
- [feature-slug]: [goal]
```

What to commit:

```bash
git add docs/features/
git commit -m "docs(gfd): initialize [project-name]"
```

</format>

<format name="task-completion">
## Task Completion (During Plan Execution)

Each task gets its own commit immediately after completion.

```
{type}({slug}-{plan}): {task-name}

- [Key change 1]
- [Key change 2]
- [Key change 3]
```

**Commit types:**
- `feat` - New feature/functionality
- `fix` - Bug fix
- `test` - Test-only (TDD RED phase)
- `refactor` - Code cleanup (TDD REFACTOR phase)
- `perf` - Performance improvement
- `chore` - Dependencies, config, tooling

**Examples:**

```bash
# Standard task
git add src/api/auth.ts src/types/user.ts
git commit -m "feat(user-auth-01): create user registration endpoint

- POST /auth/register validates email and password
- Checks for duplicate users
- Returns JWT token on success
"

# TDD task - RED phase
git add src/__tests__/jwt.test.ts
git commit -m "test(user-auth-02): add failing test for JWT generation

- Tests token contains user ID claim
- Tests token expires in 1 hour
- Tests signature verification
"

# TDD task - GREEN phase
git add src/utils/jwt.ts
git commit -m "feat(user-auth-02): implement JWT generation

- Uses jose library for signing
- Includes user ID and expiry claims
- Signs with HS256 algorithm
"
```

</format>

<format name="plan-completion">
## Plan Completion (After All Tasks Done)

After all tasks committed, one final metadata commit captures plan completion.

```
docs({slug}-{plan}): complete [plan-name] plan

Tasks completed: [N]/[N]
- [Task 1 name]
- [Task 2 name]
- [Task 3 name]

SUMMARY: docs/features/{slug}/{slug}-{plan}-SUMMARY.md
```

What to commit:

```bash
git add docs/features/{slug}/{slug}-{plan}-PLAN.md docs/features/{slug}/{slug}-{plan}-SUMMARY.md
git commit -m "docs({slug}-{plan}): complete [plan-name] plan"
```

**Note:** Code files NOT included - already committed per-task.

</format>

<format name="handoff">
## Handoff (WIP)

```
wip: {slug} paused at task [X]/[Y]

Current: [task name]
[If blocked:] Blocked: [reason]
```

What to commit:

```bash
git add docs/features/
git commit -m "wip: {slug} paused at task [X]/[Y]"
```

</format>
</commit_formats>

<example_log>

**Per-task commits (feature-based):**
```
# Feature: user-auth
1a2b3c docs(user-auth-01): complete auth setup plan
4d5e6f feat(user-auth-01): add JWT token verification
7g8h9i feat(user-auth-01): create login endpoint

# Feature: payment-flow
0j1k2l docs(payment-flow-01): complete checkout plan
3m4n5o feat(payment-flow-01): add webhook signature verification
6p7q8r feat(payment-flow-01): implement payment session creation
9s0t1u feat(payment-flow-01): create checkout page component

# Feature: product-catalog
2v3w4x docs(product-catalog-01): complete catalog schema plan
5y6z7a feat(product-catalog-01): add search and filters
8b9c0d feat(product-catalog-01): create product catalog schema

# Initialization
5c6d7e docs(gfd): initialize my-project
```

Each plan produces 2-4 commits (tasks + metadata). Clear, granular, bisectable.

</example_log>

<anti_patterns>

**Still don't commit (intermediate artifacts):**
- PLAN.md creation (commit with plan completion)
- RESEARCH.md (intermediate)
- DISCOVERY.md (intermediate)
- Minor planning tweaks

**Do commit (outcomes):**
- Each task completion (feat/fix/test/refactor)
- Plan completion metadata (docs)
- Project initialization (docs)

**Key principle:** Commit working code and shipped outcomes, not planning process.

</anti_patterns>

<commit_strategy_rationale>

## Why Per-Task Commits?

**Context engineering for AI:**
- Git history becomes primary context source for future Claude sessions
- `git log --grep="{slug}-{plan}"` shows all work for a plan
- `git diff <hash>^..<hash>` shows exact changes per task
- Less reliance on parsing SUMMARY.md = more context for actual work

**Failure recovery:**
- Task 1 committed, Task 2 failed
- Claude in next session: sees task 1 complete, can retry task 2
- Can `git reset --hard` to last successful task

**Debugging:**
- `git bisect` finds exact failing task, not just failing plan
- `git blame` traces line to specific task context
- Each commit is independently revertable

**Observability:**
- Solo developer + Claude workflow benefits from granular attribution
- Atomic commits are git best practice

</commit_strategy_rationale>
