using GestionCarp.Core.Localization;
using System.Text.RegularExpressions;

namespace GestionCarp.Core.Subtitles;

/// <summary>Temporada (si se conoce) y número de episodio.</summary>
public readonly record struct EpisodeInfo(int? Season, int Episode)
{
    public override string ToString()
        => Season is null ? L.F("ep_format", Episode) : L.F("ep_format_season", Season, Episode);

    public bool Matches(EpisodeInfo other)
        => Episode == other.Episode && (Season is null || other.Season is null || Season == other.Season);
}

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

    /// <summary>Analiza un nombre (sin extensión). Devuelve null si no encuentra un número de episodio.</summary>
    public static EpisodeInfo? Detect(string name)
    {
        var m = SeasonEpisode.Match(name);
        if (m.Success) return new EpisodeInfo(int.Parse(m.Groups["s"].Value), int.Parse(m.Groups["e"].Value));

        m = CrossFormat.Match(name);
        if (m.Success) return new EpisodeInfo(int.Parse(m.Groups["s"].Value), int.Parse(m.Groups["e"].Value));

        string cleaned = Brackets.Replace(name, " ");
        var seasonMatch = SeasonWord.Match(cleaned);
        int? season = seasonMatch.Success ? int.Parse(seasonMatch.Groups["s"].Value) : null;
        if (seasonMatch.Success)
            cleaned = cleaned.Remove(seasonMatch.Index, seasonMatch.Length).Insert(seasonMatch.Index, " ");

        m = EpisodeWord.Match(cleaned);
        if (m.Success) return new EpisodeInfo(season, int.Parse(m.Groups["e"].Value));

        m = LooseNumber.Match(cleaned);
        if (m.Success) return new EpisodeInfo(season, int.Parse(m.Groups["n"].Value));

        return null;
    }
}
