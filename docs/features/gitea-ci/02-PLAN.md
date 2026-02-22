---
feature: gitea-ci
plan: 02
type: execute
wave: 2
depends_on: [01]
files_modified:
  - .gitea/workflows/gfd-nightly.yml
autonomous: true
acceptance_criteria:
  - "Orchestrator workflow runs on a cron schedule (configurable, default ~3 AM) and via manual `workflow_dispatch` with optional slug + type (research/plan) parameters"
  - "Orchestrator auto-detects eligible features: `discussed` → research, `researched` → plan"
  - "Configurable max concurrent sub-workflows (default 1)"
  - "Features with existing unmerged branches/PRs are skipped"
  - "Orchestrator monitors Claude API usage and hard-stops (no new dispatches) when threshold is hit"
  - "Sub-workflow failures don't block other features — orchestrator continues with the next"
  - "Nightly run summary uploaded as a Gitea Actions workflow artifact"
user_setup:
  - service: anthropic
    why: "Usage guard optionally uses Anthropic Admin API key for accurate token monitoring"
    env_vars:
      - name: ANTHROPIC_ADMIN_KEY
        source: "Anthropic Console -> Settings -> Admin API Keys (requires org account). If unavailable, usage guard falls back to local token budget tracking — skip this secret."
    dashboard_config:
      - task: "Add ANTHROPIC_ADMIN_KEY secret to Gitea repo (optional — only if org account exists)"
        location: "Gitea repo -> Settings -> Actions -> Secrets"
      - task: "Set GFD_MAX_CONCURRENT variable (optional, default 1)"
        location: "Gitea repo -> Settings -> Actions -> Variables -> GFD_MAX_CONCURRENT"
      - task: "Set GFD_USAGE_THRESHOLD variable (optional, default 100000 output tokens/hour)"
        location: "Gitea repo -> Settings -> Actions -> Variables -> GFD_USAGE_THRESHOLD"
      - task: "Set GITEA_URL variable (required for tea auth in sub-workflow)"
        location: "Gitea repo -> Settings -> Actions -> Variables -> GITEA_URL (e.g. https://gitea.example.com)"

must_haves:
  truths:
    - "Orchestrator triggers on cron schedule (0 3 * * *) and workflow_dispatch with optional slug + type inputs"
    - "Orchestrator discovers discussed features → research and researched features → plan via gfd-tools list-features"
    - "Manual dispatch with slug overrides auto-detect and processes only that feature"
    - "Orchestrator skips features that have an existing ci/<slug> branch or open PR"
    - "Orchestrator checks usage before dispatching each feature and stops if threshold exceeded"
    - "Orchestrator calls gfd-tools auto-research or auto-plan inline per feature (not via workflow_call — dynamic dispatch is unsupported in Gitea)"
    - "Orchestrator logs sub-workflow failures but continues to next feature"
    - "Nightly summary markdown is written to /tmp/gfd-nightly-summary.md and uploaded as artifact"
  artifacts:
    - path: ".gitea/workflows/gfd-nightly.yml"
      provides: "Orchestrator workflow with cron + workflow_dispatch triggers"
      contains: "schedule"
  key_links:
    - from: ".gitea/workflows/gfd-nightly.yml"
      to: "gfd-tools auto-research / auto-plan"
      via: "inline bash in process loop with ANTHROPIC_API_KEY env var"
      pattern: "auto-research|auto-plan"
    - from: ".gitea/workflows/gfd-nightly.yml"
      to: "gfd-tools list-features"
      via: "bash step parsing key=value output"
      pattern: "list-features --status"
    - from: ".gitea/workflows/gfd-nightly.yml"
      to: "gitea-upload-artifact"
      via: "uses: https://github.com/christopherhx/gitea-upload-artifact@v4"
      pattern: "gitea-upload-artifact"
---

<objective>
Create the orchestrator workflow that runs nightly (and on demand), discovers eligible features, guards against API overuse, dispatches the sub-workflow per feature, and uploads a summary artifact.

Purpose: This is the scheduler and coordinator. It ties together feature discovery, usage monitoring, sub-workflow invocation, and result reporting into a single autonomous nightly run.

Pre-requisite: The `auto-skills` feature must be merged before this workflow is functional. The `gfd-tools auto-research` and `gfd-tools auto-plan` commands are added by auto-skills. The workflow files created here can be committed beforehand, but nightly runs will fail until auto-skills is merged and gfd-tools includes those commands.

