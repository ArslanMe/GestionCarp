using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GestionCarp.Core.Common;
using GestionCarp.Core.Localization;
using GestionCarp.Core.Tree;

namespace GestionCarp.App.ViewModels;

/// <summary>Nodo del TreeView. Los hijos se crean solo al mostrarse, para soportar árboles enormes.</summary>
public sealed class TreeNodeViewModel : ObservableObject
{
    private readonly Action<TreeNodeViewModel> _onSelected;
    private IReadOnlyList<TreeNodeViewModel>? _children;
    private bool _isExpanded;
    private bool _isSelected;

    public TreeNodeViewModel(TreeNode node, Action<TreeNodeViewModel> onSelected)
    {
        Node = node;
        _onSelected = onSelected;
    }

    public TreeNode Node { get; }

    public string Name => Node.Name;

    public string Icon
    {
        get
        {
            if (Node.IsDirectory) return IsExpanded ? "📂" : "📁";
            if (MediaFiles.IsVideo(Node.Name)) return "🎬";
            if (MediaFiles.IsSubtitle(Node.Name)) return "💬";
            return Path.GetExtension(Node.Name).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".heic" => "🖼",
                ".mp3" or ".flac" or ".wav" or ".m4a" or ".ogg" or ".aac" => "🎵",
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "📦",
                _ => "📄",
            };
        }
    }

    public string Details
    {
        get
        {
            if (!Node.IsDirectory) return SizeFormatter.Format(Node.Size);
            if (Node.Error is not null) return Node.Error;
            string text = L.F("node_files", SizeFormatter.Format(Node.Size), Node.FileCount);
            if (Node.FolderCount > 0) text += L.F("node_folders", Node.FolderCount);
            if (Node.IsTruncated) text += L.T("node_truncated");
            return text;
        }
    }

    /// <summary>Actualiza los textos de este nodo y de los hijos ya creados (tras cambiar de idioma).</summary>
    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(Details));
        if (_children is null) return;
        foreach (var child in _children) child.RefreshTexts();
    }

    public IReadOnlyList<TreeNodeViewModel> Children
        => _children ??= Node.Children.Select(c => new TreeNodeViewModel(c, _onSelected)).ToList();

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
                OnPropertyChanged(nameof(Icon));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value) && value)
                _onSelected(this);
        }
    }
}
