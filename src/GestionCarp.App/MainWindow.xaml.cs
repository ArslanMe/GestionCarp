using System.Windows;
using GestionCarp.App.ViewModels;

namespace GestionCarp.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
