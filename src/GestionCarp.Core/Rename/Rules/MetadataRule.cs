using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using GestionCarp.Core.Localization;
using GestionCarp.Core.Metadata;

namespace GestionCarp.Core.Rename.Rules;

/// <summary>
/// Renombra con datos internos del archivo: fecha de la foto, cámara, artista, título, pista, duración...
/// Ejemplos: "{fecha:yyyy-MM-dd HH.mm.ss}", "{pista:00} - {titulo}".
/// </summary>
public sealed class MetadataRule : RenameRule
{
    private static readonly Regex Token = new(@"\{(?<name>[\p{L}]+)(?::(?<fmt>[^}]+))?\}", RegexOptions.CultureInvariant);

    private string _template = "{fecha:yyyy-MM-dd HH.mm.ss}";
    private bool _skipMissing;

    public MetadataRule() => _template = DefaultTemplate();

    [JsonIgnore] public override string DisplayName => L.T("rule_meta");

    public string Template { get => _template; set => SetProperty(ref _template, value ?? ""); }

    /// <summary>Si falta un dato, se deja vacío en lugar de marcar el archivo con error.</summary>
    public bool SkipMissing { get => _skipMissing; set => SetProperty(ref _skipMissing, value); }

    [JsonIgnore] public override string Summary => Template.Length == 0 ? L.T("series_sum_empty") : Template;

    public static string DefaultTemplate() => Loc.Current.Language switch
    {
        AppLanguage.English => "{date:yyyy-MM-dd HH.mm.ss}",
        AppLanguage.Portuguese => "{data:yyyy-MM-dd HH.mm.ss}",
        _ => "{fecha:yyyy-MM-dd HH.mm.ss}",
    };

    /// <summary>Plantillas de ejemplo con los marcadores del idioma actual.</summary>
    public static IReadOnlyList<string> Examples() => Loc.Current.Language switch
    {
        AppLanguage.English => new[]
        {
            "{date:yyyy-MM-dd HH.mm.ss}", "{date:yyyy-MM-dd} {name}", "{track:00} - {title}",
            "{artist} - {title}", "{year} - {album}", "{name} ({resolution})", "{date:yyyy-MM-dd} {camera}",
        },
        AppLanguage.Portuguese => new[]
        {
            "{data:yyyy-MM-dd HH.mm.ss}", "{data:yyyy-MM-dd} {nome}", "{faixa:00} - {titulo}",
            "{artista} - {titulo}", "{ano} - {album}", "{nome} ({resolucao})", "{data:yyyy-MM-dd} {camera}",
        },
        _ => new[]
        {
            "{fecha:yyyy-MM-dd HH.mm.ss}", "{fecha:yyyy-MM-dd} {nombre}", "{pista:00} - {titulo}",
            "{artista} - {titulo}", "{año} - {album}", "{nombre} ({resolucion})", "{fecha:yyyy-MM-dd} {camara}",
        },
    };

    protected override string ApplyCore(string text, RenameContext context)
    {
        var meta = context.IsDirectory ? MediaMetadata.Empty : MetadataReader.Read(context.OriginalPath);
        var missing = new List<string>();

        string result = Token.Replace(Template, m =>
        {
            string name = m.Groups["name"].Value;
            string? format = m.Groups["fmt"].Success ? m.Groups["fmt"].Value : null;
            string? value = Resolve(name.ToLowerInvariant(), format, meta, text, context);
            if (value is null)
            {
                missing.Add(name);
                return "";
            }
            return FileNameValidator.Sanitize(value, "-");
        });

        if (missing.Count > 0 && !SkipMissing)
            throw new RenameRuleException(L.F("meta_missing", string.Join(", ", missing.Distinct())));
        result = NameCleaner.Tidy(result);
        return result.Length == 0 ? text : result; // sin ningún dato: se conserva el nombre
    }

    private static string? Resolve(string token, string? format, MediaMetadata meta, string currentName, RenameContext context)
    {
        var inv = CultureInfo.InvariantCulture;
        string Number(int n) => n.ToString(format ?? "0", inv);
        string Date(DateTime d) => d.ToString(format ?? "yyyy-MM-dd", inv);

        switch (token)
        {
            case "fecha" or "date" or "data":
                return Date(meta.DateTaken ?? FileDate(context.OriginalPath, created: false));
            case "modificado" or "modified":
                return Date(FileDate(context.OriginalPath, created: false));
            case "creado" or "created" or "criado":
                return Date(FileDate(context.OriginalPath, created: true));
            case "camara" or "cámara" or "camera" or "câmera":
                return meta.Camera;
            case "ancho" or "width" or "largura":
                return meta.Width is int w ? Number(w) : null;
            case "alto" or "height" or "altura":
                return meta.Height is int h ? Number(h) : null;
            case "resolucion" or "resolución" or "resolution" or "resolucao" or "resolução":
                return meta.Width is int rw && meta.Height is int rh ? $"{rw}x{rh}" : null;
            case "artista" or "artist":
                return meta.Artist;
            case "album" or "álbum":
                return meta.Album;
            case "titulo" or "título" or "title":
                return meta.Title;
            case "genero" or "género" or "genre" or "gênero":
                return meta.Genre;
            case "pista" or "track" or "faixa":
                return meta.Track is int t ? Number(t) : null;
            case "año" or "ano" or "year":
                return meta.Year is int y ? Number(y) : null;
            case "duracion" or "duración" or "duration" or "duracao" or "duração":
                return meta.Duration is TimeSpan d ? FormatDuration(d) : null;
            case "nombre" or "name" or "nome":
                return currentName;
            case "n":
                return Number(context.Index + 1);
            default:
                return "{" + token + "}";
        }
    }

    private static string FormatDuration(TimeSpan d)
        => d.TotalHours >= 1 ? $"{(int)d.TotalHours}h{d.Minutes:00}m{d.Seconds:00}s" : $"{d.Minutes}m{d.Seconds:00}s";

    private static DateTime FileDate(string path, bool created)
    {
        try
        {
            FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
            return created ? info.CreationTime : info.LastWriteTime;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return DateTime.Now;
        }
    }
}
