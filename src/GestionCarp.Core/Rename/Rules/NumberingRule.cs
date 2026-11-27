using System.Globalization;

namespace GestionCarp.Core.Rename.Rules;

public enum NumberPlacement
{
    /// <summary>El nombre se sustituye por: texto antes + número + texto después.</summary>
    ReplaceName,
    /// <summary>El número se añade al principio del nombre actual.</summary>
    Prefix,
    /// <summary>El número se añade al final del nombre actual.</summary>
    Suffix,
}

/// <summary>Numera los elementos: "Mi Serie - Cap01", "Mi Serie - Cap02"...</summary>
public sealed class NumberingRule : RenameRule
{
    private NumberPlacement _placement = NumberPlacement.ReplaceName;
    private string _textBefore = "";
    private string _textAfter = "";
    private int _start = 1;
    private int _step = 1;
    private int _digits = 2;
    private bool _restartPerFolder;

    public override string DisplayName => "Numerar";

    public NumberPlacement Placement { get => _placement; set => SetProperty(ref _placement, value); }
    public string TextBefore { get => _textBefore; set => SetProperty(ref _textBefore, value ?? ""); }
    public string TextAfter { get => _textAfter; set => SetProperty(ref _textAfter, value ?? ""); }
    public int Start { get => _start; set => SetProperty(ref _start, value); }
    public int Step { get => _step; set => SetProperty(ref _step, value); }

    /// <summary>Cifras mínimas, rellenando con ceros: 2 → 01, 3 → 001.</summary>
    public int Digits { get => _digits; set => SetProperty(ref _digits, Math.Clamp(value, 1, 10)); }

    /// <summary>Reinicia la numeración en cada carpeta (útil para temporadas).</summary>
    public bool RestartPerFolder { get => _restartPerFolder; set => SetProperty(ref _restartPerFolder, value); }

    public override string Summary
    {
        get
        {
            string sample = Format(Start);
            return Placement switch
            {
                NumberPlacement.Prefix => $"Al inicio: \"{sample}…\"",
                NumberPlacement.Suffix => $"Al final: \"…{sample}\"",
                _ => $"Nombre: \"{sample}\", \"{Format(Start + Step)}\"…",
            };
        }
    }

    public string Format(long number)
        => TextBefore + number.ToString("D" + Digits, CultureInfo.InvariantCulture) + TextAfter;

    protected override string ApplyCore(string text, RenameContext context)
    {
        int position = RestartPerFolder ? context.IndexInFolder : context.Index;
        string number = Format(Start + (long)Step * position);
        return Placement switch
        {
            NumberPlacement.Prefix => number + text,
            NumberPlacement.Suffix => text + number,
            _ => number,
        };
    }
}
