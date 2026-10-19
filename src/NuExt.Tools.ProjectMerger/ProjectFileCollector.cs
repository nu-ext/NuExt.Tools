namespace NuExt.Tools;

internal static class ProjectFileCollector
{
    private static readonly string[] s_excludePatterns =
    [
        ".g.",
        @"\.vs\",
        @"\bin\",
        @"\obj\",
        @"\bin-",
        @"\obj-",
        @"\Dependencies",
        @"\build\"
    ];

    public static List<string> GetFiles(string currentDirectory, string[] extensions)
    {
        var enabledExtensions = extensions
            .Where(extension => !extension.StartsWith('-'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var extensionOrder = enabledExtensions
            .Select((extension, index) => new { extension, index })
            .ToDictionary(item => item.extension, item => item.index, StringComparer.OrdinalIgnoreCase);

        return Directory
            .GetFiles(currentDirectory, "*", SearchOption.AllDirectories)
            .Where(file => !s_excludePatterns.Any(file.Contains) &&
                           enabledExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .OrderBy(file => extensionOrder[Path.GetExtension(file)])
            .ThenBy(file => file, StringComparer.Ordinal)
            .ToList();
    }
}