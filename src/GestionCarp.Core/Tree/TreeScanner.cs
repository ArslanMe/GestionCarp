using GestionCarp.Core.Common;

namespace GestionCarp.Core.Tree;

/// <summary>Recorre una carpeta y construye el árbol de su contenido.</summary>
public static class TreeScanner
{
    private const int ProgressInterval = 250;

    public static TreeNode Scan(string rootPath, TreeScanOptions options,
        IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var root = new DirectoryInfo(rootPath);
        if (!root.Exists)
            throw new DirectoryNotFoundException($"No existe la carpeta: {rootPath}");

        var state = new ScanState(options, progress, cancellationToken);
        var node = CreateDirectoryNode(root, isRoot: true);
        ScanDirectory(root, node, depth: 1, state);
        progress?.Report(state.Count);
        return node;
    }

    private static TreeNode CreateDirectoryNode(DirectoryInfo dir, bool isRoot = false) => new()
    {
        Name = isRoot && dir.Parent is null ? dir.FullName : dir.Name,
        FullPath = dir.FullName,
        IsDirectory = true,
        LastModified = SafeLastWrite(dir),
    };

    private static void ScanDirectory(DirectoryInfo dir, TreeNode node, int depth, ScanState state)
    {
        state.Token.ThrowIfCancellationRequested();

        if (state.Options.MaxDepth > 0 && depth > state.Options.MaxDepth)
        {
            node.IsTruncated = HasAnyEntry(dir);
            return;
        }

        List<FileSystemInfo> entries;
        try
        {
            entries = dir.EnumerateFileSystemInfos("*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = 0,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false,
            }).ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            node.Error = "Sin acceso";
            return;
        }

        var directories = new List<TreeNode>();
        var files = new List<TreeNode>();

        foreach (var entry in entries)
        {
            if (!state.Options.IncludeHidden && IsHidden(entry)) continue;
            if (state.Exclude.Any(p => p.IsMatch(entry.Name))) continue;

            if (entry is DirectoryInfo subDir)
            {
                var child = CreateDirectoryNode(subDir);
                // No seguimos accesos directos de carpeta (junctions/symlinks) para evitar bucles.
                if (subDir.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    child.IsTruncated = true;
                else
                    ScanDirectory(subDir, child, depth + 1, state);

                node.Size += child.Size;
                node.FileCount += child.FileCount;
                node.FolderCount += child.FolderCount + 1;
                directories.Add(child);
            }
            else if (entry is FileInfo file)
            {
                if (state.Include.Count > 0 && !state.Include.Any(p => p.IsMatch(file.Name))) continue;

                long size = SafeLength(file);
                node.Size += size;
                node.FileCount++;
                if (state.Options.IncludeFiles)
                {
                    files.Add(new TreeNode
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        IsDirectory = false,
                        LastModified = SafeLastWrite(file),
                        Size = size,
                    });
                }
            }

            state.Increment();
        }

        directories.Sort((a, b) => NaturalStringComparer.Instance.Compare(a.Name, b.Name));
        files.Sort((a, b) => NaturalStringComparer.Instance.Compare(a.Name, b.Name));
        node.Children.AddRange(directories);
        node.Children.AddRange(files);
    }

    private static bool HasAnyEntry(DirectoryInfo dir)
    {
        try { return dir.EnumerateFileSystemInfos().Any(); }
        catch { return false; }
    }

    private static bool IsHidden(FileSystemInfo entry)
        => (entry.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0;

    private static long SafeLength(FileInfo file)
    {
        try { return file.Length; } catch { return 0; }
    }

    private static DateTime SafeLastWrite(FileSystemInfo info)
    {
        try { return info.LastWriteTime; } catch { return DateTime.MinValue; }
    }

    private sealed class ScanState
    {
        public ScanState(TreeScanOptions options, IProgress<int>? progress, CancellationToken token)
        {
            Options = options;
            Progress = progress;
            Token = token;
            Exclude = options.ExcludePatterns.Select(p => new WildcardPattern(p)).ToList();
            Include = options.FilePatterns.Select(p => new WildcardPattern(p)).ToList();
        }

        public TreeScanOptions Options { get; }
        public IProgress<int>? Progress { get; }
        public CancellationToken Token { get; }
        public List<WildcardPattern> Exclude { get; }
        public List<WildcardPattern> Include { get; }
        public int Count { get; private set; }

        public void Increment()
        {
            Count++;
            if (Count % ProgressInterval == 0) Progress?.Report(Count);
        }
    }
}
