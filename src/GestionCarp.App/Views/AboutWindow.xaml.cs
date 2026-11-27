using System.Windows;
using GestionCarp.App.Services;
using GestionCarp.Core.Localization;
using GestionCarp.Core.Updates;

namespace GestionCarp.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = L.F("about_version", AppInfo.VersionText);
        AuthorRun.Text = AppInfo.Author;
        CopyrightText.Text = AppInfo.Copyright;
        RepoRun.Text = UpdateChecker.RepositoryUrl.Replace("https://", "");
    }

    private void OnRepoClick(object sender, RoutedEventArgs e) => AppInfo.OpenUrl(UpdateChecker.RepositoryUrl);

    private async void OnCheckUpdates(object sender, RoutedEventArgs e) => await Updater.CheckAsync(silent: false);

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
