using System.Diagnostics;
using System.IO.Compression;

namespace GfdTools.Services;

public static class GitService
{
    public record GitResult(int ExitCode, string Stdout, string Stderr);

    /// <summary>
    /// Execute a git command using ArgumentList (not string concatenation) to avoid shell quoting bugs.
    /// NOTE: In some sandboxed environments, spawning git may fail. Use CommitExists() as a fallback
    /// for commit verification.
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
        catch
        {
            return new GitResult(1, string.Empty, "git not available in this environment");
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

    /// <summary>
    /// Check if a commit hash exists by reading git object files directly.
    /// This is a fallback that works in sandboxed environments where spawning git fails.
    ///
    /// Git stores objects as: .git/objects/<first2>/<rest>
    /// For a short hash like "abc123", find all objects with that prefix.
    /// For a full 40-char hash, check the exact file.
    /// </summary>
    public static bool CommitExists(string cwd, string hash)
    {
        if (string.IsNullOrWhiteSpace(hash) || hash.Length < 4)
            return false;

        // Normalize hash to lowercase
        hash = hash.ToLowerInvariant();

        // Find the git dir
        var gitDir = FindGitDir(cwd);
        if (gitDir == null) return false;

        var objectsDir = Path.Combine(gitDir, "objects");

        // Full hash (40 chars): check exact object file
        if (hash.Length == 40)
        {
            var prefix = hash[..2];
            var rest = hash[2..];
            var objectPath = Path.Combine(objectsDir, prefix, rest);
            if (File.Exists(objectPath))
                return IsCommitObject(objectPath);

            // Also check packed objects
            return IsInPackedObjects(gitDir, hash);
        }

        // Short hash: scan objects directory for matching prefix
        var dirPrefix = hash[..2];
        var filePrefix = hash.Length > 2 ? hash[2..] : "";

        var dirPath = Path.Combine(objectsDir, dirPrefix);
        if (Directory.Exists(dirPath))
        {
            var candidates = Directory.GetFiles(dirPath)
                .Select(Path.GetFileName)
                .Where(f => f != null && f!.StartsWith(filePrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var candidate in candidates)
            {
                var fullPath = Path.Combine(dirPath, candidate!);
                if (IsCommitObject(fullPath))
                    return true;
            }
        }

        // Check packed objects with short hash prefix
        return IsInPackedObjects(gitDir, hash);
    }

    /// <summary>
    /// Read a loose git object file (zlib-compressed) and check if it's a commit.
    /// Git object format: "commit <size>\0<content>"
    /// </summary>
    private static bool IsCommitObject(string objectPath)
    {
        try
        {
            // Git object files are zlib-compressed (not gzip)
            using var fs = new FileStream(objectPath, FileMode.Open, FileAccess.Read);
            using var deflate = new ZLibStream(fs, CompressionMode.Decompress);

            // Read the header: "commit <size>\0" or "tree/blob/tag..."
            var header = new byte[7];
            var read = deflate.Read(header, 0, 7);
            if (read < 6) return false;

            // Check if starts with "commit "
            return header[0] == 'c' && header[1] == 'o' && header[2] == 'm' &&
                   header[3] == 'm' && header[4] == 'i' && header[5] == 't';
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check packed-refs and pack files for a commit hash.
    /// </summary>
    private static bool IsInPackedObjects(string gitDir, string hash)
    {
        // Check packed-refs (for tag/branch refs — not directly helpful for commit hashes)
        // Check pack index files for the hash
        var packDir = Path.Combine(gitDir, "objects", "pack");
        if (!Directory.Exists(packDir)) return false;

        foreach (var idxFile in Directory.GetFiles(packDir, "*.idx"))
        {
            try
            {
                if (PackIndexContains(idxFile, hash))
                    return true;
            }
            catch { }
        }

        return false;
    }

    /// <summary>
    /// Check a pack index (.idx) file for a hash.
    /// Pack index v2 format: 4-byte magic, 4-byte version, 256 fan-out ints (1024 bytes),
    /// then N 20-byte SHA1 entries sorted.
    /// For v1: no magic/version header.
    /// We use a simple text-based approach: read the binary and look for the hash bytes.
    /// </summary>
    private static bool PackIndexContains(string idxFile, string hash)
    {
        // Convert hash to bytes for binary comparison
        // Only attempt if we have a full 40-char hex hash
        if (hash.Length != 40) return PackIndexContainsShort(idxFile, hash);

        try
        {
            var hashBytes = Convert.FromHexString(hash);
            var content = File.ReadAllBytes(idxFile);

            // Simple scan: look for the 20-byte sequence in the file
            for (int i = 0; i <= content.Length - 20; i++)
            {
                bool found = true;
                for (int j = 0; j < 20; j++)
                {
                    if (content[i + j] != hashBytes[j]) { found = false; break; }
                }
                if (found) return true;
            }
            return false;
        }
        catch { return false; }
    }

    private static bool PackIndexContainsShort(string idxFile, string shortHash)
    {
        // For short hashes, check if any 20-byte entry starts with the given prefix bytes
        if (shortHash.Length < 4 || shortHash.Length % 2 != 0) return false;

        try
        {
            var prefixBytes = Convert.FromHexString(shortHash);
            var content = File.ReadAllBytes(idxFile);

            for (int i = 0; i <= content.Length - prefixBytes.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < prefixBytes.Length; j++)
                {
                    if (content[i + j] != prefixBytes[j]) { found = false; break; }
                }
                if (found) return true;
            }
            return false;
        }
        catch { return false; }
    }

    private static string? FindGitDir(string cwd)
    {
        var dir = cwd;
        while (!string.IsNullOrEmpty(dir))
        {
            var gitDir = Path.Combine(dir, ".git");
            if (Directory.Exists(gitDir)) return gitDir;
            var parent = Path.GetDirectoryName(dir);
            if (parent == dir) break;
            dir = parent;
        }
        return null;
    }
}