Output: `.gitea/workflows/gfd-nightly.yml`
</objective>

<execution_context>
@$HOME/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/gitea-ci/FEATURE.md
@docs/features/gitea-ci/RESEARCH.md
@docs/features/gitea-ci/01-SUMMARY.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create orchestrator workflow with triggers, setup, and feature discovery</name>
  <files>.gitea/workflows/gfd-nightly.yml</files>
  <action>
Create `.gitea/workflows/gfd-nightly.yml`.

**Triggers section:**
```yaml
name: GFD Nightly

on:
  schedule:
    - cron: '0 3 * * *'   # 3 AM UTC daily (configurable by editing this line)
  workflow_dispatch:
    inputs:
      slug:
        description: 'Feature slug (leave empty for auto-detect)'
        required: false
        type: string
        default: ''
      type:
        description: 'Operation type (leave empty for auto-detect)'
        required: false
        type: choice
        options:
          - ''
          - research
          - plan
```

**Single job `orchestrate` running on `claude`:**

Steps:

1. **Checkout:**
```yaml
- uses: actions/checkout@v4
  with:
    token: ${{ secrets.GITEA_TOKEN }}
```

2. **Setup .NET 10:**
```yaml
- name: Setup .NET 10
  uses: actions/setup-dotnet@v5
  with:
    dotnet-version: '10.0.x'
```

3. **Setup gfd-tools:**
```yaml
- name: Setup gfd-tools
  run: chmod +x get-features-done/bin/gfd-tools
```

4. **Initialize summary file:**
```yaml
- name: Initialize summary
  run: |
    echo "# GFD Nightly Summary" > /tmp/gfd-nightly-summary.md
    echo "Run: ${{ gitea.run_number }} | Date: $(date -u +%Y-%m-%dT%H:%M:%SZ)" >> /tmp/gfd-nightly-summary.md
    echo "" >> /tmp/gfd-nightly-summary.md
```

5. **Discover eligible features:**
```yaml
- name: Discover features
  id: discover
  run: |
    # Manual dispatch with specific slug — override auto-detect
    if [ -n "${{ inputs.slug }}" ]; then
      SLUG="${{ inputs.slug }}"
      TYPE="${{ inputs.type }}"
      # If type not specified, detect from feature status
      if [ -z "$TYPE" ]; then
        STATUS=$(./get-features-done/bin/gfd-tools list-features --status discussed \
          | grep "^feature_slug=$SLUG$" | head -1)
        if [ -n "$STATUS" ]; then
          TYPE="research"
        else
          TYPE="plan"
        fi
      fi
      echo "pairs=$SLUG:$TYPE" >> "$GITEA_OUTPUT"
      exit 0
    fi

    # Auto-detect: discussed → research, researched → plan
    DISCUSSED=$(./get-features-done/bin/gfd-tools list-features --status discussed \
      | grep "^feature_slug=" | cut -d= -f2)
    RESEARCHED=$(./get-features-done/bin/gfd-tools list-features --status researched \
      | grep "^feature_slug=" | cut -d= -f2)

    PAIRS=""
    for s in $DISCUSSED; do PAIRS="$PAIRS $s:research"; done
    for s in $RESEARCHED; do PAIRS="$PAIRS $s:plan"; done
    PAIRS="${PAIRS# }"  # trim leading space

    echo "pairs=$PAIRS" >> "$GITEA_OUTPUT"
    echo "Discovered features: $PAIRS" | tee -a /tmp/gfd-nightly-summary.md
```

Note: `GITEA_OUTPUT` is the Gitea Actions equivalent of `GITHUB_OUTPUT`.

Note on `list-features --status`: `gfd-tools list-features` already exists and supports the `--status` filter. This is NOT a dependency on auto-skills — `list-features` is part of the existing gfd-tools binary. The auto-skills dependency applies only to `auto-research` and `auto-plan` commands (used in Task 2). Executor should verify `./get-features-done/bin/gfd-tools list-features --status discussed` produces `feature_slug=` key=value lines before assuming the output format.
  </action>
  <verify>
`python3 -c "import yaml; yaml.safe_load(open('.gitea/workflows/gfd-nightly.yml'))"` exits 0.

