using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GestionCarp.App.ViewModels;

namespace GestionCarp.App.Views;

public partial class RenamerView : UserControl
{
    public RenamerView() => InitializeComponent();

    private RenamerViewModel? ViewModel => DataContext as RenamerViewModel;

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            ViewModel?.AddPaths(paths);
    }

    private void OnGridKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || ViewModel is null) return;
        var selected = FilesGrid.SelectedItems;
        if (ViewModel.RemoveSelectedCommand.CanExecute(selected))
            ViewModel.RemoveSelectedCommand.Execute(selected);
        e.Handled = true;
    }
}
