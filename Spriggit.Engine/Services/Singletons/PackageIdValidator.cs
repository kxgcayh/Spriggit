using System.Text.RegularExpressions;

namespace Spriggit.Engine.Services.Singletons;

/// Package ids come from files inside the repo being processed, and end up on a `dotnet tool install` command line.
public static partial class PackageIdValidator
{
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    private static partial Regex ValidId();

    public static bool IsValid(string? id) => id != null && ValidId().IsMatch(id);

    public static void ThrowIfInvalid(string? id)
    {
        if (!IsValid(id))
        {
            throw new ArgumentException($"Invalid translation package id: '{id}'");
        }
    }
}
