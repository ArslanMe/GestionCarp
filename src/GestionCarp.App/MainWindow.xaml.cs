using System;
using System.ComponentModel;
using System.Windows;
using GestionCarp.App.Services;
using GestionCarp.App.ViewModels;

namespace GestionCarp.App;

public partial class MainWindow : Window
{
    private const double PreferredWidth = 1240;
    private const double PreferredHeight = 780;
    private const double ScreenMargin = 24;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        PlaceOnScreen();
    }

    /// <summary>
    /// Restaura la última posición si sigue siendo visible; si no, ajusta la ventana al área de trabajo
    /// (pantalla menos la barra de tareas) para que la barra de título y sus botones siempre se vean.
    /// </summary>
    private void PlaceOnScreen()
    {
        var work = SystemParameters.WorkArea;
        MinWidth = Math.Min(MinWidth, work.Width);
        MinHeight = Math.Min(MinHeight, work.Height);

        var s = App.Settings;
        if (s.WindowLeft is double left && s.WindowTop is double top && s.WindowWidth is double width && s.WindowHeight is double height
            && IsVisibleOnScreens(left, top, width, height))
        {
            Width = Math.Max(MinWidth, width);
            Height = Math.Max(MinHeight, height);
            Left = left;
            Top = top;
        }
        else
        {
            Width = Math.Max(MinWidth, Math.Min(PreferredWidth, work.Width - ScreenMargin * 2));
            Height = Math.Max(MinHeight, Math.Min(PreferredHeight, work.Height - ScreenMargin * 2));
            Left = work.Left + (work.Width - Width) / 2;
            Top = work.Top + Math.Max(0, (work.Height - Height) / 2);
        }

        if (s.WindowMaximized) WindowState = WindowState.Maximized;
    }

    /// <summary>La barra de título debe quedar dentro del escritorio (contando todos los monitores).</summary>
    private static bool IsVisibleOnScreens(double left, double top, double width, double height)
    {
        double screenLeft = SystemParameters.VirtualScreenLeft;
        double screenTop = SystemParameters.VirtualScreenTop;
        double screenRight = screenLeft + SystemParameters.VirtualScreenWidth;
        double screenBottom = screenTop + SystemParameters.VirtualScreenHeight;
        return width >= 400 && height >= 300
               && top >= screenTop && top + 40 <= screenBottom
               && left + 200 <= screenRight && left + width - 200 >= screenLeft;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        var s = App.Settings;
        if (!bounds.IsEmpty)
        {
            s.WindowLeft = bounds.Left;
            s.WindowTop = bounds.Top;
            s.WindowWidth = bounds.Width;
            s.WindowHeight = bounds.Height;
        }
        s.WindowMaximized = WindowState == WindowState.Maximized;
        s.Save();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (App.Settings.CheckUpdatesOnStartup)
            await Updater.CheckAsync(silent: true);
    }
}
