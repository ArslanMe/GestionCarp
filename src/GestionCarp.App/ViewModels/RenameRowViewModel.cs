using System.Collections.Generic;
using System.IO;
using System.Linq;
using GestionCarp.Core.Common;
using GestionCarp.Core.Localization;
using GestionCarp.Core.Rename;

namespace GestionCarp.App.ViewModels;

/// <summary>Una fila de la lista del renombrador.</summary>
public sealed class RenameRowViewModel : ObservableObject
{
    private string _path;
    private string _newName = "";
    private RenameStatus _status;
    private string? _message;
    private IReadOnlyList<RenamePreviewRow> _companions = new List<RenamePreviewRow>();

    public RenameRowViewModel(RenameItem item)
    {
        _path = item.FullPath;
        IsDirectory = item.IsDirectory;
        _newName = OriginalName;
    }

    public bool IsDirectory { get; }

    public string Path
    {
        get => _path;
        set
        {
            if (SetProperty(ref _path, value))
            {
                OnPropertyChanged(nameof(OriginalName));
                OnPropertyChanged(nameof(Folder));
            }
        }
    }

    public string OriginalName => System.IO.Path.GetFileName(Path);
    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? "";
    public string Icon => IsDirectory ? "📁" : MediaFiles.IsVideo(Path) ? "🎬" : MediaFiles.IsSubtitle(Path) ? "💬" : "📄";

    public RenameItem ToItem() => new(Path, IsDirectory);

    public string NewName { get => _newName; private set => SetProperty(ref _newName, value); }

    public RenameStatus Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusIcon));
                OnPropertyChanged(nameof(IsChanged));
            }
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

    public bool IsChanged => Status is RenameStatus.Ready or RenameStatus.Warning;

    public string? Message { get => _message; private set => SetProperty(ref _message, value); }

    public string CompanionsText => _companions.Count switch
    {
        0 => "",
        1 => L.T("ren_one_sub"),
        _ => L.F("ren_n_subs", _companions.Count),
    };

    public string? CompanionsTooltip => _companions.Count == 0
        ? null
        : string.Join("\n", _companions.Select(c =>
            $"{c.OriginalName}  →  {c.NewName}" + (c.Status == RenameStatus.Error ? $"   ✖ {c.Message}" : "")));

    public void Update(RenamePreviewRow? row, IReadOnlyList<RenamePreviewRow> companions)
    {
        NewName = row?.NewName ?? OriginalName;
        Status = row?.Status ?? RenameStatus.Unchanged;
        Message = row?.Message;
        if (row?.Status != RenameStatus.Error && companions.FirstOrDefault(c => c.Status == RenameStatus.Error) is { } failed)
            Message = L.F("ren_sub_error", failed.OriginalName, failed.Message);
        _companions = companions;
        OnPropertyChanged(nameof(CompanionsText));
        OnPropertyChanged(nameof(CompanionsTooltip));
    }
}
