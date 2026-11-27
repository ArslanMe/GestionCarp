using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using GestionCarp.Core.Common;

namespace GestionCarp.Core.Tree;

public enum TreeExportFormat
{
    Text,
    Markdown,
    Json,
}

public sealed class TreeExportOptions
{
    public TreeExportFormat Format { get; set; } = TreeExportFormat.Text;
    public bool IncludeFiles { get; set; } = true;
    public bool ShowSizes { get; set; }
    public bool ShowDates { get; set; }

    /// <summary>Muestra en cada carpeta cuántos archivos y subcarpetas contiene.</summary>
    public bool ShowCounts { get; set; }

    /// <summary>Niveles a exportar por debajo de la raíz. 0 = todos.</summary>
    public int MaxDepth { get; set; }
}

/// <summary>Convierte un árbol en texto para copiar, pegar o guardar.</summary>
public static class TreeExporter
{
    public static string Export(TreeNode root, TreeExportOptions options) => options.Format switch
    {
        TreeExportFormat.Text => ToText(root, options),
        TreeExportFormat.Markdown => ToMarkdown(root, options),
        TreeExportFormat.Json => ToJson(root, options),
        _ => throw new ArgumentOutOfRangeException(nameof(options)),
    };

    public static string FileExtension(TreeExportFormat format) => format switch
    {
        TreeExportFormat.Markdown => ".md",
        TreeExportFormat.Json => ".json",
        _ => ".txt",
    };

    // ---------- Texto con líneas (estilo "tree") ----------

    private static string ToText(TreeNode root, TreeExportOptions options)
    {
        var sb = new StringBuilder();
        sb.Append(DisplayName(root)).AppendLine(Details(root, options));
        WriteTextChildren(root, prefix: "", depth: 1, options, sb);
        return sb.ToString();
    }

    private static void WriteTextChildren(TreeNode node, string prefix, int depth, TreeExportOptions options, StringBuilder sb)
    {
        var children = VisibleChildren(node, options);
        bool truncatedHere = node.IsTruncated || (options.MaxDepth > 0 && depth > options.MaxDepth && node.Children.Count > 0);
        if (options.MaxDepth > 0 && depth > options.MaxDepth)
            children = new List<TreeNode>();

        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            bool last = i == children.Count - 1 && !truncatedHere;
            sb.Append(prefix).Append(last ? "└── " : "├── ")
              .Append(DisplayName(child)).AppendLine(Details(child, options));
            if (child.IsDirectory)
                WriteTextChildren(child, prefix + (last ? "    " : "│   "), depth + 1, options, sb);
        }

        if (truncatedHere)
            sb.Append(prefix).AppendLine("└── …");
    }

    // ---------- Markdown (lista anidada) ----------

    private static string ToMarkdown(TreeNode root, TreeExportOptions options)
    {
        var sb = new StringBuilder();
        WriteMarkdown(root, indent: 0, depth: 0, options, sb);
        return sb.ToString();
    }

    private static void WriteMarkdown(TreeNode node, int indent, int depth, TreeExportOptions options, StringBuilder sb)
    {
        sb.Append(' ', indent * 2).Append("- ");
        if (node.IsDirectory)
            sb.Append("📁 **").Append(EscapeMarkdown(DisplayName(node))).Append("**");
        else
            sb.Append("📄 ").Append(EscapeMarkdown(node.Name));
        sb.AppendLine(Details(node, options));

        if (!node.IsDirectory) return;

        bool beyondDepth = options.MaxDepth > 0 && depth + 1 > options.MaxDepth;
        if (!beyondDepth)
        {
            foreach (var child in VisibleChildren(node, options))
                WriteMarkdown(child, indent + 1, depth + 1, options, sb);
        }

        if (node.IsTruncated || (beyondDepth && node.Children.Count > 0))
            sb.Append(' ', (indent + 1) * 2).AppendLine("- …");
    }

    private static string EscapeMarkdown(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c is '\\' or '`' or '*' or '_' or '[' or ']' or '<' or '>' or '#')
                sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }

    // ---------- JSON ----------

    private static string ToJson(TreeNode root, TreeExportOptions options)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            WriteJson(root, depth: 0, options, writer);
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteJson(TreeNode node, int depth, TreeExportOptions options, Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("nombre", node.Name);
        writer.WriteString("tipo", node.IsDirectory ? "carpeta" : "archivo");
        if (options.ShowSizes) writer.WriteNumber("bytes", node.Size);
        if (options.ShowDates && node.LastModified != DateTime.MinValue)
            writer.WriteString("modificado", node.LastModified.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        if (node.IsDirectory)
        {
            if (options.ShowCounts)
            {
                writer.WriteNumber("carpetas", node.FolderCount);
                writer.WriteNumber("archivos", node.FileCount);
            }
            bool beyondDepth = options.MaxDepth > 0 && depth + 1 > options.MaxDepth;
            if (node.IsTruncated || (beyondDepth && node.Children.Count > 0))
                writer.WriteBoolean("incompleto", true);
            if (node.Error is not null)
                writer.WriteString("error", node.Error);

            writer.WriteStartArray("contenido");
            if (!beyondDepth)
            {
                foreach (var child in VisibleChildren(node, options))
                    WriteJson(child, depth + 1, options, writer);
            }
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }

    // ---------- Comunes ----------

    private static List<TreeNode> VisibleChildren(TreeNode node, TreeExportOptions options)
        => options.IncludeFiles ? node.Children : node.Children.Where(c => c.IsDirectory).ToList();

    private static string DisplayName(TreeNode node)
    {
        if (!node.IsDirectory) return node.Name;
        return node.Name.EndsWith(Path.DirectorySeparatorChar) || node.Name.EndsWith(Path.AltDirectorySeparatorChar)
            ? node.Name
            : node.Name + "/";
    }

    private static string Details(TreeNode node, TreeExportOptions options)
    {
        var parts = new List<string>();
        if (options.ShowSizes) parts.Add(SizeFormatter.Format(node.Size));
        if (node.IsDirectory && options.ShowCounts)
            parts.Add($"{node.FolderCount} carpetas, {node.FileCount} archivos");
        if (options.ShowDates && node.LastModified != DateTime.MinValue)
            parts.Add(node.LastModified.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        if (node.Error is not null) parts.Add(node.Error.ToLowerInvariant());
        return parts.Count == 0 ? "" : "  (" + string.Join(" · ", parts) + ")";
    }
}
