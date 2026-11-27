using System.IO.Compression;
using System.Xml.Linq;
using GestionCarp.Core.Rename;
using GestionCarp.Core.Rename.Rules;
using GestionCarp.Core.Structure;
using GestionCarp.Core.Tree;

namespace GestionCarp.Core.Tests;

public class CleanupAndTemplateTests
{
    private static RenameContext Ctx(string path = "/x/a.mkv", int index = 0) => new(index, index, 10, path, false);

    [Theory]
    [InlineData("[Grupo] Mi.Serie.S01E02.1080p.WEB-DL.x264-XYZ.mkv", "Mi Serie S01E02.mkv")]
    [InlineData("Breaking_Bad_s02e05_720p_HDTV.mkv", "Breaking Bad s02e05.mkv")]
    [InlineData("La.Pelicula.(2019).BluRay.mkv", "La Pelicula (2019).mkv")]
    [InlineData("Nada que limpiar.mkv", "Nada que limpiar.mkv")]
    public void CleanupRemovesNoise(string input, string expected)
        => Assert.Equal(expected, new CleanupRule().Apply(input, Ctx()));

    [Fact]
    public void CleanupCanRemoveParentheses()
        => Assert.Equal("La Pelicula.mkv", new CleanupRule { RemoveParentheses = true }.Apply("La.Pelicula.(2019).mkv", Ctx()));

    [Fact]
    public void SeriesTemplateUsesNameSeasonEpisodeAndTitle()
    {
        var rule = new SeriesTemplateRule { Template = "{serie} - T{temp:00}E{ep:00} - {titulo}" };
        Assert.Equal("breaking bad - T01E05 - gray matter.mkv",
            rule.Apply("breaking.bad.s01e05.gray.matter.720p.WEB-DL.mkv", Ctx()));
    }

    [Fact]
    public void SeriesTemplateTakesSeasonAndNameFromFolders()
    {
        string path = Path.Combine("x", "Mi Serie", "Temporada 3", "Cap 4.mkv");
        var rule = new SeriesTemplateRule { Template = "{serie} - T{temp:00}E{ep:00}" };
        Assert.Equal("Mi Serie - T03E04.mkv", rule.Apply("Cap 4.mkv", Ctx(path)));
    }

    [Fact]
    public void SeriesTemplateWithExplicitNameAndEnglishTokens()
    {
        var rule = new SeriesTemplateRule { Template = "{series} S{season:00}E{episode:000}", SeriesName = "The Show", DefaultSeason = 2 };
        Assert.Equal("The Show S02E007.mkv", rule.Apply("episodio 7.mkv", Ctx()));
    }

    [Fact]
    public void SeriesTemplateWithoutEpisodeIsAnError()
        => Assert.Throws<RenameRuleException>(() => new SeriesTemplateRule().Apply("sin numero.mkv", Ctx()));
}

public class PresetTests
{
    [Fact]
    public void PresetsRoundTripAllRuleTypes()
    {
        using var t = new TempFolder();
        string file = t.Combine("presets.json");
        var store = new PresetStore(file);
        Assert.NotEmpty(store.Presets); // presets de ejemplo

        var rules = new RenameRule[]
        {
            new NumberingRule { TextBefore = "Cap", Digits = 3, Start = 5, IsEnabled = false },
            new ReplaceRule { Find = ".", ReplaceWith = " ", UseRegex = true },
            new InsertRule { Text = "X", Position = InsertPosition.End },
            new RemoveRule { Mode = RemoveMode.BetweenDelimiters, OpenDelimiter = "(" , CloseDelimiter = ")" },
            new CaseRule { Mode = CaseMode.Upper, Scope = RuleScope.NameAndExtension },
            new CleanupRule { RemoveParentheses = true },
            new SeriesTemplateRule { Template = "{serie} {ep}", SeriesName = "S" },
        };
        store.Save("Mío", rules);

        var reloaded = new PresetStore(file).Find("mío");
        Assert.NotNull(reloaded);
        Assert.Equal(rules.Select(r => r.GetType()), reloaded!.Rules.Select(r => r.GetType()));
        var numbering = Assert.IsType<NumberingRule>(reloaded.Rules[0]);
        Assert.Equal(("Cap", 3, 5, false), (numbering.TextBefore, numbering.Digits, numbering.Start, numbering.IsEnabled));
        Assert.Equal(RuleScope.NameAndExtension, reloaded.Rules[4].Scope);
        Assert.Equal("{serie} {ep}", ((SeriesTemplateRule)reloaded.Rules[6]).Template);
        Assert.DoesNotContain("Summary", File.ReadAllText(file));

        string exported = t.Combine("compartir.json");
        PresetStore.ExportToFile(reloaded, exported);
        Assert.Equal(7, PresetStore.ImportFromFile(exported).Rules.Count);
        Assert.Throws<InvalidDataException>(() => PresetStore.ImportFromFile(t.File("malo.json", "{ nada")));
    }
}

