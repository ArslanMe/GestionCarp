using GestionCarp.Core.Rename;
using GestionCarp.Core.Rename.Rules;

namespace GestionCarp.Core.Tests;

public class RenamePlannerTests
{
    private static List<RenameItem> Items(params string[] paths) => paths.Select(RenameItem.FromPath).ToList();

    [Fact]
    public void RenamesSubtitlesTogetherWithTheirVideo()
    {
        using var t = new TempFolder();
        var v1 = t.File("serie.e1.mkv");
        var v2 = t.File("serie.e2.mkv");
        t.File("serie.e1.srt");
        t.File("serie.e1.es.forced.srt");
        t.File("serie.e2.en.ass");
        t.File("otro.srt");

        var rows = RenamePlanner.Plan(Items(v1, v2), new RenameRule[] { new NumberingRule { TextBefore = "Mi Serie - Cap" } });

        Assert.All(rows, r => Assert.Equal(RenameStatus.Ready, r.Status));
        Assert.Equal(
            new[] { "Mi Serie - Cap01.mkv", "Mi Serie - Cap01.es.forced.srt", "Mi Serie - Cap01.srt", "Mi Serie - Cap02.mkv", "Mi Serie - Cap02.en.ass" },
            rows.Select(r => r.NewName));
        Assert.True(rows[1].IsCompanion);
        Assert.Equal(v1, rows[1].GroupKey);
    }

    [Fact]
    public void ListedSubtitlesDoNotConsumeNumbers()
    {
        using var t = new TempFolder();
        var v1 = t.File("a.mkv");
        var s1 = t.File("a.srt");
        var v2 = t.File("b.mkv");

        var rows = RenamePlanner.Plan(Items(v1, s1, v2), new RenameRule[] { new NumberingRule { TextBefore = "E" } });
        Assert.Equal(new[] { "E01.mkv", "E01.srt", "E02.mkv" }, rows.Select(r => r.NewName));

        var noCompanions = RenamePlanner.Plan(Items(v1, s1, v2), new RenameRule[] { new NumberingRule { TextBefore = "E" } },
            new RenamePlanOptions { RenameSubtitlesWithVideo = false });
        Assert.Equal(new[] { "E01.mkv", "E02.srt", "E03.mkv" }, noCompanions.Select(r => r.NewName));
    }

    [Fact]
    public void SubtitleBelongsToLongestMatchingVideo()
    {
        using var t = new TempFolder();
        var v1 = t.File("Show.mkv");
        t.File("Show.Part2.mkv");
        t.File("Show.Part2.es.srt");
        t.File("Show.es.srt");

        var rows = RenamePlanner.Plan(Items(v1), new RenameRule[] { new InsertRule { Text = "X " } });
        Assert.Equal(new[] { "X Show.mkv", "X Show.es.srt" }, rows.Select(r => r.NewName));
    }

    [Fact]
    public void DetectsDuplicatesInvalidNamesAndExistingFiles()
    {
        using var t = new TempFolder();
        var a = t.File("a.txt");
        var b = t.File("b.txt");
        var c = t.File("c.txt");
        t.File("taken.txt");

        var dup = RenamePlanner.Plan(Items(a, b), new RenameRule[] { new NumberingRule { TextBefore = "same", Step = 0 } });
        Assert.All(dup, r => Assert.Equal(RenameStatus.Error, r.Status));

        var invalid = RenamePlanner.Plan(Items(a), new RenameRule[] { new InsertRule { Text = "a?b" } });
        Assert.Equal(RenameStatus.Error, invalid[0].Status);
        Assert.Contains("?", invalid[0].Message);

        var exists = RenamePlanner.Plan(Items(c), new RenameRule[] { new ReplaceRule { Find = "c", ReplaceWith = "taken" } });
        Assert.Equal(RenameStatus.Error, exists[0].Status);
    }

    [Fact]
    public void SwapIsAllowed()
    {
        using var t = new TempFolder();
        var a = t.File("1.txt", "primero");
        var b = t.File("2.txt", "segundo");
        // Numerar al revés: 1 → 2 y 2 → 1
        var rows = RenamePlanner.Plan(Items(a, b), new RenameRule[] { new NumberingRule { Start = 2, Step = -1, Digits = 1 } });
        Assert.All(rows, r => Assert.Equal(RenameStatus.Ready, r.Status));

        var result = RenameExecutor.Execute(RenamePlanner.ToOperations(rows), "swap");
        Assert.True(result.Success, result.Error);
        Assert.Equal("primero", File.ReadAllText(t.Combine("2.txt")));
        Assert.Equal("segundo", File.ReadAllText(t.Combine("1.txt")));
    }

    [Theory]
    [InlineData("CON.txt", false)]
    [InlineData("nombre.", false)]
    [InlineData("a:b", false)]
    [InlineData("", false)]
    [InlineData("Capítulo 01 – ñandú.mkv", true)]
    public void ValidatesWindowsNames(string name, bool valid)
        => Assert.Equal(valid, FileNameValidator.Validate(name) is null);
}
