using System;
using System.IO;
using System.Reflection;
using System.Text;
using GestionCarp.Core.Localization;

namespace GestionCarp.App.Services;

/// <summary>
/// Abre la guía de uso. La guía va dentro del programa (sin servidores ni internet): se copia a
/// %LocalAppData%\GestionCarp\ayuda y se abre en el navegador predeterminado, en el apartado pedido.
/// </summary>
public static class HelpService
{
    private const string ResourceName = "GestionCarp.Help.guia.html";

    /// <param name="section">inicio, explorador, renombrar, subtitulos, crear…</param>
    public static void Open(string section = "inicio")
    {
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GestionCarp", "ayuda");
            Directory.CreateDirectory(folder);

            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
                                  ?? throw new FileNotFoundException(ResourceName))
            using (var file = File.Create(Path.Combine(folder, "guia.html")))
            {
                resource.CopyTo(file);
            }

            // Windows ignora "?idioma#apartado" al abrir un archivo local, así que se usa una página puente.
            string target = $"guia.html?lang={Loc.Code(Loc.Current.Language)}&s={Uri.EscapeDataString(section)}";
            string launcher = Path.Combine(folder, "abrir.html");
            File.WriteAllText(launcher,
                $"""<!DOCTYPE html><html><head><meta charset="utf-8"><meta http-equiv="refresh" content="0;url={target}"><title>GestionCarp</title></head><body><a href="{target}">GestionCarp</a></body></html>""",
                new UTF8Encoding(false));

            AppInfo.OpenUrl(launcher);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Dialogs.Error(L.F("help_failed", ex.Message));
        }
    }
}
