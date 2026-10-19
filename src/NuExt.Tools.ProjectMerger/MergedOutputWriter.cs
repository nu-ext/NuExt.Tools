using System.Text;

namespace NuExt.Tools;

internal static class MergedOutputWriter
{
    public static void Write(string outputPath, string? contextName, string currentDirectory, List<string> files)
    {
        using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);

        writer.WriteLine($"// ----- Begin Project {contextName} -----");

        foreach (var file in files)
        {
            var relativePath = file[currentDirectory.Length..];

            writer.WriteLine($"// ----- Begin file: {relativePath} -----");
            writer.Write(File.ReadAllText(file));
            writer.WriteLine();
            writer.WriteLine($"// ------ End file {relativePath} ------");
            writer.WriteLine();
        }

        writer.WriteLine($"// ------ End Project {contextName} ------");
    }
}