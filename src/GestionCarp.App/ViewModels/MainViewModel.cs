using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using GestionCarp.App.Services;
using GestionCarp.App.Views;
using GestionCarp.Core.Common;
using GestionCarp.Core.Localization;
using GestionCarp.Core.Rename;
using GestionCarp.Core.Updates;

namespace GestionCarp.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private int _selectedTab;

    public MainViewModel()
    {
        string historyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GestionCarp", "historial.json");
        var service = new RenameService(new UndoHistory(historyPath));

        Renamer = new RenamerViewModel(service);
        Subtitles = new SubtitlesViewModel(service);
        Structure = new StructureViewModel(service);
        Explorer = new ExplorerViewModel(
            sendToRenamer: paths =>
            {
                Renamer.AddPaths(paths);
                SelectedTab = 1;
            },
            sendToSubtitles: folder =>
            {
                Subtitles.FolderPath = folder;
                Subtitles.Analyze();
                SelectedTab = 2;
            },
            sendToStructure: (text, destination) =>
            {
                Structure.LoadTemplate(text, destination);
                SelectedTab = 3;
            });

        ExitCommand = new RelayCommand(() => Application.Current.Shutdown());
        SetLanguageCommand = new RelayCommand(p => SetLanguage(p as string));
        CheckUpdatesCommand = new RelayCommand(() => _ = Updater.CheckAsync(silent: false));
        OpenGitHubCommand = new RelayCommand(() => AppInfo.OpenUrl(UpdateChecker.RepositoryUrl));
        AboutCommand = new RelayCommand(ShowAbout);
        HelpCommand = new RelayCommand(() => HelpService.Open(SelectedTab switch
        {
            0 => "explorador",
            1 => "renombrar",
            2 => "subtitulos",
            3 => "crear",
            _ => "inicio",
        }));

        Loc.Current.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsSpanish));
            OnPropertyChanged(nameof(IsEnglish));
            OnPropertyChanged(nameof(IsPortuguese));
            Explorer.OnLanguageChanged();
            Renamer.OnLanguageChanged();
            Subtitles.OnLanguageChanged();
            Structure.OnLanguageChanged();
        };
    }

    public ExplorerViewModel Explorer { get; }
    public RenamerViewModel Renamer { get; }
    public SubtitlesViewModel Subtitles { get; }
    public StructureViewModel Structure { get; }

    public ICommand ExitCommand { get; }
    public ICommand SetLanguageCommand { get; }
    public ICommand CheckUpdatesCommand { get; }
    public ICommand OpenGitHubCommand { get; }
    public ICommand AboutCommand { get; }
    public ICommand HelpCommand { get; }

    public int SelectedTab { get => _selectedTab; set => SetProperty(ref _selectedTab, value); }

    public bool IsSpanish => Loc.Current.Language == AppLanguage.Spanish;
    public bool IsEnglish => Loc.Current.Language == AppLanguage.English;
    public bool IsPortuguese => Loc.Current.Language == AppLanguage.Portuguese;

    public bool CheckUpdatesOnStartup
    {
        get => App.Settings.CheckUpdatesOnStartup;
        set
        {
            App.Settings.CheckUpdatesOnStartup = value;
            App.Settings.Save();
            OnPropertyChanged();
        }
    }

    private void SetLanguage(string? code)
    {
        var language = Loc.FromCode(code);
        App.Settings.Language = Loc.Code(language);
        App.Settings.Save();
        Loc.Current.Language = language;
        // Por si se pulsó el idioma que ya estaba (el menú desmarca la casilla al hacer clic).
        OnPropertyChanged(nameof(IsSpanish));
        OnPropertyChanged(nameof(IsEnglish));
        OnPropertyChanged(nameof(IsPortuguese));
    }

    private static void ShowAbout()
    {
        var window = new AboutWindow();
        if (Application.Current.MainWindow is { IsLoaded: true } owner) window.Owner = owner;
        window.ShowDialog();
    }
}
