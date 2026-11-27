using GestionCarp.Core.Localization;
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
    Html,
    Excel,
    Word,
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
    /// <summary>Exporta a texto. Para Excel y Word (binarios) devuelve el árbol en texto como vista previa.</summary>
    public static string Export(TreeNode root, TreeExportOptions options) => options.Format switch
    {
        TreeExportFormat.Markdown => ToMarkdown(root, options),
        TreeExportFormat.Json => ToJson(root, options),
        TreeExportFormat.Html => ToHtml(root, options),
        _ => ToText(root, options),
    };

    /// <summary>Formatos que solo se pueden guardar en archivo (no copiar como texto).</summary>
    public static bool IsBinary(TreeExportFormat format) => format is TreeExportFormat.Excel or TreeExportFormat.Word;

    public static string FileExtension(TreeExportFormat format) => format switch
    {
        TreeExportFormat.Markdown => ".md",
        TreeExportFormat.Json => ".json",
        TreeExportFormat.Html => ".html",
        TreeExportFormat.Excel => ".xlsx",
        TreeExportFormat.Word => ".docx",
        _ => ".txt",
    };

    /// <summary>Guarda la exportación en un archivo, en el formato indicado.</summary>
    public static void SaveToFile(TreeNode root, TreeExportOptions options, string path)
    {
        switch (options.Format)
        {
            case TreeExportFormat.Excel:
            {
                using var file = File.Create(path);
                OfficeExporter.WriteXlsx(root, options, file);
                break;
            }
            case TreeExportFormat.Word:
            {
                using var file = File.Create(path);
                OfficeExporter.WriteDocx(root, options, file);
                break;
            }
            default:
                File.WriteAllText(path, Export(root, options), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                break;
        }
    }

    // ---------- HTML (página con carpetas plegables) ----------

    private static string ToHtml(TreeNode root, TreeExportOptions options)
    {
        string title = root.Name.TrimEnd('/', '\\');
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>")
          .AppendLine($"<html lang=\"{Loc.Code(Loc.Current.Language)}\">")
          .AppendLine("<head>")
          .AppendLine("<meta charset=\"utf-8\">")
          .AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
          .AppendLine($"<title>{Html(title)}</title>")
          .AppendLine("<style>")
          .AppendLine(":root{--bg:#fff;--fg:#1f2937;--muted:#6b7280;--line:#e5e7eb;--accent:#2563eb}")
          .AppendLine("@media (prefers-color-scheme:dark){:root{--bg:#111827;--fg:#e5e7eb;--muted:#9ca3af;--line:#374151;--accent:#60a5fa}}")
          .AppendLine("body{margin:0;padding:24px 16px;background:var(--bg);color:var(--fg);font:15px/1.5 system-ui,-apple-system,'Segoe UI',sans-serif}")
          .AppendLine("main{max-width:960px;margin:0 auto}h1{font-size:22px;margin:0 0 4px}.meta{color:var(--muted);margin:0 0 16px}")
          .AppendLine(".tools button{font:inherit;padding:4px 10px;margin-right:6px;border:1px solid var(--line);border-radius:6px;background:transparent;color:var(--fg);cursor:pointer}")
          .AppendLine("ul{list-style:none;margin:0;padding-left:20px;border-left:1px solid var(--line)}main>ul{border:0;padding:0;margin-top:12px}")
          .AppendLine("li{margin:2px 0;overflow-wrap:anywhere}summary{cursor:pointer;font-weight:600}summary::marker{color:var(--accent)}")
          .AppendLine(".d{color:var(--muted);font-size:13px;font-weight:400;margin-left:8px}footer{margin-top:24px;color:var(--muted);font-size:12px}")
          .AppendLine("</style>")
          .AppendLine("</head>")
          .AppendLine("<body><main>")
          .AppendLine($"<h1>📁 {Html(title)}</h1>")
          .AppendLine($"<p class=\"meta\">{Html(L.F("doc_summary", root.FolderCount, root.FileCount, SizeFormatter.Format(root.Size), DateTime.Now.ToString("g", Loc.Current.Culture)))}</p>")
          .AppendLine($"<div class=\"tools\"><button onclick=\"document.querySelectorAll('details').forEach(d=>d.open=true)\">{Html(L.T("html_expand"))}</button>" +
                      $"<button onclick=\"document.querySelectorAll('details').forEach(d=>d.open=false)\">{Html(L.T("html_collapse"))}</button></div>")
          .AppendLine("<ul>");
        WriteHtml(root, depth: 0, options, sb);
        sb.AppendLine("</ul>")
          .AppendLine("<footer>GestionCarp</footer>")
          .AppendLine("</main></body></html>");
        return sb.ToString();
    }

    private static void WriteHtml(TreeNode node, int depth, TreeExportOptions options, StringBuilder sb)
    {
        string details = Details(node, options).Trim();
        string detailsHtml = details.Length == 0 ? "" : $"<span class=\"d\">{Html(details.Trim('(', ')'))}</span>";
        if (!node.IsDirectory)
        {
            sb.AppendLine($"<li>📄 {Html(node.Name)}{detailsHtml}</li>");
            return;
        }

        bool beyondDepth = options.MaxDepth > 0 && depth + 1 > options.MaxDepth;
        var children = beyondDepth ? new List<TreeNode>() : VisibleChildren(node, options);
        sb.AppendLine($"<li><details{(depth < 2 ? " open" : "")}><summary>📁 {Html(node.Name.TrimEnd('/', '\\'))}{detailsHtml}</summary><ul>");
        foreach (var child in children) WriteHtml(child, depth + 1, options, sb);
        if (node.IsTruncated || (beyondDepth && node.Children.Count > 0)) sb.AppendLine("<li>…</li>");
        sb.AppendLine("</ul></details></li>");
    }

    private static string Html(string text) => System.Net.WebUtility.HtmlEncode(text);

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
            parts.Add(L.F("tree_counts", node.FolderCount, node.FileCount));
        if (options.ShowDates && node.LastModified != DateTime.MinValue)
            parts.Add(node.LastModified.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        if (node.Error is not null) parts.Add(node.Error.ToLowerInvariant());
        return parts.Count == 0 ? "" : "  (" + string.Join(" · ", parts) + ")";
    }
}
