namespace GestionCarp.Core.Rename.Rules;

public enum InsertPosition
{
    Start,
    End,
    /// <summary>Después de N caracteres contados desde el inicio.</summary>
    AtIndex,
}

/// <summary>Inserta un texto al inicio, al final o en una posición concreta.</summary>
public sealed class InsertRule : RenameRule
{
    private string _text = "";
    private InsertPosition _position = InsertPosition.Start;
    private int _index;

    public override string DisplayName => "Insertar";

    public string Text { get => _text; set => SetProperty(ref _text, value ?? ""); }
    public InsertPosition Position { get => _position; set => SetProperty(ref _position, value); }

    /// <summary>Número de caracteres que quedan antes del texto insertado (0 = al inicio).</summary>
    public int Index { get => _index; set => SetProperty(ref _index, Math.Max(0, value)); }

    public override string Summary => Position switch
    {
        InsertPosition.End => $"\"{Text}\" al final",
        InsertPosition.AtIndex => $"\"{Text}\" tras {Index} caracteres",
        _ => $"\"{Text}\" al inicio",
    };

    protected override string ApplyCore(string text, RenameContext context) => Position switch
    {
        InsertPosition.End => text + Text,
        InsertPosition.AtIndex => text.Insert(Math.Min(Index, text.Length), Text),
        _ => Text + text,
    };
}
