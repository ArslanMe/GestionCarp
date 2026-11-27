using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using GestionCarp.App.Services;
using GestionCarp.Core.Common;
using GestionCarp.Core.Compare;
using GestionCarp.Core.Localization;

namespace GestionCarp.App.ViewModels;

/// <summary>Pestaña «Comparar»: diferencias entre dos carpetas y copia de lo que falta.</summary>
public sealed class CompareViewModel : ObservableObject
{
    private readonly RenameService _service;
    private List<CompareItem> _items = new();
    private string _left = "";
    private string _right = "";
    private CompareMode _mode = CompareMode.NameAndSize;
    private bool _includeSubfolders = true;
    private bool _includeHidden;
    private string _excludePatterns = ".git; Thumbs.db; desktop.ini";
    private bool _showEqual;
    private bool _showDifferent = true;
    private bool _showOnlyLeft = true;
    private bool _showOnlyRight = true;
    private bool _isBusy;
    private string _summary = L.T("cmp_initial");
    private CancellationTokenSource? _cts;

    public CompareViewModel(RenameService service)
    {
        _service = service;
        _service.HistoryChanged += (_, _) => OnPropertyChanged(nameof(UndoDescription));

        BrowseLeftCommand = new RelayCommand(() => { if (Dialogs.PickFolder(L.T("cmp_pick_a"), Left) is { } f) Left = f; }, () => !IsBusy);
        BrowseRightCommand = new RelayCommand(() => { if (Dialogs.PickFolder(L.T("cmp_pick_b"), Right) is { } f) Right = f; }, () => !IsBusy);
        SwapCommand = new RelayCommand(Swap, () => !IsBusy);
        CompareCommand = new RelayCommand(() => _ = CompareAsync(), () => !IsBusy && Left.Trim().Length > 0 && Right.Trim().Length > 0);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        CopyToRightCommand = new RelayCommand(() => _ = CopyMissingAsync(toRight: true), () => !IsBusy && _items.Any(i => i.Status == CompareStatus.OnlyInLeft));
        CopyToLeftCommand = new RelayCommand(() => _ = CopyMissingAsync(toRight: false), () => !IsBusy && _items.Any(i => i.Status == CompareStatus.OnlyInRight));
        CopyReportCommand = new RelayCommand(CopyReport, () => _items.Count > 0);
        UndoCommand = new RelayCommand(() => _service.UndoWithConfirmation(), () => !IsBusy && _service.CanUndo);
    }

    public ObservableCollection<CompareRowViewModel> Rows { get; } = new();

    public ICommand BrowseLeftCommand { get; }
    public ICommand BrowseRightCommand { get; }
    public ICommand SwapCommand { get; }
    public ICommand CompareCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand CopyToRightCommand { get; }
    public ICommand CopyToLeftCommand { get; }
    public ICommand CopyReportCommand { get; }
    public ICommand UndoCommand { get; }

    public string Left { get => _left; set => SetProperty(ref _left, value ?? ""); }
    public string Right { get => _right; set => SetProperty(ref _right, value ?? ""); }
    public CompareMode Mode { get => _mode; set => SetProperty(ref _mode, value); }
    public bool IncludeSubfolders { get => _includeSubfolders; set => SetProperty(ref _includeSubfolders, value); }
    public bool IncludeHidden { get => _includeHidden; set => SetProperty(ref _includeHidden, value); }
    public string ExcludePatterns { get => _excludePatterns; set => SetProperty(ref _excludePatterns, value); }

