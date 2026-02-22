---
feature: auto-skills
plan: "01"
type: execute
wave: 1
depends_on: []
files_modified:
  - get-features-done/GfdTools/Services/ClaudeService.cs
autonomous: true
acceptance_criteria:
  - "Max-turns is configurable (with a sensible default) to prevent runaway token spend"
  - "Both commands abort cleanly on ambiguous decision points without making destructive choices"
  - "On abort, partial progress is discarded but an AUTO-RUN.md status file is committed explaining what happened"
  - "On success, normal artifacts are committed plus an AUTO-RUN.md summarizing the run (duration, what was produced)"

must_haves:
  truths:
    - "ClaudeService.InvokeHeadless() spawns claude with ArgumentList (no string concatenation) and captures stdout/stderr concurrently without deadlock"
    - "InvokeHeadless() accepts slug, prompt, maxTurns, model, and allowedTools parameters"
    - "RunResult carries: success bool, stdout string, stderr string, exitCode int, durationSeconds double, abortReason string"
    - "Success is determined by stdout containing a terminal signal string AND the expected artifact existing on disk — NOT exit code alone"
    - "Max-turns exit (non-zero exit with max-turns signal) is treated as abort, not hard failure"
    - "AUTO-RUN.md markdown content is assembled by ClaudeService.BuildAutoRunMd() for both success and abort cases"
  artifacts:
    - path: "get-features-done/GfdTools/Services/ClaudeService.cs"
      provides: "Headless claude subprocess invocation + RunResult + AUTO-RUN.md assembly"
      exports: ["ClaudeService", "RunResult"]
      contains: "InvokeHeadless"
  key_links:
    - from: "get-features-done/GfdTools/Services/ClaudeService.cs"
      to: "System.Diagnostics.Process"
      via: "ProcessStartInfo with ArgumentList.Add() for each arg"
      pattern: "ArgumentList\\.Add"
    - from: "get-features-done/GfdTools/Services/ClaudeService.cs"
      to: "concurrent stderr read"
      via: "Task.Run reading stderr while stdout read blocks"
      pattern: "Task\\.Run.*StandardError|StandardError.*Task\\.Run"
---

<objective>
Create ClaudeService — the subprocess invocation layer that all auto commands will use.

Purpose: Both auto-research and auto-plan need identical infrastructure for spawning claude headlessly: argument assembly, concurrent stdout/stderr capture, success/abort detection, and AUTO-RUN.md content generation. Building this as a shared service in Wave 1 lets Wave 2 commands stay thin.

Output: `get-features-done/GfdTools/Services/ClaudeService.cs` with `InvokeHeadless()`, `RunResult` record, and `BuildAutoRunMd()`.
</objective>

<execution_context>
@$HOME/.claude/get-features-done/templates/summary.md
</execution_context>

<context>
@docs/features/auto-skills/FEATURE.md
@docs/features/auto-skills/RESEARCH.md
@get-features-done/GfdTools/Services/GitService.cs
@get-features-done/GfdTools/Services/OutputService.cs
@get-features-done/GfdTools/GfdTools.csproj
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create ClaudeService with InvokeHeadless and RunResult</name>
  <files>get-features-done/GfdTools/Services/ClaudeService.cs</files>
  <action>
Create `get-features-done/GfdTools/Services/ClaudeService.cs` following the exact same pattern as `GitService.ExecGit()`.

**RunResult record** (at top of file, before the service class):
```csharp
public record RunResult(
    bool Success,
    string Stdout,
    string Stderr,
    int ExitCode,
    double DurationSeconds,
    string AbortReason   // empty string on success
);
```

**ClaudeService.InvokeHeadless()** signature:
```csharp
public static async Task<RunResult> InvokeHeadless(
    string cwd,
    string prompt,
    string[] allowedTools,
    int maxTurns = 30,
    string model = "sonnet"
)
```

Implementation requirements:

1. Record `startTime = DateTime.UtcNow` before spawning.

2. Build `ProcessStartInfo` for `claude` with `UseShellExecute = false`, `RedirectStandardInput = true`, `RedirectStandardOutput = true`, `RedirectStandardError = true`, `WorkingDirectory = cwd`.

3. Add args via `ArgumentList.Add()` ONLY — never string concatenation:
   - `ArgumentList.Add("-p")` — headless/pipe mode
   - `ArgumentList.Add("--max-turns")`, `ArgumentList.Add(maxTurns.ToString())`
   - `ArgumentList.Add("--model")`, `ArgumentList.Add(model)`
   - For each tool in `allowedTools`: `ArgumentList.Add("--allowedTools")`, `ArgumentList.Add(tool)`
   - `ArgumentList.Add("--output-format")`, `ArgumentList.Add("text")`

