using GestionCarp.Core.Localization;
using System.Text;
using System.Text.RegularExpressions;

namespace GestionCarp.Core.Rename.Rules;

public enum RemoveMode
{
    /// <summary>Quita los primeros N caracteres.</summary>
    FirstChars,
    /// <summary>Quita los últimos N caracteres.</summary>
    LastChars,
    /// <summary>Quita N caracteres a partir de una posición.</summary>
    Range,
    /// <summary>Quita todo lo que haya entre dos delimitadores, por ejemplo "[1080p]".</summary>
    BetweenDelimiters,
}

/// <summary>Elimina partes del nombre.</summary>
public sealed class RemoveRule : RenameRule
{
    private static readonly Regex MultipleSpaces = new(@"\s{2,}", RegexOptions.Compiled);

    private RemoveMode _mode = RemoveMode.FirstChars;
    private int _count = 1;
    private int _from = 1;
    private string _openDelimiter = "[";
    private string _closeDelimiter = "]";
    private bool _cleanSpaces = true;

    public override string DisplayName => L.T("rule_remove");

    public RemoveMode Mode { get => _mode; set => SetProperty(ref _mode, value); }
    public int Count { get => _count; set => SetProperty(ref _count, Math.Max(0, value)); }

    /// <summary>Posición del primer carácter a quitar (1 = el primero). Solo en modo rango.</summary>
    public int From { get => _from; set => SetProperty(ref _from, Math.Max(1, value)); }

    public string OpenDelimiter { get => _openDelimiter; set => SetProperty(ref _openDelimiter, value ?? ""); }
    public string CloseDelimiter { get => _closeDelimiter; set => SetProperty(ref _closeDelimiter, value ?? ""); }

    /// <summary>Tras eliminar, quita espacios duplicados y los de los extremos.</summary>
    public bool CleanSpaces { get => _cleanSpaces; set => SetProperty(ref _cleanSpaces, value); }

    public override string Summary => Mode switch
    {
        RemoveMode.LastChars => L.F("rem_sum_last", Count),
        RemoveMode.Range => L.F("rem_sum_range", Count, From),
        RemoveMode.BetweenDelimiters => L.F("rem_sum_between", OpenDelimiter, CloseDelimiter),
        _ => L.F("rem_sum_first", Count),
    };

    protected override string ApplyCore(string text, RenameContext context)
    {
        string result = Mode switch
        {
            RemoveMode.FirstChars => Count >= text.Length ? "" : text[Count..],
            RemoveMode.LastChars => Count >= text.Length ? "" : text[..^Count],
            RemoveMode.Range => RemoveRange(text),
            RemoveMode.BetweenDelimiters => RemoveBetween(text),
            _ => text,
        };

        if (CleanSpaces)
            result = MultipleSpaces.Replace(result, " ").Trim();
        return result;
    }

    private string RemoveRange(string text)
    {
        int start = From - 1;
        if (start >= text.Length) return text;
        int length = Math.Min(Count, text.Length - start);
        return text.Remove(start, length);
    }

    private string RemoveBetween(string text)
    {
        if (OpenDelimiter.Length == 0 || CloseDelimiter.Length == 0) return text;
        var sb = new StringBuilder();
        int pos = 0;
        while (pos < text.Length)
        {
            int open = text.IndexOf(OpenDelimiter, pos, StringComparison.Ordinal);
            if (open < 0) break;
            int close = text.IndexOf(CloseDelimiter, open + OpenDelimiter.Length, StringComparison.Ordinal);
            if (close < 0) break;
            sb.Append(text, pos, open - pos);
            pos = close + CloseDelimiter.Length;
        }
        sb.Append(text, pos, text.Length - pos);
        return sb.ToString();
    }
}
