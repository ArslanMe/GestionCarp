using System.Text.RegularExpressions;
using GestionCarp.Core.Localization;

namespace GestionCarp.Core.Tests;

public class LocalizationTests
{
    private static readonly Regex Placeholder = new(@"\{(\d+)(?::[^}]*)?\}");

    [Fact]
    public void EveryTextHasThreeLanguagesWithTheSamePlaceholders()
    {
        foreach (var (key, texts) in Strings.Table)
        {
            Assert.True(texts.Length == 3, $"«{key}» debe tener 3 idiomas");
            Assert.All(texts, t => Assert.False(string.IsNullOrWhiteSpace(t), $"«{key}» tiene un texto vacío"));
            var expected = Placeholders(texts[0]);
            Assert.All(texts, t => Assert.True(expected.SetEquals(Placeholders(t)), $"«{key}»: marcadores distintos en «{t}»"));
        }
    }

    [Fact]
    public void EveryKeyUsedInTheSourceExists()
    {
        string root = FindRepositoryRoot();
        var patterns = new[]
        {
            new Regex(@"\bL\.[TF]\(""([a-z0-9_]+)"""),
            new Regex(@"\{l:Tr ([a-z0-9_]+)\}"),
            new Regex(@"new Choice\([^,]+,\s*""([a-z0-9_]+)""\)"),
        };

        var used = new HashSet<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
                     .Where(f => f.EndsWith(".cs") || f.EndsWith(".xaml"))
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            string text = File.ReadAllText(file);
            foreach (var pattern in patterns)
                foreach (Match m in pattern.Matches(text))
                    used.Add(m.Groups[1].Value);
        }

        Assert.NotEmpty(used);
        var missing = used.Where(k => !Strings.Table.ContainsKey(k)).OrderBy(k => k).ToList();
        Assert.True(missing.Count == 0, "Claves sin traducir: " + string.Join(", ", missing));
    }

    [Theory]
    [InlineData("es", AppLanguage.Spanish)]
    [InlineData("EN", AppLanguage.English)]
    [InlineData("pt", AppLanguage.Portuguese)]
    public void ParsesLanguageCodes(string code, AppLanguage expected) => Assert.Equal(expected, Loc.FromCode(code));

    private static HashSet<string> Placeholders(string text)
        => Placeholder.Matches(text).Select(m => m.Groups[1].Value).ToHashSet();

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GestionCarp.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontró GestionCarp.sln");
    }
}

public class HelpGuideTests
{
    [Fact]
    public void GuideHasEverySectionInEveryLanguage()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GestionCarp.sln"))) dir = dir.Parent;
        string html = File.ReadAllText(Path.Combine(dir!.FullName, "src", "GestionCarp.App", "Assets", "Help", "guia.html"));

        // Los apartados que abre F1 desde cada pestaña, más los del índice.
        var sections = new[] { "inicio", "explorador", "renombrar", "reglas", "series", "presets", "subtitulos", "crear", "deshacer", "ajustes", "faq" };
        foreach (var lang in new[] { "es", "en", "pt" })
        {
            Assert.Contains($"<article lang=\"{lang}\">", html);
            foreach (var section in sections)
            {
                Assert.Contains($"id=\"{lang}-{section}\"", html);
                Assert.Contains($"href=\"#{lang}-{section}\"", html);
            }
        }
        Assert.DoesNotContain("http://", html);
        Assert.DoesNotContain("https://", html); // sin dependencias externas
    }
}
