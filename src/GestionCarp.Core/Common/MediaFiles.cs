namespace GestionCarp.Core.Common;

/// <summary>Clasifica archivos de vídeo y de subtítulos por su extensión.</summary>
public static class MediaFiles
{
    public static readonly IReadOnlySet<string> VideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".m4v", ".webm", ".ts", ".m2ts", ".flv",
        ".mpg", ".mpeg", ".3gp", ".ogv", ".divx", ".rmvb", ".vob"
    };

    public static readonly IReadOnlySet<string> SubtitleExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".srt", ".ass", ".ssa", ".sub", ".idx", ".vtt", ".sup", ".smi", ".usf"
    };

    public static bool IsVideo(string path) => VideoExtensions.Contains(Path.GetExtension(path));

    public static bool IsSubtitle(string path) => SubtitleExtensions.Contains(Path.GetExtension(path));
}
