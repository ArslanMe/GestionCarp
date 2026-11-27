namespace GestionCarp.Core.Common;

/// <summary>
/// Compara cadenas en "orden natural": "cap2" va antes que "cap10".
/// Los bloques numéricos se comparan por su valor y el texto sin distinguir mayúsculas.
/// </summary>
public sealed class NaturalStringComparer : IComparer<string?>
{
    public static readonly NaturalStringComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            bool digitX = char.IsAsciiDigit(x[i]);
            bool digitY = char.IsAsciiDigit(y[j]);

            if (digitX && digitY)
            {
                while (i < x.Length && x[i] == '0' && i + 1 < x.Length && char.IsAsciiDigit(x[i + 1])) i++;
                while (j < y.Length && y[j] == '0' && j + 1 < y.Length && char.IsAsciiDigit(y[j + 1])) j++;
                int startX = i, startY = j;
                while (i < x.Length && char.IsAsciiDigit(x[i])) i++;
                while (j < y.Length && char.IsAsciiDigit(y[j])) j++;
                int lenX = i - startX, lenY = j - startY;
                if (lenX != lenY) return lenX.CompareTo(lenY);
                int c = string.CompareOrdinal(x, startX, y, startY, lenX);
                if (c != 0) return c;
            }
            else if (!digitX && !digitY)
            {
                int startX = i, startY = j;
                while (i < x.Length && !char.IsAsciiDigit(x[i])) i++;
                while (j < y.Length && !char.IsAsciiDigit(y[j])) j++;
                int c = string.Compare(x[startX..i], y[startY..j], StringComparison.CurrentCultureIgnoreCase);
                if (c != 0) return c;
            }
            else
            {
                return digitX ? -1 : 1;
            }
        }

        int rest = (x.Length - i).CompareTo(y.Length - j);
        return rest != 0 ? rest : string.CompareOrdinal(x, y);
    }
}
