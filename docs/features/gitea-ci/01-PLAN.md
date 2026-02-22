---
feature: gitea-ci
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - .gitea/workflows/gfd-process-feature.yml
autonomous: true
acceptance_criteria:
  - "Sub-workflows run `gfd-tools auto-research` or `gfd-tools auto-plan` for each dispatched feature"
  - "Each feature's results are committed to a per-feature branch (`ci/<slug>`) with an auto-created PR"
  - "Workflow setup step installs .NET 10 and gfd-tools on the `claude` runner"
  - "Sub-workflow failures don't block other features — orchestrator continues with the next"
user_setup:
  - service: gitea
    why: "tea CLI needs a personal access token to authenticate for PR creation"
    env_vars:
      - name: GITEA_TOKEN
        source: "Gitea -> Settings -> Applications -> Generate Token (with repo permissions)"
    dashboard_config:
      - task: "Add GITEA_TOKEN secret to Gitea repo"
        location: "Gitea repo -> Settings -> Actions -> Secrets"
      - task: "Add ANTHROPIC_API_KEY secret to Gitea repo"
        location: "Gitea repo -> Settings -> Actions -> Secrets"
      - task: "Install tea CLI on claude runner host (one-time)"
        location: "Runner host: wget -O /usr/local/bin/tea https://dl.gitea.com/tea/0.10.1/tea-0.10.1-linux-amd64 && chmod +x /usr/local/bin/tea"

must_haves:
  truths:
    - "Sub-workflow accepts slug and type inputs via workflow_call"
    - "Sub-workflow checks out repo, installs .NET 10, and makes gfd-tools executable"
    - "Sub-workflow authenticates tea CLI using GITEA_TOKEN before any PR operations"
    - "Sub-workflow creates a ci/<slug> branch, runs auto-research or auto-plan, commits results, and pushes"
    - "Sub-workflow creates a PR via tea with correct title, base=main, head=ci/<slug>"
    - "Sub-workflow exit code signals success or failure to the orchestrator"
  artifacts:
    - path: ".gitea/workflows/gfd-process-feature.yml"
      provides: "Reusable sub-workflow triggered by workflow_call"
      contains: "workflow_call"
  key_links:
    - from: ".gitea/workflows/gfd-process-feature.yml"
      to: "gfd-tools auto-research / auto-plan"
      via: "bash step with ANTHROPIC_API_KEY env var"
      pattern: "auto-research|auto-plan"
    - from: ".gitea/workflows/gfd-process-feature.yml"
      to: "tea pulls create"
      via: "bash step after git push"
      pattern: "tea pulls create"
---

<objective>
Create the reusable sub-workflow that processes a single GFD feature: sets up the environment, runs the appropriate gfd-tools command, commits results to a per-feature branch, pushes, and creates a PR.

Purpose: This is the worker unit that does the actual research/planning work. The orchestrator dispatches it per feature. It must be a reusable workflow (workflow_call) so the orchestrator can invoke it.

Output: `.gitea/workflows/gfd-process-feature.yml`
</objective>

<execution_context>
@$HOME/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/gitea-ci/FEATURE.md
@docs/features/gitea-ci/RESEARCH.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create sub-workflow file with setup and environment</name>
  <files>.gitea/workflows/gfd-process-feature.yml</files>
  <action>
Create `.gitea/workflows/gfd-process-feature.yml`. This is a reusable workflow triggered by `workflow_call` with two required string inputs: `slug` and `type` (values: "research" or "plan").

The workflow has one job named `process` running on `runs-on: claude`.

Steps in order:

1. **Checkout:**
```yaml
- uses: actions/checkout@v4
  with:
    token: ${{ secrets.GITEA_TOKEN }}
```
The token ensures git push works without auth errors (configures credential helper automatically).

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

4. **Setup tea authentication:**
```yaml
- name: Setup tea auth
  run: |
    tea login add \
      --non-interactive \
      --url "${{ vars.GITEA_URL }}" \
      --token "${{ secrets.GITEA_TOKEN }}" \
      --name ci
```
This uses `vars.GITEA_URL` (a Gitea Actions variable, not secret) for the Gitea instance URL. GITEA_TOKEN is the secret with repo permissions.

5. **Configure git identity:**
```yaml
- name: Configure git
  run: |
    git config user.name "GFD CI"
    git config user.email "ci@gitea.local"
```

The full `on:` section must be:
```yaml
on:
  workflow_call:
    inputs:
      slug:
        required: true
        type: string
      type:
        required: true
        type: string
```
  </action>
  <verify>
