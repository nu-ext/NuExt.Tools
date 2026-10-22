namespace NuExt.Tools;

internal static class FolderOutputChunker
{
    private const long ChunkSizeThreshold = 128 * 1024;

    public static Dictionary<string, List<string>> GetOutputChunks(string currentDirectory, List<string> files)
    {
        var folderChunks = GetFolderChunks(currentDirectory, files);
        var outputChunks = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var folderPair in folderChunks)
        {
            AddFolderChunks(outputChunks, folderPair.Key, folderPair.Value);
        }

        return outputChunks;
    }

    private static Dictionary<string, List<string>> GetFolderChunks(string currentDirectory, List<string> files)
    {
        var chunks = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(currentDirectory, file);
            var separatorIndex = relativePath.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
            var chunkName = separatorIndex >= 0 ? relativePath[..separatorIndex] : "";

            if (!chunks.TryGetValue(chunkName, out var chunkFiles))
            {
                chunkFiles = [];
                chunks[chunkName] = chunkFiles;
            }

            chunkFiles.Add(file);
        }

        return chunks;
    }

    private static void AddFolderChunks(Dictionary<string, List<string>> outputChunks, string folderName, List<string> files)
    {
        var nestedChunks = OutputChunker.GetOutputChunks(files);
        HashSet<string>? extractedFiles = null;

        foreach (var pair in nestedChunks)
        {
            if (pair.Key.Length == 0 || GetTotalSize(pair.Value) <= ChunkSizeThreshold)
            {
                continue;
            }

            var chunkName = folderName.Length == 0 ? pair.Key : $"{folderName}.{pair.Key}";

            outputChunks.Add(chunkName, pair.Value);

            extractedFiles ??= new HashSet<string>(StringComparer.Ordinal);
            extractedFiles.UnionWith(pair.Value);
        }

        var remainingFiles = extractedFiles is null ? files : files.Where(file => !extractedFiles.Contains(file)).ToList();

        if (remainingFiles.Count > 0)
        {
            outputChunks.Add(folderName, remainingFiles);
        }
    }

    private static long GetTotalSize(List<string> files)
    {
        long totalSize = 0;

        foreach (var file in files)
        {
            totalSize += new FileInfo(file).Length;
        }

        return totalSize;
    }
}