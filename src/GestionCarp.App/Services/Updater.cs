using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GestionCarp.Core.Localization;
using GestionCarp.Core.Updates;

namespace GestionCarp.App.Services;

/// <summary>
/// Busca versiones nuevas en GitHub y, si el usuario quiere, descarga el nuevo GestionCarp.exe,
/// sustituye al actual y reinicia la aplicación.
/// </summary>
public static class Updater
{
    private const string NewFileName = "GestionCarp.new.exe";
    private const string OldFileName = "GestionCarp.old.exe";

    private static readonly HttpClient Http = CreateClient();
    private static bool _busy;

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GestionCarp/" + AppInfo.VersionText);
        return client;
    }

    /// <param name="silent">Al iniciar: no avisa si no hay novedades ni si no hay conexión.</param>
    public static async Task CheckAsync(bool silent)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            ReleaseInfo? latest;
            try
            {
                latest = await UpdateChecker.GetLatestAsync(Http);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                if (!silent) Dialogs.Error(L.F("upd_error", ex.Message));
                return;
            }

            if (latest is null)
            {
                if (!silent) Dialogs.Info(L.T("upd_no_releases"));
                return;
            }

            if (!UpdateChecker.IsNewer(latest.Version, AppInfo.Version))
            {
                if (!silent) Dialogs.Info(L.F("upd_uptodate", AppInfo.VersionText));
                return;
            }

            if (Dialogs.Confirm(L.F("upd_available", latest.Version.ToString(3), AppInfo.VersionText)))
                await InstallAsync(latest);
        }
        finally
        {
            _busy = false;
        }
    }

    private static async Task InstallAsync(ReleaseInfo release)
    {
        string? exePath = Environment.ProcessPath;
        bool canReplace = release.ExeDownloadUrl is not null && exePath is not null
                          && Path.GetExtension(exePath).Equals(".exe", StringComparison.OrdinalIgnoreCase)
                          && !Path.GetFileName(exePath).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase);
        if (!canReplace)
        {
            Dialogs.Info(L.T("upd_manual"));
            AppInfo.OpenUrl(release.PageUrl);
            return;
        }

        string folder = Path.GetDirectoryName(exePath!)!;
        string newPath = Path.Combine(folder, NewFileName);
        string oldPath = Path.Combine(folder, OldFileName);
        var progress = new DownloadWindow();
        progress.Show();
        bool movedCurrent = false;

        try
        {
            using (var response = await Http.GetAsync(release.ExeDownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                long? total = response.Content.Headers.ContentLength;
                await using var source = await response.Content.ReadAsStreamAsync();
                await using var target = File.Create(newPath);
                var buffer = new byte[81920];
                long done = 0;
                int read;
                while ((read = await source.ReadAsync(buffer)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read));
                    done += read;
                    if (total > 0) progress.SetProgress((int)(done * 100 / total.Value));
                }
            }

            // Windows permite renombrar un .exe en uso (no borrarlo): el viejo se elimina en el próximo arranque.
            if (File.Exists(oldPath)) File.Delete(oldPath);
            File.Move(exePath!, oldPath);
            movedCurrent = true;
            File.Move(newPath, exePath!);

            Process.Start(new ProcessStartInfo(exePath!) { UseShellExecute = true });
            progress.Close();
            Application.Current.Shutdown();
        }
        catch (UnauthorizedAccessException)
        {
            progress.Close();
            Restore(exePath!, newPath, oldPath, movedCurrent);
            Dialogs.Info(L.T("upd_no_permission"));
            AppInfo.OpenUrl(release.PageUrl);
        }
        catch (Exception ex)
        {
            progress.Close();
            Restore(exePath!, newPath, oldPath, movedCurrent);
            Dialogs.Error(L.F("upd_failed", ex.Message));
        }
    }

    private static void Restore(string exePath, string newPath, string oldPath, bool movedCurrent)
    {
        try
        {
            if (movedCurrent && !File.Exists(exePath) && File.Exists(oldPath)) File.Move(oldPath, exePath);
            if (File.Exists(newPath)) File.Delete(newPath);
        }
        catch (Exception)
        {
            // Si no se puede limpiar ahora, se intentará en el próximo arranque.
        }
    }

    /// <summary>Borra el ejecutable antiguo que dejó la última actualización.</summary>
    public static void CleanupPreviousVersion()
    {
        string? exePath = Environment.ProcessPath;
        if (exePath is null) return;
        string folder = Path.GetDirectoryName(exePath)!;
        _ = Task.Run(async () =>
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    foreach (var name in new[] { OldFileName, NewFileName })
                    {
                        string path = Path.Combine(folder, name);
                        if (File.Exists(path)) File.Delete(path);
                    }
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // El proceso anterior puede tardar un momento en cerrarse.
                    await Task.Delay(1000);
                }
            }
        });
    }

    private sealed class DownloadWindow : Window
    {
        private readonly ProgressBar _bar = new() { Height = 18, Minimum = 0, Maximum = 100, IsIndeterminate = true };
        private readonly TextBlock _text = new() { Text = L.T("upd_downloading"), Margin = new Thickness(0, 0, 0, 10) };

        public DownloadWindow()
        {
            Title = "GestionCarp";
            Width = 400;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.ToolWindow;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if (Application.Current.MainWindow is { IsLoaded: true } owner && !ReferenceEquals(owner, this))
                Owner = owner;
            Content = new StackPanel { Margin = new Thickness(18), Children = { _text, _bar } };
        }

        public void SetProgress(int percent)
        {
            _bar.IsIndeterminate = false;
            _bar.Value = percent;
            _text.Text = L.F("upd_downloading_pct", percent);
        }
    }
}