YAML is valid: `python3 -c "import yaml; yaml.safe_load(open('.gitea/workflows/gfd-process-feature.yml'))"` exits 0.

File contains `workflow_call` trigger with `slug` and `type` inputs. File contains `actions/setup-dotnet@v5` step. File contains tea login step with `--non-interactive` flag.
  </verify>
  <done>
`.gitea/workflows/gfd-process-feature.yml` exists with valid YAML, workflow_call trigger, .NET 10 setup, gfd-tools chmod, tea auth setup, and git config steps.
  </done>
</task>

<task type="auto">
  <name>Task 2: Add feature processing steps — branch, run, commit, push, create PR</name>
  <files>.gitea/workflows/gfd-process-feature.yml</files>
  <action>
Append the remaining steps to the `process` job in `.gitea/workflows/gfd-process-feature.yml`:

6. **Create feature branch:**
```yaml
- name: Create feature branch
  run: git checkout -B "ci/${{ inputs.slug }}"
```

7. **Run GFD auto operation** (the core step):
```yaml
- name: Run GFD auto operation
  id: run_gfd
  env:
    ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
  run: |
    set -e
    if [ "${{ inputs.type }}" = "research" ]; then
      ./get-features-done/bin/gfd-tools auto-research "${{ inputs.slug }}"
    else
      ./get-features-done/bin/gfd-tools auto-plan "${{ inputs.slug }}"
    fi
```
The `set -e` ensures non-zero exit propagates as step failure. The `ANTHROPIC_API_KEY` env var makes the key available to Claude CLI invoked by gfd-tools.

8. **Commit results** (gfd-tools auto commands commit their own artifacts, but ensure any uncommitted changes are staged):
```yaml
- name: Commit results
  run: |
    git add -A
    git diff --cached --quiet || git commit -m "ci(${{ inputs.slug }}): auto-${{ inputs.type }} results"
```
The `--quiet` + `||` pattern means: only commit if there are staged changes. This avoids failing if gfd-tools already committed everything.

9. **Push branch:**
```yaml
- name: Push branch
  run: git push origin "ci/${{ inputs.slug }}"
```

10. **Create PR:**
```yaml
- name: Create PR
  run: |
    tea pulls create \
      --title "ci(${{ inputs.slug }}): auto-${{ inputs.type }}" \
      --base main \
      --head "ci/${{ inputs.slug }}" \
      --description "Automated ${{ inputs.type }} run for feature: ${{ inputs.slug }}"
```

The workflow does NOT use `continue-on-error` (not supported in Gitea). Instead, step failure causes the whole job to fail, which the orchestrator treats as a feature failure (logs and continues to next feature). This is correct behavior.
  </action>
  <verify>
YAML is valid: `python3 -c "import yaml; yaml.safe_load(open('.gitea/workflows/gfd-process-feature.yml'))"` exits 0.

File contains all 10 steps. File contains `ANTHROPIC_API_KEY` env reference. File contains `tea pulls create` with `--base main`. File contains `git push origin`. File contains `set -e` in the run_gfd step.
  </verify>
  <done>
Sub-workflow is complete: all steps present, YAML valid. The workflow takes `slug` + `type`, creates `ci/<slug>` branch, runs the appropriate gfd-tools command with ANTHROPIC_API_KEY, commits, pushes, and creates a PR via tea. Job failure signals to orchestrator that this feature failed.
  </done>
</task>

</tasks>

<verification>
- `python3 -c "import yaml; yaml.safe_load(open('.gitea/workflows/gfd-process-feature.yml'))"` exits 0
- Grep for `workflow_call` in the file confirms it's a reusable workflow
- Grep for `auto-research` and `auto-plan` confirms both branches of the conditional are present
- Grep for `tea pulls create` confirms PR creation step exists
- Grep for `ANTHROPIC_API_KEY` confirms env var is set for the run step
- Grep for `actions/setup-dotnet@v5` confirms .NET 10 installation
</verification>

<success_criteria>
`.gitea/workflows/gfd-process-feature.yml` exists with valid YAML. It is a reusable workflow (workflow_call) that: accepts slug + type inputs, installs .NET 10, authenticates tea, creates ci/<slug> branch, runs auto-research or auto-plan with ANTHROPIC_API_KEY, commits any remaining changes, pushes branch, and creates a PR.
</success_criteria>

<output>
After completion, create `docs/features/gitea-ci/01-SUMMARY.md` summarizing what was built, key decisions made, and the final structure of the sub-workflow file.
</output>
