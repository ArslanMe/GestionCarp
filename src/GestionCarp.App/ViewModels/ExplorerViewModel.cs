using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using GestionCarp.App.Services;
using GestionCarp.Core.Common;
using GestionCarp.Core.Tree;

namespace GestionCarp.App.ViewModels;

public sealed class ExplorerViewModel : ObservableObject
{
    private const int PreviewLimit = 300_000;

    private readonly Action<IEnumerable<string>> _sendToRenamer;
    private readonly Action<string> _sendToSubtitles;

    private string _rootPath = "";
    private int _maxDepth;
    private bool _includeFiles = true;
    private bool _includeHidden;
    private string _excludePatterns = ".git; node_modules; $RECYCLE.BIN; System Volume Information";
    private string _filePatterns = "";
    private bool _isScanning;
    private string _status = "Elige una carpeta y pulsa «Analizar».";
    private TreeExportFormat _exportFormat = TreeExportFormat.Text;
    private bool _exportIncludeFiles = true;
    private bool _showSizes;
    private bool _showDates;
    private bool _showCounts;
    private int _exportDepth;
    private string _exportPreview = "";
    private TreeNode? _root;
    private TreeNodeViewModel? _selectedNode;
    private CancellationTokenSource? _cts;

    public ExplorerViewModel(Action<IEnumerable<string>> sendToRenamer, Action<string> sendToSubtitles)
    {
        _sendToRenamer = sendToRenamer;
        _sendToSubtitles = sendToSubtitles;

        BrowseCommand = new RelayCommand(Browse, () => !IsScanning);
        ScanCommand = new RelayCommand(() => _ = ScanAsync(), () => !IsScanning && !string.IsNullOrWhiteSpace(RootPath));
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsScanning);
        CopyCommand = new RelayCommand(Copy, () => _root is not null);
        SaveCommand = new RelayCommand(Save, () => _root is not null);
        SendToRenamerCommand = new RelayCommand(SendToRenamer, () => _root is not null);
        SendToSubtitlesCommand = new RelayCommand(SendToSubtitles, () => _root is not null);
        OpenInExplorerCommand = new RelayCommand(OpenInExplorer, () => _root is not null);
    }

    public ObservableCollection<TreeNodeViewModel> RootNodes { get; } = new();

    public ICommand BrowseCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SendToRenamerCommand { get; }
    public ICommand SendToSubtitlesCommand { get; }
    public ICommand OpenInExplorerCommand { get; }

    // ---------- Opciones de análisis ----------

    public string RootPath { get => _rootPath; set => SetProperty(ref _rootPath, value); }
    public int MaxDepth { get => _maxDepth; set => SetProperty(ref _maxDepth, Math.Max(0, value)); }
    public bool IncludeFiles { get => _includeFiles; set => SetProperty(ref _includeFiles, value); }
    public bool IncludeHidden { get => _includeHidden; set => SetProperty(ref _includeHidden, value); }
    public string ExcludePatterns { get => _excludePatterns; set => SetProperty(ref _excludePatterns, value); }
    public string FilePatterns { get => _filePatterns; set => SetProperty(ref _filePatterns, value); }

    public bool IsScanning { get => _isScanning; private set => SetProperty(ref _isScanning, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    // ---------- Opciones de exportación ----------

    public TreeExportFormat ExportFormat { get => _exportFormat; set { if (SetProperty(ref _exportFormat, value)) UpdatePreview(); } }
    public bool ExportIncludeFiles { get => _exportIncludeFiles; set { if (SetProperty(ref _exportIncludeFiles, value)) UpdatePreview(); } }
    public bool ShowSizes { get => _showSizes; set { if (SetProperty(ref _showSizes, value)) UpdatePreview(); } }
    public bool ShowDates { get => _showDates; set { if (SetProperty(ref _showDates, value)) UpdatePreview(); } }
    public bool ShowCounts { get => _showCounts; set { if (SetProperty(ref _showCounts, value)) UpdatePreview(); } }
    public int ExportDepth { get => _exportDepth; set { if (SetProperty(ref _exportDepth, Math.Max(0, value))) UpdatePreview(); } }

    public string ExportPreview { get => _exportPreview; private set => SetProperty(ref _exportPreview, value); }

    public bool HasTree => _root is not null;

    public string ExportTitle => ExportRoot is { } root
        ? (ReferenceEquals(root, _root)
            ? "Se exporta la carpeta analizada. Selecciona una subcarpeta en el árbol para exportar solo esa."
            : $"Se exporta la carpeta seleccionada: {root.Name}")
        : "";

    /// <summary>Exporta la carpeta seleccionada en el árbol, o la raíz si no hay selección.</summary>
    private TreeNode? ExportRoot => _selectedNode?.Node is { IsDirectory: true } selected ? selected : _root;

    private void Browse()
    {
        var folder = Dialogs.PickFolder("Elige la carpeta a analizar", RootPath);
        if (folder is null) return;
        RootPath = folder;
        _ = ScanAsync();
    }

    public async Task ScanAsync()
    {
        string path = RootPath.Trim().Trim('"');
        if (!Directory.Exists(path))
        {
            Status = "La carpeta no existe: " + path;
            return;
        }

        var options = new TreeScanOptions
        {
            MaxDepth = MaxDepth,
            IncludeFiles = IncludeFiles,
            IncludeHidden = IncludeHidden,
            ExcludePatterns = WildcardPattern.SplitList(ExcludePatterns),
            FilePatterns = WildcardPattern.SplitList(FilePatterns),
        };

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        IsScanning = true;
        Status = "Analizando…";
        var progress = new Progress<int>(n => { if (IsScanning) Status = $"Analizando… {n:N0} elementos"; });
        var watch = Stopwatch.StartNew();

        try
        {
            var root = await Task.Run(() => TreeScanner.Scan(path, options, progress, token), token);
            _root = root;
            _selectedNode = null;
            RootNodes.Clear();
            RootNodes.Add(new TreeNodeViewModel(root, OnNodeSelected) { IsExpanded = true });
            OnPropertyChanged(nameof(HasTree));
            UpdatePreview();
            Status = $"{root.FolderCount:N0} carpetas · {root.FileCount:N0} archivos · {SizeFormatter.Format(root.Size)} " +
                     $"· analizado en {watch.Elapsed.TotalSeconds:0.0} s";
        }
        catch (OperationCanceledException)
        {
            Status = "Análisis cancelado.";
        }
        catch (Exception ex)
        {
            Status = "Error: " + ex.Message;
        }
        finally
        {
            IsScanning = false;
            _cts.Dispose();
            _cts = null;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void OnNodeSelected(TreeNodeViewModel node)
    {
        _selectedNode = node;
        UpdatePreview();
    }

    private TreeExportOptions CurrentExportOptions() => new()
    {
        Format = ExportFormat,
        IncludeFiles = ExportIncludeFiles,
        ShowSizes = ShowSizes,
        ShowDates = ShowDates,
        ShowCounts = ShowCounts,
        MaxDepth = ExportDepth,
    };

    private string BuildExport() => ExportRoot is { } root ? TreeExporter.Export(root, CurrentExportOptions()) : "";

    private void UpdatePreview()
    {
        OnPropertyChanged(nameof(ExportTitle));
        string text = BuildExport();
        ExportPreview = text.Length <= PreviewLimit
            ? text
            : text[..PreviewLimit] + "\n\n… (vista previa recortada; «Copiar» y «Guardar» incluyen el árbol completo)";
    }

    private void Copy()
    {
        string text = BuildExport();
        try
        {
            Clipboard.SetText(text);
            Status = $"Copiado al portapapeles ({text.Split('\n').Length:N0} líneas).";
        }
        catch (Exception ex)
        {
            Dialogs.Error("No se pudo copiar al portapapeles: " + ex.Message);
        }
    }

    private void Save()
    {
        var root = ExportRoot;
        if (root is null) return;
        string ext = TreeExporter.FileExtension(ExportFormat);
        string filter = ExportFormat switch
        {
            TreeExportFormat.Markdown => "Markdown (*.md)|*.md",
            TreeExportFormat.Json => "JSON (*.json)|*.json",
            _ => "Texto (*.txt)|*.txt",
        };
        string name = "Estructura - " + FileNameSafe(root.Name) + ext;
        var path = Dialogs.SaveFile("Guardar estructura", name, filter + "|Todos los archivos|*.*");
        if (path is null) return;
        try
        {
            File.WriteAllText(path, BuildExport(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            Status = "Guardado en " + path;
        }
        catch (Exception ex)
        {
            Dialogs.Error("No se pudo guardar: " + ex.Message);
        }
    }

    private static string FileNameSafe(string name)
        => string.Concat(name.Split(Path.GetInvalidFileNameChars())).Trim() is { Length: > 0 } s ? s : "raíz";

    private TreeNode? SelectedOrRoot => _selectedNode?.Node ?? _root;

    private void SendToRenamer()
    {
        var node = SelectedOrRoot;
        if (node is null) return;
        if (!node.IsDirectory)
        {
            _sendToRenamer(new[] { node.FullPath });
            return;
        }

        try
        {
            var files = Directory.EnumerateFiles(node.FullPath).ToList();
            if (files.Count == 0)
            {
                Dialogs.Info("La carpeta seleccionada no tiene archivos directamente dentro.");
                return;
            }
            _sendToRenamer(files);
        }
        catch (Exception ex)
        {
            Dialogs.Error(ex.Message);
        }
    }

    private void SendToSubtitles()
    {
        var node = SelectedOrRoot;
        if (node is null) return;
        _sendToSubtitles(node.IsDirectory ? node.FullPath : Path.GetDirectoryName(node.FullPath)!);
    }

    private void OpenInExplorer()
    {
        var node = SelectedOrRoot;
        if (node is null) return;
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{node.FullPath}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Dialogs.Error(ex.Message);
        }
    }
}
