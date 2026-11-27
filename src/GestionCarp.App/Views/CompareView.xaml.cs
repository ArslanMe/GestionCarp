using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using GestionCarp.App.ViewModels;

namespace GestionCarp.App.Views;

public partial class CompareView : UserControl
{
    public CompareView() => InitializeComponent();

    private CompareViewModel? ViewModel => DataContext as CompareViewModel;

    private void OnFolderDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    private static string? DroppedFolder(DragEventArgs e)
        => (e.Data.GetData(DataFormats.FileDrop) as string[])?
            .Select(p => Directory.Exists(p) ? p : Path.GetDirectoryName(p)).FirstOrDefault(p => p is not null);

    private void OnDropLeft(object sender, DragEventArgs e)
    {
        if (ViewModel is not null && DroppedFolder(e) is { } folder) ViewModel.Left = folder;
        e.Handled = true;
    }

    private void OnDropRight(object sender, DragEventArgs e)
    {
        if (ViewModel is not null && DroppedFolder(e) is { } folder) ViewModel.Right = folder;
        e.Handled = true;
    }
}
