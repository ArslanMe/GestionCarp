namespace GestionCarp.Core.Rename;

public enum RenameStatus
{
    /// <summary>El nombre no cambia.</summary>
    Unchanged,
    /// <summary>Listo para renombrar.</summary>
    Ready,
    /// <summary>Se puede renombrar, pero hay algo a tener en cuenta.</summary>
    Warning,
    /// <summary>No se puede renombrar (nombre inválido, duplicado...).</summary>
    Error,
    /// <summary>Se omite a propósito (por ejemplo, un subtítulo sin vídeo).</summary>
    Skipped,
}

/// <summary>Resultado previsto para un elemento: nombre actual → nombre nuevo.</summary>
public sealed class RenamePreviewRow
{
    public RenamePreviewRow(string sourcePath, bool isDirectory, string targetPath)
    {
        SourcePath = sourcePath;
        IsDirectory = isDirectory;
        TargetPath = targetPath;
        GroupKey = sourcePath;
        Status = string.Equals(sourcePath, targetPath, StringComparison.Ordinal) ? RenameStatus.Unchanged : RenameStatus.Ready;
    }

    public string SourcePath { get; }
    public bool IsDirectory { get; }
    public string TargetPath { get; }

    public string OriginalName => Path.GetFileName(SourcePath);
    public string NewName => Path.GetFileName(TargetPath);

    public RenameStatus Status { get; set; }
    public string? Message { get; set; }

    /// <summary>Elementos con la misma clave se renombran juntos o no se renombra ninguno (vídeo + sus subtítulos).</summary>
    public string GroupKey { get; set; }

    /// <summary>Es un subtítulo que sigue el nombre de su vídeo.</summary>
    public bool IsCompanion { get; set; }

    /// <summary>Nombre del vídeo asociado, en el caso de subtítulos.</summary>
    public string? RelatedName { get; set; }

    /// <summary>Se va a mover/renombrar realmente.</summary>
    public bool IsMoving => Status is RenameStatus.Ready or RenameStatus.Warning
                            && !string.Equals(SourcePath, TargetPath, StringComparison.Ordinal);

    public RenameOperation ToOperation() => new(SourcePath, TargetPath, IsDirectory);

    public void SetError(string message)
    {
        Status = RenameStatus.Error;
        Message = message;
    }
}
