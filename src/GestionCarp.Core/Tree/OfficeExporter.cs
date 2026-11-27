using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using GestionCarp.Core.Localization;

namespace GestionCarp.Core.Tree;

/// <summary>
/// Genera archivos .xlsx y .docx sin dependencias externas (Office Open XML mínimo),
/// que se abren en Excel, Word, LibreOffice y Google Docs/Sheets.
/// </summary>
internal static class OfficeExporter
{
    // ---------- Excel ----------

    public static void WriteXlsx(TreeNode root, TreeExportOptions options, Stream output)
    {
        var rows = new List<(int Level, TreeNode Node, string RelativePath)>();
        Collect(root, 0, "", options, rows);

        string[] headers =
        {
            L.T("xl_level"), L.T("xl_type"), L.T("xl_name"), L.T("xl_path"), L.T("xl_bytes"),
            L.T("xl_size"), L.T("xl_modified"), L.T("xl_files"), L.T("xl_folders"),
        };

        var sheet = new StringBuilder();
        sheet.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""")
             .Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""")
             .Append("""<sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>""")
             .Append("<cols>")
             .Append("""<col min="1" max="1" width="7" customWidth="1"/><col min="2" max="2" width="10" customWidth="1"/>""")
             .Append("""<col min="3" max="3" width="48" customWidth="1"/><col min="4" max="4" width="60" customWidth="1"/>""")
             .Append("""<col min="5" max="6" width="14" customWidth="1"/><col min="7" max="7" width="18" customWidth="1"/>""")
             .Append("""<col min="8" max="9" width="10" customWidth="1"/>""")
             .Append("</cols><sheetData>");

        sheet.Append("""<row r="1">""");
        for (int c = 0; c < headers.Length; c++) TextCell(sheet, c, 1, headers[c], style: 1);
        sheet.Append("</row>");

