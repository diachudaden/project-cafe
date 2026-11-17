using Microsoft.Extensions.Hosting;

namespace ChonRieng.Api.Services;

public static class DevelopmentSeedCommand
{
    public const string Argument = "--seed-development-data";

    public static bool IsRequested(IEnumerable<string> arguments)
    {
        return arguments.Contains(Argument, StringComparer.Ordinal);
    }

    public static void EnsureAllowed(IHostEnvironment environment)
    {
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            throw new InvalidOperationException(
                $"The {Argument} command is only allowed in Development or Testing environments.");
        }
    }
}
