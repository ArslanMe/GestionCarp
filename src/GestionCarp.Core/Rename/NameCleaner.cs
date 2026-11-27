using System.Text.RegularExpressions;

namespace GestionCarp.Core.Rename;

/// <summary>Limpieza de nombres descargados: etiquetas de calidad, grupos, separadores...</summary>
public static class NameCleaner
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly Regex SquareAndCurly = new(@"\[[^\]]*\]|\{[^\}]*\}", Options);
    private static readonly Regex Parentheses = new(@"\([^\)]*\)", Options);

    /// <summary>Etiquetas típicas de calidad, origen, códec, audio y edición.</summary>
    private static readonly Regex ReleaseTags = new(
        @"(?<![\p{L}\d])(?:" +
        @"(?:480|576|720|1080|1440|2160|4320)[pi]|4k|8k|uhd|fhd|" +
        @"[xh]\.?26[45]|hevc|avc|xvid|divx|av1|10-?bits?|8-?bits?|hdr10\+?|hdr|dovi|" +
        @"web-?dl|web-?rip|bluray|blu-ray|bdrip|brrip|bdremux|remux|dvdrip|hdtv|hdrip|dvdscr|" +
        @"aac(?:2\.0|5\.1)?|e?ac3|ddp?(?:5\.1|7\.1|2\.0)?|dts(?:-?hd)?(?:-?ma)?|truehd|atmos|" +
        @"repack|proper|internal|amzn|nf|dsnp|hmax|atvp|webrip|web" +
        @")(?![\p{L}\d])", Options);

    /// <summary>Grupo de la publicación al final: "...x264-GRUPO".</summary>
    private static readonly Regex TrailingGroup = new(@"-[\p{L}\d]{2,15}$", Options);
    private static readonly Regex DotsAndUnderscores = new(@"[._]+", Options);
    private static readonly Regex RepeatedDashes = new(@"(\s*-\s*){2,}", Options);
    private static readonly Regex Spaces = new(@"\s{2,}", Options);

    public static string RemoveBrackets(string text) => SquareAndCurly.Replace(text, " ");

    public static string RemoveParentheses(string text) => Parentheses.Replace(text, " ");

    public static string RemoveReleaseTags(string text)
    {
        string result = ReleaseTags.Replace(text, " ");
        if (result != text)
        {
            string trimmed = result.TrimEnd(' ', '.', '_');
            result = TrailingGroup.Replace(trimmed, "");
        }
        return result;
    }

    public static string ReplaceSeparators(string text) => DotsAndUnderscores.Replace(text, " ");

    /// <summary>Quita espacios dobles, guiones repetidos y separadores sobrantes en los extremos.</summary>
    public static string Tidy(string text)
    {
        string result = RepeatedDashes.Replace(text, " - ");
        result = Spaces.Replace(result, " ");
        return result.Trim(' ', '-', '.', '_', '\t');
    }
}
