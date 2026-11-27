using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using GestionCarp.App.ViewModels;

namespace GestionCarp.App.Views;

public partial class SubtitlesView : UserControl
{
    public SubtitlesView() => InitializeComponent();

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not SubtitlesViewModel vm || e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        string? folder = paths.Select(p => Directory.Exists(p) ? p : Path.GetDirectoryName(p)).FirstOrDefault(p => p is not null);
        if (folder is null) return;
        vm.FolderPath = folder;
        vm.Analyze();
    }
}
