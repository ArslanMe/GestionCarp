using GestionCarp.Core.Common;

namespace GestionCarp.Core.Duplicates;

public sealed class DuplicateOptions
{
    /// <summary>Tamaño mínimo en bytes (los archivos vacíos nunca se cuentan).</summary>
    public long MinSize { get; set; } = 1;
    public bool IncludeHidden { get; set; }
    public List<string> FilePatterns { get; set; } = new();
    public List<string> ExcludePatterns { get; set; } = new();
}

public sealed record DuplicateFile(string Path, long Size, DateTime LastModified);

/// <summary>Archivos con exactamente el mismo contenido.</summary>
public sealed class DuplicateGroup
{
    public required string Hash { get; init; }
    public required long Size { get; init; }
    public required List<DuplicateFile> Files { get; init; }

    /// <summary>Espacio que se liberaría dejando una sola copia.</summary>
    public long WastedBytes => Size * (Files.Count - 1);
}

public sealed record DuplicateProgress(string Stage, int Done, int Total);

/// <summary>
/// Busca archivos duplicados por contenido: primero agrupa por tamaño, luego compara una huella del
/// comienzo y, solo si coincide, la huella completa. Así no hace falta leer todos los archivos enteros.
/// </summary>
public static class DuplicateFinder
{
    private const long PartialBytes = 64 * 1024;

    public static List<DuplicateGroup> Find(IEnumerable<string> roots, DuplicateOptions options,
        IProgress<DuplicateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var include = options.FilePatterns.Select(p => new WildcardPattern(p)).ToList();
        var exclude = options.ExcludePatterns.Select(p => new WildcardPattern(p)).ToList();
        var files = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots.Where(Directory.Exists))
            Collect(new DirectoryInfo(root), options, include, exclude, files, progress, cancellationToken);

        var sameSize = files.Values
            .Where(f => f.Length >= Math.Max(1, options.MinSize))
            .GroupBy(f => f.Length)
            .Where(g => g.Count() > 1)
            .ToList();

        int total = sameSize.Sum(g => g.Count());
        int done = 0;
        var groups = new List<DuplicateGroup>();

        foreach (var bySize in sameSize)
        {
            // 1) huella de los primeros 64 KB; 2) huella completa solo si hace falta.
            var byPartial = HashGroups(bySize, f => FileHasher.Hash(f.FullName, PartialBytes, cancellationToken));
            foreach (var candidates in byPartial)
            {
                var byFull = bySize.Key <= PartialBytes
                    ? new List<(string Hash, List<FileInfo> Files)> { candidates }
                    : HashGroups(candidates.Files, f => FileHasher.Hash(f.FullName, cancellationToken));

                foreach (var (hash, dupes) in byFull)
                {
                    groups.Add(new DuplicateGroup
                    {
                        Hash = hash,
                        Size = bySize.Key,
                        Files = dupes
                            .OrderBy(f => f.LastWriteTime)
                            .ThenBy(f => f.FullName, NaturalStringComparer.Instance)
                            .Select(f => new DuplicateFile(f.FullName, f.Length, f.LastWriteTime))
                            .ToList(),
                    });
                }
            }
            done += bySize.Count();
            progress?.Report(new DuplicateProgress("hash", done, total));
        }

        return groups.OrderByDescending(g => g.WastedBytes).ThenBy(g => g.Files[0].Path, NaturalStringComparer.Instance).ToList();
    }

    private static List<(string Hash, List<FileInfo> Files)> HashGroups(IEnumerable<FileInfo> files, Func<FileInfo, string> hasher)
    {
        var byHash = new Dictionary<string, List<FileInfo>>();
        foreach (var file in files)
        {
            string hash;
            try { hash = hasher(file); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            if (!byHash.TryGetValue(hash, out var list)) byHash[hash] = list = new List<FileInfo>();
            list.Add(file);
        }
        return byHash.Where(kv => kv.Value.Count > 1).Select(kv => (kv.Key, kv.Value)).ToList();
    }

    private static void Collect(DirectoryInfo dir, DuplicateOptions options, List<WildcardPattern> include, List<WildcardPattern> exclude,
        Dictionary<string, FileInfo> files, IProgress<DuplicateProgress>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IEnumerable<FileSystemInfo> entries;
        try
        {
            entries = dir.EnumerateFileSystemInfos("*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = options.IncludeHidden ? 0 : FileAttributes.Hidden | FileAttributes.System,
            }).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var entry in entries)
        {
            if (exclude.Any(p => p.IsMatch(entry.Name))) continue;
            if (entry is DirectoryInfo sub)
            {
                if (!sub.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    Collect(sub, options, include, exclude, files, progress, ct);
            }
            else if (entry is FileInfo file && (include.Count == 0 || include.Any(p => p.IsMatch(file.Name))))
            {
                files.TryAdd(file.FullName, file);
                if (files.Count % 500 == 0) progress?.Report(new DuplicateProgress("scan", files.Count, 0));
            }
        }
    }
}
