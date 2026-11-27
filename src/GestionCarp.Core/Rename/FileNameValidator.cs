namespace GestionCarp.Core.Rename;

/// <summary>Comprueba que un nombre sea válido en Windows.</summary>
public static class FileNameValidator
{
    private static readonly char[] InvalidChars =
        "<>:\"/\\|?*".ToCharArray().Concat(Enumerable.Range(0, 32).Select(i => (char)i)).ToArray();

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public const int MaxNameLength = 255;
    public const int MaxPathLength = 259;

    /// <summary>Devuelve un mensaje de error, o null si el nombre es válido.</summary>
    public static string? Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "El nombre está vacío";

        int bad = name.IndexOfAny(InvalidChars);
        if (bad >= 0)
        {
            char c = name[bad];
            return char.IsControl(c)
                ? "Contiene un carácter de control no permitido"
                : $"Contiene un carácter no permitido: {c}";
        }

        if (name.EndsWith('.') || name.EndsWith(' '))
            return "No puede terminar en punto ni en espacio";

        string stem = name.Split('.')[0].TrimEnd();
        if (ReservedNames.Contains(stem))
            return $"\"{stem}\" es un nombre reservado de Windows";

        if (name.Length > MaxNameLength)
            return $"El nombre es demasiado largo ({name.Length} caracteres, máximo {MaxNameLength})";

        return null;
    }

    /// <summary>Elimina o sustituye los caracteres no válidos.</summary>
    public static string Sanitize(string name, string replacement = "")
    {
        var parts = name.Split(InvalidChars);
        return string.Join(replacement, parts).TrimEnd('.', ' ');
    }
}
