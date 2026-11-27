using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using GestionCarp.App.Services;
using GestionCarp.Core.Common;
using GestionCarp.Core.Localization;
using GestionCarp.Core.Structure;

namespace GestionCarp.App.ViewModels;

/// <summary>Pestaña «Crear carpetas»: crea una estructura a partir de un árbol en texto.</summary>
public sealed class StructureViewModel : ObservableObject
{
    private readonly RenameService _service;
    private readonly DispatcherTimer _debounce;
    private List<StructurePlanItem> _plan = new();
    private string _text = "";
    private string _destination = "";
    private bool _createFiles;
    private string _summary = L.T("struct_sum_empty");

    public StructureViewModel(RenameService service)
    {
        _service = service;
        _service.FilesChanged += (_, _) => Refresh();
        _service.HistoryChanged += (_, _) => OnPropertyChanged(nameof(UndoDescription));
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Refresh(); };

        BrowseCommand = new RelayCommand(Browse);
        PasteCommand = new RelayCommand(Paste);
        ExampleCommand = new RelayCommand(() => Text = L.T("struct_example"));
        ClearCommand = new RelayCommand(() => Text = "", () => Text.Length > 0);
        CreateCommand = new RelayCommand(Create, () => HasDestination && _plan.Any(p => p.Status == StructureStatus.Create));
        UndoCommand = new RelayCommand(() => _service.UndoWithConfirmation(), () => _service.CanUndo);
    }

    public ObservableCollection<StructureRowViewModel> Rows { get; } = new();

    public ICommand BrowseCommand { get; }
    public ICommand PasteCommand { get; }
    public ICommand ExampleCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand UndoCommand { get; }

    public string Text
    {
        get => _text;
        set
        {
            if (!SetProperty(ref _text, value ?? "")) return;
            _debounce.Stop();
            _debounce.Start();
        }
    }

    public string Destination { get => _destination; set { if (SetProperty(ref _destination, value ?? "")) Refresh(); } }
    public bool CreateFiles { get => _createFiles; set { if (SetProperty(ref _createFiles, value)) Refresh(); } }
    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }
    public string UndoDescription => _service.UndoDescription;
    public bool HasRows => Rows.Count > 0;

    private string CleanDestination => Destination.Trim().Trim('"');
    private bool HasDestination => CleanDestination.Length > 0 && Directory.Exists(CleanDestination);

    /// <summary>Recibe una estructura desde otra pestaña (por ejemplo, el Explorador).</summary>
    public void LoadTemplate(string text, string? suggestedDestination)
    {
        _text = text;
        OnPropertyChanged(nameof(Text));
        if (Destination.Length == 0 && suggestedDestination is not null) _destination = suggestedDestination;
        OnPropertyChanged(nameof(Destination));
        Refresh();
    }

    private void Browse()
    {
        var folder = Dialogs.PickFolder(L.T("struct_pick"), CleanDestination);
        if (folder is not null) Destination = folder;
    }

    private void Paste()
    {
        try
        {
            if (Clipboard.ContainsText()) Text = Clipboard.GetText();
        }
        catch (Exception ex)
        {
            Dialogs.Error(ex.Message);
        }
    }

    public void Refresh()
    {
        var entries = StructureParser.Parse(Text);
        // Sin destino válido se muestra la vista previa igualmente, sin comprobar el disco.
        string destination = HasDestination
            ? CleanDestination
            : Path.Combine(Path.GetTempPath(), "gestioncarp-" + Guid.NewGuid().ToString("N"));
        _plan = StructureBuilder.Plan(entries, destination, CreateFiles);

        Rows.Clear();
        foreach (var item in _plan) Rows.Add(new StructureRowViewModel(item));
        OnPropertyChanged(nameof(HasRows));
        UpdateSummary();
        CommandManager.InvalidateRequerySuggested();
    }

    private void UpdateSummary()
    {
        if (_plan.Count == 0)
        {
            Summary = L.T("struct_sum_empty");
            return;
        }

        int folders = _plan.Count(p => p.Status == StructureStatus.Create && p.Entry.IsDirectory);
        int files = _plan.Count(p => p.Status == StructureStatus.Create && !p.Entry.IsDirectory);
        int exists = _plan.Count(p => p.Status == StructureStatus.Exists);
        int skipped = _plan.Count(p => p.Status == StructureStatus.Skipped);
        int errors = _plan.Count(p => p.Status == StructureStatus.Error);

        var parts = new List<string> { L.F("struct_sum", folders, files) };
        if (exists > 0) parts.Add(L.F("struct_sum_exists", exists));
        if (skipped > 0) parts.Add(L.F("struct_sum_skipped", skipped));
        if (errors > 0) parts.Add(L.F("struct_sum_errors", errors));
        if (!HasDestination) parts.Add(L.T("struct_no_destination"));
        Summary = string.Join(" · ", parts);
    }

    private void Create()
    {
        Refresh();
        if (!HasDestination)
        {
            Dialogs.Info(L.T("struct_no_destination"));
            return;
        }

        int folders = _plan.Count(p => p.Status == StructureStatus.Create && p.Entry.IsDirectory);
        int files = _plan.Count(p => p.Status == StructureStatus.Create && !p.Entry.IsDirectory);
        if (folders + files == 0)
        {
            Dialogs.Info(L.T("struct_nothing"));
            return;
        }

        if (!Dialogs.Confirm(L.F("struct_confirm", folders, files, CleanDestination) + "\n\n" + L.T("confirm_undo_hint")))
            return;

        var result = StructureBuilder.Create(_plan, L.F("struct_batch", folders + files));
        if (!result.Success)
        {
            Dialogs.Error(result.Error ?? L.T("err_unknown"));
            return;
        }
        _service.Record(result);
    }

    public void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(UndoDescription));
        Refresh();
    }
}

public sealed class StructureRowViewModel
{
    public StructureRowViewModel(StructurePlanItem item)
    {
        Item = item;
    }

    public StructurePlanItem Item { get; }

    public string Display => new string(' ', Item.Entry.Depth * 4)
                             + (Item.Entry.IsDirectory ? "📁 " : MediaFiles.IsVideo(Item.Entry.Name) ? "🎬 " : "📄 ")
                             + Item.Entry.Name;

    public StructureStatus Status => Item.Status;

    public string StatusIcon => Item.Status switch
    {
        StructureStatus.Create => "＋",
        StructureStatus.Exists => "=",
        StructureStatus.Error => "✖",
        _ => "–",
    };

    public string? Message => Item.Message;
}
