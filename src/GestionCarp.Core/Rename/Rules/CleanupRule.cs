using System.Text.Json.Serialization;
using GestionCarp.Core.Localization;

namespace GestionCarp.Core.Rename.Rules;

/// <summary>Limpia nombres descargados: "[Grupo] Mi.Serie.S01E02.1080p.WEB-DL.x264-XYZ" → "Mi Serie S01E02".</summary>
public sealed class CleanupRule : RenameRule
{
    private bool _replaceSeparators = true;
    private bool _removeBrackets = true;
    private bool _removeParentheses;
    private bool _removeReleaseTags = true;

    [JsonIgnore] public override string DisplayName => L.T("rule_clean");

    /// <summary>Convierte puntos y guiones bajos en espacios.</summary>
    public bool ReplaceSeparators { get => _replaceSeparators; set => SetProperty(ref _replaceSeparators, value); }

    /// <summary>Quita lo que esté entre [ ] y { }.</summary>
    public bool RemoveBrackets { get => _removeBrackets; set => SetProperty(ref _removeBrackets, value); }

    /// <summary>Quita lo que esté entre ( ) (suele ser el año).</summary>
    public bool RemoveParentheses { get => _removeParentheses; set => SetProperty(ref _removeParentheses, value); }

    /// <summary>Quita etiquetas como 1080p, WEB-DL, x264, AAC y el grupo final.</summary>
    public bool RemoveReleaseTags { get => _removeReleaseTags; set => SetProperty(ref _removeReleaseTags, value); }

    [JsonIgnore]
    public override string Summary
    {
        get
        {
            var parts = new List<string>();
            if (ReplaceSeparators) parts.Add(L.T("clean_sum_separators"));
            if (RemoveBrackets) parts.Add("[ ]");
            if (RemoveParentheses) parts.Add("( )");
            if (RemoveReleaseTags) parts.Add(L.T("clean_sum_tags"));
            return parts.Count == 0 ? L.T("clean_sum_spaces") : string.Join(", ", parts);
        }
    }

    protected override string ApplyCore(string text, RenameContext context)
    {
        string result = text;
        if (RemoveBrackets) result = NameCleaner.RemoveBrackets(result);
        if (RemoveParentheses) result = NameCleaner.RemoveParentheses(result);
        if (RemoveReleaseTags) result = NameCleaner.RemoveReleaseTags(result);
        if (ReplaceSeparators) result = NameCleaner.ReplaceSeparators(result);
        result = NameCleaner.Tidy(result);
        return result.Length == 0 ? text : result;
    }
}
