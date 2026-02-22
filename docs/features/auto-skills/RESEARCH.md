# Feature: Auto Skills — Research

**Researched:** 2026-02-20
**Domain:** C# CLI subprocess invocation, claude -p headless mode, git commit lifecycle
**Confidence:** HIGH

## Summary

`auto-research` and `auto-plan` are new subcommands added to the gfd-tools C# CLI. Each command assembles the appropriate workflow prompt (the same prompt content already used by the interactive slash commands), spawns `claude -p` as a child process with `--allowedTools`, `--max-turns`, and `--model`, waits for it to complete, inspects the output for success vs. abort signals, and then commits either the full artifact set (on success) or only an AUTO-RUN.md status file (on abort/failure).

The key insight: the existing workflows (`research-feature.md`, `plan-feature.md`) already define the exact prompts and tool lists needed. `auto-research` and `auto-plan` are thin orchestration wrappers that drive those same workflows headlessly rather than interactively. No new agent logic is required — only new C# CLI commands that invoke `claude -p` the way `GitService.ExecGit()` invokes `git`.

**Primary recommendation:** Model the new commands on `GitService.ExecGit()` for subprocess invocation. Assemble the workflow prompt from the existing workflow markdown files (or an equivalent inline string). Parse the subprocess stdout to detect `## RESEARCH COMPLETE`, `## PLANNING COMPLETE`, or abort signals. Commit accordingly.

## User Constraints (from FEATURE.md)

### Locked Decisions
- **Interaction handling:** Abort on ambiguity — never silently choose for the user
- **Abort communication:** Commit an AUTO-RUN.md status file + descriptive commit message
- **Structure:** Standalone commands in gfd-tools C# CLI (`auto-research`, `auto-plan`)
- **Invocation:** gfd-tools handles the full `claude -p` invocation internally
- **Failed plans:** Discard — only AUTO-RUN.md committed on failure
- **Cost guard:** Configurable `--max-turns` with sensible default

### Out of Scope
- No `AskUserQuestion` calls — all interaction stripped
- No silent decision-making on ambiguous state

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.CommandLine | 2.0.0-beta5.25306.1 | CLI argument parsing | Already in GfdTools.csproj — all existing commands use it |
| System.Diagnostics.Process | .NET 10 built-in | Spawn `claude -p` subprocess | Same pattern as GitService.ExecGit() — no new dependency needed |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.IO (StreamReader) | .NET 10 built-in | Capture stdout from claude subprocess | Use async reads to avoid deadlock on large outputs |
| System.Text (StringBuilder) | .NET 10 built-in | Accumulate subprocess output | Buffer stdout while process runs |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Process.Start direct | CliWrap NuGet | CliWrap is cleaner but adds a dependency; Process.Start follows existing GitService pattern |
| Inline prompt strings | Reading workflow .md files at runtime | Reading files adds I/O and path dependency; inline strings are more predictable for headless commands |

**Installation:** No new packages required.

---

## Architecture Patterns

### Recommended Project Structure
```
GfdTools/
├── Commands/
│   ├── AutoResearchCommand.cs    # new: auto-research <slug> [--max-turns N]
│   └── AutoPlanCommand.cs        # new: auto-plan <slug> [--max-turns N]
├── Services/
│   └── ClaudeService.cs          # new: InvokeHeadless(...) → RunResult
└── Program.cs                    # register AutoResearchCommand, AutoPlanCommand
```

### Pattern 1: Command Factory (matches all existing commands)
**What:** Each command is a static class with a `Create(string cwd)` method returning `Command`.
**When to use:** Always — every command in the project follows this pattern.
**Example:**
```csharp
// Source: existing GfdTools/Commands/FeatureUpdateStatusCommand.cs
public static class AutoResearchCommand
{
    public static Command Create(string cwd)
    {
        var cmd = new Command("auto-research") { Description = "Run research workflow headlessly" };
        var slugArg = new Argument<string>("slug") { Description = "Feature slug" };
        var maxTurnsOpt = new Option<int>("--max-turns", () => 40, "Max agentic turns (default: 40)");
        cmd.Add(slugArg);
        cmd.Add(maxTurnsOpt);

        cmd.SetAction(pr =>
        {
            var slug = pr.GetValue(slugArg)!;
            var maxTurns = pr.GetValue(maxTurnsOpt);
            // ... orchestration logic
            return 0;
        });

        return cmd;
    }
}
```

