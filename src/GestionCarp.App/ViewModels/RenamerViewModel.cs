using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using GestionCarp.App.Services;
using GestionCarp.Core.Common;
using GestionCarp.Core.Localization;
using GestionCarp.Core.Rename;
using GestionCarp.Core.Rename.Rules;

namespace GestionCarp.App.ViewModels;

public sealed class RenamerViewModel : ObservableObject
{
    private readonly RenameService _service;
    private readonly PresetStore _presets = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GestionCarp", "presets.json"));
    private string? _selectedPreset;
    private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);
    private List<RenamePreviewRow> _plan = new();
    private RenameRule? _selectedRule;
    private bool _renameSubtitlesWithVideo = true;
    private bool _includeSubfolders;
    private bool _includeFolders;
    private string _addFilter = "";
    private string _summary = "";
    private bool _suspendRefresh;

    public RenamerViewModel(RenameService service)
    {
        _service = service;
        _service.FilesChanged += (_, executed) => OnFilesChanged(executed);
        _service.HistoryChanged += (_, _) => OnPropertyChanged(nameof(UndoDescription));

        Rules.CollectionChanged += OnRulesChanged;
        Items.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasItems));
            Refresh();
        };

        AddRuleCommand = new RelayCommand(p => AddRule(p as string));
        RemoveRuleCommand = new RelayCommand(() => { if (SelectedRule is { } r) Rules.Remove(r); }, () => SelectedRule is not null);
        MoveRuleUpCommand = new RelayCommand(() => MoveRule(-1), () => SelectedRule is not null && Rules.IndexOf(SelectedRule) > 0);
        MoveRuleDownCommand = new RelayCommand(() => MoveRule(1), () => SelectedRule is not null && Rules.IndexOf(SelectedRule) < Rules.Count - 1);

        AddFilesCommand = new RelayCommand(AddFiles);
        AddFolderCommand = new RelayCommand(AddFolder);
        RemoveSelectedCommand = new RelayCommand(p => RemoveItems(p as IList), p => p is IList { Count: > 0 });
        ClearCommand = new RelayCommand(ClearItems, () => Items.Count > 0);
        SortCommand = new RelayCommand(SortItems, () => Items.Count > 1);
        MoveItemsUpCommand = new RelayCommand(p => MoveItems(p as IList, -1), p => p is IList { Count: > 0 });
        MoveItemsDownCommand = new RelayCommand(p => MoveItems(p as IList, 1), p => p is IList { Count: > 0 });

        LoadPresetCommand = new RelayCommand(LoadPreset, () => SelectedPreset is not null);
        SavePresetCommand = new RelayCommand(SavePreset, () => Rules.Count > 0);
        DeletePresetCommand = new RelayCommand(DeletePreset, () => SelectedPreset is not null);
        ExportPresetCommand = new RelayCommand(ExportPreset, () => SelectedPreset is not null);
        ImportPresetCommand = new RelayCommand(ImportPreset);
        ReloadPresetNames();

        RenameCommand = new RelayCommand(Rename, () => _plan.Any(r => r.IsMoving));
        UndoCommand = new RelayCommand(Undo, () => _service.CanUndo);

        UpdateSummary();
    }

    public ObservableCollection<RenameRule> Rules { get; } = new();
    public ObservableCollection<RenameRowViewModel> Items { get; } = new();

    public ICommand AddRuleCommand { get; }
    public ICommand RemoveRuleCommand { get; }
    public ICommand MoveRuleUpCommand { get; }
    public ICommand MoveRuleDownCommand { get; }
    public ICommand AddFilesCommand { get; }
    public ICommand AddFolderCommand { get; }
    public ICommand RemoveSelectedCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand SortCommand { get; }
    public ICommand MoveItemsUpCommand { get; }
    public ICommand MoveItemsDownCommand { get; }
    public ICommand LoadPresetCommand { get; }
    public ICommand SavePresetCommand { get; }
    public ICommand DeletePresetCommand { get; }
    public ICommand ExportPresetCommand { get; }
    public ICommand ImportPresetCommand { get; }
    public ICommand RenameCommand { get; }

    public ObservableCollection<string> PresetNames { get; } = new();

    public string? SelectedPreset { get => _selectedPreset; set => SetProperty(ref _selectedPreset, value); }

    /// <summary>Ejemplos para el desplegable de plantillas de series.</summary>
    /// <summary>Ejemplos de plantillas de metadatos, con los marcadores del idioma actual.</summary>
    public IReadOnlyList<string> MetadataTemplateExamples => MetadataRule.Examples();

    public static IReadOnlyList<string> SeriesTemplateExamples { get; } = new[]
    {
        "{serie} - T{temp:00}E{ep:00}",
        "{serie} - S{temp:00}E{ep:00}",
        "{serie} - {temp}x{ep:00}",
        "{serie} - T{temp:00}E{ep:00} - {titulo}",
        "{serie} {temp}x{ep:00} {titulo}",
        "Cap {ep:00}",
    };
    public ICommand UndoCommand { get; }

    public RenameRule? SelectedRule
    {
        get => _selectedRule;
        set
        {
            if (SetProperty(ref _selectedRule, value))
                OnPropertyChanged(nameof(HasSelectedRule));
        }
    }

    public bool HasSelectedRule => SelectedRule is not null;
    public bool HasRules => Rules.Count > 0;
    public bool HasItems => Items.Count > 0;

    public bool RenameSubtitlesWithVideo
    {
        get => _renameSubtitlesWithVideo;
        set { if (SetProperty(ref _renameSubtitlesWithVideo, value)) Refresh(); }
    }

    public bool IncludeSubfolders { get => _includeSubfolders; set => SetProperty(ref _includeSubfolders, value); }
    public bool IncludeFolders { get => _includeFolders; set => SetProperty(ref _includeFolders, value); }
    public string AddFilter { get => _addFilter; set => SetProperty(ref _addFilter, value); }

    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }
    public string UndoDescription => _service.UndoDescription;

    // ---------- Reglas ----------

    private void AddRule(string? kind)
    {
        RenameRule rule = kind switch
        {
            "Reemplazar" => new ReplaceRule(),
            "Insertar" => new InsertRule(),
            "Eliminar" => new RemoveRule(),
            "Mayusculas" => new CaseRule(),
            "Limpiar" => new CleanupRule(),
            "Serie" => new SeriesTemplateRule(),
            "Metadatos" => new MetadataRule(),
            _ => new NumberingRule { TextBefore = SuggestSeriesName() },
        };
        Rules.Add(rule);
        SelectedRule = rule;
    }

    /// <summary>Propone como texto base el nombre de la carpeta de los archivos (suele ser el de la serie).</summary>
    private string SuggestSeriesName()
    {
        var folder = Items.FirstOrDefault()?.Folder;
        return string.IsNullOrEmpty(folder) ? "" : Path.GetFileName(folder) + " - ";
    }

    // ---------- Presets ----------

    private void ReloadPresetNames(string? select = null)
    {
        PresetNames.Clear();
        foreach (var preset in _presets.Presets) PresetNames.Add(preset.Name);
        SelectedPreset = select is not null && PresetNames.Contains(select) ? select : PresetNames.FirstOrDefault();
    }

    private void ReplaceRules(IEnumerable<RenameRule> rules)
    {
        _suspendRefresh = true;
        try
        {
            while (Rules.Count > 0) Rules.RemoveAt(Rules.Count - 1);
            foreach (var rule in rules) Rules.Add(rule);
        }
        finally
        {
            _suspendRefresh = false;
        }
        SelectedRule = Rules.FirstOrDefault();
        Refresh();
    }

    private void LoadPreset()
    {
        var preset = SelectedPreset is null ? null : _presets.Find(SelectedPreset);
        if (preset is null) return;
        if (Rules.Count > 0 && !Dialogs.Confirm(L.F("preset_replace_confirm", preset.Name))) return;
        ReplaceRules(PresetStore.Clone(preset.Rules));
    }

    private void SavePreset()
    {
        if (Rules.Count == 0)
        {
            Dialogs.Info(L.T("preset_no_rules"));
            return;
        }
        string? name = Dialogs.Prompt(L.T("preset_name_title"), L.T("preset_name_prompt"), SelectedPreset ?? "");
        if (string.IsNullOrWhiteSpace(name)) return;
        if (_presets.Find(name) is not null && !Dialogs.Confirm(L.F("preset_overwrite", name))) return;
        _presets.Save(name, Rules);
        ReloadPresetNames(name.Trim());
    }

    private void DeletePreset()
    {
        if (SelectedPreset is null || !Dialogs.Confirm(L.F("preset_delete_confirm", SelectedPreset))) return;
        _presets.Delete(SelectedPreset);
        ReloadPresetNames();
    }

    private void ExportPreset()
    {
        var preset = SelectedPreset is null ? null : _presets.Find(SelectedPreset);
        if (preset is null) return;
        string? path = Dialogs.SaveFile(L.T("preset_export"), preset.Name + ".gcpreset.json", PresetFilter);
        if (path is null) return;
        try
        {
            PresetStore.ExportToFile(preset, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Dialogs.Error(ex.Message);
        }
    }

    private void ImportPreset()
    {
        var files = Dialogs.PickFiles(L.T("preset_import"), PresetFilter);
        if (files is null || files.Length == 0) return;
        try
        {
            var preset = PresetStore.ImportFromFile(files[0]);
            if (_presets.Find(preset.Name) is not null && !Dialogs.Confirm(L.F("preset_overwrite", preset.Name))) return;
            _presets.Save(preset.Name, preset.Rules);
            ReloadPresetNames(preset.Name);
            Dialogs.Info(L.F("preset_imported", preset.Name));
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Dialogs.Error(ex.Message);
        }
    }

    private static string PresetFilter => L.T("preset_filter") + " (*.json)|*.json|" + L.T("dlg_filter_all") + "|*.*";

    private void MoveRule(int delta)
    {
        if (SelectedRule is null) return;
        var rule = SelectedRule;
        int index = Rules.IndexOf(rule);
        int target = index + delta;
        if (target < 0 || target >= Rules.Count) return;
        Rules.Move(index, target);
        SelectedRule = rule;
    }

    private void OnRulesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (RenameRule rule in e.OldItems) rule.PropertyChanged -= OnRulePropertyChanged;
        if (e.NewItems is not null)
            foreach (RenameRule rule in e.NewItems) rule.PropertyChanged += OnRulePropertyChanged;
        OnPropertyChanged(nameof(HasRules));
        Refresh();
    }

    private void OnRulePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RenameRule.Summary))
            Refresh();
    }

    // ---------- Lista de archivos ----------

    public void AddPaths(IEnumerable<string> paths)
    {
        var filters = WildcardPattern.ParseList(AddFilter);
        var toAdd = new List<RenameItem>();

        foreach (var raw in paths)
        {
            string path = Path.TrimEndingDirectorySeparator(raw);
            if (Directory.Exists(path))
                toAdd.AddRange(ExpandFolder(path, filters));
            else if (File.Exists(path))
                toAdd.Add(new RenameItem(path, false));
        }

        _suspendRefresh = true;
        try
        {
            foreach (var item in toAdd.Where(i => _paths.Add(i.FullPath)))
                Items.Add(new RenameRowViewModel(item));
        }
        finally
        {
            _suspendRefresh = false;
        }
        OnPropertyChanged(nameof(HasItems));
        Refresh();
    }

    private IEnumerable<RenameItem> ExpandFolder(string folder, List<WildcardPattern> filters)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = IncludeSubfolders, IgnoreInaccessible = true };
        List<RenameItem> result = new();
        try
        {
            if (IncludeFolders)
            {
                result.AddRange(Directory.EnumerateDirectories(folder, "*", options)
                    .OrderBy(d => d, NaturalStringComparer.Instance)
                    .Select(d => new RenameItem(d, true)));
            }

            result.AddRange(Directory.EnumerateFiles(folder, "*", options)
                .Where(f => filters.Count == 0 || filters.Any(p => p.IsMatch(Path.GetFileName(f))))
                .OrderBy(f => Path.GetDirectoryName(f), NaturalStringComparer.Instance)
                .ThenBy(f => Path.GetFileName(f), NaturalStringComparer.Instance)
                .Select(f => new RenameItem(f, false)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Dialogs.Error(L.F("ren_read_error", folder, ex.Message));
        }
        return result;
    }

    private void AddFiles()
    {
        var files = Dialogs.PickFiles(L.T("ren_pick_files"));
        if (files is not null) AddPaths(files);
    }

    private void AddFolder()
    {
        var folder = Dialogs.PickFolder(L.T("ren_pick_folder"), Items.FirstOrDefault()?.Folder);
        if (folder is not null) AddPaths(new[] { folder });
    }

    private void RemoveItems(IList? selected)
    {
        if (selected is null) return;
        var rows = selected.OfType<RenameRowViewModel>().ToList();
        _suspendRefresh = true;
        try
        {
            foreach (var row in rows)
            {
                Items.Remove(row);
                _paths.Remove(row.Path);
            }
        }
        finally
        {
            _suspendRefresh = false;
        }
        Refresh();
    }

    private void ClearItems()
    {
        _paths.Clear();
        Items.Clear();
    }

    private void SortItems()
    {
        var sorted = Items
            .OrderBy(i => i.Folder, NaturalStringComparer.Instance)
            .ThenBy(i => i.OriginalName, NaturalStringComparer.Instance)
            .ToList();
        _suspendRefresh = true;
        try
        {
            for (int i = 0; i < sorted.Count; i++)
            {
                int current = Items.IndexOf(sorted[i]);
                if (current != i) Items.Move(current, i);
            }
        }
        finally
        {
            _suspendRefresh = false;
        }
        Refresh();
    }

    private void MoveItems(IList? selected, int delta)
    {
        if (selected is null) return;
        var rows = selected.OfType<RenameRowViewModel>()
            .OrderBy(r => Items.IndexOf(r) * -delta)
            .ToList();
        _suspendRefresh = true;
        try
        {
            foreach (var row in rows)
            {
                int index = Items.IndexOf(row);
                int target = index + delta;
                if (target < 0 || target >= Items.Count || rows.Contains(Items[target])) continue;
                Items.Move(index, target);
            }
        }
        finally
        {
            _suspendRefresh = false;
        }
        Refresh();
    }

    // ---------- Vista previa y ejecución ----------

    public void Refresh()
    {
        if (_suspendRefresh) return;

        var items = Items.Select(i => i.ToItem()).ToList();
        _plan = RenamePlanner.Plan(items, Rules.ToList(), new RenamePlanOptions { RenameSubtitlesWithVideo = RenameSubtitlesWithVideo });

        var bySource = _plan.GroupBy(r => r.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var companionsByGroup = _plan
            .Where(r => r.IsCompanion && !_paths.Contains(r.SourcePath))
            .GroupBy(r => r.GroupKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<RenamePreviewRow>)g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in Items)
        {
            bySource.TryGetValue(row.Path, out var planRow);
            row.Update(planRow, companionsByGroup.GetValueOrDefault(row.Path) ?? Array.Empty<RenamePreviewRow>());
        }

        UpdateSummary();
        CommandManager.InvalidateRequerySuggested();
    }

    private void UpdateSummary()
    {
        if (Items.Count == 0)
        {
            Summary = L.T("ren_sum_empty");
            return;
        }

        int changing = _plan.Count(r => r.IsMoving && !r.IsCompanion);
        int subtitles = _plan.Count(r => r.IsMoving && r.IsCompanion);
        int errors = _plan.Count(r => r.Status == RenameStatus.Error);
        int warnings = _plan.Count(r => r.Status == RenameStatus.Warning);

        var parts = new List<string> { L.F("ren_sum_items", Items.Count), L.F("ren_sum_changing", changing) };
        if (subtitles > 0) parts.Add(L.F("ren_sum_subs", subtitles));
        if (warnings > 0) parts.Add(L.F("ren_sum_warn", warnings));
        if (errors > 0) parts.Add(L.F("ren_sum_errors", errors));
        Summary = string.Join(" · ", parts);
    }

    private void Rename()
    {
        Refresh();
        var operations = RenamePlanner.ToOperations(_plan);
        if (operations.Count == 0)
        {
            Dialogs.Info(L.T("ren_nothing"));
            return;
        }

        int errors = _plan.Count(r => r.Status == RenameStatus.Error);
        string message = L.F("ren_confirm", operations.Count);
        if (errors > 0) message += "\n" + L.F("ren_confirm_errors", errors);
        message += "\n\n" + L.T("confirm_undo_hint");
        if (!Dialogs.Confirm(message)) return;

        var result = _service.Execute(operations, L.F("ren_batch", operations.Count));
        if (!result.Success)
            Dialogs.Error(result.Error ?? L.T("err_unknown"));
    }

    public void OnLanguageChanged()
    {
        foreach (var rule in Rules) rule.RefreshTexts();
        OnPropertyChanged(nameof(UndoDescription));
        OnPropertyChanged(nameof(MetadataTemplateExamples));
        Refresh();
    }

    private void Undo() => _service.UndoWithConfirmation();

    /// <summary>Tras renombrar o deshacer (aquí o en otra pestaña), actualiza las rutas de la lista.</summary>
    private void OnFilesChanged(IReadOnlyList<RenameOperation> executed)
    {
        _paths.Clear();
        foreach (var row in Items)
        {
            row.Path = PathRebaser.Rebase(row.Path, executed);
            _paths.Add(row.Path);
        }
        OnPropertyChanged(nameof(UndoDescription));
        Refresh();
    }
}
