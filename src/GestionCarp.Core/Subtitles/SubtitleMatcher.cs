using GestionCarp.Core.Localization;
using GestionCarp.Core.Common;
using GestionCarp.Core.Rename;

namespace GestionCarp.Core.Subtitles;

public enum SubtitleMatchMode
{
    /// <summary>Empareja por el número de episodio detectado en ambos nombres.</summary>
    ByEpisode,
    /// <summary>Empareja por orden alfabético (natural) dentro de cada carpeta.</summary>
    ByOrder,
}

public sealed class SubtitleMatchOptions
{
    public SubtitleMatchMode Mode { get; set; } = SubtitleMatchMode.ByEpisode;

    /// <summary>Conserva etiquetas como ".es" o ".forced" al final del nombre.</summary>
    public bool KeepLanguageTags { get; set; } = true;

    /// <summary>Idioma a añadir si el subtítulo no tiene ninguno (por ejemplo "es"). Vacío = ninguno.</summary>
    public string DefaultLanguage { get; set; } = "";

    /// <summary>Mueve los subtítulos de subcarpetas como "Subs" junto al vídeo.</summary>
    public bool MoveToVideoFolder { get; set; } = true;

    /// <summary>Subtítulos que el usuario ha desmarcado: no se tocan.</summary>
    public ISet<string> Excluded { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Empareja subtítulos con vídeos aunque tengan nombres distintos y propone renombrarlos con el
/// nombre exacto del vídeo, para que el reproductor los cargue automáticamente.
/// </summary>
public static class SubtitleMatcher
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    private static readonly HashSet<string> SubtitleFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "subs", "sub", "subtitles", "subtitle", "subtitulos", "subtítulos", "subtitulo", "subtítulo", "legendas", "legenda",
    };

    /// <summary>Carpeta a la que "pertenece" un subtítulo: la suya, o la superior si está en "Subs".</summary>
    public static string HomeFolder(string subtitlePath)
    {
        string dir = Path.GetDirectoryName(subtitlePath) ?? "";
        return SubtitleFolderNames.Contains(Path.GetFileName(dir)) ? Path.GetDirectoryName(dir) ?? dir : dir;
    }

    public static List<RenamePreviewRow> Match(IReadOnlyList<string> videos, IReadOnlyList<string> subtitles,
        SubtitleMatchOptions options, Func<string, bool>? pathExists = null)
    {
        var pairs = new Dictionary<string, string>(PathComparer);
        var reasons = new Dictionary<string, string>(PathComparer);

        if (options.Mode == SubtitleMatchMode.ByOrder)
            MatchByOrder(videos, subtitles, pairs, reasons);
        else
            MatchByEpisode(videos, subtitles, pairs, reasons);

        var rows = new List<RenamePreviewRow>();
        foreach (var subtitle in subtitles
                     .OrderBy(s => pairs.TryGetValue(s, out var v) ? v : "￿" + s, NaturalStringComparer.Instance)
                     .ThenBy(s => s, NaturalStringComparer.Instance))
        {
            if (options.Excluded.Contains(subtitle) && pairs.TryGetValue(subtitle, out var excludedVideo))
            {
                rows.Add(new RenamePreviewRow(subtitle, false, subtitle)
                {
                    Status = RenameStatus.Skipped,
                    RelatedName = Path.GetFileName(excludedVideo),
                    Message = L.T("sub_unchecked"),
                });
                continue;
            }

            if (!pairs.TryGetValue(subtitle, out var video))
            {
                rows.Add(new RenamePreviewRow(subtitle, false, subtitle)
                {
                    Status = RenameStatus.Skipped,
                    Message = reasons.GetValueOrDefault(subtitle, L.T("sub_no_video")),
                });
                continue;
            }

            string videoStem = Path.GetFileNameWithoutExtension(video);
            string subtitleStem = Path.GetFileNameWithoutExtension(subtitle);
            string suffix = "";
            if (options.KeepLanguageTags)
                suffix = SubtitleNaming.GetSuffixForVideo(subtitleStem, videoStem) ?? SubtitleNaming.SplitLanguageTags(subtitleStem).Tags;
            if (suffix.Length == 0)
                suffix = SubtitleNaming.NormalizeTag(options.DefaultLanguage);

            string targetFolder = options.MoveToVideoFolder
                ? Path.GetDirectoryName(video) ?? ""
                : Path.GetDirectoryName(subtitle) ?? "";
            string target = Path.Combine(targetFolder, videoStem + suffix + Path.GetExtension(subtitle));

            var episode = EpisodeDetector.Detect(videoStem);
            rows.Add(new RenamePreviewRow(subtitle, false, target)
            {
                RelatedName = Path.GetFileName(video),
                Message = options.Mode == SubtitleMatchMode.ByEpisode && episode is not null ? L.F("sub_episode", episode) : null,
            });
        }

        RenamePlanner.Validate(rows, pathExists);
        return rows;
    }