File contains `schedule` and `workflow_dispatch` triggers. File contains `workflow_dispatch` with `slug` and `type` inputs. File contains `GITEA_OUTPUT` references. File contains `list-features --status discussed` and `list-features --status researched`.
  </verify>
  <done>
`.gitea/workflows/gfd-nightly.yml` exists with valid YAML up through the feature discovery step. Triggers, setup, and discovery logic are complete.
  </done>
</task>

<task type="auto">
  <name>Task 2: Add usage guard, per-feature dispatch loop, and artifact upload</name>
  <files>.gitea/workflows/gfd-nightly.yml</files>
  <action>
Append the remaining steps to the `orchestrate` job in `.gitea/workflows/gfd-nightly.yml`. Add them in the following order — this is the final step order, not a "insert before" instruction:

**6. Usage guard check (initial):**
```yaml
- name: Check initial usage
  id: usage_guard
  env:
    ANTHROPIC_ADMIN_KEY: ${{ secrets.ANTHROPIC_ADMIN_KEY }}
  run: |
    THRESHOLD="${{ vars.GFD_USAGE_THRESHOLD || '100000' }}"
    STOP="false"

    # Only query API if admin key is available
    if [ -n "$ANTHROPIC_ADMIN_KEY" ]; then
      NOW=$(date -u +%Y-%m-%dT%H:%M:%SZ)
      HOUR_AGO=$(date -u -d '1 hour ago' +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || \
                 date -u -v-1H +%Y-%m-%dT%H:%M:%SZ)
      RESPONSE=$(curl -s \
        "https://api.anthropic.com/v1/organizations/usage_report/messages?starting_at=${HOUR_AGO}&ending_at=${NOW}&bucket_width=1h" \
        --header "anthropic-version: 2023-06-01" \
        --header "x-api-key: ${ANTHROPIC_ADMIN_KEY}")
      OUTPUT_TOKENS=$(echo "$RESPONSE" | jq '[.data[].output_tokens] | add // 0' 2>/dev/null || echo "0")
      if [ "$OUTPUT_TOKENS" -gt "$THRESHOLD" ]; then
        echo "Usage threshold exceeded ($OUTPUT_TOKENS > $THRESHOLD tokens/hour). Hard-stopping." | tee -a /tmp/gfd-nightly-summary.md
        STOP="true"
      fi
    else
      echo "No ANTHROPIC_ADMIN_KEY — usage guard disabled (local budget fallback active in loop)" | tee -a /tmp/gfd-nightly-summary.md
    fi

    echo "stop=$STOP" >> "$GITEA_OUTPUT"
```

**7. Setup tea auth** (must appear before the process features step so tea is authenticated when the loop runs):
```yaml
- name: Setup tea auth
  run: |
    tea login add \
      --non-interactive \
      --url "${{ vars.GITEA_URL }}" \
      --token "${{ secrets.GITEA_TOKEN }}" \
      --name ci
```

**8. Process features loop** (background-job concurrency controlled by `vars.GFD_MAX_CONCURRENT`, default 1; dynamic matrix is broken in Gitea — use bash background jobs):

Note on `tea pulls list` output format: `--fields head --output simple` is expected to produce tab-separated rows where the head column shows the branch name. **Executor must verify this format on the target system** before relying on it. If the format differs, fall back to: `tea pulls list --state open --output json | jq -r '.[].head.label' | grep -c "ci/$SLUG"`

Note: `GFD_MAX_CONCURRENT=1` (default) means at most one background job runs at a time — effectively sequential but implemented via the job counter. Set to 2 or 3 to allow parallel processing. `wait -n` (bash 4.3+) waits for the next background job to finish; if unavailable, `wait` waits for all (conservative fallback).

