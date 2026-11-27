using System.IO;
using System.Windows;
using System.Windows.Controls;
using GestionCarp.App.ViewModels;

namespace GestionCarp.App.Views;

public partial class DuplicatesView : UserControl
{
    public DuplicatesView() => InitializeComponent();

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not DuplicatesViewModel vm || e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        foreach (var path in paths)
            if (Directory.Exists(path)) vm.AddFolder(path, search: false);
    }
}
