using System.CommandLine;
using GfdTools.Services;

namespace GfdTools.Commands;

public static class ConfigGetCommand
{
    public static Command Create(string cwd)
    {
        var cmd = new Command("config-get") { Description = "Get config values as key=value pairs" };
        var keyArg = new Argument<string?>("key") { Description = "Config key (optional; omit to get all)", DefaultValueFactory = _ => null };
        cmd.Add(keyArg);

        cmd.SetAction(pr =>
        {
            var key = pr.GetValue(keyArg);

            if (key != null)
            {
                var fields = ConfigService.GetAllFields(cwd);
                if (!fields.TryGetValue(key, out var value))
                {
                    Output.Write(key, "");
                    return 0;
                }
                Output.Write(key, value);
                return 0;
            }

            // Output all config fields
            var all = ConfigService.GetAllFields(cwd);
            foreach (var (k, v) in all)
            {
                Output.Write(k, v);
            }
            return 0;
        });

        return cmd;
    }
}
