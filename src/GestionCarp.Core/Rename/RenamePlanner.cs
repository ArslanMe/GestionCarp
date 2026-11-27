using GestionCarp.Core.Localization;
using GestionCarp.Core.Common;
using GestionCarp.Core.Subtitles;

namespace GestionCarp.Core.Rename;

public sealed class RenamePlanOptions
{
    /// <summary>
    /// Al renombrar un vídeo, renombra también sus subtítulos (mismo nombre base, con o sin
    /// etiqueta de idioma: "Cap01.srt", "Cap01.es.srt"), aunque no estén en la lista.
    /// </summary>
    public bool RenameSubtitlesWithVideo { get; set; } = true;
}

/// <summary>Calcula la vista previa del renombrado y detecta conflictos. No toca el disco.</summary>
public static class RenamePlanner
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    public static List<RenamePreviewRow> Plan(IReadOnlyList<RenameItem> items, IReadOnlyList<RenameRule> rules,
        RenamePlanOptions? options = null)
    {
        options ??= new RenamePlanOptions();
        var activeRules = rules.Where(r => r.IsEnabled).ToList();

        var companions = options.RenameSubtitlesWithVideo
            ? FindCompanions(items)
            : new Dictionary<string, List<(string Path, string Suffix)>>(PathComparer);
        var companionPaths = new HashSet<string>(companions.Values.SelectMany(l => l.Select(c => c.Path)), PathComparer);

        var primaries = items.Where(i => !companionPaths.Contains(i.FullPath)).ToList();
        var folderCounters = new Dictionary<string, int>(PathComparer);
        var rows = new List<RenamePreviewRow>(items.Count);

        for (int index = 0; index < primaries.Count; index++)
        {
            var item = primaries[index];
            string directory = item.Directory;
            int inFolder = folderCounters.GetValueOrDefault(directory);
            folderCounters[directory] = inFolder + 1;

            var context = new RenameContext(index, inFolder, primaries.Count, item.FullPath, item.IsDirectory);
            string newName = item.Name;
            string? ruleError = null;
            try
            {
                foreach (var rule in activeRules)
                    newName = rule.Apply(newName, context);
            }
            catch (RenameRuleException ex)
            {
                ruleError = ex.Message;
                newName = item.Name;
            }

            var row = new RenamePreviewRow(item.FullPath, item.IsDirectory, Path.Combine(directory, newName));
            if (ruleError is not null) row.SetError(ruleError);
            rows.Add(row);

            if (companions.TryGetValue(item.FullPath, out var subtitles))
            {
                string newBase = Path.GetFileNameWithoutExtension(newName);
                foreach (var (path, suffix) in subtitles)
                {
                    string subtitleName = newBase + suffix + Path.GetExtension(path);
                    rows.Add(new RenamePreviewRow(path, false, Path.Combine(directory, subtitleName))
                    {
                        GroupKey = item.FullPath,
                        IsCompanion = true,
                        RelatedName = item.Name,
                        Message = L.F("plan_companion", item.Name),
                    });
                }
            }
        }

        Validate(rows);
        return rows;
    }

    /// <summary>
    /// Comprueba nombres válidos, duplicados y archivos ya existentes. Los elementos con error se
    /// quedan como están, y eso puede provocar nuevos conflictos, por eso se repite hasta estabilizar.
    /// </summary>
    public static void Validate(IList<RenamePreviewRow> rows, Func<string, bool>? pathExists = null)
    {
        pathExists ??= p => File.Exists(p) || Directory.Exists(p);

        foreach (var row in rows.Where(r => r.IsMoving))
        {
            string? error = FileNameValidator.Validate(row.NewName);
            if (error is not null)
                row.SetError(error);
            else if (row.TargetPath.Length > FileNameValidator.MaxPathLength)
            {
                row.Status = RenameStatus.Warning;
                row.Message = L.T("plan_long_path");
            }
        }

        bool changed;
        do
        {
            changed = PropagateGroupErrors(rows);

            var movingSources = new HashSet<string>(rows.Where(r => r.IsMoving).Select(r => r.SourcePath), PathComparer);
            var finalPaths = new Dictionary<string, int>(PathComparer);
            foreach (var row in rows)
            {
                string key = row.IsMoving ? row.TargetPath : row.SourcePath;
                finalPaths[key] = finalPaths.GetValueOrDefault(key) + 1;
            }

            foreach (var row in rows.Where(r => r.IsMoving).ToList())
            {
                if (finalPaths[row.TargetPath] > 1)
                {
                    row.SetError(L.T("plan_duplicate"));
                    changed = true;
                }
                else if (!PathComparer.Equals(row.SourcePath, row.TargetPath)
                         && !movingSources.Contains(row.TargetPath)
                         && pathExists(row.TargetPath))
                {
                    row.SetError(L.T("plan_exists"));
                    changed = true;
                }
            }
        } while (changed);
    }

    private static bool PropagateGroupErrors(IList<RenamePreviewRow> rows)
    {
        bool changed = false;
        var failedGroups = new HashSet<string>(rows.Where(r => r.Status == RenameStatus.Error).Select(r => r.GroupKey), PathComparer);
        foreach (var row in rows.Where(r => r.IsMoving && failedGroups.Contains(r.GroupKey)))
        {
            row.SetError(row.IsCompanion
                ? L.T("plan_video_failed")
                : L.T("plan_sub_failed"));
            changed = true;
        }
        return changed;
    }

    public static List<RenameOperation> ToOperations(IEnumerable<RenamePreviewRow> rows)
        => rows.Where(r => r.IsMoving).Select(r => r.ToOperation()).ToList();

    /// <summary>Busca, para cada vídeo de la lista, los subtítulos de su carpeta que comparten nombre.</summary>
    private static Dictionary<string, List<(string Path, string Suffix)>> FindCompanions(IReadOnlyList<RenameItem> items)
    {
        var result = new Dictionary<string, List<(string, string)>>(PathComparer);
        var listedVideos = new HashSet<string>(
            items.Where(i => !i.IsDirectory && MediaFiles.IsVideo(i.FullPath)).Select(i => i.FullPath), PathComparer);

        foreach (var folder in listedVideos.GroupBy(p => Path.GetDirectoryName(p) ?? "", PathComparer))
        {
            List<string> files;
            try
            {
                files = Directory.Exists(folder.Key) ? Directory.EnumerateFiles(folder.Key).ToList() : new List<string>();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            var videosInFolder = files.Where(MediaFiles.IsVideo).Union(folder, PathComparer).ToList();
            foreach (var subtitle in files.Where(MediaFiles.IsSubtitle).OrderBy(f => f, NaturalStringComparer.Instance))
            {
                string subtitleStem = Path.GetFileNameWithoutExtension(subtitle);
                string? bestVideo = null;
                string? bestSuffix = null;
                int bestLength = -1;
                foreach (var video in videosInFolder)
                {
                    string videoStem = Path.GetFileNameWithoutExtension(video);
                    string? suffix = SubtitleNaming.GetSuffixForVideo(subtitleStem, videoStem);
                    if (suffix is not null && videoStem.Length > bestLength)
                    {
                        bestVideo = video;
                        bestSuffix = suffix;
                        bestLength = videoStem.Length;
                    }
                }

                if (bestVideo is not null && listedVideos.Contains(bestVideo))
                {
                    if (!result.TryGetValue(bestVideo, out var list))
                        result[bestVideo] = list = new List<(string, string)>();
                    list.Add((subtitle, bestSuffix!));
                }
            }
        }
        return result;
    }
}
