using System.Text.Json;
using System.Text.Json.Serialization;
using GestionCarp.Core.Localization;
using GestionCarp.Core.Rename.Rules;

namespace GestionCarp.Core.Rename;

/// <summary>Una combinación de reglas guardada con un nombre.</summary>
public sealed class RulePreset
{
    public string Name { get; set; } = "";
    public List<RenameRule> Rules { get; set; } = new();
}

/// <summary>Guarda y carga presets de reglas (archivo JSON), y permite exportarlos para compartirlos.</summary>
public sealed class PresetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string? _filePath;
    private readonly List<RulePreset> _presets = new();

    public PresetStore(string? filePath)
    {
        _filePath = filePath;
        if (filePath is not null && File.Exists(filePath))
        {
            try
            {
                _presets.AddRange(JsonSerializer.Deserialize<List<RulePreset>>(File.ReadAllText(filePath), JsonOptions) ?? new());
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                // Archivo dañado: se empieza con los presets de ejemplo.
            }
        }
        else
        {
            _presets.AddRange(BuiltIn());
            Save();
        }
    }

    public IReadOnlyList<RulePreset> Presets => _presets;

    public RulePreset? Find(string name) => _presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.CurrentCultureIgnoreCase));

    /// <summary>Guarda (o sustituye si ya existe uno con ese nombre) una copia de las reglas.</summary>
    public void Save(string name, IEnumerable<RenameRule> rules)
    {
        var preset = new RulePreset { Name = name.Trim(), Rules = Clone(rules) };
        int index = _presets.FindIndex(p => string.Equals(p.Name, preset.Name, StringComparison.CurrentCultureIgnoreCase));
        if (index >= 0) _presets[index] = preset;
        else _presets.Add(preset);
        Save();
    }

    public bool Delete(string name)
    {
        int removed = _presets.RemoveAll(p => string.Equals(p.Name, name, StringComparison.CurrentCultureIgnoreCase));
        if (removed > 0) Save();
        return removed > 0;
    }

    /// <summary>Copias nuevas de las reglas del preset, listas para editar sin tocar el original.</summary>
    public static List<RenameRule> Clone(IEnumerable<RenameRule> rules)
        => JsonSerializer.Deserialize<List<RenameRule>>(JsonSerializer.Serialize(rules.ToList(), JsonOptions), JsonOptions) ?? new();

    public static void ExportToFile(RulePreset preset, string path)
        => File.WriteAllText(path, JsonSerializer.Serialize(preset, JsonOptions));

    /// <summary>Lee un preset exportado. Lanza <see cref="InvalidDataException"/> si el archivo no es válido.</summary>
    public static RulePreset ImportFromFile(string path)
    {
        try
        {
            var preset = JsonSerializer.Deserialize<RulePreset>(File.ReadAllText(path), JsonOptions);
            if (preset is null || preset.Rules.Count == 0) throw new InvalidDataException(L.T("preset_invalid"));
            if (string.IsNullOrWhiteSpace(preset.Name)) preset.Name = Path.GetFileNameWithoutExtension(path);
            return preset;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new InvalidDataException(L.T("preset_invalid"), ex);
        }
    }

    private void Save()
    {
        if (_filePath is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(_presets, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Presets de ejemplo que se crean la primera vez.</summary>
    public static IEnumerable<RulePreset> BuiltIn()
    {
        yield return new RulePreset
        {
            Name = L.T("preset_builtin_series"),
            Rules = { new CleanupRule(), new SeriesTemplateRule() },
        };
        yield return new RulePreset
        {
            Name = L.T("preset_builtin_clean"),
            Rules = { new CleanupRule(), new CaseRule { Mode = CaseMode.Title } },
        };
        yield return new RulePreset
        {
            Name = L.T("preset_builtin_number"),
            Rules = { new NumberingRule { TextBefore = L.T("preset_builtin_number_text") } },
        };
    }
}
