using System.Text.RegularExpressions;
using GestionCarp.Core.Localization;

namespace GestionCarp.Core.Subtitles;

/// <summary>Temporada (si se conoce) y número de episodio.</summary>
public readonly record struct EpisodeInfo(int? Season, int Episode)
{
    public override string ToString()
        => Season is null ? L.F("ep_format", Episode) : L.F("ep_format_season", Season, Episode);

    public bool Matches(EpisodeInfo other)
        => Episode == other.Episode && (Season is null || other.Season is null || Season == other.Season);
}

/// <summary>Episodio detectado y la posición del marcador ("S01E05", "Cap 5"...) dentro del nombre.</summary>
public readonly record struct EpisodeMatch(EpisodeInfo Info, int Start, int End);

/// <summary>Detecta el número de episodio en nombres como "S01E05", "1x05", "Cap 5", "Episodio 05" o "Serie - 05".</summary>
public static class EpisodeDetector
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly Regex SeasonEpisode = new(@"(?<![\p{L}\d])s(?<s>\d{1,2})[\s._-]*e(?<e>\d{1,4})(?!\d)", Options);
    private static readonly Regex CrossFormat = new(@"(?<![\p{L}\d])(?<s>\d{1,2})x(?<e>\d{1,3})(?![\p{L}\d])", Options);
    private static readonly Regex SeasonWord = new(@"(?<!\p{L})(?:temporada|season|saison|temp)\.?[\s._-]*(?<s>\d{1,2})(?!\d)", Options);
    private static readonly Regex EpisodeWord = new(
        @"(?<!\p{L})(?:cap[ií]tulo|cap|epis[oó]dio|episode|epis|ep)\.?[\s._#-]*(?<e>\d{1,4})(?!\d)|(?<![\p{L}\d])e(?<e>\d{1,4})(?!\d)",
        Options);
    private static readonly Regex Brackets = new(@"\[[^\]]*\]|\([^\)]*\)|\{[^\}]*\}", Options);
    private static readonly Regex LooseNumber = new(@"(?<![\p{L}\d])(?<n>\d{1,3})(?![\p{L}\d])", Options);
    private static readonly Regex SeasonFolder = new(@"^(?:temporada|season|saison|temp|s|t)[\s._-]*(?<s>\d{1,2})$", Options);

    /// <summary>Analiza un nombre (sin extensión). Devuelve null si no encuentra un número de episodio.</summary>
    public static EpisodeInfo? Detect(string name) => Match(name)?.Info;

    /// <summary>Como <see cref="Detect"/>, pero indicando dónde está el marcador del episodio.</summary>
    public static EpisodeMatch? Match(string name)
    {
        var m = SeasonEpisode.Match(name);
        if (m.Success) return new EpisodeMatch(new EpisodeInfo(Int(m, "s"), Int(m, "e")), m.Index, m.Index + m.Length);

        m = CrossFormat.Match(name);
        if (m.Success) return new EpisodeMatch(new EpisodeInfo(Int(m, "s"), Int(m, "e")), m.Index, m.Index + m.Length);

        // Se tapan con espacios (misma longitud) los corchetes y la temporada para no confundirlos con el episodio.
        string cleaned = Brackets.Replace(name, x => new string(' ', x.Length));
        var seasonMatch = SeasonWord.Match(cleaned);
        int? season = seasonMatch.Success ? Int(seasonMatch, "s") : null;
        if (seasonMatch.Success)
            cleaned = cleaned[..seasonMatch.Index] + new string(' ', seasonMatch.Length) + cleaned[(seasonMatch.Index + seasonMatch.Length)..];

        m = EpisodeWord.Match(cleaned);
        if (!m.Success) m = LooseNumber.Match(cleaned);
        if (!m.Success) return null;

        int episode = m.Groups["e"].Success ? Int(m, "e") : Int(m, "n");
        int start = seasonMatch.Success ? Math.Min(seasonMatch.Index, m.Index) : m.Index;
        int end = seasonMatch.Success ? Math.Max(seasonMatch.Index + seasonMatch.Length, m.Index + m.Length) : m.Index + m.Length;
        return new EpisodeMatch(new EpisodeInfo(season, episode), start, end);
    }

    /// <summary>Temporada indicada por el nombre de una carpeta: "Temporada 2", "Season 02", "S2".</summary>
    public static int? SeasonFromFolder(string folderName)
    {
        var m = SeasonFolder.Match(folderName.Trim());
        return m.Success ? Int(m, "s") : null;
    }

    private static int Int(System.Text.RegularExpressions.Match m, string group) => int.Parse(m.Groups[group].Value);
}
