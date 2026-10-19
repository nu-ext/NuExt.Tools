using System.CommandLine;

namespace NuExt.Tools;

internal static class Program
{
    private static readonly string[] DefaultExtensions =
    [
        ".csproj",
        ".vcxproj",
        ".props",
        ".targets",
        ".cs",
        ".h",
        ".cpp",
        ".mm",
        ".css",
        ".js",
        ".json",
        ".html",
        ".md"
    ];

    public static int Main(string[] args)
    {
        var pathArgument = new Argument<string?>("path")
        {
            Description = "Directory containing the project files.",
            Arity = ArgumentArity.ZeroOrOne
        };

        var extensionsArgument = new Argument<string?>("extensions")
        {
            Description = "Semicolon-separated list of file extensions.",
            Arity = ArgumentArity.ZeroOrOne
        };

        var singleOption = new Option<bool>("--single")
        {
            Description = "Merge all files into a single output file."
        };

        var rootCommand = new RootCommand("Merges project files into text files suitable for AI analysis.");

        rootCommand.Arguments.Add(pathArgument);
        rootCommand.Arguments.Add(extensionsArgument);
        rootCommand.Options.Add(singleOption);

        rootCommand.SetAction(parseResult =>
        {
            var path = parseResult.GetValue(pathArgument) ?? Environment.CurrentDirectory;

            var extensionsValue = parseResult.GetValue(extensionsArgument);

            var extensions = string.IsNullOrWhiteSpace(extensionsValue)
                ? DefaultExtensions
                : extensionsValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var singleOutput = parseResult.GetValue(singleOption);

            ProjectMerger.Merge(path, extensions, singleOutput);
        });

        return rootCommand.Parse(args).Invoke();
    }
}