using System.Text.RegularExpressions;

namespace GestionCarp.Core.Common;

/// <summary>Patrón con comodines estilo Windows (* y ?), sin distinguir mayúsculas.</summary>
public sealed class WildcardPattern
{
    private readonly Regex _regex;

    public WildcardPattern(string pattern)
    {
        Pattern = pattern;
        string regex = "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
        _regex = new Regex(regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public string Pattern { get; }

    public bool IsMatch(string name) => _regex.IsMatch(name);

    /// <summary>Divide una lista "*.mkv; *.mp4, node_modules" en patrones.</summary>
    public static List<WildcardPattern> ParseList(string? text)
        => SplitList(text).Select(p => new WildcardPattern(p)).ToList();

    public static List<string> SplitList(string? text)
        => string.IsNullOrWhiteSpace(text)
            ? new List<string>()
            : text.Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Where(p => p.Length > 0).ToList();
}
