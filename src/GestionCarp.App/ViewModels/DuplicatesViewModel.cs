using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using GestionCarp.App.Services;
using GestionCarp.Core.Common;
using GestionCarp.Core.Duplicates;
using GestionCarp.Core.Localization;

namespace GestionCarp.App.ViewModels;

public enum KeepPolicy
{
    Oldest,
    Newest,
    ShortestPath,
}

/// <summary>Pestaña «Duplicados»: encuentra archivos idénticos y manda a la Papelera las copias sobrantes.</summary>
public sealed class DuplicatesViewModel : ObservableObject
{
    private List<DuplicateGroup> _groups = new();
    private string? _selectedFolder;
    private int _minSizeKb = 1;
    private string _filePatterns = "";
    private string _excludePatterns = ".git; node_modules; $RECYCLE.BIN; System Volume Information";
    private bool _includeHidden;
    private KeepPolicy _keep = KeepPolicy.Oldest;
    private bool _isBusy;
    private string _summary = L.T("dup_initial");
    private CancellationTokenSource? _cts;

    public DuplicatesViewModel()
    {
        RowsView = CollectionViewSource.GetDefaultView(Rows);
        RowsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(DuplicateRowViewModel.GroupHeader)));

        AddFolderCommand = new RelayCommand(() => { if (Dialogs.PickFolder(L.T("dup_pick"), Folders.LastOrDefault()) is { } f) AddFolder(f, search: false); }, () => !IsBusy);
        RemoveFolderCommand = new RelayCommand(() => { if (SelectedFolder is { } f) Folders.Remove(f); }, () => !IsBusy && SelectedFolder is not null);
        SearchCommand = new RelayCommand(() => _ = SearchAsync(), () => !IsBusy && Folders.Count > 0);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        AutoMarkCommand = new RelayCommand(AutoMark, () => !IsBusy && Rows.Count > 0);
        UnmarkAllCommand = new RelayCommand(() => { foreach (var r in Rows) r.IsMarked = false; UpdateSummary(); }, () => Rows.Any(r => r.IsMarked));
        RecycleCommand = new RelayCommand(RecycleMarked, () => !IsBusy && Rows.Any(r => r.IsMarked));
        OpenLocationCommand = new RelayCommand(p => OpenLocation(p as DuplicateRowViewModel), p => p is DuplicateRowViewModel);
    }

    public ObservableCollection<string> Folders { get; } = new();
    public ObservableCollection<DuplicateRowViewModel> Rows { get; } = new();
    public ICollectionView RowsView { get; }

    public ICommand AddFolderCommand { get; }
    public ICommand RemoveFolderCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand AutoMarkCommand { get; }
    public ICommand UnmarkAllCommand { get; }
    public ICommand RecycleCommand { get; }
    public ICommand OpenLocationCommand { get; }

    public string? SelectedFolder { get => _selectedFolder; set => SetProperty(ref _selectedFolder, value); }
    public int MinSizeKb { get => _minSizeKb; set => SetProperty(ref _minSizeKb, Math.Max(0, value)); }
    public string FilePatterns { get => _filePatterns; set => SetProperty(ref _filePatterns, value); }
    public string ExcludePatterns { get => _excludePatterns; set => SetProperty(ref _excludePatterns, value); }
    public bool IncludeHidden { get => _includeHidden; set => SetProperty(ref _includeHidden, value); }
    public KeepPolicy Keep { get => _keep; set => SetProperty(ref _keep, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }
    public bool HasRows => Rows.Count > 0;
    public bool HasFolders => Folders.Count > 0;

    public void AddFolder(string folder, bool search)
    {
        folder = Path.TrimEndingDirectorySeparator(folder);
        if (Directory.Exists(folder) && !Folders.Contains(folder, StringComparer.OrdinalIgnoreCase)) Folders.Add(folder);
        OnPropertyChanged(nameof(HasFolders));
        if (search) _ = SearchAsync();
    }

    public async Task SearchAsync()
    {
        if (Folders.Count == 0) return;
        var options = new DuplicateOptions
        {
            MinSize = Math.Max(1, MinSizeKb * 1024L),
            IncludeHidden = IncludeHidden,
            FilePatterns = WildcardPattern.SplitList(FilePatterns),
            ExcludePatterns = WildcardPattern.SplitList(ExcludePatterns),
        };
        var roots = Folders.ToList();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        IsBusy = true;
        Summary = L.T("dup_scanning");
        var progress = new Progress<DuplicateProgress>(p =>
        {
            if (!IsBusy) return;
            Summary = p.Stage == "scan" ? L.F("dup_scanning_n", p.Done) : L.F("dup_hashing", p.Done, p.Total);
        });

        try
        {
            _groups = await Task.Run(() => DuplicateFinder.Find(roots, options, progress, token), token);
            Fill();
        }
        catch (OperationCanceledException)
        {
            Summary = L.T("exp_cancelled");
        }
        finally
        {
            IsBusy = false;
            _cts.Dispose();
            _cts = null;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void Fill()
    {
        Rows.Clear();
        int index = 1;
        foreach (var group in _groups)
        {
            string header = L.F("dup_group", index++, group.Files.Count, SizeFormatter.Format(group.Size), SizeFormatter.Format(group.WastedBytes));
            foreach (var file in group.Files)
                Rows.Add(new DuplicateRowViewModel(file, group, header, UpdateSummary));
        }
        OnPropertyChanged(nameof(HasRows));
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        if (_groups.Count == 0)
        {
            Summary = IsBusy ? Summary : (Folders.Count == 0 ? L.T("dup_initial") : L.T("dup_none"));
            return;
        }
        long wasted = _groups.Sum(g => g.WastedBytes);
        int extra = _groups.Sum(g => g.Files.Count - 1);
        var marked = Rows.Where(r => r.IsMarked).ToList();
        string text = L.F("dup_sum", _groups.Count, extra, SizeFormatter.Format(wasted));
        if (marked.Count > 0) text += " · " + L.F("dup_sum_marked", marked.Count, SizeFormatter.Format(marked.Sum(r => r.File.Size)));
        Summary = text;
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>Marca en cada grupo todas las copias menos la que se conserva.</summary>
    private void AutoMark()
    {
        foreach (var group in Rows.GroupBy(r => r.Group))
        {
            var keep = Keep switch
            {
                KeepPolicy.Newest => group.OrderByDescending(r => r.File.LastModified).First(),
                KeepPolicy.ShortestPath => group.OrderBy(r => r.File.Path.Length).ThenBy(r => r.File.Path, NaturalStringComparer.Instance).First(),
                _ => group.OrderBy(r => r.File.LastModified).First(),
            };
            foreach (var row in group) row.SetMarked(!ReferenceEquals(row, keep));
        }
        UpdateSummary();
    }

    private void RecycleMarked()
    {
        var marked = Rows.Where(r => r.IsMarked).ToList();
        if (marked.Count == 0) return;
        if (marked.GroupBy(r => r.Group).Any(g => g.Count() == g.Key.Files.Count))
        {
            Dialogs.Info(L.T("dup_keep_one"));
            return;
        }
        long bytes = marked.Sum(r => r.File.Size);
        if (!Dialogs.Confirm(L.F("dup_confirm", marked.Count, SizeFormatter.Format(bytes)))) return;

        var removed = RecycleBin.Send(marked.Select(r => r.File.Path).ToList());
        var removedSet = new HashSet<string>(removed, StringComparer.OrdinalIgnoreCase);
        foreach (var group in _groups) group.Files.RemoveAll(f => removedSet.Contains(f.Path));
        _groups.RemoveAll(g => g.Files.Count < 2);
        Fill();
        if (removed.Count < marked.Count) Dialogs.Error(L.F("dup_some_failed", marked.Count - removed.Count));
        else Summary = L.F("dup_done", removed.Count, SizeFormatter.Format(bytes)) + " · " + Summary;
    }

    private static void OpenLocation(DuplicateRowViewModel? row)
    {
        if (row is null) return;
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{row.File.Path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Dialogs.Error(ex.Message);
        }
    }

    public void OnLanguageChanged()
    {
        if (_groups.Count > 0) Fill(); else UpdateSummary();
    }
}

public sealed class DuplicateRowViewModel : ObservableObject
{
    private readonly Action _changed;
    private bool _isMarked;

    public DuplicateRowViewModel(DuplicateFile file, DuplicateGroup group, string header, Action changed)
    {
        File = file;
        Group = group;
        GroupHeader = header;
        _changed = changed;
    }

    public DuplicateFile File { get; }
    public DuplicateGroup Group { get; }
    public string GroupHeader { get; }
    public string Name => Path.GetFileName(File.Path);
    public string Folder => Path.GetDirectoryName(File.Path) ?? "";
    public string Modified => File.LastModified.ToString("g", CultureInfo.CurrentCulture);

    public bool IsMarked
    {
        get => _isMarked;
        set { if (SetProperty(ref _isMarked, value)) _changed(); }
    }

    /// <summary>Cambia la marca sin recalcular el resumen (para marcar muchos de golpe).</summary>
    public void SetMarked(bool value)
    {
        if (_isMarked == value) return;
        _isMarked = value;
        OnPropertyChanged(nameof(IsMarked));
    }
}
