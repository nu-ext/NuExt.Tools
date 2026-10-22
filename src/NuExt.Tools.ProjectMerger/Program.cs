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
        ".xaml",
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

        var folderOption = new Option<bool>("--folder")
        {
            Description = "Merge files by their first-level directory and split named chunks larger than 128 KB into separate outputs."
        };

        var rootCommand = new RootCommand("Merges project files into text files suitable for AI analysis.");

        rootCommand.Arguments.Add(pathArgument);
        rootCommand.Arguments.Add(extensionsArgument);
        rootCommand.Options.Add(singleOption);
        rootCommand.Options.Add(folderOption);

        rootCommand.SetAction(parseResult =>
        {
            try
            {
                var path = parseResult.GetValue(pathArgument) ?? Environment.CurrentDirectory;
                var extensionsValue = parseResult.GetValue(extensionsArgument);

                var extensions = string.IsNullOrWhiteSpace(extensionsValue)
                    ? DefaultExtensions
                    : extensionsValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                var singleOutput = parseResult.GetValue(singleOption);
                var folderOutput = parseResult.GetValue(folderOption);

                if (singleOutput && folderOutput)
                {
                    Console.Error.WriteLine("Options --single and --folder cannot be used together.");
                    return 1;
                }

                ProjectMerger.Merge(path, extensions, singleOutput, folderOutput);
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.Message);
                return 1;
            }
        });

        return rootCommand.Parse(args).Invoke();
    }
}