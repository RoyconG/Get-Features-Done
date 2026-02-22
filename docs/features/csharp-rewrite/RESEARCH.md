# Feature: C# Rewrite — Research

**Researched:** 2026-02-20
**Domain:** .NET 10 console app, System.CommandLine 2.0, CLI tool port from Node.js
**Confidence:** HIGH

## Summary

This feature ports `get-features-done/bin/gfd-tools.cjs` — a zero-dependency Node.js CLI of ~1470 lines — to a C# console app targeting .NET 10 using System.CommandLine 2.0. The scope is bounded by grep-confirmed actively-used commands; dead commands in gfd-tools.cjs are not ported. Output changes from JSON to key=value pairs (one per line) to reduce token consumption.

The technical work is straightforward: .NET 10 SDK is available in Fedora 43 via `sudo dnf install dotnet-sdk-10.0`, System.CommandLine 2.0.3 is stable, and the existing codebase patterns are well-understood from reading gfd-tools.cjs. The main risk areas are: (1) correctly identifying which commands are actually used vs referenced (some agent files reference commands that don't exist in gfd-tools.cjs), (2) adapting workflows/agent files to parse key=value output instead of JSON, and (3) ensuring `dotnet run --project` build caching works acceptably for repeated CLI invocations.

**Primary recommendation:** Build the C# project in `get-features-done/GfdTools/`, use System.CommandLine 2.0.3, target net10.0, output key=value pairs via stdout, errors via stderr with non-zero exit codes. Do NOT use a shell wrapper — `dotnet run --project get-features-done/GfdTools/` is the invocation pattern. Install .NET 10 SDK first via dnf before starting implementation.

---

## User Constraints (from FEATURE.md)

### Locked Decisions

- **Target framework:** .NET 10 (`net10.0`)
- **CLI library:** System.CommandLine 2.0
- **Project location:** `get-features-done/GfdTools/` (alongside current .cjs)
- **Code layout:** Single project — Commands/, Models/, Services/ folders
- **Invocation:** `dotnet run --project` (caches build after first run)
- **Output format:** key=value pairs, one per line (not JSON)
- **Command scope:** Grep workflows/agents to find active commands; skip dead code
- **Bug handling:** Fix bugs found during porting rather than replicating
- **Verification approach:** Claude's discretion
- **Wrapper script:** Claude's discretion
- **Lists in output:** Claude's discretion (repeated keys vs comma-separated)
- **Error reporting:** Claude's discretion (stderr+exit code vs error= key)

### Out of Scope

- Exploring alternatives to System.CommandLine 2.0 (locked)
- Exploring alternatives to .NET 10 (locked)
- Porting commands not found in workflow/agent grepping
- Replicating bugs from gfd-tools.cjs

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.CommandLine | 2.0.3 | CLI argument parsing, subcommand routing | Locked choice; official Microsoft library, stable as of Feb 10, 2026 |
| .NET 10 SDK | 10.0.103 | Runtime and build toolchain | Locked choice; GA since Nov 11, 2025; available in Fedora 43 |

### Supporting

| Component | Version | Purpose | When to Use |
|-----------|---------|---------|-------------|
| `dotnet-sdk-10.0` (Fedora pkg) | 10.0.103-1.fc43 (stable) | SDK installation | Must install before creating project |
| `global.json` | n/a | Pin SDK version to 10.x | Prevents accidental use of system .NET 8 for this project |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| System.CommandLine 2.0 | Spectre.Console.Cli, Cocona | Locked — don't evaluate |
| `dotnet run --project` | Published binary | Locked — don't evaluate |
| key=value output | JSON | Locked — don't evaluate |

**Installation:**

```bash
sudo dnf install dotnet-sdk-10.0
dotnet add package System.CommandLine --version 2.0.3
```

---

## Architecture Patterns

### Recommended Project Structure

```
get-features-done/GfdTools/
├── GfdTools.csproj          # net10.0, System.CommandLine reference
├── Program.cs               # RootCommand setup, routes to commands
├── Commands/
│   ├── CommitCommand.cs
│   ├── ConfigGetCommand.cs
│   ├── FeatureCommands.cs   # feature add-decision, add-blocker
│   ├── FeaturePlanIndexCommand.cs
│   ├── FeatureUpdateStatusCommand.cs
│   ├── FrontmatterCommands.cs   # frontmatter get, set, merge
│   ├── HistoryDigestCommand.cs
│   ├── InitCommands.cs      # init new-project, new-feature, plan-feature, execute-feature, progress, map-codebase
│   ├── ListFeaturesCommand.cs
│   ├── ProgressCommand.cs
│   ├── SummaryExtractCommand.cs
│   └── VerifyCommands.cs    # verify plan-structure, commits, (artifacts, key-links)
├── Models/
│   ├── FeatureInfo.cs       # Mirrors findFeatureInternal result
│   ├── Config.cs            # GFD config.json shape
│   └── Frontmatter.cs       # Parsed YAML frontmatter
└── Services/
    ├── ConfigService.cs     # loadConfig logic
    ├── FeatureService.cs    # findFeatureInternal, listFeaturesInternal
    ├── FrontmatterService.cs # extractFrontmatter, reconstructFrontmatter, spliceFrontmatter
    ├── GitService.cs        # execGit, isGitIgnored
    └── OutputService.cs     # key=value output, error to stderr
```

### Pattern 1: key=value Output

**What:** Each command writes one `key=value` line per output field to stdout. Lists use repeated keys or comma-separated values (decide per command, documented below). Errors go to stderr + non-zero exit code.

**When to use:** Always. Never write JSON to stdout.

**Example:**
```csharp
// Source: Feature definition (locked decision)
// OutputService.cs
public static void Write(string key, string value)
    => Console.WriteLine($"{key}={value}");

public static void WriteList(string key, IEnumerable<string> values)
{
    foreach (var v in values)
        Console.WriteLine($"{key}={v}");  // repeated keys for arrays
}

public static void Error(string message)
{
    Console.Error.WriteLine(message);
    Environment.Exit(1);
}
```

**Caller pattern:**

```csharp
// In a command handler:
Output.Write("found", "true");
Output.Write("slug", featureInfo.Slug);
Output.Write("status", featureInfo.Status);
Output.WriteList("plans", featureInfo.Plans);
```

**Recommendation for lists:** Use repeated keys (e.g., `plan=01-PLAN.md`, `plan=02-PLAN.md`). This is unambiguous and doesn't require escaping. Comma-separated is fragile when values contain commas (plan names could).

**Recommendation for errors:** Write to stderr + exit 1. Do NOT write `error=...` to stdout, because workflows check `$?` (exit code) and Claude parses stdout. Mixing error keys into stdout creates ambiguity.

### Pattern 2: System.CommandLine 2.0 Command Hierarchy

**What:** Each `command subcommand` pair becomes a `Command` with `SetAction`. The root command routes to top-level commands.

**Example:**
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/standard/commandline/get-started-tutorial
// Program.cs
var rootCommand = new RootCommand("GFD Tools CLI");

var commitCommand = new Command("commit", "Commit docs to git");
var messageArg = new Argument<string>("message");
var filesOption = new Option<string[]>("--files") { AllowMultipleArgumentsPerToken = true };
var amendOption = new Option<bool>("--amend");

commitCommand.Arguments.Add(messageArg);
commitCommand.Options.Add(filesOption);
commitCommand.Options.Add(amendOption);
commitCommand.SetAction(pr => {
    var svc = new CommitService(pr.GetValue(messageArg), pr.GetValue(filesOption), pr.GetValue(amendOption));
    svc.Run();
    return 0;
});
rootCommand.Subcommands.Add(commitCommand);

return rootCommand.Parse(args).Invoke();
```

### Pattern 3: Frontmatter Parsing in C#

**What:** Port the JS frontmatter YAML parser exactly. The custom parser in gfd-tools.cjs handles: scalars, arrays (inline `[]` and multi-line `- item`), nested objects, boolean/integer coercion.

**When to use:** FrontmatterService handles all reads/writes to .md files with `---` delimiters.

**Key bug to fix (not replicate):** The JS `extractFrontmatter` uses `lines.indexOf(line)` to find the current line's index when determining if the next line is an array or object. This is O(n) and breaks if the same line content appears twice. Port should use a proper index-tracked loop (`for (int i = 0; i < lines.Length; i++)`).

### Pattern 4: Git Operations via Process

**What:** Use `Process.Start` / `Process.WaitForExit` to shell out to `git`, mirroring the JS `execSync` approach.

```csharp
// Services/GitService.cs
public static (int exitCode, string stdout, string stderr) ExecGit(string cwd, string[] args)
{
    var psi = new ProcessStartInfo("git", string.Join(" ", args.Select(a => $"\"{a}\"")))
    {
        WorkingDirectory = cwd,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    using var p = Process.Start(psi)!;
    var stdout = p.StandardOutput.ReadToEnd();
    var stderr = p.StandardError.ReadToEnd();
    p.WaitForExit();
    return (p.ExitCode, stdout.Trim(), stderr.Trim());
}
```

### Anti-Patterns to Avoid

- **Don't use `Console.Out.WriteLine` directly in command handlers:** Route all output through `OutputService` so output format is centrally controlled and testable.
- **Don't parse JSON from gfd-tools.cjs to validate parity:** The workflows and agents must be updated to parse key=value; don't create a JSON compatibility shim.
- **Don't use `dotnet publish` for deployment:** Feature specifies `dotnet run --project` (development-mode execution, not published binary).
- **Don't use `System.Text.Json` for output:** Output is key=value, not JSON.
- **Don't target net8.0:** Must target net10.0 even though .NET 8 is currently installed. This requires installing .NET 10 SDK first.

---

## Actively-Used Commands (Grep Results)

Based on grepping all workflow and agent files, these commands are actively invoked:

| Command | Subcommand | Used In |
|---------|-----------|---------|
| `commit` | (none) | All workflows, executor agent, planner agent |
| `init` | `new-project` | new-project workflow |
| `init` | `new-feature` | new-feature workflow |
| `init` | `plan-feature` | plan-feature workflow, research-feature workflow, discuss-feature workflow, gfd-researcher agent, gfd-planner agent |
| `init` | `execute-feature` | execute-feature workflow, gfd-executor agent |
| `init` | `progress` | progress workflow |
| `init` | `map-codebase` | map-codebase workflow |
| `feature-update-status` | (none) | All workflows, executor agent, planner agent |
| `feature` | `add-decision` | gfd-executor agent |
| `feature` | `add-blocker` | gfd-executor agent |
| `feature-plan-index` | (none) | execute-feature workflow |
| `list-features` | (none) | execute-feature workflow, status workflow, progress workflow |
| `progress` | `bar` | progress workflow |
| `frontmatter` | `get` | (indirect via verifier; referenced in planner with `validate` subcommand) |
| `frontmatter` | `set` | (indirect) |
| `frontmatter` | `merge` | convert-from-gsd workflow |
| `history-digest` | (none) | gfd-planner agent |
| `summary-extract` | (none) | gfd-verifier agent |
| `verify` | `plan-structure` | gfd-planner agent |
| `verify` | `commits` | gfd-verifier agent |
| `config-get` | (none) | gfd-executor agent |

**Commands in gfd-tools.cjs NOT actively used (skip porting):**
- `generate-slug` — not found in workflow/agent grep
- `current-timestamp` — not found
- `config-set` — not found
- `verify-summary` — not found
- `verify-path-exists` — not found
- `template select/fill` — not found
- `resolve-model` — not found (model resolution happens inside `init` commands)
- `find-feature` — not found
- `validate health` — not found
- `progress table/json` (only `progress bar` is used)

### Commands Referenced But NOT Implemented in gfd-tools.cjs

These appear in agent files but the corresponding code doesn't exist in gfd-tools.cjs — they will error at runtime:

| Agent | Command Called | Status in .cjs | Action |
|-------|---------------|----------------|--------|
| gfd-verifier | `verify artifacts $PLAN_PATH` | NOT IMPLEMENTED | Port with implementation (new command) |
| gfd-verifier | `verify key-links $PLAN_PATH` | NOT IMPLEMENTED | Port with implementation (new command) |
| gfd-planner | `frontmatter validate $PLAN_PATH --schema plan` | NOT IMPLEMENTED | Port with implementation (new command) |

**Decision:** These three missing commands are bugs in gfd-tools.cjs. The feature says "fix bugs found during porting rather than replicating." Implement them correctly in the C# port.

- `verify artifacts`: Read PLAN frontmatter `must_haves` field, check each artifact path exists, return `all_passed`, `passed`, `total`, `artifacts[]` with per-artifact status.
- `verify key-links`: Read PLAN frontmatter `must_haves.key_links`, grep codebase for each link, return `all_verified`, `verified`, `total`, `links[]`.
- `frontmatter validate --schema plan`: Validate required frontmatter fields per schema (`feature`, `plan`, `type`, `wave`, `depends_on`, `files_modified`, `autonomous`), return `valid`, `errors[]`.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| CLI argument parsing | Custom arg splitter | System.CommandLine 2.0 | Handles --flags, positional args, subcommands, help, validation |
| YAML frontmatter | Full YAML library | Port the custom parser from gfd-tools.cjs | The format used is a tiny subset of YAML; a full YAML library is overkill and adds a dependency |
| Git operations | LibGit2Sharp | `Process.Start("git", ...)` | Mirrors the JS approach; avoids binary dependency |

**Key insight:** Keep zero NuGet dependencies except System.CommandLine. The C# port must remain as self-contained as the Node.js original.

---

## Common Pitfalls

### Pitfall 1: .NET SDK Version Conflict

**What goes wrong:** `dotnet` on the system is .NET 8. Running `dotnet run` in the GfdTools project without a `global.json` will use .NET 8 SDK (the only installed SDK), causing a build error because `net10.0` target framework isn't available.

**Why it happens:** The feature targets .NET 10, but .NET 8 is the only installed SDK. Without .NET 10 SDK, `dotnet build -f net10.0` fails.

**How to avoid:**
1. Install .NET 10 SDK FIRST: `sudo dnf install dotnet-sdk-10.0`
2. Create `get-features-done/GfdTools/global.json` to pin the SDK:
   ```json
   {
     "sdk": {
       "version": "10.0.100",
       "rollForward": "latestFeature"
     }
   }
   ```
3. Verify: `cd get-features-done/GfdTools && dotnet --version` should show 10.x.x

**Warning signs:** `error NETSDK1045: The current .NET SDK does not support targeting .NET 10.0.`

### Pitfall 2: `dotnet run` First-Run Latency

**What goes wrong:** `dotnet run --project get-features-done/GfdTools/` takes 3-10 seconds on first run (cold build). Subsequent invocations on unchanged code take ~1-2 seconds (incremental build check). Workflows that call gfd-tools many times in sequence will feel slow.

**Why it happens:** `dotnet run` always checks for changes before running. It does NOT skip the build check even on cache hits.

**How to avoid:**
- This is acceptable per the feature definition (invocation via `dotnet run --project`). The feature explicitly accepts this tradeoff.
- Do NOT attempt to work around by using `dotnet run --no-build` in workflows (that skips build even if sources changed).
- The build output is cached in `bin/` and `obj/` — only changed files trigger recompilation.

**Warning signs:** Workflows taking noticeably longer than Node.js equivalent.

### Pitfall 3: JSON Parsing in Workflows Must Be Replaced

**What goes wrong:** Every workflow and agent file currently does `Parse JSON for: key1, key2, key3`. After the C# port, output is key=value lines, not JSON. If workflows aren't updated, they'll fail to extract values.

**Why it happens:** The feature requires updating all workflow and agent files to use the new output format.

**How to avoid:** When updating workflow/agent files, replace JSON parsing patterns with key=value parsing:

```bash
# Old pattern (JSON):
INIT=$(node .../gfd-tools.cjs init plan-feature "${SLUG}")
# parse INIT as JSON to extract executor_model, commit_docs, etc.

# New pattern (key=value):
INIT=$(dotnet run --project get-features-done/GfdTools/ -- init plan-feature "${SLUG}")
executor_model=$(echo "$INIT" | grep "^executor_model=" | cut -d= -f2-)
commit_docs=$(echo "$INIT" | grep "^commit_docs=" | cut -d= -f2-)
```

**Warning signs:** Workflow step says "Parse JSON for: ..." but tool now outputs key=value.

### Pitfall 4: `--raw` Flag is Not Needed in C# Port

**What goes wrong:** gfd-tools.cjs has a `--raw` flag that outputs scalar values directly instead of JSON. The C# port's default output is already key=value (not JSON), so `--raw` is meaningless.

**Why it happens:** The `--raw` flag was a workaround for extracting scalar values from JSON output. With key=value output, you just `grep "^key=" | cut -d= -f2-`.

**How to avoid:** Do NOT port the `--raw` flag. Update any workflow that uses `--raw` to use key=value extraction instead.

**Warning signs:** Seeing `--raw` in workflow bash snippets that call the C# tool.

### Pitfall 5: Frontmatter Parser Edge Cases

**What goes wrong:** The JS frontmatter parser has a bug: `lines.indexOf(line)` finds the first occurrence of that string in the array, not the current line. If two lines have identical content, the parser may read the wrong "next line" when deciding if a value is an array or object.

**Why it happens:** JS Array.indexOf returns the index of the first matching element.

**How to avoid:** Port the frontmatter parser using index-based iteration. Use `for (int i = 0; i < lines.Length; i++)` and look at `lines[i + 1]` directly. This fixes the bug rather than replicating it.

### Pitfall 6: Multiline String Values in feature_content / research_content

**What goes wrong:** `init plan-feature` and `init execute-feature` return file contents in the `feature_content` and `research_content` keys. These are multiline. key=value output breaks if the value contains newlines.

**Why it happens:** File contents span multiple lines, but key=value format assumes single-line values.

**How to avoid:** For multi-line file content, either:
- Write to a temp file and return the path as `feature_content_file=/tmp/gfd-xxx.md` (best)
- Base64-encode the value as `feature_content_b64=<base64>` (Claude can decode it)
- Omit file contents from init commands (Claude can read files directly — simpler)

**Recommendation:** Omit `feature_content` and `research_content` from the C# `init` commands. The workflows that use `--include feature` do so to avoid redundant reads, but Claude can read files directly. Removing file content from init output eliminates the multiline problem entirely. Update workflow instructions to read files separately.

### Pitfall 7: Git Shell Argument Quoting

**What goes wrong:** Passing arguments to `git` via `Process.Start` with quoted string concatenation leads to escaping bugs (e.g., commit messages with quotes).

**Why it happens:** `Process.Start` with a single string argument uses shell parsing on some systems.

**How to avoid:** Use the `ProcessStartInfo` overload with `ArgumentList` (array form), not a single string. This bypasses shell parsing entirely:

```csharp
var psi = new ProcessStartInfo("git");
psi.ArgumentList.Add("commit");
psi.ArgumentList.Add("-m");
psi.ArgumentList.Add(message);  // No manual quoting needed
```

---

## Code Examples

Verified patterns from official sources:

### System.CommandLine 2.0 — Subcommand with Options

```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/standard/commandline/get-started-tutorial
var featureUpdateStatusCmd = new Command("feature-update-status", "Update feature status");
var slugArg = new Argument<string>("slug");
var statusArg = new Argument<string>("status");

featureUpdateStatusCmd.Arguments.Add(slugArg);
featureUpdateStatusCmd.Arguments.Add(statusArg);
featureUpdateStatusCmd.SetAction(pr =>
{
    var svc = new FeatureService(Environment.CurrentDirectory);
    return svc.UpdateStatus(pr.GetValue(slugArg), pr.GetValue(statusArg));
});
rootCommand.Subcommands.Add(featureUpdateStatusCmd);
```

### key=value Output Pattern

```csharp
// OutputService.cs (no external dependency)
public static class Output
{
    public static void Write(string key, object? value)
        => Console.WriteLine($"{key}={value}");

    public static void WriteList(string key, IEnumerable<string> values)
    {
        foreach (var v in values)
            Console.WriteLine($"{key}={v}");
    }

    public static void WriteBool(string key, bool value)
        => Console.WriteLine($"{key}={value.ToString().ToLower()}");

    public static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}
```

### Frontmatter Parse (fixed index bug)

```csharp
// Services/FrontmatterService.cs
public static Dictionary<string, object?> Extract(string content)
{
    var result = new Dictionary<string, object?>();
    var match = Regex.Match(content, @"^---\n([\s\S]*?)\n---");
    if (!match.Success) return result;

    var lines = match.Groups[1].Value.Split('\n');
    // Use index-based loop to fix the lines.indexOf bug from JS version
    for (int i = 0; i < lines.Length; i++)
    {
        var line = lines[i];
        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')) continue;
        // ... proper index-based parsing
    }
    return result;
}
```

### Invocation Pattern for Workflows

```bash
# How C# tool is invoked in updated workflow/agent files:
INIT=$(dotnet run --project $HOME/.claude/get-features-done/GfdTools/ -- init plan-feature "${SLUG}")

# Extract individual values:
feature_found=$(echo "$INIT" | grep "^feature_found=" | cut -d= -f2-)
feature_dir=$(echo "$INIT" | grep "^feature_dir=" | cut -d= -f2-)
feature_name=$(echo "$INIT" | grep "^feature_name=" | cut -d= -f2-)
plans=$(echo "$INIT" | grep "^plan=")  # repeated key for list
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| System.CommandLine beta | System.CommandLine 2.0.3 (stable) | GA in 2025 | API changed significantly from beta — don't use beta docs |
| `dotnet run -p` (project short form) | `dotnet run --project` | .NET 6 | `-p` deprecated; use `--project` |
| .NET 8 only on system | .NET 10 available via dnf | Fedora 43 package: 10.0.102 (stable), 10.0.103 (testing) | Must install before development |

**Deprecated/outdated:**
- System.CommandLine beta API: Pre-2.0 had `Handler.SetHandler()` pattern. 2.0 uses `command.SetAction()`. Use 2.0 API only.
- `-p` abbreviation for `--project` in dotnet run: deprecated since .NET 6, removed in .NET 7.

---

## Open Questions

1. **Wrapper script decision**
   - What we know: Feature says Claude's discretion. `dotnet run --project /full/path/GfdTools/` is verbose but unambiguous.
   - What's unclear: Whether a shell script wrapper `gfd-tools` that calls `dotnet run --project` would be simpler to maintain in workflow files.
   - Recommendation: No wrapper. Keep the direct `dotnet run --project` invocation. Workflows are markdown files; the verbosity is acceptable and the wrapper adds an extra file to maintain.

2. **`--include` flag behavior for init commands**
   - What we know: `init plan-feature --include feature` currently embeds file contents in JSON output. C# port can't easily embed multiline content in key=value output.
   - What's unclear: Whether workflows need the embedded content or can tolerate a separate file-read step.
   - Recommendation: Drop `--include` support. Remove the `--include feature` flag handling. Update workflow markdown to read files separately. The workflow text says "Load all context in one call" as an optimization, not a hard requirement.

3. **`progress bar` format**
   - What we know: Progress bar currently uses Unicode block characters (█░) embedded in a string. With key=value output: `bar=[████░░░░░░] 3/10 (30%)`.
   - What's unclear: Whether Claude parses the bar value or just displays it.
   - Recommendation: Return `bar=[████░░░░░░] 3/10 (30%)` as a single value in `bar=` key. The progress workflow just displays it: `PROGRESS_BAR=$(... progress bar --raw)`.

---

## Sources

### Primary (HIGH confidence)

- https://dotnet.microsoft.com/en-us/download/dotnet/10.0 — .NET 10 version history; confirmed GA 10.0.3 (Feb 10, 2026)
- https://www.nuget.org/packages/System.CommandLine — System.CommandLine 2.0.3 confirmed latest stable (Feb 10, 2026)
- https://learn.microsoft.com/en-us/dotnet/standard/commandline/get-started-tutorial — System.CommandLine 2.0 API: `SetAction`, `GetValue`, `RootCommand`, `Command`, `Argument`, `Option` patterns
- https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-run — `dotnet run --project` flag, build caching behavior
- https://learn.microsoft.com/en-us/dotnet/core/tools/global-json — `global.json` SDK version pinning
- https://packages.fedoraproject.org/pkgs/dotnet10.0/dotnet-sdk-10.0/ — Fedora 43: `dotnet-sdk-10.0` version 10.0.102-1.fc43 (stable)
- `./get-features-done/bin/gfd-tools.cjs` — Full source code of existing CLI (ground truth, HIGH confidence)
- All workflow and agent files grepped for command usage (ground truth)

### Secondary (MEDIUM confidence)

- .NET 10 dotnet run build caching: WebSearch confirmed incremental build check on every `dotnet run` invocation; first run is cold (3-10s), subsequent runs are fast (~1-2s) when source unchanged.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — Official docs and NuGet confirmed versions
- Architecture: HIGH — Based on reading all gfd-tools.cjs source code and confirmed command usage grep
- Pitfalls: HIGH for .NET SDK conflict (verified by checking installed version); MEDIUM for output format migration (based on reading all workflow/agent files)
- Missing commands: HIGH — confirmed by reading gfd-tools.cjs source and comparing to grep of agent files

**Research date:** 2026-02-20
**Valid until:** 2026-05-20 (stable domain; System.CommandLine 2.0 is GA, .NET 10 is GA)
