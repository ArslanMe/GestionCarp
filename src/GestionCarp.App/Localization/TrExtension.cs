using System;
using System.Windows.Data;
using System.Windows.Markup;
using GestionCarp.Core.Localization;

namespace GestionCarp.App.Localization;

/// <summary>
/// Texto traducido en XAML: <c>Text="{l:Tr lbl_folder}"</c>. Es un enlace al idioma actual,
/// así que cambia en cuanto el usuario elige otro idioma.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }

    public TrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]") { Source = Loc.Current, Mode = BindingMode.OneWay };
        return binding.ProvideValue(serviceProvider);
    }
}
