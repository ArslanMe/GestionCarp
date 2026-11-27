using System.Windows;
using System.Windows.Threading;

namespace GestionCarp.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnUnhandledException;
        base.OnStartup(e);
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show("Ha ocurrido un error inesperado:\n\n" + e.Exception.Message, "GestionCarp",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
