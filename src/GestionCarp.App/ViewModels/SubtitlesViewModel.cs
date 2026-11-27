using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using GestionCarp.App.Services;
using GestionCarp.Core.Common;
using GestionCarp.Core.Localization;
using GestionCarp.Core.Rename;
using GestionCarp.Core.Subtitles;

namespace GestionCarp.App.ViewModels;

public sealed class SubtitlesViewModel : ObservableObject
{
    private readonly RenameService _service;
    private readonly HashSet<string> _excluded = new(StringComparer.OrdinalIgnoreCase);
    private List<string> _videos = new();
    private List<string> _subtitles = new();
    private List<RenamePreviewRow> _plan = new();
    private bool _loaded;

    private string _folderPath = "";
    private bool _includeSubfolders = true;
    private SubtitleMatchMode _mode = SubtitleMatchMode.ByEpisode;
    private bool _keepLanguageTags = true;
    private string _defaultLanguage = "";
    private bool _moveToVideoFolder = true;
    private string _summary = L.T("sub_initial");

    public SubtitlesViewModel(RenameService service)
    {
        _service = service;
        _service.FilesChanged += (_, _) => { if (_loaded) Analyze(); };
        _service.HistoryChanged += (_, _) => OnPropertyChanged(nameof(UndoDescription));

        BrowseCommand = new RelayCommand(Browse);
        AnalyzeCommand = new RelayCommand(Analyze, () => !string.IsNullOrWhiteSpace(FolderPath));
        ApplyCommand = new RelayCommand(Apply, () => _plan.Any(r => r.IsMoving));
        UndoCommand = new RelayCommand(Undo, () => _service.CanUndo);
        SelectAllCommand = new RelayCommand(() => SetAll(true), () => Rows.Count > 0);
        SelectNoneCommand = new RelayCommand(() => SetAll(false), () => Rows.Count > 0);
    }

    public ObservableCollection<SubtitleRowViewModel> Rows { get; } = new();

    public ICommand BrowseCommand { get; }
    public ICommand AnalyzeCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand SelectNoneCommand { get; }

    public string FolderPath { get => _folderPath; set => SetProperty(ref _folderPath, value); }

