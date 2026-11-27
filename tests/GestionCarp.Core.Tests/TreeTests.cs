using GestionCarp.Core.Tree;

namespace GestionCarp.Core.Tests;

public class TreeTests
{
    [Fact]
    public void ScansAndExportsTextTree()
    {
        using var t = new TempFolder();
        t.File("Temporada 1/Cap10.mkv", "1234");
        t.File("Temporada 1/Cap2.mkv", "12");
        t.File("Temporada 2/Cap1.mkv");
        t.File("notas.txt");

        var root = TreeScanner.Scan(t.Path, new TreeScanOptions());
        Assert.Equal(4, root.FileCount);
        Assert.Equal(2, root.FolderCount);
        Assert.Equal(8, root.Size);

        string text = TreeExporter.Export(root, new TreeExportOptions());
        var lines = text.TrimEnd().Split(Environment.NewLine);
        Assert.Equal(new[]
        {
            Path.GetFileName(t.Path) + "/",
            "├── Temporada 1/",
            "│   ├── Cap2.mkv",
            "│   └── Cap10.mkv",
            "├── Temporada 2/",
            "│   └── Cap1.mkv",
            "└── notas.txt",
        }, lines);
    }

    [Fact]
    public void RespectsFiltersDepthAndFoldersOnly()
    {
        using var t = new TempFolder();
        t.File("a/b/c/deep.mkv");
        t.File("a/x.mkv");
        t.File("a/x.txt");
        t.File("node_modules/lib.js");

        var root = TreeScanner.Scan(t.Path, new TreeScanOptions
        {
            MaxDepth = 2,
            ExcludePatterns = { "node_modules" },
            FilePatterns = { "*.mkv" },
        });

        Assert.Single(root.Children); // solo "a"
        var a = root.Children[0];
        Assert.Equal(new[] { "b", "x.mkv" }, a.Children.Select(c => c.Name));
        Assert.True(a.Children[0].IsTruncated);

        string foldersOnly = TreeExporter.Export(root, new TreeExportOptions { IncludeFiles = false });
        Assert.DoesNotContain("x.mkv", foldersOnly);
        Assert.Contains("└── …", foldersOnly);
    }

    [Fact]
    public void ExportsMarkdownAndJson()
    {
        using var t = new TempFolder();
        t.File("Serie_A/ep*1.mkv".Replace("*", "_"));

        var root = TreeScanner.Scan(t.Path, new TreeScanOptions());
        string md = TreeExporter.Export(root, new TreeExportOptions { Format = TreeExportFormat.Markdown });
        Assert.Contains("  - 📁 **Serie\\_A/**", md);
        Assert.Contains("    - 📄 ep\\_1.mkv", md);

        string json = TreeExporter.Export(root, new TreeExportOptions { Format = TreeExportFormat.Json, ShowSizes = true });
        Assert.Contains("\"nombre\": \"Serie_A\"", json);
        Assert.Contains("\"bytes\": 1", json);
    }
}
