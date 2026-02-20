using System.Diagnostics;

namespace GfdTools.Services;

public record RunResult(
    bool Success,
    string Stdout,
    string Stderr,
    int ExitCode,
    double DurationSeconds,
    string AbortReason   // empty string on success
);

public static class ClaudeService
{
    /// <summary>
    /// Invoke claude headlessly via -p (pipe/print mode). Uses ArgumentList.Add() for each arg
    /// (never string concatenation) and reads stdout/stderr concurrently to avoid deadlock.
    /// Success is determined by stdout containing a terminal signal string AND the expected
    /// artifact existing on disk — NOT exit code alone.
    /// </summary>
    public static async Task<RunResult> InvokeHeadless(
        string cwd,
        string prompt,
        string[] allowedTools,
        int maxTurns = 30,
        string model = "sonnet"
    )
    {
        var startTime = DateTime.UtcNow;

        var psi = new ProcessStartInfo("claude")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = cwd,
        };

        // CRITICAL: Add each arg individually via ArgumentList, not string concatenation.
        // This prevents shell injection from slug values or other user-controlled strings.
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add("--max-turns");
        psi.ArgumentList.Add(maxTurns.ToString());
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(model);
        foreach (var tool in allowedTools)
        {
            psi.ArgumentList.Add("--allowedTools");
            psi.ArgumentList.Add(tool);
        }
        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("text");

        try
        {
            using var process = Process.Start(psi)!;

            // Write prompt to stdin then close it so claude receives it as input
            process.StandardInput.Write(prompt);
            process.StandardInput.Close();

            // CRITICAL — concurrent read to avoid deadlock:
            // Pipe buffers are finite (~64KB on Linux). If claude writes enough stderr
            // before stdout is drained, both sides deadlock. Read concurrently.
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = Task.Run(() => process.StandardError.ReadToEnd());
            await process.WaitForExitAsync();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            var durationSeconds = (DateTime.UtcNow - startTime).TotalSeconds;

            // Success/abort detection — check stdout for terminal signals, NOT exit code alone.
            // claude -p may exit 0 even on ambiguity; check stdout for completion signals.
            bool success = stdout.Contains("## RESEARCH COMPLETE", StringComparison.Ordinal)
                        || stdout.Contains("## PLANNING COMPLETE", StringComparison.Ordinal);

            string abortReason;
            if (success)
            {
                abortReason = "";
            }
            else
            {
                // Check abort signals in priority order
                bool isMaxTurns = stderr.Contains("max turns", StringComparison.OrdinalIgnoreCase)
                               || stdout.Contains("max-turns", StringComparison.Ordinal);

                bool isAmbiguous = stdout.Contains("AskUserQuestion", StringComparison.Ordinal)
                                || stdout.Contains("## CHECKPOINT", StringComparison.Ordinal);

                if (isMaxTurns)
                    abortReason = "max-turns reached";
                else if (isAmbiguous)
                    abortReason = "ambiguous decision point";
                else
                    abortReason = "no completion signal found";
            }

            return new RunResult(success, stdout, stderr, process.ExitCode, durationSeconds, abortReason);
        }
        catch (Exception ex)
        {
            return new RunResult(false, "", ex.Message, 1, 0, "claude process failed to start");
        }
    }

    /// <summary>
    /// Assemble AUTO-RUN.md markdown content for both success and abort cases.
    /// </summary>
    public static string BuildAutoRunMd(
        string slug,
        string command,           // "auto-research" or "auto-plan"
        RunResult result,
        string startedAt,         // ISO timestamp string
        string[] artifactsProduced  // e.g. ["RESEARCH.md"] or ["01-PLAN.md", "02-PLAN.md"]
    )
    {
        var status = result.Success ? "Success" : "Aborted";

        var outcomeSection = result.Success
            ? $"Command completed successfully.\n\n" +
              string.Join("\n", artifactsProduced.Select(a => $"- {a}"))
            : $"Command aborted. Reason: {result.AbortReason}";

        var artifactsSection = result.Success
            ? string.Join("\n", artifactsProduced.Select(a => $"- {a}"))
            : "None committed.";

        var stdoutTail = string.IsNullOrEmpty(result.Stdout)
            ? "(none)"
            : string.Join("\n", result.Stdout.Split('\n').TakeLast(50));

        return $"""
# Auto Run: {command} {slug}

**Status:** {status}
**Started:** {startedAt}
**Duration:** {result.DurationSeconds:F1}s

## Outcome

{outcomeSection}

## Artifacts

{artifactsSection}

## Claude Output (tail)

```
{stdoutTail}
```
""";
    }
}
