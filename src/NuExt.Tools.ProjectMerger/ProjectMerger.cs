using System.Text;

namespace NuExt.Tools;

internal static class ProjectMerger
{
    private static readonly HashSet<string> NativePairs =
    [
        ".cpp|.h",
        ".h|.mm"
    ];

    public static void Merge(string currentDirectory, string[] extensions, bool singleOutput)
    {
        string[] exclude =
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

        var enabledExts = extensions.Where(extension => !extension.StartsWith('-')).ToList();

        var files = Directory
            .GetFiles(currentDir, "*", SearchOption.AllDirectories)
            .Where(file => !exclude.Any(file.Contains) && enabledExts.Contains(Path.GetExtension(file)))
            .ToList();

        if (files.Count == 0)
        {
            Console.WriteLine("Nothing to merge.");
            return;
        }

        var extensionOrder = enabledExts
            .Select((extension, index) => new { extension, index })
            .ToDictionary(item => item.extension, item => item.index);

        var sortedFiles = files
            .OrderBy(file => extensionOrder[Path.GetExtension(file)])
            .ThenBy(file => file)
            .ToList();

        var fileNames = string.Join(Environment.NewLine, sortedFiles.Select(file => file[currentDir.Length..]));

        Console.WriteLine($"Files[{sortedFiles.Count}]: {fileNames}");

        var outputChunks = singleOutput
            ? new Dictionary<string, List<string>>(StringComparer.Ordinal)
            {
                [""] = sortedFiles
            }
            : GetOutputChunks(sortedFiles)
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

            using (var writer = new StreamWriter(outputPath, false, Encoding.UTF8))
            {
                writer.WriteLine($"// ----- Begin Project {contextName} -----");

                foreach (var file in chunkFiles)
                {
                    var relativePath = file[currentDir.Length..];

                    writer.WriteLine($"// ----- Begin file: {relativePath} -----");
                    writer.Write(File.ReadAllText(file));
                    writer.WriteLine();
                    writer.WriteLine($"// ------ End file {relativePath} ------");
                    writer.WriteLine();
                }

                writer.WriteLine($"// ------ End Project {contextName} ------");
            }

            Console.WriteLine($"{Environment.NewLine}Merged content{Environment.NewLine}" +
                              string.Join(Environment.NewLine, chunkFiles.Select(Path.GetFileName)));

            Console.WriteLine($"written to {outputPath}");
        }
    }

    private static Dictionary<string, List<string>> GetOutputChunks(List<string> files)
    {
        var fileOrder = files
            .Select((file, index) => new { file, index })
            .ToDictionary(item => item.file, item => item.index, StringComparer.Ordinal);

        var chunks = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            var chunkName = GetInitialChunkName(file);

            if (!chunks.TryGetValue(chunkName, out var chunkFiles))
            {
                chunkFiles = [];
                chunks[chunkName] = chunkFiles;
            }

            chunkFiles.Add(file);
        }

        if (!chunks.TryGetValue("", out var commonChunkFiles))
        {
            commonChunkFiles = [];
            chunks[""] = commonChunkFiles;
        }

        bool changed;

        do
        {
            changed = false;

            var keys = chunks.Keys
                .Where(key => key.Length > 0)
                .OrderByDescending(GetChunkDepth)
                .ThenBy(key => key, StringComparer.Ordinal)
                .ToList();

            foreach (var key in keys)
            {
                if (!chunks.TryGetValue(key, out var chunkFiles))
                {
                    continue;
                }

                if (!ShouldPromoteChunk(chunkFiles))
                {
                    continue;
                }

                var parentKey = GetParentChunkName(key) ?? "";

                if (!chunks.TryGetValue(parentKey, out var parentFiles))
                {
                    parentFiles = [];
                    chunks[parentKey] = parentFiles;
                }

                parentFiles.AddRange(chunkFiles);
                chunks.Remove(key);
                changed = true;
            }
        }
        while (changed);

        foreach (var chunkFiles in chunks.Values)
        {
            chunkFiles.Sort((first, second) => fileOrder[first].CompareTo(fileOrder[second]));
        }

        return chunks;
    }

    private static string GetInitialChunkName(string file)
    {
        var fileName = Path.GetFileNameWithoutExtension(file);
        var parts = fileName.Split('.', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return fileName;
        }

        if (parts.Length == 1 && parts[0].EndsWith("Extensions", StringComparison.Ordinal))
        {
            return parts[0][..^"Extensions".Length];
        }

        if (parts.Length == 1 && parts[0].EndsWith("Factory", StringComparison.Ordinal))
        {
            return parts[0][..^"Factory".Length];
        }

        return string.Join('.', parts);
    }

    private static bool ShouldPromoteChunk(List<string> chunkFiles)
    {
        if (chunkFiles.Count == 1)
        {
            return true;
        }

        if (chunkFiles.Count != 2)
        {
            return false;
        }

        var first = chunkFiles[0];
        var second = chunkFiles[1];

        var firstName = Path.GetFileNameWithoutExtension(first);
        var secondName = Path.GetFileNameWithoutExtension(second);

        if (!string.Equals(firstName, secondName, StringComparison.Ordinal))
        {
            return false;
        }

        return IsNativePair(Path.GetExtension(first), Path.GetExtension(second));
    }

    private static bool IsNativePair(string firstExtension, string secondExtension)
    {
        var pair = string.Compare(firstExtension, secondExtension, StringComparison.OrdinalIgnoreCase) <= 0
            ? $"{firstExtension.ToLowerInvariant()}|{secondExtension.ToLowerInvariant()}"
            : $"{secondExtension.ToLowerInvariant()}|{firstExtension.ToLowerInvariant()}";

        return NativePairs.Contains(pair);
    }

    private static string? GetParentChunkName(string chunkName)
    {
        var index = chunkName.LastIndexOf('.');
        return index > 0 ? chunkName[..index] : null;
    }

    private static int GetChunkDepth(string chunkName)
    {
        var depth = 0;

        foreach (var character in chunkName)
        {
            if (character == '.')
            {
                depth++;
            }
        }

        return depth;
    }
}