    public bool ShowEqual { get => _showEqual; set { if (SetProperty(ref _showEqual, value)) Fill(); } }
    public bool ShowDifferent { get => _showDifferent; set { if (SetProperty(ref _showDifferent, value)) Fill(); } }
    public bool ShowOnlyLeft { get => _showOnlyLeft; set { if (SetProperty(ref _showOnlyLeft, value)) Fill(); } }
    public bool ShowOnlyRight { get => _showOnlyRight; set { if (SetProperty(ref _showOnlyRight, value)) Fill(); } }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }
    public string UndoDescription => _service.UndoDescription;
    public bool HasRows => Rows.Count > 0;

    private string CleanLeft => Left.Trim().Trim('"');
    private string CleanRight => Right.Trim().Trim('"');

    /// <summary>Desde el menú contextual: la primera carpeta va a A y la siguiente a B.</summary>
    public void AddFolder(string folder)
    {
        if (IsBusy) return;
        if (CleanLeft.Length == 0)
        {
            Left = folder;
        }
        else if (CleanRight.Length == 0)
        {
            Right = folder;
            _ = CompareAsync();
        }
        else
        {
            // Ya había dos: se empieza una comparación nueva.
            Left = folder;
            Right = "";
            _items.Clear();
            Fill();
        }
        if (CleanRight.Length == 0) Summary = L.T("cmp_waiting_b");
    }

    private void Swap()
    {
        (Left, Right) = (Right, Left);
        if (_items.Count > 0) _ = CompareAsync();
    }

    public async Task CompareAsync()
    {
        string left = CleanLeft, right = CleanRight;
        if (!Directory.Exists(left)) { Summary = L.F("folder_not_exists", left); return; }
        if (!Directory.Exists(right)) { Summary = L.F("folder_not_exists", right); return; }

        var options = new GestionCarp.Core.Compare.CompareOptions
        {
            Mode = Mode,
            IncludeSubfolders = IncludeSubfolders,
            IncludeHidden = IncludeHidden,
            ExcludePatterns = WildcardPattern.SplitList(ExcludePatterns),
        };
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        IsBusy = true;
        Summary = L.T("cmp_working");
        var progress = new Progress<int>(n => { if (IsBusy) Summary = L.F("cmp_working_n", n); });

        try
        {
            _items = await Task.Run(() => FolderComparer.Compare(left, right, options, progress, token), token);
            Fill();
        }
        catch (OperationCanceledException)
        {
            Summary = L.T("exp_cancelled");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Summary = L.F("exp_error", ex.Message);
        }
        finally
        {
            IsBusy = false;
            _cts.Dispose();
            _cts = null;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private bool Visible(CompareItem item) => item.Status switch
    {
        CompareStatus.Equal => ShowEqual,
        CompareStatus.OnlyInLeft => ShowOnlyLeft,
        CompareStatus.OnlyInRight => ShowOnlyRight,
        _ => ShowDifferent,
    };

    private void Fill()
    {
        Rows.Clear();
        foreach (var item in _items.Where(Visible)) Rows.Add(new CompareRowViewModel(item));
        OnPropertyChanged(nameof(HasRows));
        UpdateSummary();
        CommandManager.InvalidateRequerySuggested();
    }

    private void UpdateSummary()
    {
        if (_items.Count == 0 && !IsBusy)
        {
            Summary = L.T("cmp_initial");
            return;
        }
        var files = _items.Where(i => !i.IsDirectory || i.Status is CompareStatus.OnlyInLeft or CompareStatus.OnlyInRight).ToList();
        int equal = files.Count(i => i.Status == CompareStatus.Equal);
        int different = files.Count(i => i.Status is CompareStatus.Different or CompareStatus.TypeMismatch);
        int onlyLeft = files.Count(i => i.Status == CompareStatus.OnlyInLeft);
        int onlyRight = files.Count(i => i.Status == CompareStatus.OnlyInRight);
        Summary = different + onlyLeft + onlyRight == 0
            ? L.F("cmp_sum_identical", equal)
            : L.F("cmp_sum", equal, different, onlyLeft, onlyRight);
    }

    private async Task CopyMissingAsync(bool toRight)
    {
        var status = toRight ? CompareStatus.OnlyInLeft : CompareStatus.OnlyInRight;
        string targetRoot = toRight ? CleanRight : CleanLeft;
        var missing = _items.Where(i => i.Status == status).ToList();
        if (missing.Count == 0) return;

        long bytes = missing.Sum(i => (toRight ? i.Left : i.Right)!.Size);
        int files = missing.Sum(i => (toRight ? i.Left : i.Right)!.FileCount);
        if (!Dialogs.Confirm(L.F("cmp_copy_confirm", files, SizeFormatter.Format(bytes), targetRoot) + "\n\n" + L.T("confirm_undo_hint")))
            return;

        var pairs = missing.Select(i => ((toRight ? i.Left : i.Right)!.FullPath,
            Path.Combine(new[] { targetRoot }.Concat(i.RelativePath.Split('/')).ToArray()))).ToList();

        IsBusy = true;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var progress = new Progress<int>(n => Summary = L.F("cmp_copying", n, pairs.Count));
        try
        {
            var result = await Task.Run(() => FolderCopier.CopyMissing(pairs, L.F("cmp_batch", files, toRight ? "A → B" : "B → A"), progress, token), token);
            if (!result.Success) Dialogs.Error(result.Error ?? L.T("err_unknown"));
            else _service.Record(result);
        }
        finally
        {
            IsBusy = false;
            _cts.Dispose();
            _cts = null;
        }
        await CompareAsync();
    }

    private void CopyReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("A: " + CleanLeft).AppendLine("B: " + CleanRight).AppendLine(Summary).AppendLine();
        foreach (var item in _items.Where(Visible))
            sb.Append('[').Append(CompareRowViewModel.StatusText(item.Status)).Append("] ").Append(item.RelativePath)
              .AppendLine(item.Detail is null ? "" : "  (" + item.Detail + ")");
        try
        {
            Clipboard.SetText(sb.ToString());
            Summary = L.T("cmp_report_copied");
        }
        catch (Exception ex)
        {
            Dialogs.Error(L.F("exp_copy_failed", ex.Message));
        }
    }

    public void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(UndoDescription));
        if (_items.Count > 0) Fill(); else Summary = L.T("cmp_initial");
    }
}

public sealed class CompareRowViewModel
{
    public CompareRowViewModel(CompareItem item) => Item = item;

    public CompareItem Item { get; }
    public CompareStatus Status => Item.Status;

    public string Display => new string(' ', Item.Depth * 4) + (Item.IsDirectory ? "📁 " : "📄 ") + Item.Name;

    public string StatusIcon => Item.Status switch
    {
        CompareStatus.Equal => "=",
        CompareStatus.OnlyInLeft => "◀",
        CompareStatus.OnlyInRight => "▶",
        _ => "≠",
    };

    public string StatusLabel => StatusText(Item.Status);

    public static string StatusText(CompareStatus status) => status switch
    {
        CompareStatus.Equal => L.T("cmp_status_equal"),
        CompareStatus.OnlyInLeft => L.T("cmp_status_only_a"),
        CompareStatus.OnlyInRight => L.T("cmp_status_only_b"),
        _ => L.T("cmp_status_different"),
    };

    public string SizeLeft => Item.Left is { } l ? SizeFormatter.Format(l.Size) : "";
    public string SizeRight => Item.Right is { } r ? SizeFormatter.Format(r.Size) : "";
    public string DateLeft => Item.Left is { } l ? l.LastModified.ToString("g", CultureInfo.CurrentCulture) : "";
    public string DateRight => Item.Right is { } r ? r.LastModified.ToString("g", CultureInfo.CurrentCulture) : "";
    public string? Detail => Item.Detail;
}
