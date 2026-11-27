using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace GestionCarp.App.Services;

public static class Dialogs
{
    private const string AppName = "GestionCarp";

    public const string MediaFilter =
        "Todos los archivos|*.*|" +
        "Vídeos|*.mkv;*.mp4;*.avi;*.mov;*.wmv;*.m4v;*.webm;*.ts;*.m2ts;*.flv;*.mpg;*.mpeg|" +
        "Subtítulos|*.srt;*.ass;*.ssa;*.sub;*.idx;*.vtt;*.sup";

    public static string? PickFolder(string title, string? initial = null)
    {
        var dialog = new OpenFolderDialog { Title = title };
        if (!string.IsNullOrWhiteSpace(initial) && Directory.Exists(initial))
            dialog.InitialDirectory = initial;
        return Show(dialog) ? dialog.FolderName : null;
    }

    public static string[]? PickFiles(string title, string filter = MediaFilter)
    {
        var dialog = new OpenFileDialog { Title = title, Multiselect = true, Filter = filter };
        return Show(dialog) ? dialog.FileNames : null;
    }

    public static string? SaveFile(string title, string fileName, string filter)
    {
        var dialog = new SaveFileDialog { Title = title, FileName = fileName, Filter = filter, AddExtension = true };
        return Show(dialog) ? dialog.FileName : null;
    }

    public static void Info(string message)
        => Message(message, MessageBoxButton.OK, MessageBoxImage.Information);

    public static void Error(string message)
        => Message(message, MessageBoxButton.OK, MessageBoxImage.Error);

    public static bool Confirm(string message)
        => Message(message, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    private static Window? Owner => Application.Current?.MainWindow is { IsLoaded: true } w ? w : null;

    private static bool Show(CommonDialog dialog)
        => (Owner is { } owner ? dialog.ShowDialog(owner) : dialog.ShowDialog()) == true;

    private static MessageBoxResult Message(string text, MessageBoxButton buttons, MessageBoxImage image)
        => Owner is { } owner
            ? MessageBox.Show(owner, text, AppName, buttons, image)
            : MessageBox.Show(text, AppName, buttons, image);
}
