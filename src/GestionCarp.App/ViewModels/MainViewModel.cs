using System;
using System.IO;
using System.Linq;
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
        Compare = new CompareViewModel(service);
        Duplicates = new DuplicatesViewModel();
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
            4 => "comparar",
            5 => "duplicados",
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
            Compare.OnLanguageChanged();
            Duplicates.OnLanguageChanged();
            App.RefreshShellIntegration();
        };
    }

    public ExplorerViewModel Explorer { get; }
    public RenamerViewModel Renamer { get; }
    public SubtitlesViewModel Subtitles { get; }
    public StructureViewModel Structure { get; }
    public CompareViewModel Compare { get; }
    public DuplicatesViewModel Duplicates { get; }

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

    /// <summary>Activa o quita «GestionCarp» del menú contextual del Explorador de Windows.</summary>
    public bool ExplorerMenu
    {
        get => App.Settings.ExplorerMenu;
        set
        {
            try
            {
                if (value) ShellIntegration.Install();
                else ShellIntegration.Uninstall();
                App.Settings.ExplorerMenu = value;
                App.Settings.Save();
                if (value) Dialogs.Info(L.T("shell_installed"));
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException or IOException)
            {
                Dialogs.Error(L.F("shell_failed", ex.Message));
            }
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Atiende los argumentos de línea de comandos (menú contextual del Explorador o arrastrar sobre el .exe):
    /// «--explorar ruta», «--renombrar rutas…», «--subtitulos», «--comparar», «--duplicados», «--crear», o solo rutas.
    /// </summary>
    public void HandleArguments(string[] args)
    {
        if (args.Length == 0) return;
        string verb = args[0].StartsWith("--", StringComparison.Ordinal) ? args[0][2..].ToLowerInvariant() : "explorar";
        var paths = (args[0].StartsWith("--", StringComparison.Ordinal) ? args.Skip(1) : args)
            .Select(p => p.Trim().Trim('"'))
            .Where(p => p.Length > 0 && (Directory.Exists(p) || File.Exists(p)))
            .ToList();
        if (paths.Count == 0) return;
        string folder = Directory.Exists(paths[0]) ? paths[0] : Path.GetDirectoryName(paths[0]) ?? paths[0];

        switch (verb)
        {
            case "renombrar":
                Renamer.AddPaths(paths);
                SelectedTab = 1;
                break;
            case "subtitulos":
                Subtitles.FolderPath = folder;
                Subtitles.Analyze();
                SelectedTab = 2;
                break;
            case "crear":
                Structure.Destination = folder;
                SelectedTab = 3;
                break;
            case "comparar":
                Compare.AddFolder(folder);
                SelectedTab = 4;
                break;
            case "duplicados":
                foreach (var p in paths.Where(Directory.Exists)) Duplicates.AddFolder(p, search: false);
                _ = Duplicates.SearchAsync();
                SelectedTab = 5;
                break;
            default:
                Explorer.RootPath = folder;
                _ = Explorer.ScanAsync();
                SelectedTab = 0;
                break;
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