public class StructureTests
{
    [Fact]
    public void ParsesTheExportedTextTree()
    {
        using var t = new TempFolder();
        t.File("Mi Serie/Temporada 1/Cap01.mkv");
        t.Dir("Mi Serie/Temporada 2/Subs");
        var root = TreeScanner.Scan(t.Combine("Mi Serie"), new TreeScanOptions());

        foreach (var format in new[] { TreeExportFormat.Text, TreeExportFormat.Markdown, TreeExportFormat.Json })
        {
            string text = TreeExporter.Export(root, new TreeExportOptions { Format = format, ShowSizes = true, ShowCounts = true });
            var entries = StructureParser.Parse(text);
            Assert.Equal(
                new[] { "Mi Serie", "Mi Serie/Temporada 1", "Mi Serie/Temporada 1/Cap01.mkv", "Mi Serie/Temporada 2", "Mi Serie/Temporada 2/Subs" },
                entries.Select(e => e.RelativePath));
            Assert.Equal(new[] { true, true, false, true, true }, entries.Select(e => e.IsDirectory));
        }
    }

    [Fact]
    public void ParsesIndentedLists()
    {
        const string text = "Proyecto\n    Documentos\n        notas.txt\n    Fotos/\n\tVacaciones\n- Otra.Carpeta\n";
        var entries = StructureParser.Parse(text);
        Assert.Equal(new[] { "Proyecto", "Proyecto/Documentos", "Proyecto/Documentos/notas.txt", "Proyecto/Fotos", "Proyecto/Vacaciones", "Proyecto/Otra.Carpeta" },
            entries.Select(e => e.RelativePath));
        Assert.False(entries[2].IsDirectory);
        Assert.True(entries[5].IsDirectory);
    }

    [Fact]
    public void CreatesAndUndoes()
    {
        using var t = new TempFolder();
        t.Dir("Mi Serie/Temporada 1");
        var entries = StructureParser.Parse("Mi Serie/\n├── Temporada 1/\n├── Temporada 2/\n│   └── leeme.txt\n└── Mal?Nombre/\n    └── dentro/\n");
        var plan = StructureBuilder.Plan(entries, t.Path, createFiles: true);

        Assert.Equal(
            new[] { StructureStatus.Exists, StructureStatus.Exists, StructureStatus.Create, StructureStatus.Create, StructureStatus.Error, StructureStatus.Skipped },
            plan.Select(p => p.Status));

        var result = StructureBuilder.Create(plan, "crear");
        Assert.True(result.Success, result.Error);
        Assert.True(File.Exists(t.Combine("Mi Serie/Temporada 2/leeme.txt")));

        var history = new UndoHistory();
        history.Push(result.Batch!);
        File.WriteAllText(t.Combine("Mi Serie/Temporada 2/otro.txt"), "contenido nuevo");

        var undo = history.UndoLast();
        Assert.True(undo.Success, undo.Error);
        Assert.False(File.Exists(t.Combine("Mi Serie/Temporada 2/leeme.txt")));
        Assert.True(Directory.Exists(t.Combine("Mi Serie/Temporada 2"))); // conservada: ya no está vacía
        Assert.NotNull(undo.Notice);
        Assert.True(Directory.Exists(t.Combine("Mi Serie/Temporada 1"))); // existía antes: no se toca
    }
}

public class OfficeExportTests
{
    private static TreeNode SampleTree(TempFolder t)
    {
        t.File("Serie & Co/Temporada 1/Cap <01>.mkv", "12345");
        t.File("Serie & Co/notas.txt");
        return TreeScanner.Scan(t.Combine("Serie & Co"), new TreeScanOptions());
    }

    [Fact]
    public void WritesValidXlsx()
    {
        using var t = new TempFolder();
        var root = SampleTree(t);
        string path = t.Combine("out.xlsx");
        TreeExporter.SaveToFile(root, new TreeExportOptions { Format = TreeExportFormat.Excel }, path);

        using var zip = ZipFile.OpenRead(path);
        foreach (var entry in zip.Entries) XDocument.Load(entry.Open()); // XML bien formado
        var sheet = XDocument.Load(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        Assert.Equal(5, sheet.Descendants(ns + "row").Count()); // cabecera + 4 elementos
        Assert.Contains(sheet.Descendants(ns + "t"), e => e.Value.Trim() == "Cap <01>.mkv");
    }

    [Fact]
    public void WritesValidDocxAndHtml()
    {
        using var t = new TempFolder();
        var root = SampleTree(t);
        string path = t.Combine("out.docx");
        TreeExporter.SaveToFile(root, new TreeExportOptions { Format = TreeExportFormat.Word }, path);
        using (var zip = ZipFile.OpenRead(path))
        {
            var doc = XDocument.Load(zip.GetEntry("word/document.xml")!.Open());
            Assert.Contains("└── notas.txt", doc.Root!.Value);
        }

        string html = TreeExporter.Export(root, new TreeExportOptions { Format = TreeExportFormat.Html });
        Assert.Contains("Serie &amp; Co", html);
        Assert.Contains("Cap &lt;01&gt;.mkv", html);
        Assert.Contains("<details", html);
    }
}
