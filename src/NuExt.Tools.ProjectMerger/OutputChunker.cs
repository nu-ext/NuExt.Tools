namespace NuExt.Tools;

internal static class OutputChunker
{
    private static readonly HashSet<string> s_nativePairs =
    [
        ".cpp|.h",
        ".h|.mm"
    ];

    public static Dictionary<string, List<string>> GetOutputChunks(List<string> files)
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

        return s_nativePairs.Contains(pair);
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