### Pattern 2: Subprocess Invocation (matches GitService.ExecGit)
**What:** Use `ProcessStartInfo` with `ArgumentList` (not string concatenation) and redirect stdout/stderr.
**When to use:** Every `claude -p` invocation.
**Example:**
```csharp
// Source: derived from GfdTools/Services/GitService.cs ExecGit pattern
public record ClaudeResult(int ExitCode, string Stdout, string Stderr);

public static ClaudeResult InvokeHeadless(string cwd, string prompt, string[] allowedTools, string model, int maxTurns)
{
    var psi = new ProcessStartInfo("claude")
    {
        WorkingDirectory = cwd,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };

    psi.ArgumentList.Add("-p");
    psi.ArgumentList.Add(prompt);
    psi.ArgumentList.Add("--allowedTools");
    psi.ArgumentList.Add(string.Join(",", allowedTools));
    psi.ArgumentList.Add("--model");
    psi.ArgumentList.Add(model);
    psi.ArgumentList.Add("--max-turns");
    psi.ArgumentList.Add(maxTurns.ToString());
    psi.ArgumentList.Add("--output-format");
    psi.ArgumentList.Add("text");

    try
    {
        using var process = Process.Start(psi)!;
        // IMPORTANT: Read stdout and stderr concurrently to avoid deadlock
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new ClaudeResult(process.ExitCode, stdoutTask.Result.TrimEnd(), stderrTask.Result.TrimEnd());
    }
    catch (Exception ex)
    {
        return new ClaudeResult(1, string.Empty, $"claude not available: {ex.Message}");
    }
}
```

### Pattern 3: Success/Abort Detection by Stdout Scanning
**What:** Check subprocess stdout for terminal signal strings to determine outcome.
**When to use:** After every `claude -p` invocation.
**Example:**
```csharp
// Signals emitted by the existing gfd-researcher agent
bool success = result.Stdout.Contains("## RESEARCH COMPLETE", StringComparison.Ordinal)
            || result.Stdout.Contains("## PLANNING COMPLETE", StringComparison.Ordinal);

bool aborted = result.ExitCode != 0
            || result.Stdout.Contains("## RESEARCH BLOCKED", StringComparison.Ordinal)
            || result.Stdout.Contains("## PLANNING INCONCLUSIVE", StringComparison.Ordinal)
            || result.Stdout.Contains("AskUserQuestion", StringComparison.Ordinal);
// Max-turns hit: claude -p exits with non-zero exit code when --max-turns is reached
```

### Pattern 4: AUTO-RUN.md as Status Artifact
**What:** Write a markdown file summarizing the run outcome. Commit it regardless of success/failure.
**When to use:** End of every auto-research/auto-plan run.
**On success:**
```markdown
# AUTO-RUN: auto-research — [slug]

**Outcome:** SUCCESS
**Started:** [ISO timestamp]
**Completed:** [ISO timestamp]
**Duration:** [N]s
**Max turns:** [N] (limit: [N])

## What was produced
- docs/features/[slug]/RESEARCH.md

## Run output (tail)
[last 20 lines of claude stdout]
```
**On abort:**
```markdown
# AUTO-RUN: auto-research — [slug]

**Outcome:** ABORTED
**Reason:** [why it aborted — max-turns, blocked signal, ambiguous state, non-zero exit]
**Started:** [ISO timestamp]
**Aborted:** [ISO timestamp]

## What was NOT produced
- RESEARCH.md was not written (partial work discarded)

## Run output (tail)
[last 20 lines of claude stdout for diagnosis]
```

### Pattern 5: Ambiguous State Detection (abort before spawning)
**What:** Pre-flight checks using existing `gfd-tools init plan-feature` / `find-feature` output. If state is ambiguous, write AUTO-RUN.md and abort without calling claude.
**Abort conditions for auto-research:**
- Feature not found
- Feature status is not `discussed` or `researching`
- RESEARCH.md already exists (feature is already `researched` or beyond)
- Feature is `done`

