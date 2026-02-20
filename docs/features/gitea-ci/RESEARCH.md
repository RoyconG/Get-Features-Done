# Feature: Gitea CI — Research

**Researched:** 2026-02-20
**Domain:** Gitea Actions workflow orchestration, CI/CD automation, Claude API monitoring
**Confidence:** MEDIUM

## User Constraints (from FEATURE.md)

### Locked Decisions
- **Feature selection:** Auto-detect by FEATURE.md status (`discussed` → research, `researched` → plan)
- **Architecture:** Orchestrator workflow dispatches sub-workflows with feature slug parameter
- **Parallelism:** Configurable max concurrent (default 1)
- **Runner:** Self-hosted `claude` runner, Claude CLI pre-installed, needs .NET 10 + gfd-tools setup
- **Secrets:** `ANTHROPIC_API_KEY` stored as Gitea secret
- **Branch strategy:** Per-feature branches (`ci/<slug>`), auto-create PRs
- **Stale branches:** Skip features with existing unmerged branches/PRs
- **Usage guard:** Monitor Claude API usage, hard-stop on threshold
- **Failure policy:** Continue with next feature, log failure
- **Summary:** Workflow artifact (not committed to repo)
- **Notifications:** None — check PRs and workflow UI manually
- **Manual dispatch:** Accepts optional slug + type (research vs plan) parameters

### Dependency
This feature depends on `auto-skills` being implemented first. The `gfd-tools auto-research` and `gfd-tools auto-plan` commands must exist before the CI workflows can call them.

