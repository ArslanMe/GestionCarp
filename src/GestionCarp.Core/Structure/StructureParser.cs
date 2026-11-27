using System.Text.Json;
using System.Text.RegularExpressions;

namespace GestionCarp.Core.Structure;

/// <summary>Un elemento a crear, con su ruta relativa ("Mi Serie/Temporada 1").</summary>
public sealed record StructureEntry(string RelativePath, bool IsDirectory, int Depth, int LineNumber)
{
    public string Name => Path.GetFileName(RelativePath);
}

/// <summary>
/// Convierte texto en una estructura de carpetas. Acepta el árbol que exporta GestionCarp (├── └──),
/// listas con sangría (espacios o tabuladores), listas de Markdown y el JSON exportado.
/// Las líneas que terminan en "/" son carpetas; las demás, archivos si tienen extensión.
/// </summary>
public static class StructureParser
{
    private const string TreeChars = " \t│├└─┬┼┃┣┗━|`+*-•·>";
    private static readonly Regex TrailingDetails = new(@"\s{2,}\([^()]*\)\s*$", RegexOptions.CultureInvariant);
    private static readonly Regex Icons = new(@"^(?:[☀-➿️‍]|\uD83C[\uDC00-\uDFFF]|\uD83D[\uDC00-\uDFFF]|\uD83E[\uDC00-\uDFFF])+\s*", RegexOptions.CultureInvariant);
    private static readonly Regex MarkdownEscape = new(@"\\([\\`*_\[\]<>#])", RegexOptions.CultureInvariant);
    /// <summary>Extensión corta (".mkv", ".docx"): así "Mi.Serie" se considera carpeta.</summary>
    private static readonly Regex FileExtension = new(@"\.[\p{L}\d]{1,4}$", RegexOptions.CultureInvariant);

    public static List<StructureEntry> Parse(string text)
    {
        string trimmed = text.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            var fromJson = TryParseJson(trimmed);
            if (fromJson is not null) return fromJson;
        }
        return ParseLines(text);
    }

    private static List<StructureEntry> ParseLines(string text)
    {
        var raw = new List<(string Path, bool SlashDir, int Depth, int Line)>();
        var stack = new List<(int Indent, string Path)>();
        var lines = text.Replace("\r\n", "\n").Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd();
            if (line.Trim().Length == 0 || line.TrimStart().StartsWith("```")) continue;

            int prefix = 0;
            int indent = 0;
            while (prefix < line.Length && TreeChars.Contains(line[prefix]))
            {
                // Un guion seguido de letra es parte del nombre, no una viñeta.
                if (line[prefix] == '-' && prefix + 1 < line.Length && line[prefix + 1] != ' ' && line[prefix + 1] != '-') break;
                // "**Nombre**" al principio es negrita de Markdown, no una viñeta.
                if (line[prefix] == '*' && prefix + 1 < line.Length && line[prefix + 1] == '*') break;
                indent += line[prefix] == '\t' ? 4 : 1;
                prefix++;
            }

            string name = CleanName(line[prefix..]);
            if (name.Length == 0 || name is "…" or "...") continue;

            bool slashDir = name.EndsWith('/') || name.EndsWith('\\');
            name = name.TrimEnd('/', '\\').Trim();
            if (name.Length == 0) continue;

            while (stack.Count > 0 && stack[^1].Indent >= indent) stack.RemoveAt(stack.Count - 1);
            string path = stack.Count == 0 ? name : stack[^1].Path + "/" + name;
            stack.Add((indent, path));
            raw.Add((path, slashDir, stack.Count - 1, i + 1));
        }

        var parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in raw)
        {
            int cut = entry.Path.LastIndexOf('/');
            if (cut > 0) parents.Add(entry.Path[..cut]);
        }

        return raw.Select(e => new StructureEntry(
                e.Path,
                e.SlashDir || parents.Contains(e.Path) || !FileExtension.IsMatch(e.Path),
                e.Depth,
                e.Line))
            .ToList();
    }

    private static string CleanName(string text)
    {
        string name = TrailingDetails.Replace(text, "").Trim();
        name = Icons.Replace(name, "");
        if (name.StartsWith("**") && name.Contains("**", StringComparison.Ordinal))
            name = name.Replace("**", "");
        name = MarkdownEscape.Replace(name, "$1");
        return name.Trim();
    }

    private static List<StructureEntry>? TryParseJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var result = new List<StructureEntry>();
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                foreach (var item in doc.RootElement.EnumerateArray()) AddJson(item, "", 0, result);
            else
                AddJson(doc.RootElement, "", 0, result);
            return result.Count > 0 ? result : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void AddJson(JsonElement element, string parent, int depth, List<StructureEntry> result)
    {
        if (element.ValueKind != JsonValueKind.Object) return;
        string? name = Get(element, "nombre", "name", "nome");
        if (string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim().TrimEnd('/', '\\');
        string type = Get(element, "tipo", "type") ?? "";
        bool hasChildren = TryChildren(element, out var children);
        bool isDir = hasChildren || type is "carpeta" or "folder" or "pasta" or "directory";
        string path = parent.Length == 0 ? name : parent + "/" + name;
        result.Add(new StructureEntry(path, isDir, depth, 0));
        if (hasChildren)
            foreach (var child in children.EnumerateArray()) AddJson(child, path, depth + 1, result);
    }

    private static bool TryChildren(JsonElement element, out JsonElement children)
    {
        foreach (var key in new[] { "contenido", "children", "conteudo" })
        {
            if (element.TryGetProperty(key, out children) && children.ValueKind == JsonValueKind.Array) return true;
        }
        children = default;
        return false;
    }

    private static string? Get(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
            if (element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        return null;
    }
}
