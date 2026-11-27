using System.Windows;
using System.Windows.Threading;
using GestionCarp.App.Services;
using GestionCarp.Core.Localization;

namespace GestionCarp.App;

public partial class App : Application
{
    public static AppSettings Settings { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnUnhandledException;

        Settings = AppSettings.Load();
        Loc.Current.Language = Loc.FromCode(string.IsNullOrEmpty(Settings.Language) ? null : Settings.Language);
        Loc.Current.ApplyCulture();
        Updater.CleanupPreviousVersion();

        base.OnStartup(e);
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(L.F("app_unexpected", e.Exception.Message), "GestionCarp", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
