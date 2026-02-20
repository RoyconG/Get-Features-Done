using System.CommandLine;
using System.Text.Json;
using GfdTools.Services;

namespace GfdTools.Commands;

public static class FrontmatterCommands
{
    public static Command Create(string cwd)
    {
        var cmd = new Command("frontmatter") { Description = "Frontmatter operations on markdown files" };

        // frontmatter get <file> [--field <name>]
        var getCmd = new Command("get") { Description = "Get frontmatter fields from a file" };
        var getFileArg = new Argument<string>("file") { Description = "Path to markdown file" };
        var getFieldOpt = new Option<string?>("--field") { Description = "Specific field to get" };
        getCmd.Add(getFileArg);
        getCmd.Add(getFieldOpt);
        getCmd.SetAction(pr =>
        {
            var filePath = pr.GetValue(getFileArg)!;
            var field = pr.GetValue(getFieldOpt);

            var fullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(cwd, filePath);
            if (!File.Exists(fullPath))
                return Output.Fail($"File not found: {filePath}");

            var content = File.ReadAllText(fullPath);
            var fm = FrontmatterService.Extract(content);

            if (field != null)
            {
                if (!fm.TryGetValue(field, out var value))
                    return Output.Fail($"Field not found: {field}");

                WriteValue(field, value);
                return 0;
            }

            // Output all fields
            foreach (var (k, v) in fm)
            {
                WriteValue(k, v);
            }
            return 0;
        });
        cmd.Add(getCmd);

        // frontmatter merge <file> --data <json>
        var mergeCmd = new Command("merge") { Description = "Merge JSON data into frontmatter" };
        var mergeFileArg = new Argument<string>("file") { Description = "Path to markdown file" };
        var mergeDataOpt = new Option<string>("--data") { Description = "JSON data to merge", Required = true };
        mergeCmd.Add(mergeFileArg);
        mergeCmd.Add(mergeDataOpt);
        mergeCmd.SetAction(pr =>
        {
            var filePath = pr.GetValue(mergeFileArg)!;
            var dataJson = pr.GetValue(mergeDataOpt)!;

            var fullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(cwd, filePath);
            if (!File.Exists(fullPath))
                return Output.Fail($"File not found: {filePath}");

            Dictionary<string, object?> mergeData;
            try
            {
                mergeData = ParseJsonToDictionary(dataJson);
            }
            catch
            {
                return Output.Fail("Invalid JSON for --data");
            }

            var content = File.ReadAllText(fullPath);
            var fm = FrontmatterService.Extract(content);
            foreach (var (k, v) in mergeData)
                fm[k] = v;

            var newContent = FrontmatterService.Splice(content, fm);
            File.WriteAllText(fullPath, newContent);

            Output.WriteBool("merged", true);
            Output.WriteList("fields", mergeData.Keys);
            return 0;
        });
        cmd.Add(mergeCmd);

        // frontmatter validate <file> --schema <name>
        var validateCmd = new Command("validate") { Description = "Validate frontmatter fields against a schema" };
        var validateFileArg = new Argument<string>("file") { Description = "Path to markdown file" };
        var validateSchemaOpt = new Option<string>("--schema") { Description = "Schema name (e.g. plan)", Required = true };
        validateCmd.Add(validateFileArg);
        validateCmd.Add(validateSchemaOpt);
        validateCmd.SetAction(pr =>
        {
            var filePath = pr.GetValue(validateFileArg)!;
            var schema = pr.GetValue(validateSchemaOpt)!;

            var fullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(cwd, filePath);
            if (!File.Exists(fullPath))
                return Output.Fail($"File not found: {filePath}");

            var content = File.ReadAllText(fullPath);
            var fm = FrontmatterService.Extract(content);

            // Schema definitions
            var schemaFields = schema switch
            {
                "plan" => new[] { "feature", "plan", "type", "wave", "depends_on", "files_modified", "autonomous" },
                "summary" => new[] { "feature", "plan", "subsystem", "tags", "provides" },
                "feature" => new[] { "name", "slug", "status" },
                _ => Array.Empty<string>()
            };

            if (schemaFields.Length == 0)
            {
                Console.Error.WriteLine($"Unknown schema: {schema}. Available: plan, summary, feature");
                return 1;
            }

            var missing = new List<string>();
            var present = new List<string>();

            foreach (var field in schemaFields)
            {
                if (fm.ContainsKey(field))
                    present.Add(field);
                else
                    missing.Add(field);
            }

            Output.WriteBool("valid", missing.Count == 0);
            Output.Write("schema", schema);
            Output.WriteList("missing", missing);
            Output.WriteList("present", present);
            return 0;
        });
        cmd.Add(validateCmd);

        return cmd;
    }

    private static void WriteValue(string key, object? value)
    {
        if (value is List<string> list)
        {
            Output.WriteList(key, list);
        }
        else if (value is Dictionary<string, object?> dict)
        {
            // Flatten nested object as key.subkey=value
            foreach (var (k, v) in dict)
                Output.Write($"{key}.{k}", v);
        }
        else if (value is bool b)
        {
            Output.WriteBool(key, b);
        }
        else
        {
            Output.Write(key, value);
        }
    }

    private static Dictionary<string, object?> ParseJsonToDictionary(string json)
    {
        var result = new Dictionary<string, object?>();
        using var doc = JsonDocument.Parse(json);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            result[prop.Name] = ParseJsonElement(prop.Value);
        }

        return result;
    }

    private static object? ParseJsonElement(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when el.TryGetInt32(out var i) => i,
            JsonValueKind.Null => null,
            JsonValueKind.Array => el.EnumerateArray()
                .Select(e => ParseJsonElement(e)?.ToString() ?? "")
                .ToList(),
            _ => el.GetRawText(),
        };
    }
}
