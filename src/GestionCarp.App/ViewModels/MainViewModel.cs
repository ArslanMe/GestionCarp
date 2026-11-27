using System;
using System.IO;
using GestionCarp.App.Services;
using GestionCarp.Core.Common;
using GestionCarp.Core.Rename;

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
            });
    }

    public ExplorerViewModel Explorer { get; }
    public RenamerViewModel Renamer { get; }
    public SubtitlesViewModel Subtitles { get; }

    public int SelectedTab { get => _selectedTab; set => SetProperty(ref _selectedTab, value); }
}
