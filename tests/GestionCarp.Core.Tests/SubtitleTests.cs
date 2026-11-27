using GestionCarp.Core.Rename;
using GestionCarp.Core.Subtitles;

namespace GestionCarp.Core.Tests;

public class SubtitleTests
{
    [Theory]
    [InlineData("Breaking.Bad.S02E05.720p.WEB", 2, 5)]
    [InlineData("serie 1x07 titulo", 1, 7)]
    [InlineData("Mi Serie - Capítulo 12", null, 12)]
    [InlineData("Mi Serie Cap.03", null, 3)]
    [InlineData("Temporada 2 - Episodio 04", 2, 4)]
    [InlineData("[Grupo] Anime - 05 [1080p][ABCD1234]", null, 5)]
    [InlineData("Show.E09", null, 9)]
    [InlineData("Show 2019 - 08 x264", null, 8)]
    [InlineData("Minha Série - Episódio 07", null, 7)]
    public void DetectsEpisodes(string name, int? season, int episode)
        => Assert.Equal(new EpisodeInfo(season, episode), EpisodeDetector.Detect(name));

    [Fact]
    public void NoEpisodeInPlainName() => Assert.Null(EpisodeDetector.Detect("Pelicula de verano 1080p"));

    [Theory]
    [InlineData("Serie 1x01.es.forced", "Serie 1x01", ".es.forced")]
    [InlineData("Serie.S01E01.spa", "Serie.S01E01", ".spa")]
    [InlineData("Serie.S01E01.pt-BR", "Serie.S01E01", ".pt-BR")]
    [InlineData("Serie.S01E01.WEB-DL", "Serie.S01E01.WEB-DL", "")]
    [InlineData("es", "es", "")]
    public void SplitsLanguageTags(string stem, string expectedBase, string expectedTags)
        => Assert.Equal((expectedBase, expectedTags), SubtitleNaming.SplitLanguageTags(stem));

    [Fact]
    public void MatchesByEpisodeKeepingLanguageAndMovingFromSubsFolder()
    {
        using var t = new TempFolder();
        var v1 = t.File("Serie/Mi Serie - Cap01.mkv");
        var v2 = t.File("Serie/Mi Serie - Cap02.mkv");
        var s1 = t.File("Serie/Subs/serie.s01e01.spanish.srt");
        var s2 = t.File("Serie/serie.s01e02.en.srt");
        var s3 = t.File("Serie/serie.s01e02.srt");
        var s4 = t.File("Serie/readme_subs.srt");

        var rows = SubtitleMatcher.Match(new[] { v1, v2 }, new[] { s1, s2, s3, s4 }, new SubtitleMatchOptions { DefaultLanguage = "es" });
        var byName = rows.ToDictionary(r => r.SourcePath);

        Assert.Equal(t.Combine("Serie/Mi Serie - Cap01.spanish.srt"), byName[s1].TargetPath);
        Assert.Equal("Mi Serie - Cap02.en.srt", byName[s2].NewName);
        Assert.Equal("Mi Serie - Cap02.es.srt", byName[s3].NewName);
        Assert.Equal(RenameStatus.Skipped, byName[s4].Status);
        Assert.All(new[] { s1, s2, s3 }, s => Assert.Equal(RenameStatus.Ready, byName[s].Status));
    }

    [Fact]
    public void MatchesByOrderPerLanguage()
    {
        using var t = new TempFolder();
        var v1 = t.File("A.mkv");
        var v2 = t.File("B.mkv");
        var subs = new[] { t.File("x1.es.srt"), t.File("x2.es.srt"), t.File("x1.en.srt"), t.File("x2.en.srt"), t.File("x3.en.srt") };

        var rows = SubtitleMatcher.Match(new[] { v1, v2 }, subs, new SubtitleMatchOptions { Mode = SubtitleMatchMode.ByOrder });
        var names = rows.Where(r => r.IsMoving).Select(r => r.NewName).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "A.en.srt", "A.es.srt", "B.en.srt", "B.es.srt" }, names);
        Assert.Single(rows, r => r.Status == RenameStatus.Skipped);
    }

    [Fact]
    public void DuplicateSubtitleTargetsAreFlagged()
    {
        using var t = new TempFolder();
        var v = t.File("Cap01.mkv");
        var rows = SubtitleMatcher.Match(new[] { v }, new[] { t.File("ep1.srt"), t.File("episodio 1.srt") }, new SubtitleMatchOptions());
        Assert.All(rows, r => Assert.Equal(RenameStatus.Error, r.Status));
    }
}
