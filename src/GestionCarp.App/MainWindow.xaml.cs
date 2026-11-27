using System.Windows;
using GestionCarp.App.Services;
using GestionCarp.App.ViewModels;

namespace GestionCarp.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (App.Settings.CheckUpdatesOnStartup)
            await Updater.CheckAsync(silent: true);
    }
}
