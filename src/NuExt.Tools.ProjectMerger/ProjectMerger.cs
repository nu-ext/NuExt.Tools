namespace NuExt.Tools;

internal static class ProjectMerger
{
    public static void Merge(string currentDirectory, string[] extensions, bool singleOutput)
    {
        var currentDir = Path.GetFullPath(currentDirectory);

        if (!Directory.Exists(currentDir))
        {
            Console.Error.WriteLine($"Directory does not exist: {currentDir}");
            return;
        }

        var contextName = Path.GetFileName(currentDir);

        if (contextName == "src")
        {
            contextName = Path.GetFileName(Path.GetDirectoryName(currentDir));
        }
        else if (contextName == "tests")
        {
            contextName = Path.GetFileName(Path.GetDirectoryName(currentDir)) + ".Tests";
        }

        if (!currentDir.EndsWith(Path.DirectorySeparatorChar))
        {
            currentDir += Path.DirectorySeparatorChar;
        }

        Console.WriteLine($"Directory: {currentDir}, Context: {contextName}");

        var files = ProjectFileCollector.GetFiles(currentDir, extensions);
        if (files.Count == 0)
        {
            Console.WriteLine("Nothing to merge.");
            return;
        }

        var fileNames = string.Join(Environment.NewLine, files.Select(file => file[currentDir.Length..]));
        Console.WriteLine($"Files[{files.Count}]: {fileNames}");

        var outputChunks = singleOutput
            ? new Dictionary<string, List<string>>(StringComparer.Ordinal)
            {
                [""] = files
            }
            : OutputChunker.GetOutputChunks(files)
                .OrderBy(pair => pair.Key.Length == 0 ? 1 : 0)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        var index = 0;
        foreach (var pair in outputChunks)
        {
            var chunkName = pair.Key;
            var chunkFiles = pair.Value;

            var outputPath = Path.Combine(Environment.CurrentDirectory,
                $"[{contextName} {++index:00}]" +
                $"{(chunkName.Length > 0 ? " " + chunkName : chunkName)}-merged.txt");

            MergedOutputWriter.Write(outputPath, contextName, currentDir, chunkFiles);

            Console.WriteLine($"{Environment.NewLine}Merged content{Environment.NewLine}" +
                              string.Join(Environment.NewLine, chunkFiles.Select(Path.GetFileName)));

            Console.WriteLine($"written to {outputPath}");
        }
    }
}