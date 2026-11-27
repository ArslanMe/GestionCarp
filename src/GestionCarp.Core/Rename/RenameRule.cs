using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using GestionCarp.Core.Rename.Rules;
using GestionCarp.Core.Common;

namespace GestionCarp.Core.Rename;

/// <summary>Parte del nombre sobre la que actúa una regla.</summary>
public enum RuleScope
{
    /// <summary>Solo el nombre, se conserva la extensión.</summary>
    Name,
    /// <summary>Solo la extensión (sin el punto).</summary>
    Extension,
    /// <summary>El nombre completo, extensión incluida.</summary>
    NameAndExtension,
}

/// <summary>Datos de contexto que recibe cada regla al procesar un elemento.</summary>
public sealed record RenameContext(int Index, int IndexInFolder, int Total, string OriginalPath, bool IsDirectory);

/// <summary>Una regla de renombrado. Las reglas se aplican en cadena, en el orden de la lista.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "regla")]
[JsonDerivedType(typeof(NumberingRule), "numerar")]
[JsonDerivedType(typeof(ReplaceRule), "reemplazar")]
[JsonDerivedType(typeof(InsertRule), "insertar")]
[JsonDerivedType(typeof(RemoveRule), "eliminar")]
[JsonDerivedType(typeof(CaseRule), "mayusculas")]
[JsonDerivedType(typeof(CleanupRule), "limpiar")]
[JsonDerivedType(typeof(SeriesTemplateRule), "plantilla_series")]
[JsonDerivedType(typeof(MetadataRule), "metadatos")]
public abstract class RenameRule : ObservableObject
{
    private bool _isEnabled = true;
    private RuleScope _scope = RuleScope.Name;

    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }

    public RuleScope Scope { get => _scope; set => SetProperty(ref _scope, value); }

    /// <summary>Nombre del tipo de regla ("Numerar", "Reemplazar"...).</summary>
    [JsonIgnore] public abstract string DisplayName { get; }

    /// <summary>Resumen de la configuración, para mostrar en la lista de reglas.</summary>
    [JsonIgnore] public abstract string Summary { get; }

    /// <summary>Transforma el texto (nombre, extensión o ambos según <see cref="Scope"/>).</summary>
    protected abstract string ApplyCore(string text, RenameContext context);

    public string Apply(string fileName, RenameContext context)
    {
        if (context.IsDirectory || Scope == RuleScope.NameAndExtension)
            return ApplyCore(fileName, context);

        string extension = Path.GetExtension(fileName);
        string name = fileName[..^extension.Length];
        string extNoDot = extension.TrimStart('.');

        if (Scope == RuleScope.Name)
            return ApplyCore(name, context) + extension;

        string newExt = ApplyCore(extNoDot, context);
        return newExt.Length == 0 ? name : name + "." + newExt;
    }

    /// <summary>Vuelve a notificar los textos visibles (tras cambiar de idioma).</summary>
    public void RefreshTexts()
    {
        base.OnPropertyChanged(nameof(DisplayName));
        base.OnPropertyChanged(nameof(Summary));
    }

    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName != nameof(Summary))
            base.OnPropertyChanged(nameof(Summary));
    }
}

/// <summary>Error de configuración de una regla (por ejemplo, una expresión regular inválida).</summary>
public sealed class RenameRuleException : Exception
{
    public RenameRuleException(string message, Exception? inner = null) : base(message, inner) { }
}
