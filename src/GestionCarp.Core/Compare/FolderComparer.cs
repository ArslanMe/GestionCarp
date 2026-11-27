using GestionCarp.Core.Common;
using GestionCarp.Core.Localization;

namespace GestionCarp.Core.Compare;

public enum CompareMode
{
    /// <summary>Mismo nombre y tamaño = iguales (rápido).</summary>
    NameAndSize,
    /// <summary>Además, la misma fecha de modificación.</summary>
    NameSizeAndDate,
    /// <summary>Compara el contenido byte a byte (exacto, más lento).</summary>
    Content,
}

public enum CompareStatus
{
    Equal,
    Different,
    OnlyInLeft,
    OnlyInRight,
    /// <summary>En un lado es carpeta y en el otro archivo.</summary>
    TypeMismatch,
}

public sealed class CompareOptions
{
    public CompareMode Mode { get; set; } = CompareMode.NameAndSize;
    public bool IncludeSubfolders { get; set; } = true;
    public bool IncludeHidden { get; set; }
    public List<string> ExcludePatterns { get; set; } = new();
}

/// <summary>Datos de un lado de la comparación.</summary>
public sealed record CompareSide(string FullPath, bool IsDirectory, long Size, DateTime LastModified, int FileCount);

public sealed class CompareItem
{
    public required string RelativePath { get; init; }
    public required CompareStatus Status { get; init; }
    public CompareSide? Left { get; init; }
    public CompareSide? Right { get; init; }

    /// <summary>Por qué son distintos (tamaño, fecha o contenido).</summary>
    public string? Detail { get; init; }

    public bool IsDirectory => (Left ?? Right)!.IsDirectory;
    public int Depth => RelativePath.Count(c => c == '/');
    public string Name => RelativePath[(RelativePath.LastIndexOf('/') + 1)..];
}

/// <summary>Compara dos carpetas y lista qué está solo en una, qué es igual y qué es distinto.</summary>
public static class FolderComparer
{
    /// <summary>Diferencia de fecha que se tolera (los sistemas FAT guardan con 2 s de precisión).</summary>
    private static readonly TimeSpan DateTolerance = TimeSpan.FromSeconds(2);

    public static List<CompareItem> Compare(string left, string right, CompareOptions options,
        IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(left)) throw new DirectoryNotFoundException(L.F("tree_not_found", left));
        if (!Directory.Exists(right)) throw new DirectoryNotFoundException(L.F("tree_not_found", right));

