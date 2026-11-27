using System.Globalization;

namespace GestionCarp.Core.Rename.Rules;

public enum CaseMode
{
    Lower,
    Upper,
    /// <summary>Cada Palabra Con Mayúscula Inicial.</summary>
    Title,
    /// <summary>Solo la primera letra en mayúscula.</summary>
    Sentence,
}

/// <summary>Cambia mayúsculas y minúsculas.</summary>
public sealed class CaseRule : RenameRule
{
    private CaseMode _mode = CaseMode.Title;

    public override string DisplayName => "Mayúsculas";

    public CaseMode Mode { get => _mode; set => SetProperty(ref _mode, value); }

    public override string Summary => Mode switch
    {
        CaseMode.Lower => "todo en minúsculas",
        CaseMode.Upper => "TODO EN MAYÚSCULAS",
        CaseMode.Sentence => "Tipo oración",
        _ => "Tipo Título",
    };

    protected override string ApplyCore(string text, RenameContext context)
    {
        var culture = CultureInfo.CurrentCulture;
        switch (Mode)
        {
            case CaseMode.Lower:
                return text.ToLower(culture);
            case CaseMode.Upper:
                return text.ToUpper(culture);
            case CaseMode.Sentence:
            {
                string lower = text.ToLower(culture);
                for (int i = 0; i < lower.Length; i++)
                {
                    if (char.IsLetter(lower[i]))
                        return lower[..i] + char.ToUpper(lower[i], culture) + lower[(i + 1)..];
                }
                return lower;
            }
            default:
                return culture.TextInfo.ToTitleCase(text.ToLower(culture));
        }
    }
}
