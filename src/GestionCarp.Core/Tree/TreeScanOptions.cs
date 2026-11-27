namespace GestionCarp.Core.Tree;

public sealed class TreeScanOptions
{
    /// <summary>Niveles a analizar por debajo de la raíz. 0 = sin límite.</summary>
    public int MaxDepth { get; set; }

    /// <summary>Si es falso, solo se muestran carpetas (los archivos cuentan para tamaños y totales).</summary>
    public bool IncludeFiles { get; set; } = true;

    public bool IncludeHidden { get; set; }

    /// <summary>Nombres o patrones (con * y ?) de carpetas o archivos a omitir por completo.</summary>
    public List<string> ExcludePatterns { get; set; } = new();

    /// <summary>Patrones de archivo a incluir (por ejemplo "*.mkv"). Vacío = todos.</summary>
    public List<string> FilePatterns { get; set; } = new();
}
