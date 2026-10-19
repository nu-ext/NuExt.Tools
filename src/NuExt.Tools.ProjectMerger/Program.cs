using System.Text;

namespace NuExt.Tools.ProjectMerger;

internal class Program
{
    static void Main(string[] args)
    {
        var singleOutput = args.Any(arg => string.Equals(arg, "--single", StringComparison.OrdinalIgnoreCase));

        var positionalArgs = args.Where(arg => !arg.StartsWith("--", StringComparison.Ordinal)).ToArray();

        string[] extensions = positionalArgs.Length > 1 && !string.IsNullOrWhiteSpace(positionalArgs[1])
            ? positionalArgs[1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [".csproj", ".vcxproj", ".props", ".targets", ".cs", ".h", ".cpp", ".mm", ".css", ".js", ".json", ".html", ".md"];

        string[] exclude = [".g.", "\\.vs\\", "\\bin\\", "\\obj\\", "\\bin-", "\\obj-", "\\Dependencies", "\\build\\"];

        var currentDir = positionalArgs.Length > 0 ? Path.GetFullPath(positionalArgs[0]) : AppContext.BaseDirectory;

        if (!Directory.Exists(currentDir))
        {
            Console.WriteLine($"Directory does not exist: {currentDir}");
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

        var enabledExts = extensions.Where(ext => !ext.StartsWith('-')).ToList();
        var files = Directory.GetFiles(currentDir, "*", SearchOption.AllDirectories).Where(f => !exclude.Any(ex => f.Contains(ex)) && enabledExts.Contains(Path.GetExtension(f))).ToList();

        if (files.Count == 0)
        {
            Console.WriteLine($"Nothing to merge.");
            return;
        }

        var extensionOrder = enabledExts.Select((ext, index) => new { ext, index }).ToDictionary(x => x.ext, x => x.index);
        var sortedFiles = files.OrderBy(f => extensionOrder[Path.GetExtension(f)]).ThenBy(f => f).ToList();

        var fileNames = string.Join(Environment.NewLine, sortedFiles.Select(f => f.Substring(currentDir.Length)));
        Console.WriteLine($"Files[{sortedFiles.Count}]: {fileNames}");

        var outputChunks = singleOutput
            ? new Dictionary<string, List<string>>(StringComparer.Ordinal) { [""] = sortedFiles }
            : GetOutputChunks(sortedFiles)
                .OrderBy(pair => pair.Key.Length == 0 ? 1 : 0)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        int index = 0;
        foreach (var pair in outputChunks)
        {
            var chunkName = pair.Key;
            var chunkFiles = pair.Value;

            string outputPath = Path.Combine(Environment.CurrentDirectory, $"[{contextName} {++index:00}]{(chunkName.Length > 0 ? " " + chunkName : chunkName)}-merged.txt");

            using (var writer = new StreamWriter(outputPath, false, Encoding.UTF8))
            {
                writer.WriteLine($"// ----- Begin Project {contextName} -----");

                foreach (var file in chunkFiles)
                {
                    string relativePath = file.Substring(currentDir.Length);
                    writer.WriteLine($"// ----- Begin file: {relativePath} -----");

                    string content = File.ReadAllText(file);
                    writer.Write(content);
                    writer.WriteLine();

                    writer.WriteLine($"// ------ End file {relativePath} ------");
                    writer.WriteLine();
                }

                writer.WriteLine($"// ------ End Project {contextName} ------");
            }

            Console.WriteLine($"{Environment.NewLine}Merged content{Environment.NewLine}{string.Join(Environment.NewLine, chunkFiles.Select(f => Path.GetFileName(f)))}");
            Console.WriteLine($"written to {outputPath}");

        }

        static Dictionary<string, List<string>> GetOutputChunks(List<string> files)
        {
            var fileOrder = files
                .Select((file, index) => new { file, index })
                .ToDictionary(x => x.file, x => x.index, StringComparer.Ordinal);

            var dict = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var file in files)
            {
                var chunkName = GetInitialChunkName(file);

                if (!dict.TryGetValue(chunkName, out var chunkFiles))
                {
                    chunkFiles = [];
                    dict[chunkName] = chunkFiles;
                }

                chunkFiles.Add(file);
            }

            if (!dict.TryGetValue("", out var commonChunkFiles))
            {
                commonChunkFiles = [];
                dict[""] = commonChunkFiles;
            }

            bool changed;
            do
            {
                changed = false;

                var keys = dict.Keys
                    .Where(key => key.Length > 0)
                    .OrderByDescending(GetChunkDepth)
                    .ThenBy(key => key, StringComparer.Ordinal)
                    .ToList();

                foreach (var key in keys)
                {
                    if (!dict.TryGetValue(key, out var chunkFiles))
                        continue;

                    if (!ShouldPromoteChunk(chunkFiles))
                        continue;

                    var parentKey = GetParentChunkName(key) ?? "";

                    if (!dict.TryGetValue(parentKey, out var parentFiles))
                    {
                        parentFiles = [];
                        dict[parentKey] = parentFiles;
                    }

                    parentFiles.AddRange(chunkFiles);
                    dict.Remove(key);

                    changed = true;
                }
            }
            while (changed);

            foreach (var chunkFiles in dict.Values)
            {
                chunkFiles.Sort((x, y) => fileOrder[x].CompareTo(fileOrder[y]));
            }

            return dict;

            static string GetInitialChunkName(string file)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var parts = fileName.Split('.', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 0)
                    return fileName;

                if (parts.Length == 1 && parts[0].EndsWith("Extensions", StringComparison.Ordinal))
                    return parts[0][..^"Extensions".Length];

                if (parts.Length == 1 && parts[0].EndsWith("Factory", StringComparison.Ordinal))
                    return parts[0][..^"Factory".Length];

                return string.Join('.', parts);
            }

            static bool ShouldPromoteChunk(List<string> chunkFiles)
            {
                if (chunkFiles.Count == 1)
                    return true;

                if (chunkFiles.Count != 2)
                    return false;

                var first = chunkFiles[0];
                var second = chunkFiles[1];

                var firstName = Path.GetFileNameWithoutExtension(first);
                var secondName = Path.GetFileNameWithoutExtension(second);

                if (!string.Equals(firstName, secondName, StringComparison.Ordinal))
                    return false;

                var firstExt = Path.GetExtension(first);
                var secondExt = Path.GetExtension(second);

                return IsNativePair(firstExt, secondExt);
            }

            static bool IsNativePair(string firstExt, string secondExt)
            {
                return
                    string.Equals(firstExt, ".h", StringComparison.OrdinalIgnoreCase) &&
                    (string.Equals(secondExt, ".cpp", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(secondExt, ".mm", StringComparison.OrdinalIgnoreCase))
                    ||
                    string.Equals(secondExt, ".h", StringComparison.OrdinalIgnoreCase) &&
                    (string.Equals(firstExt, ".cpp", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(firstExt, ".mm", StringComparison.OrdinalIgnoreCase));
            }

            static string? GetParentChunkName(string chunkName)
            {
                var index = chunkName.LastIndexOf('.');
                return index > 0 ? chunkName[..index] : null;
            }

            static int GetChunkDepth(string chunkName)
            {
                var depth = 0;

                foreach (var ch in chunkName)
                {
                    if (ch == '.')
                        depth++;
                }

                return depth;
            }
        }
    }
}
