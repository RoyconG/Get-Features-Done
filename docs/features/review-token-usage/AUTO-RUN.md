# Auto Run: auto-research review-token-usage

**Status:** Success
**Started:** 2026-02-22T07:46:13Z
**Duration:** 314.4s

## Outcome

Command completed successfully.

- RESEARCH.md

## Artifacts

- RESEARCH.md

## Claude Output (tail)

```
## RESEARCH COMPLETE

The research for the **Review Token Usage** feature is complete. Here's a summary of key findings:

**File created:** `docs/features/review-token-usage/RESEARCH.md`

### Key Findings

1. **Token data IS available from headless runs** — The SDK's `SDKResultMessage` type includes `total_cost_usd`, `usage` (input/output/cache tokens), and `modelUsage` (per-model breakdown). The current `ClaudeService.InvokeHeadless()` uses `--output-format text` which discards this data; switching to `--output-format stream-json` captures it.

2. **Per-role model overrides need minimal config schema extension** — The existing `ConfigService.cs` supports profiles (`quality`/`balanced`/`budget`). Adding a `model_overrides` map to `config.json` and updating `ResolveModel()` is the minimal change.

3. **Interactive Task() token data is unreliable** — The `TaskOutput.usage` field is optional in the SDK TypeScript reference. Token reporting should be "exact for headless runs, best-effort for interactive runs."

4. **`/gfd:configure-models` follows an established pattern** — Needs a command file (`commands/gfd/configure-models.md`) and workflow file. Config can be written directly via the Write tool — no new CLI command needed.

5. **Recommended model defaults by role:** mapper=haiku, verifier=haiku, researcher=sonnet, planner=sonnet, executor=sonnet. The audit finding is that the `quality` profile (all opus) is overkill for mapper and verifier roles.

### Confidence: HIGH

```