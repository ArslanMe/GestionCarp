using GestionCarp.Core.Updates;

namespace GestionCarp.Core.Tests;

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("v0.1.7", "0.1.7")]
    [InlineData("0.2", "0.2.0")]
    [InlineData("V1.2.3-beta", "1.2.3")]
    [InlineData("1.2.3.4", "1.2.3")]
    public void ParsesTags(string tag, string expected) => Assert.Equal(Version.Parse(expected), UpdateChecker.ParseVersion(tag));

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    public void RejectsNonVersions(string tag) => Assert.Null(UpdateChecker.ParseVersion(tag));

    [Fact]
    public void ComparesVersions()
    {
        Assert.True(UpdateChecker.IsNewer(new Version(0, 1, 10), new Version(0, 1, 9)));
        Assert.False(UpdateChecker.IsNewer(new Version(0, 1, 5), new Version(0, 1, 5, 0)));
        Assert.False(UpdateChecker.IsNewer(new Version(0, 1, 4), new Version(0, 2, 0)));
    }

    [Fact]
    public void ParsesGitHubReleaseJson()
    {
        const string json = """
        {
          "tag_name": "v0.1.12",
          "html_url": "https://github.com/ArslanMe/GestionCarp/releases/tag/v0.1.12",
          "body": "Notas",
          "assets": [
            { "name": "otra-cosa.zip", "browser_download_url": "https://example.invalid/otra.zip" },
            { "name": "GestionCarp.exe", "browser_download_url": "https://example.invalid/GestionCarp.exe" }
          ]
        }
        """;
        var release = UpdateChecker.ParseRelease(json);
        Assert.NotNull(release);
        Assert.Equal(new Version(0, 1, 12), release!.Version);
        Assert.Equal("https://example.invalid/GestionCarp.exe", release.ExeDownloadUrl);
        Assert.EndsWith("/v0.1.12", release.PageUrl);
    }
}