        var exclude = options.ExcludePatterns.Select(p => new WildcardPattern(p)).ToList();
        var result = new List<CompareItem>();
        int count = 0;
        CompareFolder(left, right, "", options, exclude, result, ref count, progress, cancellationToken);
        progress?.Report(count);
        return result;
    }

    private static void CompareFolder(string left, string right, string relative, CompareOptions options,
        List<WildcardPattern> exclude, List<CompareItem> result, ref int count, IProgress<int>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var leftEntries = List(left, options, exclude);
        var rightEntries = List(right, options, exclude);
        var names = leftEntries.Keys.Union(rightEntries.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => leftEntries.TryGetValue(n, out var l) ? (l is DirectoryInfo ? 0 : 1) : (rightEntries[n] is DirectoryInfo ? 0 : 1))
            .ThenBy(n => n, NaturalStringComparer.Instance);

        foreach (var name in names)
        {
            string path = relative.Length == 0 ? name : relative + "/" + name;
            leftEntries.TryGetValue(name, out var l);
            rightEntries.TryGetValue(name, out var r);
            if (++count % 200 == 0) progress?.Report(count);

            if (l is null || r is null)
            {
                var only = (l ?? r)!;
                result.Add(new CompareItem
                {
                    RelativePath = path,
                    Status = l is null ? CompareStatus.OnlyInRight : CompareStatus.OnlyInLeft,
                    Left = l is null ? null : Side(l),
                    Right = r is null ? null : Side(r),
                });
                continue;
            }

            if (l is DirectoryInfo ld && r is DirectoryInfo rd)
            {
                if (!options.IncludeSubfolders) continue;
                int before = result.Count;
                CompareFolder(ld.FullName, rd.FullName, path, options, exclude, result, ref count, progress, ct);
                bool allEqual = result.Skip(before).All(i => i.Status == CompareStatus.Equal);
                result.Insert(before, new CompareItem
                {
                    RelativePath = path,
                    Status = allEqual ? CompareStatus.Equal : CompareStatus.Different,
                    Left = Side(ld),
                    Right = Side(rd),
                    Detail = allEqual ? null : L.T("cmp_detail_content_inside"),
                });
                continue;
            }

            if (l is FileInfo lf && r is FileInfo rf)
            {
                var (equal, detail) = CompareFiles(lf, rf, options.Mode, ct);
                result.Add(new CompareItem
                {
                    RelativePath = path,
                    Status = equal ? CompareStatus.Equal : CompareStatus.Different,
                    Left = Side(lf),
                    Right = Side(rf),
                    Detail = detail,
                });
                continue;
            }

            result.Add(new CompareItem
            {
                RelativePath = path,
                Status = CompareStatus.TypeMismatch,
                Left = Side(l),
                Right = Side(r),
                Detail = L.T("cmp_detail_type"),
            });
        }
    }

    private static (bool Equal, string? Detail) CompareFiles(FileInfo left, FileInfo right, CompareMode mode, CancellationToken ct)
    {
        if (left.Length != right.Length) return (false, L.T("cmp_detail_size"));
        if (mode == CompareMode.NameSizeAndDate && (left.LastWriteTimeUtc - right.LastWriteTimeUtc).Duration() > DateTolerance)
            return (false, L.T("cmp_detail_date"));
        if (mode == CompareMode.Content)
        {
            try
            {
                if (!SameContent(left.FullName, right.FullName, ct)) return (false, L.T("cmp_detail_content"));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return (false, ex.Message);
            }
        }
        return (true, null);
    }

    /// <summary>Compara byte a byte, deteniéndose en la primera diferencia.</summary>
    public static bool SameContent(string a, string b, CancellationToken ct = default)
    {
        const int size = 1 << 16;
        using var fa = new FileStream(a, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, size, FileOptions.SequentialScan);
        using var fb = new FileStream(b, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, size, FileOptions.SequentialScan);
        if (fa.Length != fb.Length) return false;
        var ba = new byte[size];
        var bb = new byte[size];
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            int ra = fa.ReadAtLeast(ba, size, throwOnEndOfStream: false);
            int rb = fb.ReadAtLeast(bb, size, throwOnEndOfStream: false);
            if (ra != rb) return false;
            if (ra == 0) return true;
            if (!ba.AsSpan(0, ra).SequenceEqual(bb.AsSpan(0, rb))) return false;
        }
    }

    private static Dictionary<string, FileSystemInfo> List(string folder, CompareOptions options, List<WildcardPattern> exclude)
    {
        var result = new Dictionary<string, FileSystemInfo>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", new EnumerationOptions
                     {
                         IgnoreInaccessible = true,
                         AttributesToSkip = options.IncludeHidden ? 0 : FileAttributes.Hidden | FileAttributes.System,
                     }))
            {
                if (exclude.Any(p => p.IsMatch(entry.Name))) continue;
                if (entry is DirectoryInfo && entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                result[entry.Name] = entry;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return result;
    }

    private static CompareSide Side(FileSystemInfo info) => info switch
    {
        FileInfo f => new CompareSide(f.FullName, false, f.Length, f.LastWriteTime, 1),
        DirectoryInfo d => new CompareSide(d.FullName, true, FolderSize(d, out int files), d.LastWriteTime, files),
        _ => throw new ArgumentOutOfRangeException(nameof(info)),
    };

    private static long FolderSize(DirectoryInfo dir, out int files)
    {
        long size = 0;
        files = 0;
        try
        {
            foreach (var f in dir.EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }))
            {
                size += f.Length;
                files++;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return size;
    }
}
