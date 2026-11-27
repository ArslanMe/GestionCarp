using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GestionCarp.Core.Updates;

/// <summary>Una versión publicada en GitHub Releases.</summary>
public sealed record ReleaseInfo(Version Version, string Tag, string PageUrl, string? ExeDownloadUrl, string? Notes);

/// <summary>Consulta la última versión publicada del repositorio en GitHub.</summary>
public static class UpdateChecker
{
    public const string Owner = "ArslanMe";
    public const string Repository = "GestionCarp";
    public const string ExeAssetName = "GestionCarp.exe";

    public static string RepositoryUrl => $"https://github.com/{Owner}/{Repository}";
    public static string LatestReleasePage => RepositoryUrl + "/releases/latest";
    private static string LatestReleaseApi => $"https://api.github.com/repos/{Owner}/{Repository}/releases/latest";

    /// <summary>"v0.1.7" → 0.1.7. Ignora sufijos como "-beta" o "+abc". Devuelve null si no es una versión.</summary>
    public static Version? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string s = text.Trim().TrimStart('v', 'V');
        int cut = s.IndexOfAny(new[] { '-', '+', ' ' });
        if (cut >= 0) s = s[..cut];
        if (!s.Contains('.')) s += ".0";
        return Version.TryParse(s, out var version) ? Normalize(version) : null;
    }

    /// <summary>Iguala las versiones a 3 componentes (1.2 → 1.2.0; 1.2.3.4 → 1.2.3).</summary>
    public static Version Normalize(Version v) => new(v.Major, Math.Max(0, v.Minor), Math.Max(0, v.Build));

    public static bool IsNewer(Version candidate, Version current) => Normalize(candidate) > Normalize(current);

    /// <summary>Lee la respuesta JSON de la API de GitHub.</summary>
    public static ReleaseInfo? ParseRelease(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        string tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
        var version = ParseVersion(tag);
        if (version is null) return null;

        string page = root.TryGetProperty("html_url", out var u) ? u.GetString() ?? LatestReleasePage : LatestReleasePage;
        string? notes = root.TryGetProperty("body", out var b) ? b.GetString() : null;
        string? exeUrl = null;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.TryGetProperty("name", out var n)
                    && string.Equals(n.GetString(), ExeAssetName, StringComparison.OrdinalIgnoreCase)
                    && asset.TryGetProperty("browser_download_url", out var d))
                {
                    exeUrl = d.GetString();
                    break;
                }
            }
        }
        return new ReleaseInfo(version, tag, page, exeUrl, notes);
    }

    /// <summary>Devuelve la última versión publicada, o null si todavía no hay ninguna.</summary>
    public static async Task<ReleaseInfo?> GetLatestAsync(HttpClient http, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("GestionCarp", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseRelease(json);
    }
}
