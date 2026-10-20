namespace NuExt.Tools;

internal static class FolderOutputChunker
{
    public static Dictionary<string, List<string>> GetOutputChunks(string currentDirectory, List<string> files)
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
}