**Abort conditions for auto-plan:**
- Feature not found
- Feature status is not `researched` or `planning`
- Plans already exist (has_plans = true)
- Feature is `done`

**Why abort vs. proceed:** The interactive workflows handle these cases with `AskUserQuestion`. Without user interaction, any of these states represents an ambiguous decision point — the system cannot know the user's intent. Abort is safe; silent overwrite is not.

### Anti-Patterns to Avoid
- **String concatenation for process args:** Use `ArgumentList.Add()` always. GitService has a comment explicitly calling this out. Shell injection risk from slug values.
- **Synchronous stdout read without concurrent stderr read:** Deadlocks when the process fills its stderr buffer while the parent is blocking on stdout. Always use two tasks reading concurrently.
- **Checking only exit code for success:** `claude -p` may exit 0 even if it hit a wall; check stdout for terminal signal strings. Also check that the expected artifact file actually exists on disk.
- **Writing partial artifacts on abort:** If RESEARCH.md is partially written by claude before it aborts, discard it. Only AUTO-RUN.md is committed on abort.
- **Long timeout without max-turns:** Without `--max-turns`, a runaway claude session can run indefinitely and cost unbounded tokens. Always pass `--max-turns`.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Subprocess spawning | Custom pipe/fork logic | `System.Diagnostics.Process` | Already in .NET 10, same as GitService |
| CLI arg parsing | Manual argv parsing | `System.CommandLine` (already in project) | Handles `--option`, `Argument`, validation |
| Feature state reads | Re-implement frontmatter parsing | `FeatureService.FindFeature(cwd, slug)` | Already correct, battle-tested |
| Status transitions | Direct file writes | `gfd-tools feature-update-status` call | Centralized validation + Output contract |
| Timestamps | Custom datetime formatting | `DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")` | Already used in current-timestamp command |

**Key insight:** The hard parts (frontmatter parsing, git operations, status transitions, config loading) are all already implemented correctly in existing services. The new commands are thin orchestrators that compose existing services plus one new `ClaudeService.InvokeHeadless()` method.

---

## Common Pitfalls

### Pitfall 1: Deadlock on Large Claude Output
**What goes wrong:** `process.StandardOutput.ReadToEnd()` blocks while the process tries to write stderr; both sides deadlock.
**Why it happens:** Pipe buffers are finite (~64KB on Linux). If claude writes enough stderr before stdout is drained, the process hangs.
**How to avoid:** Always start two `Task<string>` — one for stdout and one for stderr — before calling `WaitForExit()`. Use `await Task.WhenAll(stdoutTask, stderrTask)` or `.Result` after both are started.
**Warning signs:** Process never exits, timeout required to recover.

### Pitfall 2: Exit Code ≠ Success
**What goes wrong:** Trusting `ExitCode == 0` as the only success signal. `claude -p` exits 0 even if it hit ambiguity or produced no artifact.
**Why it happens:** Claude writes its signal string (`## RESEARCH COMPLETE`) to stdout and then exits 0. But if it decides to ask the user a question instead, it also exits 0 with different stdout content.
**How to avoid:** Check stdout for the specific terminal signal AND verify the artifact file exists on disk.
**Warning signs:** AUTO-RUN.md says success but RESEARCH.md is absent.

### Pitfall 3: Max-Turns Exit is Non-Zero
**What goes wrong:** When `--max-turns` is exhausted, `claude -p` exits with a non-zero exit code. If the code only checks stdout for abort signals, this case is missed.
**Why it happens:** Documented behavior: "Exits with an error when the limit is reached" (official CLI reference).
**How to avoid:** Treat any non-zero exit code as abort, regardless of stdout content. Log the exit code in AUTO-RUN.md.
**Warning signs:** Claude appears to still be working when the run terminates unexpectedly.

