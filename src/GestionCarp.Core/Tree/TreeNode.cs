namespace GestionCarp.Core.Tree;

/// <summary>Un elemento (carpeta o archivo) del árbol analizado.</summary>
public sealed class TreeNode
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public bool IsDirectory { get; init; }
    public DateTime LastModified { get; init; }

    /// <summary>Tamaño en bytes. En carpetas, la suma de los archivos analizados dentro.</summary>
    public long Size { get; set; }

    /// <summary>Número de archivos dentro (recursivo, solo carpetas).</summary>
    public int FileCount { get; set; }

    /// <summary>Número de subcarpetas dentro (recursivo, solo carpetas).</summary>
    public int FolderCount { get; set; }

    /// <summary>La carpeta tiene contenido que no se analizó por el límite de profundidad.</summary>
    public bool IsTruncated { get; set; }

    /// <summary>Mensaje si no se pudo leer la carpeta (por ejemplo, acceso denegado).</summary>
    public string? Error { get; set; }

    public List<TreeNode> Children { get; } = new();
}