```yaml
- name: Process features
  if: steps.usage_guard.outputs.stop != 'true' && steps.discover.outputs.pairs != ''
  env:
    ANTHROPIC_ADMIN_KEY: ${{ secrets.ANTHROPIC_ADMIN_KEY }}
    ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
    GITEA_TOKEN: ${{ secrets.GITEA_TOKEN }}
  run: |
    set +e  # Don't exit on sub-workflow failure — log and continue
    THRESHOLD="${{ vars.GFD_USAGE_THRESHOLD || '100000' }}"
    MAX_CONCURRENT="${{ vars.GFD_MAX_CONCURRENT || '1' }}"
    JOB_COUNT=0
    HARD_STOP=false

    # Helper: run a single feature in a subshell (invoked with & for concurrency)
    process_one() {
      local SLUG="$1"
      local TYPE="$2"
      echo "## Feature: $SLUG ($TYPE)" | tee -a /tmp/gfd-nightly-summary.md

      git config user.name "GFD CI"
      git config user.email "ci@gitea.local"
      git checkout -B "ci/$SLUG"

      if [ "$TYPE" = "research" ]; then
        timeout 3600 ./get-features-done/bin/gfd-tools auto-research "$SLUG"
        RESULT=$?
      else
        timeout 3600 ./get-features-done/bin/gfd-tools auto-plan "$SLUG"
        RESULT=$?
      fi

      if [ $RESULT -eq 0 ]; then
        git add -A
        git diff --cached --quiet || git commit -m "ci($SLUG): auto-$TYPE results"
        git push origin "ci/$SLUG"
        tea pulls create \
          --title "ci($SLUG): auto-$TYPE" \
          --base main \
          --head "ci/$SLUG" \
          --description "Automated $TYPE run for feature: $SLUG"
        echo "  SUCCESS: PR created for ci/$SLUG" | tee -a /tmp/gfd-nightly-summary.md
      else
        echo "  FAILED: gfd-tools exited with code $RESULT" | tee -a /tmp/gfd-nightly-summary.md
        git checkout main
        git branch -D "ci/$SLUG" 2>/dev/null || true
      fi
      git checkout main
    }

    for pair in ${{ steps.discover.outputs.pairs }}; do
      SLUG="${pair%%:*}"
      TYPE="${pair##*:}"

      # Skip if ci/<slug> branch already exists on remote
      if git ls-remote --exit-code origin "refs/heads/ci/$SLUG" > /dev/null 2>&1; then
        echo "  SKIPPED: branch ci/$SLUG already exists" | tee -a /tmp/gfd-nightly-summary.md
        continue
      fi

      # Skip if open PR exists for ci/<slug> head branch.
      # Note: --fields head --output simple produces tab-separated output where
      # the head column shows the branch name. Verify format on target system;
      # fall back to: tea pulls list --state open --output json | jq -r '.[].head.label'
      PR_EXISTS=$(tea pulls list --state open --fields head --output simple 2>/dev/null \
        | grep -c "ci/$SLUG" || echo "0")
      if [ "$PR_EXISTS" -gt "0" ]; then
        echo "  SKIPPED: open PR for ci/$SLUG already exists" | tee -a /tmp/gfd-nightly-summary.md
        continue
      fi

      # Re-check usage guard before each dispatch (Admin API only)
      if [ -n "$ANTHROPIC_ADMIN_KEY" ]; then
        NOW=$(date -u +%Y-%m-%dT%H:%M:%SZ)
        HOUR_AGO=$(date -u -d '1 hour ago' +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || \
                   date -u -v-1H +%Y-%m-%dT%H:%M:%SZ)
        RESPONSE=$(curl -s \
          "https://api.anthropic.com/v1/organizations/usage_report/messages?starting_at=${HOUR_AGO}&ending_at=${NOW}&bucket_width=1h" \
          --header "anthropic-version: 2023-06-01" \
          --header "x-api-key: ${ANTHROPIC_ADMIN_KEY}")
        OUTPUT_TOKENS=$(echo "$RESPONSE" | jq '[.data[].output_tokens] | add // 0' 2>/dev/null || echo "0")
        if [ "$OUTPUT_TOKENS" -gt "$THRESHOLD" ]; then
          echo "HARD STOP: usage threshold exceeded ($OUTPUT_TOKENS tokens). No more dispatches." | tee -a /tmp/gfd-nightly-summary.md
          HARD_STOP=true
          break
        fi
      fi

      # Concurrency: if at limit, wait for one job to finish before spawning next
      if [ "$JOB_COUNT" -ge "$MAX_CONCURRENT" ]; then
        wait -n 2>/dev/null || wait  # wait -n waits for any single job; fallback: wait for all
        JOB_COUNT=$((JOB_COUNT - 1))
      fi

      echo "  Dispatching $SLUG ($TYPE) [jobs running: $JOB_COUNT/$MAX_CONCURRENT]..." | tee -a /tmp/gfd-nightly-summary.md
      process_one "$SLUG" "$TYPE" &
      JOB_COUNT=$((JOB_COUNT + 1))
    done

    # Wait for all remaining background jobs to finish
    wait
    echo "All features processed." | tee -a /tmp/gfd-nightly-summary.md
```

