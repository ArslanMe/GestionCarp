using System.ComponentModel;
using System.Globalization;

namespace GestionCarp.Core.Localization;

public enum AppLanguage
{
    Spanish,
    English,
    Portuguese,
}

/// <summary>
/// Idioma actual de la aplicación. La interfaz se enlaza a su indexador (<c>Loc.Current["clave"]</c>), así que
/// al cambiar de idioma todos los textos se actualizan sin reiniciar.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Current { get; } = new();

    private AppLanguage _language = AppLanguage.Spanish;

    private Loc() { }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Se lanza después de cambiar de idioma.</summary>
    public event EventHandler? LanguageChanged;

    public AppLanguage Language
    {
        get => _language;
        set
        {
            if (_language == value) return;
            _language = value;
            ApplyCulture();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public CultureInfo Culture => CultureFor(Language);

    public string this[string key] => Get(key);

    public string Get(string key)
        => Strings.Table.TryGetValue(key, out var texts) ? texts[(int)Language] : key;

    public void ApplyCulture()
    {
        var culture = Culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    public static CultureInfo CultureFor(AppLanguage language) => language switch
    {
        AppLanguage.English => CultureInfo.GetCultureInfo("en-US"),
        AppLanguage.Portuguese => CultureInfo.GetCultureInfo("pt-BR"),
        _ => CultureInfo.GetCultureInfo("es-ES"),
    };

    public static string Code(AppLanguage language) => language switch
    {
        AppLanguage.English => "en",
        AppLanguage.Portuguese => "pt",
        _ => "es",
    };

    /// <summary>"es", "en", "pt" (o null/desconocido → idioma del sistema).</summary>
    public static AppLanguage FromCode(string? code)
    {
        string c = (code ?? CultureInfo.InstalledUICulture.TwoLetterISOLanguageName).ToLowerInvariant();
        return c switch
        {
            "es" => AppLanguage.Spanish,
            "pt" => AppLanguage.Portuguese,
            "en" => AppLanguage.English,
            _ => code is null ? AppLanguage.English : FromCode(null),
        };
    }
}

/// <summary>Atajos para obtener textos traducidos desde el código.</summary>
public static class L
{
    public static string T(string key) => Loc.Current.Get(key);

    public static string F(string key, params object?[] args) => string.Format(Loc.Current.Culture, T(key), args);
}
