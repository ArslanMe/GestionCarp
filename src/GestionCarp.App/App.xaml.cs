using System;
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

        // Si ya hay una ventana abierta, se le pasan los argumentos y esta copia se cierra.
        bool primary = SingleInstance.TryBecomePrimary();
        if (!primary && SingleInstance.SendToPrimary(e.Args))
        {
            Shutdown();
            return;
        }

        Settings = AppSettings.Load();
        Loc.Current.Language = Loc.FromCode(string.IsNullOrEmpty(Settings.Language) ? null : Settings.Language);
        Loc.Current.ApplyCulture();
        Updater.CleanupPreviousVersion();
        RefreshShellIntegration();

        base.OnStartup(e);

        var window = new MainWindow(e.Args);
        MainWindow = window;
        window.Show();

        if (primary)
            SingleInstance.StartListening(args => Dispatcher.BeginInvoke(() => window.HandleArguments(args)));
    }

    /// <summary>Si el programa cambió de carpeta (o de idioma), actualiza el menú contextual.</summary>
    public static void RefreshShellIntegration()
    {
        if (!Settings.ExplorerMenu) return;
        try { ShellIntegration.Install(); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException) { }
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(L.F("app_unexpected", e.Exception.Message), "GestionCarp", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
