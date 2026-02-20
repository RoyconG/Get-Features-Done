using System.Text;
using SystemRegex = System.Text.RegularExpressions.Regex;
using RegexOptions = System.Text.RegularExpressions.RegexOptions;

namespace GfdTools.Services;

public static class FrontmatterService
{
    /// <summary>
    /// Extract YAML frontmatter from markdown content.
    /// BUG FIX: Uses index-based loop (for i = 0; i &lt; lines.Length; i++) instead of
    /// lines.indexOf(line) which breaks when two lines have identical content (JS bug).
    /// </summary>
    public static Dictionary<string, object?> Extract(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return new Dictionary<string, object?>();

        var match = SystemRegex.Match(content, @"^---\n([\s\S]*?)\n---", RegexOptions.Multiline);
        if (!match.Success)
            return new Dictionary<string, object?>();

        var yaml = match.Groups[1].Value;
        var result = new Dictionary<string, object?>();
        var lines = yaml.Split('\n');

        string? currentKey = null;
        List<string>? currentArray = null;
        Dictionary<string, object?>? currentObject = null;
        string? objectKey = null;

        // BUG FIX: Use index-based iteration, not indexOf, so identical lines don't confuse the parser.
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Trim() == string.Empty || line.TrimStart().StartsWith('#'))
                continue;

            // Check for object field (indented key: value)
            var objectFieldMatch = SystemRegex.Match(line, @"^(\s{2,4})(\w[\w-]*):\s*(.*)$");
            if (currentObject != null && objectFieldMatch.Success)
            {
                var fieldKey = objectFieldMatch.Groups[2].Value;
                var fieldValueStr = objectFieldMatch.Groups[3].Value.Trim();
                object? fieldValue = fieldValueStr == "" ? null
                    : fieldValueStr == "true" ? true
                    : fieldValueStr == "false" ? false
                    : SystemRegex.IsMatch(fieldValueStr, @"^\d+$") ? (object)int.Parse(fieldValueStr)
                    : fieldValueStr;
                currentObject[fieldKey] = fieldValue;
                continue;
            }

            // Check for array item (indented - item)
            var arrayItemMatch = SystemRegex.Match(line, @"^\s{2,4}-\s+(.*)$");
            if (currentArray != null && arrayItemMatch.Success)
            {
                var val = arrayItemMatch.Groups[1].Value.Trim();
                val = UnquoteString(val);
                currentArray.Add(val);
                continue;
            }

            // Top-level key: value
            var topLevelMatch = SystemRegex.Match(line, @"^([\w][\w-]*):\s*(.*)");
            if (topLevelMatch.Success)
            {
                // Flush previous array/object
                if (currentArray != null && currentKey != null)
                {
                    result[currentKey] = currentArray;
                    currentArray = null;
                }
                if (currentObject != null && objectKey != null)
                {
                    result[objectKey] = currentObject;
                    currentObject = null;
                    objectKey = null;
                }

                currentKey = topLevelMatch.Groups[1].Value;
                var value = topLevelMatch.Groups[2].Value.Trim();

                if (value == "")
                {
                    // Look at next non-empty line to determine if array or object
                    // BUG FIX: Use i+1 directly instead of indexOf(line) which was broken in JS
                    var nextLine = i + 1 < lines.Length ? lines[i + 1] : "";
                    if (SystemRegex.IsMatch(nextLine, @"^\s+-\s"))
                    {
                        currentArray = [];
                    }
                    else if (SystemRegex.IsMatch(nextLine, @"^\s+\w"))
                    {
                        currentObject = new Dictionary<string, object?>();
                        objectKey = currentKey;
                        currentKey = null;
                    }
                    else
                    {
                        result[currentKey] = null;
                    }
                }
                else if (value.StartsWith('[') && value.EndsWith(']'))
                {
                    var inner = value[1..^1].Trim();
                    if (inner == "")
                    {
                        result[currentKey] = new List<string>();
                    }
                    else
                    {
                        var items = inner.Split(',').Select(v =>
                        {
                            var trimmed = v.Trim();
                            return UnquoteString(trimmed);
                        }).ToList();
                        result[currentKey] = items;
                    }
                    currentArray = null;
                }
                else if (value == "true")
                {
                    result[currentKey] = true;
                    currentKey = null;
                }
                else if (value == "false")
                {
                    result[currentKey] = false;
                    currentKey = null;
                }
                else if (SystemRegex.IsMatch(value, @"^\d+$"))
                {
                    result[currentKey] = int.Parse(value);
                    currentKey = null;
                }
                else
                {
                    result[currentKey] = UnquoteString(value);
                    currentKey = null;
                }
            }
        }

        // Flush remaining
        if (currentArray != null && currentKey != null)
            result[currentKey] = currentArray;
        if (currentObject != null && objectKey != null)
            result[objectKey] = currentObject;

        return result;
    }

    /// <summary>
    /// Reconstruct YAML frontmatter from a dictionary. Matches JS behavior.
    /// </summary>
    public static string Reconstruct(Dictionary<string, object?> obj)
    {
        var sb = new StringBuilder();

        foreach (var (key, value) in obj)
        {
            if (value is null)
            {
                sb.AppendLine($"{key}:");
            }
            else if (value is List<string> list)
            {
                if (list.Count == 0)
                {
                    sb.AppendLine($"{key}: []");
                }
                else if (list.Count <= 3 && list.All(v => v.Length < 30))
                {
                    var inlineItems = list.Select(v =>
                        v.Contains(',') || v.Contains(' ') ? $"\"{v}\"" : v);
                    sb.AppendLine($"{key}: [{string.Join(", ", inlineItems)}]");
                }
                else
                {
                    sb.AppendLine($"{key}:");
                    foreach (var item in list)
                    {
                        var formatted = (item.Contains(':') || item.Contains(','))
                            ? $"\"{item}\""
                            : item;
                        sb.AppendLine($"  - {formatted}");
                    }
                }
            }
            else if (value is Dictionary<string, object?> nested)
            {
                sb.AppendLine($"{key}:");
                foreach (var (k, v) in nested)
                {
                    sb.AppendLine($"  {k}: {(v is null ? "" : v)}");
                }
            }
            else
            {
                sb.AppendLine($"{key}: {value}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Replace frontmatter in content with new frontmatter.
    /// </summary>
    public static string Splice(string content, Dictionary<string, object?> newFm)
    {
        var fmMatch = SystemRegex.Match(content, @"^---\n[\s\S]*?\n---");
        var newFmStr = $"---\n{Reconstruct(newFm)}---";

        if (!fmMatch.Success)
        {
            return $"{newFmStr}\n\n{content}";
        }

        return content[..fmMatch.Index] + newFmStr + content[(fmMatch.Index + fmMatch.Length)..];
    }

    private static string UnquoteString(string s)
    {
        if (s.StartsWith('"') && s.EndsWith('"') && s.Length >= 2)
            return s[1..^1];
        if (s.StartsWith('\'') && s.EndsWith('\'') && s.Length >= 2)
            return s[1..^1];
        return s;
    }
}