### Out of Scope
- Notifications (Slack, email, etc.)
- Committing run summary to repo (it's a workflow artifact only)
- GitHub Actions (Gitea-only)

---

## Summary

This feature adds Gitea Actions workflows to autonomously run GFD research and planning overnight. The core challenge is Gitea Actions compatibility: it is "mostly compatible" with GitHub Actions but has meaningful gaps — specifically, `concurrency` is listed as ignored in official docs, and dynamic matrix (using job outputs as matrix inputs) is a known-broken feature as of 2026. These gaps require architectural adjustments.

The orchestrator+sub-workflow model maps well to Gitea's `workflow_call` reusable workflow pattern, which does work in Gitea (though there are authentication quirks with cross-repo reusable workflows — same-repo workflows are simpler and more reliable). The runner is self-hosted and runs in host mode, which means `.NET 10` and `gfd-tools` can be installed once at runner registration time or via a setup step that checks for their presence.

For creating per-feature PRs and checking for existing unmerged branches, the `tea` CLI (Gitea's official CLI tool) is the right tool. It supports `tea pulls create`, `tea pulls list`, and `tea branches list` with JSON output. For uploading the nightly summary as a workflow artifact, `actions/gitea-upload-artifact` (the Gitea-native fork of `upload-artifact`) is the correct action. Artifact upload requires act_runner to use the external public Gitea URL.

**Primary recommendation:** Use two workflow files — an orchestrator (`.gitea/workflows/gfd-nightly.yml`) with cron + `workflow_dispatch`, and a sub-workflow (`.gitea/workflows/gfd-process-feature.yml`) triggered by `workflow_call` with `slug` and `type` inputs. Feature scanning uses `gfd-tools list-features --status discussed` and `--status researched`. Parallelism is controlled via a sequential loop (not dynamic matrix, which is broken) with a counter. Use `tea` CLI for PR operations.

---

## Standard Stack

### Core
| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| Gitea Actions | 1.23+ | CI/CD runtime | Project is on Gitea; Actions is native CI |
| act_runner | latest | Executes jobs on `claude` runner | Only officially supported runner |
| `tea` CLI | 0.10.1 | PR creation, branch/PR existence checks | Gitea's official CLI, supports all needed operations |
| `actions/gitea-upload-artifact` | v4 (via ChristopherHX) | Upload nightly summary artifact | Gitea-native fork of upload-artifact action |
| `actions/setup-dotnet` | v5 | Install .NET 10 in workflow setup step | Official action, supports .NET 10 (`10.0.x`) |

### Supporting
| Component | Version | Purpose | When to Use |
|-----------|---------|---------|-------------|
| Anthropic Usage API | v1 | Monitor Claude API token spend | In orchestrator's usage-guard check before each dispatch |
| `gfd-tools list-features` | current | Scan for eligible features by status | Orchestrator feature discovery step |
| `gfd-tools auto-research` | (auto-skills feature) | Run research headlessly | Sub-workflow step for `discussed` features |
| `gfd-tools auto-plan` | (auto-skills feature) | Run planning headlessly | Sub-workflow step for `researched` features |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Sequential loop for parallelism control | Dynamic matrix with `max-parallel` | Dynamic matrix is broken in Gitea — job outputs don't propagate to matrix inputs. Sequential loop avoids this entirely. |
| `tea` CLI for PR ops | Gitea REST API via `curl` | Both work, but `tea` is higher-level and idiomatic. `curl` + `jq` is a fallback if `tea` isn't pre-installed on runner. |
| `workflow_call` sub-workflow | Standalone workflow with `workflow_dispatch` | `workflow_call` is cleaner (orchestrator controls invocation); `workflow_dispatch`-based dispatch requires extra API calls. |

**Installation (on runner host, one-time):**
```bash
# tea CLI
wget -O /usr/local/bin/tea https://dl.gitea.com/tea/0.10.1/tea-0.10.1-linux-amd64
chmod +x /usr/local/bin/tea
tea login add --url https://YOUR_GITEA_URL --token YOUR_TOKEN --name ci

# .NET 10 — or use setup-dotnet action in workflow
# gfd-tools — cloned from repo and built, or via the install.sh mechanism
```

---

## Architecture Patterns

### Recommended File Structure
```
.gitea/
└── workflows/
    ├── gfd-nightly.yml         # Orchestrator: cron + workflow_dispatch
    └── gfd-process-feature.yml # Sub-workflow: workflow_call with slug + type
```

### Pattern 1: Orchestrator + Reusable Sub-Workflow

**What:** The orchestrator scans eligible features, then calls the sub-workflow once per feature (sequentially by default, up to `max_concurrent` in parallel via background job tracking). The sub-workflow handles one feature: checkout branch, run `gfd-tools auto-research` or `gfd-tools auto-plan`, commit, create PR.

**When to use:** Always — this is the locked architecture decision.

**Orchestrator skeleton:**
```yaml
# .gitea/workflows/gfd-nightly.yml
name: GFD Nightly

on:
  schedule:
    - cron: '0 3 * * *'   # 3 AM daily
  workflow_dispatch:
    inputs:
      slug:
        description: 'Feature slug (leave empty for auto-detect)'
        required: false
        type: string
      type:
        description: 'Operation type (research or plan)'
        required: false
        type: choice
        options: ['', 'research', 'plan']

jobs:
  orchestrate:
    runs-on: claude
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: '10.0.x'

      - name: Setup gfd-tools
        run: |
          # gfd-tools is a dotnet run wrapper — the project must be present
          # Runner has repo checked out, so gfd-tools should be available at
          # get-features-done/bin/gfd-tools (the wrapper script)
          chmod +x get-features-done/bin/gfd-tools

      - name: Discover eligible features
        id: discover
        run: |
          # Manual dispatch with specific slug
          if [ -n "${{ inputs.slug }}" ]; then
            echo "features=${{ inputs.slug }}" >> "$GITEA_OUTPUT"
            echo "types=${{ inputs.type }}" >> "$GITEA_OUTPUT"
            exit 0
          fi
          # Auto-detect: discussed → research, researched → plan
          DISCUSSED=$(./get-features-done/bin/gfd-tools list-features --status discussed | grep "^feature_slug=" | cut -d= -f2)
          RESEARCHED=$(./get-features-done/bin/gfd-tools list-features --status researched | grep "^feature_slug=" | cut -d= -f2)
          # Build slug:type pairs
          PAIRS=""
          for s in $DISCUSSED; do PAIRS="$PAIRS $s:research"; done
          for s in $RESEARCHED; do PAIRS="$PAIRS $s:plan"; done
          echo "pairs=$PAIRS" >> "$GITEA_OUTPUT"

      - name: Check usage guard
        id: usage_check
        run: |
          # Query Anthropic usage API (last 1h)
          # Requires ANTHROPIC_ADMIN_KEY secret with admin key (sk-ant-admin...)
          USAGE=$(curl -s "https://api.anthropic.com/v1/organizations/usage_report/messages?\
            starting_at=$(date -u -d '1 hour ago' +%Y-%m-%dT%H:%M:%SZ)&\
            ending_at=$(date -u +%Y-%m-%dT%H:%M:%SZ)&\
            bucket_width=1h" \
            --header "anthropic-version: 2023-06-01" \
            --header "x-api-key: ${{ secrets.ANTHROPIC_ADMIN_KEY }}")
          # Parse total output tokens from response (use jq)
          # Compare against threshold; write "stop=true" if over
          echo "stop=false" >> "$GITEA_OUTPUT"

      # Sequential dispatch loop (avoid dynamic matrix — broken in Gitea)
      - name: Process features
        if: steps.usage_check.outputs.stop != 'true'
        run: |
          MAX_CONCURRENT="${{ vars.GFD_MAX_CONCURRENT || '1' }}"
          for pair in ${{ steps.discover.outputs.pairs }}; do
            slug="${pair%%:*}"
            type="${pair##*:}"
            # Skip if ci/<slug> branch or open PR exists
            if git ls-remote --exit-code origin "ci/$slug" > /dev/null 2>&1; then
              echo "Skipping $slug — branch ci/$slug already exists"
              continue
            fi
            # Check for open PR for this branch
            PR_COUNT=$(tea pulls list --state open --output json | jq "[.[] | select(.head.label == \"ci/$slug\")] | length" 2>/dev/null || echo "0")
            if [ "$PR_COUNT" -gt "0" ]; then
              echo "Skipping $slug — open PR already exists"
              continue
            fi
            # Re-check usage guard
            # ... (same check as above)

            # Invoke sub-workflow synchronously (Gitea doesn't support workflow_dispatch API easily)
            # Alternative: run sub-workflow logic inline, or use workflow_call pattern below
            echo "Processing $slug ($type)"
          done

      - name: Upload summary artifact
        if: always()
        uses: https://github.com/christopherhx/gitea-upload-artifact@v4
        with:
          name: gfd-nightly-summary
          path: /tmp/gfd-summary.md
          retention-days: 30
```

**Sub-workflow skeleton:**
```yaml
# .gitea/workflows/gfd-process-feature.yml
name: GFD Process Feature

on:
  workflow_call:
    inputs:
      slug:
        required: true
        type: string
      type:
        required: true
        type: string

jobs:
  process:
    runs-on: claude
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: '10.0.x'

      - name: Create feature branch
        run: |
          git config user.name "GFD CI"
          git config user.email "ci@gitea.local"
          git checkout -b "ci/${{ inputs.slug }}"

      - name: Run GFD auto operation
        env:
          ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
        run: |
          if [ "${{ inputs.type }}" = "research" ]; then
            ./get-features-done/bin/gfd-tools auto-research "${{ inputs.slug }}"
          else
            ./get-features-done/bin/gfd-tools auto-plan "${{ inputs.slug }}"
          fi

      - name: Push branch
        run: |
          git push origin "ci/${{ inputs.slug }}"

      - name: Create PR
        run: |
          tea pulls create \
            --title "ci(${{ inputs.slug }}): auto-${{ inputs.type }}" \
            --base main \
            --head "ci/${{ inputs.slug }}" \
            --description "Automated ${{ inputs.type }} run for ${{ inputs.slug }}"
```

### Pattern 2: Feature Discovery via gfd-tools list-features

**What:** Use `gfd-tools list-features --status <status>` to get feature slugs, then parse the key=value output.

**How gfd-tools outputs:**
```
count=2
total=10
feature_slug=my-feature
feature_name=My Feature
feature_status=discussed
...
```

**Parsing pattern in bash:**
```bash
SLUGS=$(./get-features-done/bin/gfd-tools list-features --status discussed \
  | grep "^feature_slug=" | cut -d= -f2)
```

### Pattern 3: Branch and PR Existence Check

**What:** Before processing a feature, check for `ci/<slug>` branch and open PRs. Skip if found.

```bash
# Check branch exists on remote
if git ls-remote --exit-code origin "refs/heads/ci/$SLUG" > /dev/null 2>&1; then
  echo "Skipping — branch exists"
  continue
fi

# Check open PR exists for this head branch
PR_EXISTS=$(tea pulls list --state open --output json \
  | jq "any(.[]; .head.label == \"ci/$SLUG\")")
if [ "$PR_EXISTS" = "true" ]; then
  echo "Skipping — open PR exists"
  continue
fi
```

### Pattern 4: Claude API Usage Guard

**What:** Query Anthropic's Usage API before each feature dispatch. Hard-stop if usage exceeds threshold.

**Requirements:** Separate `ANTHROPIC_ADMIN_KEY` secret (Admin API key starting with `sk-ant-admin...`). This is DIFFERENT from the `ANTHROPIC_API_KEY` used to run Claude.

```bash
# Check usage in last hour
NOW=$(date -u +%Y-%m-%dT%H:%M:%SZ)
HOUR_AGO=$(date -u -d '1 hour ago' +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || \
           date -u -v-1H +%Y-%m-%dT%H:%M:%SZ)  # macOS fallback

RESPONSE=$(curl -s \
  "https://api.anthropic.com/v1/organizations/usage_report/messages?starting_at=${HOUR_AGO}&ending_at=${NOW}&bucket_width=1h" \
  --header "anthropic-version: 2023-06-01" \
  --header "x-api-key: ${ANTHROPIC_ADMIN_KEY}")

OUTPUT_TOKENS=$(echo "$RESPONSE" | jq '[.data[].output_tokens] | add // 0')
THRESHOLD=100000  # configurable

if [ "$OUTPUT_TOKENS" -gt "$THRESHOLD" ]; then
  echo "Usage threshold exceeded ($OUTPUT_TOKENS > $THRESHOLD). Hard-stopping."
  exit 0  # Exit cleanly — don't fail the orchestrator
fi
```

### Anti-Patterns to Avoid

- **Dynamic matrix for parallelism:** `strategy.matrix` with dynamic values from job outputs is broken in Gitea as of 2026. Use a sequential bash loop instead.
- **`concurrency:` group syntax:** Listed as "currently ignored" in Gitea Actions comparison docs. Don't rely on it for de-duplication — use branch/PR existence checks instead.
- **Cross-repo `workflow_call`:** Auth issues with cross-repo reusable workflows have been reported in Gitea 1.25.3. Keep both workflows in the same repo (`.gitea/workflows/`).
- **`timeout-minutes` on jobs:** Ignored in Gitea Actions. Use shell-level timeouts (`timeout 3600 command`) if needed.
- **`permissions:` blocks:** Ignored in Gitea Actions — don't waste effort configuring them.
- **`continue-on-error`:** Not supported in Gitea Actions. Handle failure/continue logic in bash instead.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Branch/PR management | Custom git API calls | `tea` CLI | Tea handles auth, pagination, JSON output cleanly |
| Artifact upload | Custom S3/storage upload | `actions/gitea-upload-artifact@v4` (ChristopherHX fork) | Native Gitea artifact storage, integrated with UI |
| .NET installation | apt-get or manual download | `actions/setup-dotnet@v5` | Handles version selection, PATH setup, caching |
| Feature status query | grep/awk on FEATURE.md files | `gfd-tools list-features --status` | Already implemented; correct parsing of frontmatter |
| Usage data | Token tracking in local state | Anthropic Usage API (`/v1/organizations/usage_report/messages`) | Accurate historical data, no local state management |

**Key insight:** The Gitea Actions ecosystem mirrors GitHub Actions closely enough that most `actions/*` actions work. The main gap is dynamic matrix and concurrency group — both require bash-level workarounds.

---

## Common Pitfalls

### Pitfall 1: Dynamic Matrix Not Working
**What goes wrong:** You try to generate a list of features dynamically (from a prior job's outputs) and feed it into `strategy.matrix`. Gitea generates only one job or errors.
**Why it happens:** Gitea doesn't evaluate template expressions inside the `matrix` key — it processes the whole workflow before jobs run.
**How to avoid:** Use a sequential bash loop in a single job. For true parallelism up to N, use background processes with `&` and `wait`, or just keep `max_concurrent=1` (the locked default).
**Warning signs:** Matrix job count is always 1 regardless of input list size.

### Pitfall 2: Missing Admin API Key for Usage Guard
**What goes wrong:** Usage guard fails with 401 or 403 because the `ANTHROPIC_API_KEY` (standard key) is used instead of an Admin API key.
**Why it happens:** The Usage and Cost API requires an Admin API key (`sk-ant-admin...`) provisioned through the Anthropic Console by an org admin — it's a separate credential from the regular API key.
**How to avoid:** Store a second secret: `ANTHROPIC_ADMIN_KEY`. Only org admins can create these. If unavailable, fall back to token counting from Claude CLI output (less accurate but no extra credential needed).
**Warning signs:** `{"error": "Unauthorized"}` from the usage API endpoint.

### Pitfall 3: Stale Runner Cache Causing Mysterious Failures
**What goes wrong:** Reusable workflows or multi-step jobs fail with cryptic errors, seemingly unrelated to workflow changes.
**Why it happens:** act_runner's execution environment can become "polluted" after extended use or failed tasks.
**How to avoid:** If runner starts producing unexplained failures, restart or recreate the act_runner container/process.
**Warning signs:** Errors that reference missing files or auth issues that weren't present before.

### Pitfall 4: `tea` Not Authenticated in CI
**What goes wrong:** `tea pulls create` fails with auth error.
**Why it happens:** `tea` needs a login configured (stored at `~/.config/tea/config.yml`). On a fresh runner checkout, this won't exist.
**How to avoid:** Either pre-configure tea on the runner host (one-time setup) OR use `tea login add --non-interactive --url $GITEA_URL --token ${{ secrets.GITEA_TOKEN }}` in a setup step. Store a `GITEA_TOKEN` secret (personal access token with repo permissions).
**Warning signs:** `No login found` or `authentication required` from tea commands.

### Pitfall 5: Cron Schedule Not Triggering (Fast-Forward Merge Bug)
**What goes wrong:** After merging the workflow file via fast-forward-only strategy, the schedule never fires.
**Why it happens:** Gitea had a bug (fixed in v1.24.0) where fast-forward merges didn't register the cron schedule in the database.
**How to avoid:** Ensure Gitea is version 1.24.0+ (where the fix was merged). Or merge workflow PRs with a merge commit strategy.
**Warning signs:** No workflow runs appear on the schedule; `action_schedule_spec` table has no entries for the workflow.

### Pitfall 6: `git push` Fails Due to Missing Runner Credentials
**What goes wrong:** `git push origin ci/$SLUG` fails with auth error even though `GITEA_TOKEN` is a secret.
**Why it happens:** The git remote URL uses https, but credentials aren't configured for the checkout.
**How to avoid:** Use `actions/checkout@v4` with a token, which configures git credential helper automatically:
```yaml
- uses: actions/checkout@v4
  with:
    token: ${{ secrets.GITEA_TOKEN }}
```
Or manually configure git credentials in a step.

### Pitfall 7: Usage Guard Needs Separate Admin Key (Org Required)
**What goes wrong:** Admin API key creation fails — the "Admin API keys" option isn't available.
**Why it happens:** Admin API keys require an Anthropic organization (not individual account). Individual accounts can't create admin keys.
**How to avoid:** If on an individual Anthropic account, the usage guard must use a different approach — e.g., checking Claude CLI's `--output-stats` flag output from `auto-research`/`auto-plan`, accumulating locally during the nightly run, or skipping the API-based guard and using a simpler token budget passed as a workflow variable.
**Warning signs:** No "Admin API Keys" section in Anthropic Console settings.

---

## Code Examples

### Feature Discovery and Sequential Processing

```bash
# Source: gfd-tools list-features implementation (codebase)
# Parse gfd-tools key=value output
parse_gfd_output() {
  local output="$1"
  local key="$2"
  echo "$output" | grep "^${key}=" | cut -d= -f2-
}

# Discover discussed features
DISCUSSED_OUTPUT=$(./get-features-done/bin/gfd-tools list-features --status discussed)
DISCUSSED_SLUGS=$(echo "$DISCUSSED_OUTPUT" | grep "^feature_slug=" | cut -d= -f2)

RESEARCHED_OUTPUT=$(./get-features-done/bin/gfd-tools list-features --status researched)
RESEARCHED_SLUGS=$(echo "$RESEARCHED_OUTPUT" | grep "^feature_slug=" | cut -d= -f2)
```

### PR Existence Check via tea

```bash
# Source: tea CLI docs (https://gitea.com/gitea/tea/src/branch/main/docs/CLI.md)
SLUG="my-feature"
OPEN_PRS=$(tea pulls list --state open --fields head --output simple)
if echo "$OPEN_PRS" | grep -q "ci/$SLUG"; then
  echo "PR already exists for ci/$SLUG — skipping"
fi
```

### Upload Nightly Summary Artifact

```yaml
# Source: https://gitea.com/actions/gitea-upload-artifact
- name: Upload nightly summary
  if: always()
  uses: https://github.com/christopherhx/gitea-upload-artifact@v4
  with:
    name: gfd-nightly-summary-${{ gitea.run_number }}
    path: /tmp/gfd-nightly-summary.md
    retention-days: 30
```

### Setup .NET 10 in Workflow

```yaml
# Source: https://gitea.com/actions/setup-dotnet
- name: Setup .NET 10
  uses: actions/setup-dotnet@v5
  with:
    dotnet-version: '10.0.x'
```

### workflow_dispatch with Optional Inputs

```yaml
# Source: Gitea forum - workflow_dispatch added in Gitea 1.23
on:
  workflow_dispatch:
    inputs:
      slug:
        description: 'Feature slug (optional, leave empty for auto-detect)'
        required: false
        type: string
        default: ''
      type:
        description: 'Operation type'
        required: false
        type: choice
        options:
          - ''
          - research
          - plan
```

### Reusable Sub-Workflow Trigger

```yaml
# In sub-workflow: .gitea/workflows/gfd-process-feature.yml
on:
  workflow_call:
    inputs:
      slug:
        required: true
        type: string
      type:
        required: true
        type: string

# In orchestrator, calling it:
jobs:
  call-sub:
    uses: ./.gitea/workflows/gfd-process-feature.yml
    with:
      slug: my-feature
      type: research
    secrets: inherit
```

Note: `workflow_call` with static `with:` values works. The limitation is that `with:` values can't be dynamically generated from matrix outputs. For a loop of features, you'd invoke each `workflow_call` job with a static feature name, or use bash-level sequential processing in one job.

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| No workflow_dispatch inputs | workflow_dispatch inputs supported | Gitea 1.23 (2024) | Manual slug + type dispatch is now possible |
| No schedule support | Schedule/cron supported (with bug fix) | Gitea 1.24.0 (2025) | Nightly cron runs now reliable |
| No concurrency support | concurrency groups merged (PR #32751) | Gitea 2024-2025 | BUT still listed as "ignored" in comparison docs — verify on target instance |
| No reusable workflows | workflow_call supported | Gitea (date unclear) | Orchestrator → sub-workflow pattern works |
| Concurrency via `concurrency:` | Concurrency via bash logic + branch checks | N/A | Gitea concurrency docs say it's ignored — use bash guards |

**Deprecated/outdated:**
- Dynamic matrix with job outputs: Broken as of 2026, open issue, no ETA for fix. Don't use.

---

## Open Questions

1. **Is `concurrency:` actually respected on the target Gitea instance?**
   - What we know: Official comparison docs list it as "currently ignored." A PR (#32751) merged it. Sources contradict.
   - What's unclear: Whether the merged PR made it into a stable release and what version the target Gitea runs.
   - Recommendation: Test a simple concurrency group at the start of implementation. If ignored, the bash branch/PR check is the fallback.

2. **Does the usage guard need an Admin API key, or is there a simpler approach?**
   - What we know: The Anthropic Usage API requires `sk-ant-admin...` key, which requires an org account. Individual accounts can't create admin keys.
   - What's unclear: Whether the project's Anthropic account is individual or organizational.
   - Recommendation: If admin key is unavailable, use a simpler token budget: accumulate token counts returned by `gfd-tools auto-research/plan` (if they report usage in AUTO-RUN.md) and hard-stop when the running total exceeds a configurable threshold.

3. **How does `workflow_call` handle the sequential loop use case?**
   - What we know: `workflow_call` with static `with:` values works. Dynamic matrix doesn't.
   - What's unclear: Whether a single orchestrator job with a bash loop (calling sub-workflow logic inline) or one `uses:` job per feature (with static slug) is more maintainable.
   - Recommendation: For `max_concurrent=1` (the default), run all logic in one orchestrator job with a bash loop. For future parallelism, one job-per-feature with hardcoded slugs becomes unwieldy — revisit when parallelism > 1 is needed.

4. **Is `tea` pre-installed on the `claude` runner host?**
   - What we know: The runner is self-hosted with Claude CLI pre-installed. tea is a separate install.
   - Recommendation: Add tea installation to runner setup documentation and/or add a setup step in the workflow that checks for tea and installs it if missing.

5. **What does `gfd-tools auto-research` / `gfd-tools auto-plan` return on success/failure?**
   - What we know: These commands are defined in `auto-skills` (not yet implemented). On abort, they commit an AUTO-RUN.md. On success, they commit artifacts + AUTO-RUN.md.
   - What's unclear: Exit code conventions (does failure return non-zero?).
   - Recommendation: Treat any non-zero exit as failure. Wrap in `if gfd-tools auto-research "$slug"; then ... else ... fi` to handle both success and failure paths.

---

## Sources

### Primary (HIGH confidence)
- https://docs.gitea.com/usage/actions/comparison — Gitea vs GitHub Actions feature gaps (concurrency ignored, dynamic expressions unsupported)
- https://docs.gitea.com/usage/actions/act-runner — Runner configuration, labels, host mode
- https://docs.gitea.com/usage/actions/secrets — Secret storage and access in workflows
- https://gitea.com/gitea/tea/src/branch/main/docs/CLI.md — tea CLI commands for PR and branch management
- https://gitea.com/actions/setup-dotnet — setup-dotnet action, .NET 10 support
- https://gitea.com/actions/gitea-upload-artifact — Gitea-native artifact upload action
- https://platform.claude.com/docs/en/api/usage-cost-api — Anthropic Usage API (admin key required, org-only)
- `/var/home/conroy/Projects/GFD/get-features-done/GfdTools/Commands/ListFeaturesCommand.cs` — `list-features --status` command implementation
- `/var/home/conroy/Projects/GFD/get-features-done/GfdTools/Program.cs` — gfd-tools command registry

### Secondary (MEDIUM confidence)
- https://github.com/go-gitea/gitea/issues/25179 — Dynamic matrix broken (open issue as of Feb 2025)
- https://github.com/go-gitea/gitea/issues/34472 — Schedule bug fixed in Gitea 1.24.0
- https://github.com/go-gitea/gitea/issues/24769 — concurrency PR #32751 merged (but comparison docs still say "ignored")
- https://forum.gitea.com/t/do-gitea-actions-support-user-inputs/8749 — workflow_dispatch inputs added in Gitea 1.23
- https://forum.gitea.com/t/reusable-workflows-usability/10155 — Reusable workflows work with caveats (runner pollution, cross-repo auth)

### Tertiary (LOW confidence)
- https://forum.gitea.com/t/simple-reusable-workflow-example-not-working/11960 — Cross-repo workflow_call auth issues (version 1.25.3); same-repo workflows should be unaffected
- WebSearch results on concurrency control — confirmed Gitea has `max-parallel` in matrix but dynamic matrix is broken

---

## Metadata

**Confidence breakdown:**
- Gitea Actions feature support: MEDIUM — Official docs verified, but some claims (concurrency) have contradictory sources; requires testing on target instance
- Feature discovery via gfd-tools: HIGH — Read from codebase directly; `list-features --status` is implemented
- tea CLI for PR ops: HIGH — Official CLI docs verified; specific flag syntax confirmed
- Anthropic Usage API: HIGH — Official docs fetched; admin key requirement confirmed
- Artifact upload: MEDIUM — ChristopherHX fork verified on gitea.com; tested patterns documented
- Parallelism approach (bash loop): HIGH — Consequence of confirmed broken dynamic matrix
- Auto-skills dependency: HIGH — FEATURE.md for auto-skills read directly; commands not yet implemented

**Research date:** 2026-02-20
**Valid until:** 2026-03-20 (Gitea Actions is still evolving; verify concurrency support on target instance)
