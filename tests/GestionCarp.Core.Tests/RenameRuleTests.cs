using GestionCarp.Core.Rename;
using GestionCarp.Core.Rename.Rules;

namespace GestionCarp.Core.Tests;

public class RenameRuleTests
{
    private static RenameContext Ctx(int index = 0, int inFolder = 0, bool dir = false)
        => new(index, inFolder, 10, "/x", dir);

    [Fact]
    public void NumberingReplacesNameAndKeepsExtension()
    {
        var rule = new NumberingRule { TextBefore = "Mi Serie - Cap", Digits = 2 };
        Assert.Equal("Mi Serie - Cap01.mkv", rule.Apply("asdf.mkv", Ctx(0)));
        Assert.Equal("Mi Serie - Cap12.mkv", rule.Apply("asdf.mkv", Ctx(11)));
    }

    [Fact]
    public void NumberingPrefixStepAndRestartPerFolder()
    {
        var rule = new NumberingRule { Placement = NumberPlacement.Prefix, TextAfter = " - ", Start = 10, Step = 5, Digits = 3, RestartPerFolder = true };
        Assert.Equal("015 - video.mp4", rule.Apply("video.mp4", Ctx(index: 7, inFolder: 1)));
    }

    [Fact]
    public void NumberingOnFolderUsesWholeName()
    {
        var rule = new NumberingRule { TextBefore = "Temporada ", Digits = 1 };
        Assert.Equal("Temporada 1", rule.Apply("Season.One", Ctx(dir: true)));
    }

    [Fact]
    public void ReplaceVariants()
    {
        Assert.Equal("Mi Serie 01.mkv", new ReplaceRule { Find = ".", ReplaceWith = " " }.Apply("Mi.Serie.01.mkv", Ctx()));
        Assert.Equal("b-a.txt", new ReplaceRule { Find = "A", ReplaceWith = "b", ReplaceAll = false }.Apply("a-a.txt", Ctx()));
        Assert.Equal("a-a.txt", new ReplaceRule { Find = "A", ReplaceWith = "b", CaseSensitive = true }.Apply("a-a.txt", Ctx()));
        Assert.Equal("Cap 5.srt", new ReplaceRule { Find = @"^.*?(\d+)$", ReplaceWith = "Cap $1", UseRegex = true }.Apply("x_y_5.srt", Ctx()));
        Assert.Equal("video.mkv", new ReplaceRule { Find = "MKV", ReplaceWith = "mkv", Scope = RuleScope.Extension }.Apply("video.MKV", Ctx()));
    }

    [Fact]
    public void InvalidRegexThrowsRuleException()
        => Assert.Throws<RenameRuleException>(() => new ReplaceRule { Find = "(", UseRegex = true }.Apply("a.txt", Ctx()));

    [Fact]
    public void InsertAndRemove()
    {
        Assert.Equal("[HD] a.mkv", new InsertRule { Text = "[HD] " }.Apply("a.mkv", Ctx()));
        Assert.Equal("a_fin.mkv", new InsertRule { Text = "_fin", Position = InsertPosition.End }.Apply("a.mkv", Ctx()));
        Assert.Equal("ab-cd.mkv", new InsertRule { Text = "-", Position = InsertPosition.AtIndex, Index = 2 }.Apply("abcd.mkv", Ctx()));

        Assert.Equal("cd.txt", new RemoveRule { Count = 2 }.Apply("abcd.txt", Ctx()));
        Assert.Equal("ab.txt", new RemoveRule { Mode = RemoveMode.LastChars, Count = 2 }.Apply("abcd.txt", Ctx()));
        Assert.Equal("ad.txt", new RemoveRule { Mode = RemoveMode.Range, From = 2, Count = 2 }.Apply("abcd.txt", Ctx()));
        Assert.Equal("Serie 01.mkv", new RemoveRule { Mode = RemoveMode.BetweenDelimiters }.Apply("[Grupo] Serie 01 [1080p].mkv", Ctx()));
    }

    [Fact]
    public void ChangesCase()
    {
        Assert.Equal("Mi Serie Genial.MKV", new CaseRule { Mode = CaseMode.Title }.Apply("mI SERIE genial.MKV", Ctx()));
        Assert.Equal("mi serie.mkv", new CaseRule { Mode = CaseMode.Lower, Scope = RuleScope.NameAndExtension }.Apply("Mi Serie.MKV", Ctx()));
        Assert.Equal("01 - Mi serie.txt", new CaseRule { Mode = CaseMode.Sentence }.Apply("01 - mI SERIE.txt", Ctx()));
    }

    [Fact]
    public void SummaryChangesWithProperties()
    {
        var rule = new NumberingRule();
        var changed = new List<string?>();
        rule.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        rule.TextBefore = "Cap";
        Assert.Contains(nameof(RenameRule.Summary), changed);
    }
}
