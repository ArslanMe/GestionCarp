using System.Text.RegularExpressions;

namespace GestionCarp.Core.Rename.Rules;

/// <summary>Busca un texto y lo sustituye por otro (opcionalmente con expresiones regulares).</summary>
public sealed class ReplaceRule : RenameRule
{
    private string _find = "";
    private string _replaceWith = "";
    private bool _caseSensitive;
    private bool _replaceAll = true;
    private bool _useRegex;

    public override string DisplayName => "Reemplazar";

    public string Find { get => _find; set => SetProperty(ref _find, value ?? ""); }
    public string ReplaceWith { get => _replaceWith; set => SetProperty(ref _replaceWith, value ?? ""); }
    public bool CaseSensitive { get => _caseSensitive; set => SetProperty(ref _caseSensitive, value); }
    public bool ReplaceAll { get => _replaceAll; set => SetProperty(ref _replaceAll, value); }
    public bool UseRegex { get => _useRegex; set => SetProperty(ref _useRegex, value); }

    public override string Summary
        => Find.Length == 0 ? "(sin texto a buscar)" : $"\"{Find}\" → \"{ReplaceWith}\"" + (UseRegex ? " (regex)" : "");

    protected override string ApplyCore(string text, RenameContext context)
    {
        if (Find.Length == 0) return text;

        if (UseRegex)
        {
            try
            {
                var options = CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                var regex = new Regex(Find, options | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
                return ReplaceAll ? regex.Replace(text, ReplaceWith) : regex.Replace(text, ReplaceWith, 1);
            }
            catch (ArgumentException ex)
            {
                throw new RenameRuleException("Expresión regular no válida: " + ex.Message, ex);
            }
            catch (RegexMatchTimeoutException ex)
            {
                throw new RenameRuleException("La expresión regular tarda demasiado.", ex);
            }
        }

        var comparison = CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (ReplaceAll)
            return text.Replace(Find, ReplaceWith, comparison);

        int index = text.IndexOf(Find, comparison);
        return index < 0 ? text : text[..index] + ReplaceWith + text[(index + Find.Length)..];
    }
}