### Pitfall 4: Prompt Assembly Missing Critical Context
**What goes wrong:** The headless prompt omits codebase docs or FEATURE.md content that the interactive workflow includes, causing the research/planning output to be poor.
**Why it happens:** The interactive workflow reads these files at runtime with shell commands and inlines them. The headless command must do the same file-reading in C# and inline them into the prompt string.
**How to avoid:** Read `docs/features/<slug>/FEATURE.md` and `docs/features/codebase/*.md` (up to N files) in C# and embed their content in the prompt string before passing to `claude -p`. Mirror exactly what the workflow does.
**Warning signs:** RESEARCH.md produced but doesn't mention codebase patterns.

### Pitfall 5: Feature Status Not Reverted on Abort
**What goes wrong:** `auto-research` updates status to `researching` before spawning claude, then aborts without reverting — leaving the feature in an intermediate state.
**Why it happens:** Happy-path code updates status; abort path forgets to revert.
**How to avoid:** Update status to the in-progress state (`researching`/`planning`) only if you will also revert on abort. On abort, call `feature-update-status` to revert to the pre-run status before committing AUTO-RUN.md.
**Warning signs:** Feature stuck in `researching` status indefinitely.

### Pitfall 6: AUTO-RUN.md Committed Over Itself
**What goes wrong:** Second failed run overwrites the first AUTO-RUN.md, losing diagnostic history.
**Why it happens:** File always written to the same path.
**How to avoid:** Acceptable — AUTO-RUN.md is a "last run" status file, not a log. One file per feature is correct. Document this in the file header.

---

## Code Examples

Verified patterns from official sources and existing codebase:

### Assembling the Research Prompt (inline, not reading workflow file)
```csharp
// Source: derived from GfdTools/Services logic + plan-feature.md workflow
private static string BuildResearchPrompt(string slug, string featureName, string featureContent, string codebaseDocs)
{
    var agentInstructions = File.ReadAllText(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude", "agents", "gfd-researcher.md"));

    return $"""
        {agentInstructions}

        <objective>
        Research how to implement feature: {featureName} ({slug})
        Answer: "What do I need to know to PLAN this feature well?"
        </objective>

        <feature_context>
        **Slug:** {slug}
        **Name:** {featureName}

        **Feature Definition:**
        {featureContent}
        </feature_context>

        <project_context>
        **Codebase docs:** {codebaseDocs}
        </project_context>

        <downstream_consumer>
        Your RESEARCH.md feeds into the gfd-planner. Be prescriptive:
        - Specific approaches with rationale
        - What NOT to do and why
        - Patterns to follow from existing codebase
        - Dependencies and integration points
        - Potential gotchas or edge cases
        </downstream_consumer>

        <output>
        Write to: docs/features/{slug}/RESEARCH.md
        Return ## RESEARCH COMPLETE with brief summary when done.
        DO NOT call AskUserQuestion — this is an automated run. If you encounter ambiguity, write ## RESEARCH BLOCKED explaining why and stop.
        </output>
        """;
}
```

### Registering Commands in Program.cs
```csharp
// Source: existing GfdTools/Program.cs pattern
// ─── auto-research ────────────────────────────────────────────────────────────
rootCommand.Add(AutoResearchCommand.Create(cwd));

// ─── auto-plan ────────────────────────────────────────────────────────────────
rootCommand.Add(AutoPlanCommand.Create(cwd));
```

### Writing and Committing AUTO-RUN.md
```csharp
// Pattern: write file, then use GitService to commit
var autoRunPath = Path.Combine(cwd, "docs", "features", slug, "AUTO-RUN.md");
File.WriteAllText(autoRunPath, autoRunContent);

// Commit only AUTO-RUN.md on abort (no partial artifacts)
GitService.ExecGit(cwd, ["add", autoRunPath]);
GitService.ExecGit(cwd, ["commit", "-m", $"docs({slug}): auto-research aborted — {reason}"]);
```

