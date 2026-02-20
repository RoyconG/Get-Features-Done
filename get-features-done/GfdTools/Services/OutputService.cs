namespace GfdTools.Services;

/// <summary>
/// All stdout output in the entire project MUST go through this class.
/// Outputs key=value pairs, one per line.
/// </summary>
public static class Output
{
    public static void Write(string key, object? value)
    {
        Console.WriteLine($"{key}={value}");
    }

    public static void WriteBool(string key, bool value)
    {
        Console.WriteLine($"{key}={value.ToString().ToLowerInvariant()}");
    }

    public static void WriteList(string key, IEnumerable<string> values)
    {
        foreach (var v in values)
        {
            Console.WriteLine($"{key}={v}");
        }
    }

    /// <summary>
    /// Write error to stderr and return exit code 1.
    /// </summary>
    public static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}
