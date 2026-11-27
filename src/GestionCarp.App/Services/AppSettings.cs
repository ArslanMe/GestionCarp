using System;
using System.IO;
using System.Text.Json;

namespace GestionCarp.App.Services;

/// <summary>Preferencias del usuario, guardadas en %LocalAppData%\GestionCarp\ajustes.json.</summary>
public sealed class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GestionCarp", "ajustes.json");

    /// <summary>"es", "en" o "pt". Vacío = idioma del sistema.</summary>
    public string Language { get; set; } = "";

    public bool CheckUpdatesOnStartup { get; set; } = true;

    /// <summary>El usuario activó «GestionCarp» en el menú contextual del Explorador.</summary>
    public bool ExplorerMenu { get; set; }

    /// <summary>Posición y tamaño de la ventana al cerrarla (en píxeles lógicos). Null = sin guardar.</summary>
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