### Output Contract for new commands
```csharp
// Source: OutputService.cs pattern — all output via Output.Write()
Output.Write("slug", slug);
Output.Write("outcome", "success");  // or "aborted"
Output.Write("duration_seconds", elapsed.TotalSeconds.ToString("F1"));
Output.Write("artifact", $"docs/features/{slug}/RESEARCH.md");
Output.Write("auto_run_md", $"docs/features/{slug}/AUTO-RUN.md");
Output.WriteBool("committed", true);
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| JS gfd-tools.cjs (synchronous execSync) | C# GfdTools with async Process.Start | 2026-02-20 (this codebase) | Async stdout/stderr read is now the correct pattern |
| `claude -p` undocumented `--max-turns` | `--max-turns` documented in official CLI reference | Current | Safe to use; exits with error on limit |

**Deprecated/outdated:**
- `gfd-tools.cjs` (JS) — replaced by C# GfdTools. All new commands go in C# only. The bash wrapper at `get-features-done/bin/gfd-tools` invokes the C# project via `dotnet run`.

---

## Open Questions

1. **Prompt delivery mechanism: inline string vs. `--system-prompt-file`**
   - What we know: `claude -p "prompt"` works for short prompts; `--system-prompt-file` works for large prompts in print mode
   - What's unclear: the research/plan prompts are large (agent definition + feature content + codebase docs). If total prompt length approaches CLI limits, inline may fail.
   - Recommendation: Use `--append-system-prompt-file` with a temp file for the agent definition, and pass feature/codebase content inline. Or write the full prompt to a temp file and use stdin (`--input-format text` with piped stdin). Test with a real feature to see if prompt length is an issue.

2. **`--dangerously-skip-permissions` vs. `--allowedTools` scope**
   - What we know: `--allowedTools "Read,Write,Edit,Bash,Grep,Glob,Task"` is the existing interactive tool set. In headless mode, `Task` spawns subagents which also need tool permissions.
   - What's unclear: Whether subagents spawned via `Task()` within a `claude -p` session inherit the parent's `--allowedTools` or need their own.
   - Recommendation: Start with `--allowedTools "Read,Write,Edit,Bash,Grep,Glob,Task,WebSearch,WebFetch"` (matching the interactive command definitions). If subagent tool permissions are insufficient, escalate to `--dangerously-skip-permissions` only for the sandboxed auto-run context.

3. **Concurrent stdout/stderr read with `WaitForExit()`**
   - What we know: Must use async reads to avoid deadlock. `.Result` on both tasks after `WaitForExit()` is called after both tasks are started.
   - What's unclear: Whether `process.WaitForExit()` with redirect streams can still deadlock in .NET 10.
   - Recommendation: Use `process.WaitForExitAsync()` (available in .NET 5+) for fully async invocation, or the established pattern of starting both `ReadToEndAsync()` tasks before `WaitForExit()`.

---

## Sources

### Primary (HIGH confidence)
- https://code.claude.com/docs/en/cli-reference — Confirmed `--max-turns`, `--allowedTools`, `--output-format`, `--model`, `--permission-mode` flags; exit behavior on max-turns
- https://code.claude.com/docs/en/headless — Confirmed `-p` flag behavior, tool approval pattern
- `./get-features-done/GfdTools/Services/GitService.cs` — ExecGit pattern for subprocess invocation
- `./get-features-done/GfdTools/Services/OutputService.cs` — Output contract
- `./get-features-done/GfdTools/Program.cs` — Command registration pattern
- `./get-features-done/GfdTools/GfdTools.csproj` — .NET 10, System.CommandLine beta5
- `./get-features-done/workflows/research-feature.md` — Exact research prompt structure
- `./get-features-done/workflows/plan-feature.md` — Exact plan prompt structure and status lifecycle

### Secondary (MEDIUM confidence)
- GitHub issue #16963 anthropics/claude-code — `--max-turns` confirmed present in CLI even if underdocumented in `--help`

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — C# project structure and dependencies are directly inspected
- Architecture: HIGH — subprocess pattern is identical to existing GitService, command pattern is identical to all existing commands
- Claude -p flags: HIGH — verified against official documentation
- Pitfalls: HIGH — deadlock pattern is a known .NET issue; status revert is derived from reading the workflow lifecycle directly
- Open questions: MEDIUM — prompt delivery method needs empirical validation

**Research date:** 2026-02-20
**Valid until:** 2026-03-22 (stable domain, but claude CLI may update)