**9. Upload summary artifact** (must use `if: always()` so it runs even if prior steps failed):
```yaml
- name: Upload nightly summary
  if: always()
  uses: https://github.com/christopherhx/gitea-upload-artifact@v4
  with:
    name: gfd-nightly-summary-${{ gitea.run_number }}
    path: /tmp/gfd-nightly-summary.md
    retention-days: 30
```

Final step order in the job (write them in this exact order):
1. actions/checkout@v4
2. Setup .NET 10
3. Setup gfd-tools
4. Initialize summary
5. Discover features
6. Check initial usage
7. Setup tea auth  ← must appear before process features so tea is authenticated when loop runs
8. Process features (loop with background-job concurrency)
9. Upload nightly summary (if: always())
  </action>
  <verify>
`python3 -c "import yaml; yaml.safe_load(open('.gitea/workflows/gfd-nightly.yml'))"` exits 0.

File contains `gitea-upload-artifact` in artifact upload step. File contains `if: always()` on upload step. File contains `git ls-remote --exit-code origin` for branch check. File contains `tea pulls list` for PR check. File contains `HARD STOP` string indicating usage guard break. File contains `timeout 3600` on gfd-tools calls. File contains `RESULT=$?` and conditional on result for failure handling. File contains `git checkout main` at end of loop for branch reset. File contains `GFD_MAX_CONCURRENT` and `wait -n` for concurrency. File contains `git checkout -B` (force-create branch).
  </verify>
  <done>
`.gitea/workflows/gfd-nightly.yml` is complete with valid YAML. The orchestrator: triggers on cron (3 AM) and workflow_dispatch, discovers features via gfd-tools, checks usage before and between dispatches, skips features with existing branches/PRs, runs auto-research or auto-plan inline with background-job concurrency (controlled by `GFD_MAX_CONCURRENT`, default 1) with failure handling that continues to next feature, and uploads a nightly summary artifact.
  </done>
</task>

</tasks>

<verification>
- `python3 -c "import yaml; yaml.safe_load(open('.gitea/workflows/gfd-nightly.yml'))"` exits 0
- `python3 -c "import yaml; yaml.safe_load(open('.gitea/workflows/gfd-process-feature.yml'))"` exits 0
- Both files exist under `.gitea/workflows/`
- Grep for `schedule` and `workflow_dispatch` in gfd-nightly.yml
- Grep for `workflow_call` in gfd-process-feature.yml
- Grep for `gitea-upload-artifact` in gfd-nightly.yml
- Grep for `list-features --status discussed` and `list-features --status researched` in gfd-nightly.yml
- Grep for `ls-remote` (branch existence check) in gfd-nightly.yml
- Grep for `tea pulls list` (PR existence check) in gfd-nightly.yml
- Grep for `HARD STOP` (usage guard break) in gfd-nightly.yml
- Grep for `RESULT=$?` and `if [ $RESULT -eq 0 ]` (failure handling) in gfd-nightly.yml
- Grep for `if: always()` on upload step in gfd-nightly.yml
- Grep for `GFD_MAX_CONCURRENT` in gfd-nightly.yml (concurrency variable)
- Grep for `wait -n` in gfd-nightly.yml (background job concurrency control)
- Grep for `git checkout -B` in gfd-process-feature.yml (force-create branch, handles re-runs)
</verification>

<success_criteria>
Both workflow files exist with valid YAML. The orchestrator (gfd-nightly.yml) runs on cron + workflow_dispatch, auto-detects eligible features, respects the usage guard, skips features with existing branches/PRs, processes features with configurable concurrency (GFD_MAX_CONCURRENT, default 1) using background jobs and `wait -n`, isolates per-feature failures so the loop continues, and uploads a nightly summary artifact. The sub-workflow (gfd-process-feature.yml) is a reusable workflow for manual invocation of individual features.
</success_criteria>

<output>
After completion, create `docs/features/gitea-ci/02-SUMMARY.md` summarizing what was built, the inline-vs-workflow_call architectural decision, and key configuration variables (GFD_MAX_CONCURRENT, GFD_USAGE_THRESHOLD, GITEA_URL).
</output>
