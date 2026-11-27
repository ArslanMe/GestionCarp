using GestionCarp.Core.Localization;
using GestionCarp.Core.Rename;

namespace GestionCarp.Core.Compare;

/// <summary>
/// Copia a una carpeta los elementos que le faltan, sin sobrescribir nunca nada.
/// Queda registrado para poder deshacerlo (se borran las copias si siguen intactas).
/// </summary>
public static class FolderCopier
{
    /// <param name="items">Pares (origen, destino) de archivos o carpetas que no existen en el destino.</param>
    public static RenameResult CopyMissing(IReadOnlyList<(string Source, string Target)> items, string description,
        IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var createdFolders = new List<string>();
        var copiedFiles = new List<CopiedFile>();
        int done = 0;

        try
        {
            foreach (var (source, target) in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Directory.Exists(source))
                    CopyFolder(source, target, createdFolders, copiedFiles, cancellationToken);
                else
                    CopyFile(source, target, createdFolders, copiedFiles);
                progress?.Report(++done);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            var partial = new RenameBatch { CreatedPaths = createdFolders, CopiedFiles = copiedFiles };
            Undo(partial);
            return new RenameResult { Success = false, Error = L.F("cmp_copy_failed", ex.Message) };
        }

        return new RenameResult
        {
            Success = true,
            Batch = new RenameBatch { Description = description, Timestamp = DateTime.Now, CreatedPaths = createdFolders, CopiedFiles = copiedFiles },
        };
    }

    private static void CopyFolder(string source, string target, List<string> createdFolders, List<CopiedFile> copiedFiles, CancellationToken ct)
    {
        EnsureFolder(target, createdFolders);
        foreach (var dir in Directory.EnumerateDirectories(source))
        {
            ct.ThrowIfCancellationRequested();
            CopyFolder(dir, Path.Combine(target, Path.GetFileName(dir)), createdFolders, copiedFiles, ct);
        }
        foreach (var file in Directory.EnumerateFiles(source))
        {
            ct.ThrowIfCancellationRequested();
            CopyFile(file, Path.Combine(target, Path.GetFileName(file)), createdFolders, copiedFiles);
        }
    }

    private static void CopyFile(string source, string target, List<string> createdFolders, List<CopiedFile> copiedFiles)
    {
        EnsureFolder(Path.GetDirectoryName(target)!, createdFolders);
        if (File.Exists(target) || Directory.Exists(target)) return; // nunca sobrescribir
        File.Copy(source, target, overwrite: false);
        copiedFiles.Add(new CopiedFile(target, new FileInfo(target).Length));
    }

    private static void EnsureFolder(string folder, List<string> createdFolders)
    {
        if (Directory.Exists(folder)) return;
        var missing = new Stack<string>();
        string? current = folder;
        while (current is not null && !Directory.Exists(current))
        {
            missing.Push(current);
            current = Path.GetDirectoryName(current);
        }
        while (missing.Count > 0)
        {
            string dir = missing.Pop();
            Directory.CreateDirectory(dir);
            createdFolders.Add(dir);
        }
    }

    /// <summary>Borra las copias que sigan iguales (mismo tamaño) y las carpetas creadas que sigan vacías.</summary>
    public static int Undo(RenameBatch batch)
    {
        int kept = 0;
        for (int i = batch.CopiedFiles.Count - 1; i >= 0; i--)
        {
            var copy = batch.CopiedFiles[i];
            try
            {
                if (!File.Exists(copy.Path)) continue;
                if (new FileInfo(copy.Path).Length == copy.Length) File.Delete(copy.Path);
                else kept++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                kept++;
            }
        }
        for (int i = batch.CreatedPaths.Count - 1; i >= 0; i--)
        {
            string dir = batch.CreatedPaths[i];
            try
            {
                if (!Directory.Exists(dir)) continue;
                if (Directory.EnumerateFileSystemEntries(dir).Any()) kept++;
                else Directory.Delete(dir);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                kept++;
            }
        }
        return kept;
    }
}
