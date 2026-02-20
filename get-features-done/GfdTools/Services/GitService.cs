using System.Diagnostics;

namespace GfdTools.Services;

public static class GitService
{
    public record GitResult(int ExitCode, string Stdout, string Stderr);

    /// <summary>
    /// Execute a git command using ArgumentList (not string concatenation) to avoid shell quoting bugs.
    /// </summary>
    public static GitResult ExecGit(string cwd, string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // CRITICAL: Add each arg individually via ArgumentList, not string concatenation.
        // This fixes shell quoting bugs present in the JS version.
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        try
        {
            using var process = Process.Start(psi)!;
            var stdout = process.StandardOutput.ReadToEnd().TrimEnd();
            var stderr = process.StandardError.ReadToEnd().TrimEnd();
            process.WaitForExit();
            return new GitResult(process.ExitCode, stdout, stderr);
        }
        catch (Exception ex)
        {
            return new GitResult(1, string.Empty, ex.Message);
        }
    }

    /// <summary>
    /// Check if a relative path is gitignored.
    /// </summary>
    public static bool IsGitIgnored(string cwd, string relPath)
    {
        var result = ExecGit(cwd, ["check-ignore", "-q", relPath]);
        return result.ExitCode == 0;
    }
}
