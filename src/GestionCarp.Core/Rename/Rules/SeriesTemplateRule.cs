using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using GestionCarp.Core.Localization;
using GestionCarp.Core.Subtitles;

namespace GestionCarp.Core.Rename.Rules;

/// <summary>
/// Renombra episodios con una plantilla: "{serie} - T{temp:00}E{ep:00} - {titulo}".
/// Detecta temporada y episodio en el nombre (o la temporada en la carpeta "Temporada 2").
/// </summary>
public sealed class SeriesTemplateRule : RenameRule
{
    private static readonly Regex Token = new(@"\{(?<name>[\p{L}]+)(?::(?<fmt>[0#]+))?\}", RegexOptions.CultureInvariant);

    private string _template = "";
    private string _seriesName = "";
    private int _defaultSeason = 1;

    public SeriesTemplateRule() => _template = DefaultTemplate();

    [JsonIgnore] public override string DisplayName => L.T("rule_series");

    /// <summary>Plantilla con marcadores: {serie} {temp} {ep} {titulo} {n} {nombre}. Admite formato: {ep:00}.</summary>
    public string Template { get => _template; set => SetProperty(ref _template, value ?? ""); }

    /// <summary>Nombre de la serie. Vacío = se deduce del nombre del archivo o de la carpeta.</summary>
    public string SeriesName { get => _seriesName; set => SetProperty(ref _seriesName, value ?? ""); }

    /// <summary>Temporada a usar si no aparece ni en el nombre ni en la carpeta.</summary>
    public int DefaultSeason { get => _defaultSeason; set => SetProperty(ref _defaultSeason, Math.Max(0, value)); }

    [JsonIgnore] public override string Summary => Template.Length == 0 ? L.T("series_sum_empty") : Template;

    /// <summary>Plantilla inicial según el idioma.</summary>
    public static string DefaultTemplate() => Loc.Current.Language == AppLanguage.English
        ? "{series} - S{season:00}E{episode:00}"
        : "{serie} - T{temp:00}E{ep:00}";

    protected override string ApplyCore(string text, RenameContext context)
    {
        var match = EpisodeDetector.Match(text)
                    ?? throw new RenameRuleException(L.T("series_no_episode"));

        string folder = Path.GetFileName(Path.GetDirectoryName(context.OriginalPath) ?? "");
        int? folderSeason = EpisodeDetector.SeasonFromFolder(folder);
        int season = match.Info.Season ?? folderSeason ?? DefaultSeason;

        string series = SeriesName.Trim();
        if (series.Length == 0) series = Clean(text[..match.Start]);
        if (series.Length == 0)
        {
            // "Mi Serie/Temporada 2/Cap 5.mkv" → la carpeta de la serie es la superior a la temporada.
            string? dir = Path.GetDirectoryName(context.OriginalPath);
            if (folderSeason is not null) dir = Path.GetDirectoryName(dir ?? "");
            series = Path.GetFileName(dir ?? "");
        }
        string title = Clean(text[match.End..]);

        string result = Token.Replace(Template, m =>
        {
            string format = m.Groups["fmt"].Success ? m.Groups["fmt"].Value : "0";
            return m.Groups["name"].Value.ToLowerInvariant() switch
            {
                "serie" or "series" or "show" or "série" => series,
                "temp" or "temporada" or "season" or "s" => season.ToString(format, CultureInfo.InvariantCulture),
                "ep" or "episodio" or "episode" or "episódio" or "e" => match.Info.Episode.ToString(format, CultureInfo.InvariantCulture),
                "titulo" or "título" or "title" => title,
                "n" => (context.Index + 1).ToString(format, CultureInfo.InvariantCulture),
                "nombre" or "name" or "nome" => text,
                _ => m.Value,
            };
        });
        return NameCleaner.Tidy(result);
    }

    private static string Clean(string part)
    {
        string result = NameCleaner.RemoveBrackets(part);
        result = NameCleaner.RemoveReleaseTags(result);
        result = NameCleaner.ReplaceSeparators(result);
        return NameCleaner.Tidy(result);
    }
}
