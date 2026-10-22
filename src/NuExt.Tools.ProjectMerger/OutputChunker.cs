namespace NuExt.Tools;

internal static class OutputChunker
{
    private readonly record struct ChunkCandidate(string Name, bool RequiresExistingFile);

    private sealed record FileChunkCandidates(string File, List<ChunkCandidate> Candidates);

    private static readonly HashSet<string> s_nativePairs =
    [
        ".cpp|.h",
        ".h|.mm"
    ];

    public static Dictionary<string, List<string>> GetOutputChunks(List<string> files)
    {
        var fileOrder = files
            .Select((file, index) => new { File = file, Index = index })
            .ToDictionary(item => item.File, item => item.Index, StringComparer.Ordinal);

        var fileCandidates = files
            .Select(file => new FileChunkCandidates(file, GetChunkCandidates(Path.GetFileNameWithoutExtension(file))))
            .ToList();

        var existingNames = files
            .Select(static file => Path.GetFileNameWithoutExtension(file)!)
            .ToHashSet(StringComparer.Ordinal);

        var candidateCounts = GetCandidateCounts(fileCandidates);
        var chunks = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var item in fileCandidates)
        {
            var chunkName = GetChunkName(item.Candidates, existingNames, candidateCounts);

            if (!chunks.TryGetValue(chunkName, out var chunkFiles))
            {
                chunkFiles = [];
                chunks[chunkName] = chunkFiles;
            }

            chunkFiles.Add(item.File);
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

    private static Dictionary<string, int> GetCandidateCounts(List<FileChunkCandidates> fileCandidates)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var item in fileCandidates)
        {
            foreach (var candidate in item.Candidates.DistinctBy(candidate => candidate.Name))
            {
                counts.TryGetValue(candidate.Name, out var count);
                counts[candidate.Name] = count + 1;
            }
        }

        return counts;
    }

    private static List<ChunkCandidate> GetChunkCandidates(string fileName)
    {
        var parts = fileName.Split('.', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return [new ChunkCandidate(fileName, false)];
        }

        var candidates = new List<ChunkCandidate>();
        var firstPart = parts[0];
        var baseName = GetBaseName(firstPart);

        if (!string.Equals(baseName, firstPart, StringComparison.Ordinal))
        {
            candidates.Add(new ChunkCandidate(baseName, true));
        }

        candidates.Add(new ChunkCandidate(firstPart, false));

        for (var i = 1; i < parts.Length; i++)
        {
            candidates.Add(new ChunkCandidate(string.Join('.', parts, 0, i + 1), false));
        }

        return candidates;
    }

    private static string GetBaseName(string name)
    {
        if (name.EndsWith("Extensions", StringComparison.Ordinal))
        {
            return name[..^"Extensions".Length];
        }

        if (name.EndsWith("Factory", StringComparison.Ordinal))
        {
            return name[..^"Factory".Length];
        }

        return name;
    }

    private static string GetChunkName(List<ChunkCandidate> candidates, HashSet<string> existingNames, Dictionary<string, int> candidateCounts)
    {
        foreach (var candidate in candidates)
        {
            if (existingNames.Contains(candidate.Name))
            {
                return candidate.Name;
            }

            if (!candidate.RequiresExistingFile && candidateCounts.TryGetValue(candidate.Name, out var count) && count > 1)
            {
                return candidate.Name;
            }
        }

        return candidates[^1].Name;
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