    private static void MatchByEpisode(IReadOnlyList<string> videos, IReadOnlyList<string> subtitles,
        Dictionary<string, string> pairs, Dictionary<string, string> reasons)
    {
        var videoInfos = videos
            .Select(v => (Path: v, Folder: Path.GetDirectoryName(v) ?? "", Info: EpisodeDetector.Detect(Path.GetFileNameWithoutExtension(v))))
            .Where(v => v.Info is not null)
            .ToList();

        foreach (var subtitle in subtitles)
        {
            var info = EpisodeDetector.Detect(Path.GetFileNameWithoutExtension(subtitle));
            if (info is null)
            {
                reasons[subtitle] = L.T("sub_no_number");
                continue;
            }

            string home = HomeFolder(subtitle);
            var candidates = videoInfos.Where(v => PathComparer.Equals(v.Folder, home) && v.Info!.Value.Matches(info.Value)).ToList();
            if (candidates.Count == 0)
                candidates = videoInfos.Where(v => v.Info!.Value.Matches(info.Value)).ToList();
            if (candidates.Count > 1)
            {
                var exact = candidates.Where(v => v.Info!.Value.Season is not null && v.Info.Value.Season == info.Value.Season).ToList();
                if (exact.Count == 1) candidates = exact;
            }

            if (candidates.Count == 1)
                pairs[subtitle] = candidates[0].Path;
            else if (candidates.Count == 0)
                reasons[subtitle] = L.F("sub_no_episode_video", info);
            else
                reasons[subtitle] = L.F("sub_ambiguous", info);
        }
    }

    private static void MatchByOrder(IReadOnlyList<string> videos, IReadOnlyList<string> subtitles,
        Dictionary<string, string> pairs, Dictionary<string, string> reasons)
    {
        var videosByFolder = videos
            .GroupBy(v => Path.GetDirectoryName(v) ?? "", PathComparer)
            .ToDictionary(g => g.Key, g => g.OrderBy(v => v, NaturalStringComparer.Instance).ToList(), PathComparer);

        // Cada idioma/formato se empareja por separado: Cap01.es.srt y Cap01.en.srt van al mismo vídeo.
        var groups = subtitles.GroupBy(s =>
        {
            string tags = SubtitleNaming.SplitLanguageTags(Path.GetFileNameWithoutExtension(s)).Tags;
            return (Home: HomeFolder(s).ToLowerInvariant(), Tags: tags.ToLowerInvariant(), Ext: Path.GetExtension(s).ToLowerInvariant());
        });

        foreach (var group in groups)
        {
            var sorted = group.OrderBy(s => s, NaturalStringComparer.Instance).ToList();
            if (!videosByFolder.TryGetValue(HomeFolder(sorted[0]), out var folderVideos))
            {
                foreach (var s in sorted) reasons[s] = L.T("sub_no_videos_folder");
                continue;
            }

            for (int i = 0; i < sorted.Count; i++)
            {
                if (i < folderVideos.Count)
                    pairs[sorted[i]] = folderVideos[i];
                else
                    reasons[sorted[i]] = L.T("sub_more_than_videos");
            }
        }
    }
}
