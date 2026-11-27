using GestionCarp.Core.Localization;
using GestionCarp.Core.Rename;

namespace GestionCarp.Core.Structure;

public enum StructureStatus
{
    /// <summary>Se creará.</summary>
    Create,
    /// <summary>Ya existe: no se toca.</summary>
    Exists,
    /// <summary>No se creará (archivos desactivados, o falla su carpeta).</summary>
    Skipped,
    /// <summary>Nombre no válido o conflicto.</summary>
    Error,
}

public sealed class StructurePlanItem
{
    public required StructureEntry Entry { get; init; }
    public required string FullPath { get; init; }
    public StructureStatus Status { get; set; }
    public string? Message { get; set; }
}

/// <summary>Planifica y crea carpetas (y archivos vacíos) a partir de una estructura, con opción de deshacer.</summary>
public static class StructureBuilder
{
    public static List<StructurePlanItem> Plan(IReadOnlyList<StructureEntry> entries, string destination, bool createFiles)
    {
        var plan = new List<StructurePlanItem>();
        var failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            string fullPath = Path.Combine(new[] { destination }.Concat(entry.RelativePath.Split('/')).ToArray());
            var item = new StructurePlanItem { Entry = entry, FullPath = fullPath, Status = StructureStatus.Create };
            plan.Add(item);

            int cut = entry.RelativePath.LastIndexOf('/');
            string parent = cut > 0 ? entry.RelativePath[..cut] : "";

            if (parent.Length > 0 && failed.Contains(parent))
            {
                item.Status = StructureStatus.Skipped;
                item.Message = L.T("struct_parent_failed");
                failed.Add(entry.RelativePath);
            }
            else if (FileNameValidator.Validate(entry.Name) is { } error)
            {
                item.Status = StructureStatus.Error;
                item.Message = error;
                failed.Add(entry.RelativePath);
            }
            else if (!seen.Add(entry.RelativePath))
            {
                item.Status = StructureStatus.Skipped;
                item.Message = L.T("struct_repeated");
            }
            else if (entry.IsDirectory && File.Exists(fullPath))
            {
                item.Status = StructureStatus.Error;
                item.Message = L.T("struct_file_in_the_way");
                failed.Add(entry.RelativePath);
            }
            else if (!entry.IsDirectory && Directory.Exists(fullPath))
            {
                item.Status = StructureStatus.Error;
                item.Message = L.T("struct_folder_in_the_way");
            }
            else if (Directory.Exists(fullPath) || File.Exists(fullPath))
            {
                item.Status = StructureStatus.Exists;
                item.Message = L.T("struct_exists");
            }
            else if (!entry.IsDirectory && !createFiles)
            {
                item.Status = StructureStatus.Skipped;
                item.Message = L.T("struct_files_off");
            }
            else if (fullPath.Length > FileNameValidator.MaxPathLength)
            {
                item.Message = L.T("plan_long_path");
            }
        }
        return plan;
    }

    /// <summary>Crea lo planificado. Si algo falla, borra lo que llegó a crear.</summary>
    public static RenameResult Create(IReadOnlyList<StructurePlanItem> plan, string description)
    {
        var created = new List<string>();
        try
        {
            foreach (var item in plan.Where(p => p.Status == StructureStatus.Create))
            {
                if (item.Entry.IsDirectory)
                {
                    if (Directory.Exists(item.FullPath)) continue;
                    Directory.CreateDirectory(item.FullPath);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(item.FullPath)!);
                    using (new FileStream(item.FullPath, FileMode.CreateNew)) { }
                }
                created.Add(item.FullPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            RemoveCreated(created);
            return new RenameResult { Success = false, Error = L.F("struct_failed", ex.Message) };
        }

        return new RenameResult
        {
            Success = true,
            Batch = new RenameBatch { Description = description, Timestamp = DateTime.Now, CreatedPaths = created },
        };
    }

    /// <summary>
    /// Deshace una creación: borra en orden inverso las carpetas vacías y los archivos vacíos creados.
    /// Lo que ya tenga contenido se conserva, para no perder nada.
    /// </summary>
    public static RenameResult Undo(RenameBatch batch)
    {
        int kept = RemoveCreated(batch.CreatedPaths);
        return new RenameResult
        {
            Success = true,
            Batch = new RenameBatch { Description = L.F("exec_undo_prefix", batch.Description) },
            Notice = kept > 0 ? L.F("struct_undo_kept", kept) : null,
        };
    }

    private static int RemoveCreated(IReadOnlyList<string> created)
    {
        int kept = 0;
        for (int i = created.Count - 1; i >= 0; i--)
        {
            string path = created[i];
            try
            {
                if (Directory.Exists(path))
                {
                    if (Directory.EnumerateFileSystemEntries(path).Any()) kept++;
                    else Directory.Delete(path);
                }
                else if (File.Exists(path))
                {
                    if (new FileInfo(path).Length > 0) kept++;
                    else File.Delete(path);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                kept++;
            }
        }
        return kept;
    }
}
