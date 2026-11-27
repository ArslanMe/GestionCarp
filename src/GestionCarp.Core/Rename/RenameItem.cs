namespace GestionCarp.Core.Rename;

/// <summary>Un archivo o carpeta de la lista del renombrador.</summary>
public sealed record RenameItem(string FullPath, bool IsDirectory)
{
    public string FullPath { get; } = Path.TrimEndingDirectorySeparator(FullPath);

    public string Name => Path.GetFileName(FullPath);

    public string Directory => Path.GetDirectoryName(FullPath) ?? "";

    public static RenameItem FromPath(string path) => new(path, System.IO.Directory.Exists(path));
}
