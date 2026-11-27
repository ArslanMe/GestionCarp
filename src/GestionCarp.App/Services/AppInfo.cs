using System;
using System.Diagnostics;
using System.Reflection;
using GestionCarp.Core.Updates;

namespace GestionCarp.App.Services;

public static class AppInfo
{
    public const string Author = "Raudel Camacho";

    public static Version Version { get; } =
        UpdateChecker.Normalize(Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0));

    public static string VersionText => Version.ToString(3);

    public static string Copyright => $"© {Math.Max(2026, DateTime.Now.Year)} {Author}";

    public static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Dialogs.Error(ex.Message);
        }
    }
}
