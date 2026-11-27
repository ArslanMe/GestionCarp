namespace GestionCarp.Core.Subtitles;

/// <summary>
/// Reglas de nombres de subtítulos que entienden los reproductores (VLC, MPC-HC, Plex, Kodi...):
/// el subtítulo debe llamarse igual que el vídeo, opcionalmente con idioma: "Cap01.mkv" → "Cap01.es.srt".
/// </summary>
public static class SubtitleNaming
{
    private static readonly HashSet<string> LanguageCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        // ISO 639-1
        "es", "en", "fr", "de", "it", "pt", "ja", "ko", "zh", "ru", "ca", "gl", "eu", "nl", "pl", "sv",
        "no", "nb", "da", "fi", "el", "tr", "ar", "he", "hi", "cs", "hu", "ro", "uk", "vi", "th", "id",
        // ISO 639-2
        "spa", "eng", "fre", "fra", "ger", "deu", "ita", "por", "jpn", "kor", "chi", "zho", "rus", "cat",
        "glg", "eus", "baq", "dut", "nld", "pol", "swe", "nor", "dan", "fin", "gre", "ell", "tur", "ara",
        "heb", "hin", "cze", "ces", "hun", "rum", "ron", "ukr", "vie", "tha", "ind", "lat",
    };

    private static readonly HashSet<string> OtherTags = new(StringComparer.OrdinalIgnoreCase)
    {
        // Nombres de idioma
        "spanish", "español", "espanol", "castellano", "latino", "latin", "english", "ingles", "inglés",
        "french", "frances", "francés", "german", "aleman", "alemán", "italian", "italiano",
        "portuguese", "portugues", "portugués", "português", "brazilian", "brasileiro", "japanese", "japones", "japonés", "catalan", "català", "catalán",
        // Indicadores
        "forced", "forzado", "forzados", "sdh", "cc", "default", "full", "completo", "completos",
    };

    /// <summary>
    /// Si el subtítulo pertenece al vídeo (mismo nombre, o mismo nombre + ".etiqueta"), devuelve
    /// la parte final que hay que conservar (por ejemplo ".es.forced"; "" si es idéntico). Si no, null.
    /// </summary>
    public static string? GetSuffixForVideo(string subtitleStem, string videoStem)
    {
        if (videoStem.Length == 0) return null;
        if (subtitleStem.Equals(videoStem, StringComparison.OrdinalIgnoreCase)) return "";
        if (subtitleStem.Length > videoStem.Length
            && subtitleStem.StartsWith(videoStem, StringComparison.OrdinalIgnoreCase)
            && subtitleStem[videoStem.Length] is '.' or '_')
            return subtitleStem[videoStem.Length..];
        return null;
    }

    /// <summary>Separa las etiquetas finales de idioma/tipo: "Serie 1x01.es.forced" → ("Serie 1x01", ".es.forced").</summary>
    public static (string Base, string Tags) SplitLanguageTags(string stem)
    {
        var tokens = stem.Split('.');
        int keep = tokens.Length;
        while (keep > 1 && IsTag(tokens[keep - 1]))
            keep--;

        if (keep == tokens.Length) return (stem, "");
        string baseName = string.Join('.', tokens, 0, keep);
        string tags = "." + string.Join('.', tokens, keep, tokens.Length - keep);
        return (baseName, tags);
    }

    public static bool IsTag(string token)
    {
        token = token.Trim();
        if (token.Length == 0) return false;
        if (LanguageCodes.Contains(token) || OtherTags.Contains(token)) return true;

        // Variantes regionales: es-ES, es-419, pt-BR, en_US
        int dash = token.IndexOfAny(new[] { '-', '_' });
        if (dash is 2 or 3 && token.Length - dash - 1 is >= 2 and <= 4)
            return LanguageCodes.Contains(token[..dash]) && token[(dash + 1)..].All(char.IsLetterOrDigit);

        return false;
    }

    /// <summary>Normaliza una etiqueta de idioma escrita por el usuario: " .ES " → ".ES"; vacío → "".</summary>
    public static string NormalizeTag(string? language)
    {
        string tag = (language ?? "").Trim().Trim('.');
        return tag.Length == 0 ? "" : "." + tag;
    }
}