        int r = 2;
        foreach (var (level, node, relative) in rows)
        {
            sheet.Append($"""<row r="{r}">""");
            NumberCell(sheet, 0, r, level, style: 0);
            TextCell(sheet, 1, r, node.IsDirectory ? L.T("xl_folder") : L.T("xl_file"), style: 0);
            TextCell(sheet, 2, r, new string(' ', level * 4) + node.Name, style: node.IsDirectory ? 1 : 0);
            TextCell(sheet, 3, r, relative, style: 0);
            NumberCell(sheet, 4, r, node.Size, style: 2);
            TextCell(sheet, 5, r, Common.SizeFormatter.Format(node.Size), style: 3);
            if (node.LastModified != DateTime.MinValue)
                TextCell(sheet, 6, r, node.LastModified.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), style: 0);
            if (node.IsDirectory)
            {
                NumberCell(sheet, 7, r, node.FileCount, style: 2);
                NumberCell(sheet, 8, r, node.FolderCount, style: 2);
            }
            sheet.Append("</row>");
            r++;
        }
        sheet.Append("</sheetData>")
             .Append($"""<autoFilter ref="A1:I{Math.Max(1, r - 1)}"/>""")
             .Append("</worksheet>");

        string sheetName = SheetName(L.T("xl_sheet"));
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        Add(zip, "[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""");
        Add(zip, "_rels/.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
        Add(zip, "xl/workbook.xml", $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="{Escape(sheetName)}" sheetId="1" r:id="rId1"/></sheets><definedNames><definedName name="_xlnm._FilterDatabase" localSheetId="0" hidden="1">'{Escape(sheetName.Replace("'", "''"))}'!$A$1:$I${Math.Max(1, r - 1)}</definedName></definedNames></workbook>""");
        Add(zip, "xl/_rels/workbook.xml.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""");
        Add(zip, "xl/styles.xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="4"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="3" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" applyAlignment="1"><alignment horizontal="right"/></xf></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>""");
        Add(zip, "xl/worksheets/sheet1.xml", sheet.ToString());
    }

    private static void Collect(TreeNode node, int level, string parentPath, TreeExportOptions options, List<(int, TreeNode, string)> rows)
    {
        string relative = level == 0 ? node.Name : (parentPath.Length == 0 ? node.Name : parentPath + "/" + node.Name);
        rows.Add((level, node, level == 0 ? node.Name : relative));
        if (!node.IsDirectory || (options.MaxDepth > 0 && level + 1 > options.MaxDepth)) return;
        foreach (var child in node.Children)
        {
            if (!child.IsDirectory && !options.IncludeFiles) continue;
            Collect(child, level + 1, level == 0 ? "" : relative, options, rows);
        }
    }

    private static string Column(int index) => ((char)('A' + index)).ToString();

    private static void TextCell(StringBuilder sb, int col, int row, string text, int style)
        => sb.Append($"""<c r="{Column(col)}{row}" t="inlineStr" s="{style}"><is><t xml:space="preserve">{Escape(text)}</t></is></c>""");

    private static void NumberCell(StringBuilder sb, int col, int row, long value, int style)
        => sb.Append($"""<c r="{Column(col)}{row}" s="{style}"><v>{value.ToString(CultureInfo.InvariantCulture)}</v></c>""");

    private static string SheetName(string name)
    {
        var clean = new string(name.Where(c => "[]:*?/\\".IndexOf(c) < 0).ToArray()).Trim();
        if (clean.Length == 0) clean = "Sheet1";
        return clean.Length > 31 ? clean[..31] : clean;
    }

    // ---------- Word ----------

    public static void WriteDocx(TreeNode root, TreeExportOptions options, Stream output)
    {
        var textOptions = new TreeExportOptions
        {
            Format = TreeExportFormat.Text,
            IncludeFiles = options.IncludeFiles,
            ShowSizes = options.ShowSizes,
            ShowDates = options.ShowDates,
            ShowCounts = options.ShowCounts,
            MaxDepth = options.MaxDepth,
        };
        var lines = TreeExporter.Export(root, textOptions).TrimEnd().Split(Environment.NewLine);

        var body = new StringBuilder();
        body.Append(Paragraph(L.F("doc_title", root.Name.TrimEnd('/', '\\')), bold: true, size: 32, mono: false, after: 80));
        body.Append(Paragraph(L.F("doc_summary", root.FolderCount, root.FileCount, Common.SizeFormatter.Format(root.Size),
            DateTime.Now.ToString("g", Loc.Current.Culture)), bold: false, size: 20, mono: false, after: 240, color: "666666"));
        foreach (var line in lines)
            body.Append(Paragraph(line, bold: false, size: 19, mono: true, after: 0));

        string document = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>"""
                          + body
                          + """<w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1134" w:right="1134" w:bottom="1134" w:left="1134" w:header="709" w:footer="709" w:gutter="0"/></w:sectPr></w:body></w:document>""";

        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        Add(zip, "[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>""");
        Add(zip, "_rels/.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>""");
        Add(zip, "word/document.xml", document);
    }

    private static string Paragraph(string text, bool bold, int size, bool mono, int after, string? color = null)
    {
        var run = new StringBuilder("<w:rPr>");
        if (mono) run.Append("""<w:rFonts w:ascii="Consolas" w:hAnsi="Consolas" w:cs="Consolas"/>""");
        if (bold) run.Append("<w:b/>");
        if (color is not null) run.Append($"""<w:color w:val="{color}"/>""");
        run.Append($"""<w:sz w:val="{size}"/><w:szCs w:val="{size}"/></w:rPr>""");
        return $"""<w:p><w:pPr><w:spacing w:before="0" w:after="{after}" w:line="240" w:lineRule="auto"/></w:pPr><w:r>{run}<w:t xml:space="preserve">{Escape(text)}</w:t></w:r></w:p>""";
    }

    // ---------- Comunes ----------

    private static void Add(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string Escape(string text)
    {
        var clean = new string(text.Where(c => c >= 0x20 || c == '\t').ToArray());
        return SecurityElement.Escape(clean) ?? "";
    }
}