4. Start the process. Write prompt to stdin then close it:
   ```csharp
   process.StandardInput.Write(prompt);
   process.StandardInput.Close();
   ```

5. **CRITICAL — concurrent read to avoid deadlock**: Read stdout and stderr concurrently using `Task.Run`:
   ```csharp
   var stdoutTask = process.StandardOutput.ReadToEndAsync();
   var stderrTask = Task.Run(() => process.StandardError.ReadToEnd());
   await process.WaitForExitAsync();
   var stdout = await stdoutTask;
   var stderr = await stderrTask;
   ```

6. Compute `durationSeconds = (DateTime.UtcNow - startTime).TotalSeconds`.

7. **Success/abort detection** — check stdout for terminal signals, NOT exit code:
   - Success: stdout contains `## RESEARCH COMPLETE` OR `## PLANNING COMPLETE`
   - Abort signals (in order of priority check):
     - Max-turns: stderr contains `max turns` (case-insensitive) OR stdout contains `max-turns`
     - Ambiguous state / AskUserQuestion pattern: stdout contains `AskUserQuestion` OR stdout contains `## CHECKPOINT`
     - Generic failure: none of the above but no success signal found
   - Determine `abortReason` string:
     - `""` on success
     - `"max-turns reached"` for max-turns case
     - `"ambiguous decision point"` for checkpoint/AskUserQuestion case
     - `"no completion signal found"` for generic failure

8. Return `new RunResult(success, stdout, stderr, process.ExitCode, durationSeconds, abortReason)`.

9. Wrap the entire Process.Start block in try/catch; on exception return `new RunResult(false, "", ex.Message, 1, 0, "claude process failed to start")`.

**BuildAutoRunMd() method** (static, in same class):
```csharp
public static string BuildAutoRunMd(
    string slug,
    string command,       // "auto-research" or "auto-plan"
    RunResult result,
    string startedAt,     // ISO timestamp string
    string[] artifactsProduced  // e.g. ["RESEARCH.md"] or ["01-PLAN.md", "02-PLAN.md"]
)
```

Returns a markdown string:
```markdown
# Auto Run: {command} {slug}

**Status:** {Success or Aborted}
**Started:** {startedAt}
**Duration:** {result.DurationSeconds:F1}s

## Outcome

{if success: "Command completed successfully." + list of artifacts produced}
{if abort: "Command aborted. Reason: " + result.AbortReason}

## Artifacts

{list each artifact, or "None committed." if abort}

## Claude Output (tail)

```
{last 50 lines of result.Stdout, or "(none)" if empty}
```
```

Use `string.Join("\n", result.Stdout.Split('\n').TakeLast(50))` for the tail.
  </action>
  <verify>
Run from the GfdTools directory:
```bash
cd ./get-features-done/GfdTools && dotnet build 2>&1
```
Build must succeed with 0 errors. ClaudeService.cs must compile cleanly. Confirm `ClaudeService` and `RunResult` are recognized.
  </verify>
  <done>
`dotnet build` exits 0. `ClaudeService.cs` exists at `get-features-done/GfdTools/Services/ClaudeService.cs`. `InvokeHeadless` method signature matches spec. `BuildAutoRunMd` returns a markdown string. No deadlock risk (concurrent stderr read confirmed in code). No string concatenation in ArgumentList section.
  </done>
</task>

</tasks>

<verification>
- `dotnet build` exits 0 from the GfdTools directory
- `ClaudeService.cs` exists and is non-empty
- `InvokeHeadless` uses `ArgumentList.Add()` for every argument (grep confirms no string concatenation in argument assembly)
- Concurrent stderr read is present (grep for `Task.Run` + `StandardError`)
- `RunResult` record has all 6 fields: Success, Stdout, Stderr, ExitCode, DurationSeconds, AbortReason
- `BuildAutoRunMd` is present and returns a markdown string
</verification>

<success_criteria>
The service layer for headless claude invocation is complete and compiles. Wave 2 commands can import `ClaudeService` and `RunResult` without modification to this file.
</success_criteria>

<output>
After completion, create `docs/features/auto-skills/01-SUMMARY.md` following the summary template at `$HOME/.claude/get-features-done/templates/summary.md`.
</output>
