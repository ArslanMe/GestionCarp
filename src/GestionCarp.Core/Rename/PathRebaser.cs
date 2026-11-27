namespace GestionCarp.Core.Rename;

public static class PathRebaser
{
    /// <summary>Calcula dónde está ahora una ruta tras aplicar las operaciones (en su orden de ejecución).</summary>
    public static string Rebase(string path, IEnumerable<RenameOperation> executedInOrder)
    {
        foreach (var op in executedInOrder)
        {
            if (string.Equals(path, op.SourcePath, StringComparison.OrdinalIgnoreCase))
            {
                path = op.TargetPath;
            }
            else if (op.IsDirectory && path.Length > op.SourcePath.Length
                     && path.StartsWith(op.SourcePath, StringComparison.OrdinalIgnoreCase)
                     && (path[op.SourcePath.Length] == Path.DirectorySeparatorChar || path[op.SourcePath.Length] == Path.AltDirectorySeparatorChar))
            {
                path = op.TargetPath + path[op.SourcePath.Length..];
            }
        }
        return path;
    }
}
