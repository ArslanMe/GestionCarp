using System;
using GestionCarp.Core.Localization;
using Microsoft.Win32;

namespace GestionCarp.App.Services;

/// <summary>
/// Añade (o quita) «GestionCarp» al menú contextual del Explorador de Windows, solo para el usuario actual
/// (HKEY_CURRENT_USER), sin permisos de administrador. En Windows 11 aparece en «Mostrar más opciones».
/// </summary>
public static class ShellIntegration
{
    private const string FolderKey = @"Software\Classes\Directory\shell\GestionCarp";
    private const string BackgroundKey = @"Software\Classes\Directory\Background\shell\GestionCarp";
    private const string FileKey = @"Software\Classes\*\shell\GestionCarp";

    /// <summary>Verbos del submenú: (orden, clave de texto, argumento de línea de comandos).</summary>
    private static readonly (string Order, string TextKey, string Verb)[] FolderVerbs =
    {
        ("1", "shell_explore", "--explorar"),
        ("2", "shell_rename", "--renombrar"),
        ("3", "shell_subtitles", "--subtitulos"),
        ("4", "shell_compare", "--comparar"),
        ("5", "shell_duplicates", "--duplicados"),
        ("6", "shell_create", "--crear"),
    };

    public static bool IsInstalled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(FolderKey);
        return key is not null;
    }

    public static void Install()
    {
        string exe = Environment.ProcessPath ?? throw new InvalidOperationException("ProcessPath");
        Uninstall();
        WriteMenu(FolderKey, exe, "%1");
        WriteMenu(BackgroundKey, exe, "%V");

        using var file = Registry.CurrentUser.CreateSubKey(FileKey);
        file.SetValue("MUIVerb", L.T("shell_file_rename"));
        file.SetValue("Icon", $"\"{exe}\",0");
        using var command = file.CreateSubKey("command");
        command.SetValue("", $"\"{exe}\" --renombrar \"%1\"");
    }

    public static void Uninstall()
    {
        foreach (var key in new[] { FolderKey, BackgroundKey, FileKey })
            Registry.CurrentUser.DeleteSubKeyTree(key, throwOnMissingSubKey: false);
    }

    private static void WriteMenu(string path, string exe, string placeholder)
    {
        using var root = Registry.CurrentUser.CreateSubKey(path);
        root.SetValue("MUIVerb", "GestionCarp");
        root.SetValue("Icon", $"\"{exe}\",0");
        root.SetValue("SubCommands", "");
        using var shell = root.CreateSubKey("shell");
        foreach (var (order, textKey, verb) in FolderVerbs)
        {
            using var item = shell.CreateSubKey(order + verb.TrimStart('-'));
            item.SetValue("MUIVerb", L.T(textKey));
            using var command = item.CreateSubKey("command");
            command.SetValue("", $"\"{exe}\" {verb} \"{placeholder}\"");
        }
    }
}