    public bool IncludeSubfolders { get => _includeSubfolders; set { if (SetProperty(ref _includeSubfolders, value) && _loaded) Analyze(); } }
    public SubtitleMatchMode Mode { get => _mode; set { if (SetProperty(ref _mode, value)) Recompute(); } }
    public bool KeepLanguageTags { get => _keepLanguageTags; set { if (SetProperty(ref _keepLanguageTags, value)) Recompute(); } }
    public string DefaultLanguage { get => _defaultLanguage; set { if (SetProperty(ref _defaultLanguage, value)) Recompute(); } }
    public bool MoveToVideoFolder { get => _moveToVideoFolder; set { if (SetProperty(ref _moveToVideoFolder, value)) Recompute(); } }

    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }
    public string UndoDescription => _service.UndoDescription;
    public bool HasRows => Rows.Count > 0;

    private void Browse()
    {
        var folder = Dialogs.PickFolder(L.T("sub_pick"), FolderPath);
        if (folder is null) return;
        FolderPath = folder;
        Analyze();
    }

    public void Analyze()
    {
        string folder = FolderPath.Trim().Trim('"');
        if (!Directory.Exists(folder))
        {
            Summary = L.F("folder_not_exists", folder);
            return;
        }

        try
        {
            var files = Directory.EnumerateFiles(folder, "*", new EnumerationOptions
            {
                RecurseSubdirectories = IncludeSubfolders,
                IgnoreInaccessible = true,
            }).ToList();
            _videos = files.Where(MediaFiles.IsVideo).ToList();
            _subtitles = files.Where(MediaFiles.IsSubtitle).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Summary = L.F("sub_read_error", ex.Message);
            return;
        }

        _loaded = true;
        _excluded.RemoveWhere(p => !File.Exists(p));
        Recompute();
    }

    private void Recompute()
    {
        if (!_loaded) return;

        _plan = SubtitleMatcher.Match(_videos, _subtitles, new SubtitleMatchOptions
        {
            Mode = Mode,
            KeepLanguageTags = KeepLanguageTags,
            DefaultLanguage = DefaultLanguage,
            MoveToVideoFolder = MoveToVideoFolder,
            Excluded = _excluded,
        });

        string root = FolderPath.Trim().Trim('"');
        bool sameRows = Rows.Count == _plan.Count && Rows.Select(r => r.SourcePath).SequenceEqual(_plan.Select(p => p.SourcePath));
        if (!sameRows)
        {
            Rows.Clear();
            foreach (var row in _plan)
                Rows.Add(new SubtitleRowViewModel(row.SourcePath, root, OnIncludeChanged) { });
        }

        for (int i = 0; i < _plan.Count; i++)
            Rows[i].Update(_plan[i], included: !_excluded.Contains(_plan[i].SourcePath));

        int ready = _plan.Count(r => r.IsMoving);
        int unchanged = _plan.Count(r => r.Status == RenameStatus.Unchanged);
        int skipped = _plan.Count(r => r.Status == RenameStatus.Skipped);
        int errors = _plan.Count(r => r.Status == RenameStatus.Error);

        var parts = new List<string> { L.F("sub_sum_videos", _videos.Count), L.F("sub_sum_subs", _subtitles.Count), L.F("sub_sum_ready", ready) };
        if (unchanged > 0) parts.Add(L.F("sub_sum_ok", unchanged));
        if (skipped > 0) parts.Add(L.F("sub_sum_skipped", skipped));
        if (errors > 0) parts.Add(L.F("sub_sum_errors", errors));
        Summary = string.Join(" · ", parts);

        OnPropertyChanged(nameof(HasRows));
        CommandManager.InvalidateRequerySuggested();
    }

    private void OnIncludeChanged(SubtitleRowViewModel row, bool include)
    {
        if (include) _excluded.Remove(row.SourcePath);
        else _excluded.Add(row.SourcePath);
        Recompute();
    }

    private void SetAll(bool include)
    {
        if (include) _excluded.Clear();
        else foreach (var row in Rows) _excluded.Add(row.SourcePath);
        Recompute();
    }

    private void Apply()
    {
        Recompute();
        var operations = RenamePlanner.ToOperations(_plan);
        if (operations.Count == 0)
        {
            Dialogs.Info(L.T("sub_nothing"));
            return;
        }

        if (!Dialogs.Confirm(L.F("sub_confirm", operations.Count) + "\n\n" + L.T("confirm_undo_hint")))
            return;

        var result = _service.Execute(operations, L.F("sub_batch", operations.Count));
        if (!result.Success)
            Dialogs.Error(result.Error ?? L.T("err_unknown"));
    }

    public void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(UndoDescription));
        if (_loaded) Recompute();
        else Summary = L.T("sub_initial");
    }

    private void Undo() => _service.UndoWithConfirmation();
}

public sealed class SubtitleRowViewModel : ObservableObject
{
    private readonly string _root;
    private readonly Action<SubtitleRowViewModel, bool> _onIncludeChanged;
    private bool _include = true;
    private bool _updating;
    private string _newName = "";
    private string _videoName = "";
    private RenameStatus _status;
    private string? _message;

    public SubtitleRowViewModel(string sourcePath, string root, Action<SubtitleRowViewModel, bool> onIncludeChanged)
    {
        SourcePath = sourcePath;
        _root = root;
        _onIncludeChanged = onIncludeChanged;
    }

    public string SourcePath { get; }

    public string CurrentName => Relative(SourcePath);

    public bool Include
    {
        get => _include;
        set
        {
            if (SetProperty(ref _include, value) && !_updating)
                _onIncludeChanged(this, value);
        }
    }

    public string VideoName { get => _videoName; private set => SetProperty(ref _videoName, value); }
    public string NewName { get => _newName; private set => SetProperty(ref _newName, value); }
    public string? Message { get => _message; private set => SetProperty(ref _message, value); }

    public RenameStatus Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
                OnPropertyChanged(nameof(StatusIcon));
        }
    }

    public string StatusIcon => Status switch
    {
        RenameStatus.Ready => "✔",
        RenameStatus.Warning => "⚠",
        RenameStatus.Error => "✖",
        RenameStatus.Skipped => "–",
        _ => "=",
    };

    public void Update(RenamePreviewRow row, bool included)
    {
        _updating = true;
        Include = included;
        _updating = false;
        VideoName = row.RelatedName ?? "";
        NewName = row.Status is RenameStatus.Skipped ? "" : Relative(row.TargetPath);
        Status = row.Status;
        Message = row.Status == RenameStatus.Unchanged ? L.T("sub_already_ok") : row.Message;
    }

    private string Relative(string path)
    {
        try { return Path.GetRelativePath(_root, path); }
        catch (ArgumentException) { return path; }
    }
}
