using System.Collections.Generic;
using GestionCarp.Core.Common;
using GestionCarp.Core.Localization;
using GestionCarp.Core.Rename;
using GestionCarp.Core.Rename.Rules;
using GestionCarp.Core.Subtitles;
using GestionCarp.Core.Tree;

namespace GestionCarp.App.ViewModels;

/// <summary>Opción de un desplegable; su texto cambia con el idioma.</summary>
public sealed class Choice : ObservableObject
{
    private readonly string _key;

    public Choice(object value, string key)
    {
        Value = value;
        _key = key;
        Loc.Current.LanguageChanged += (_, _) => OnPropertyChanged(nameof(Label));
    }

    public object Value { get; }
    public string Label => L.T(_key);
    public override string ToString() => Label;
}

/// <summary>Listas de opciones de los desplegables.</summary>
public static class Choices
{
    public static IReadOnlyList<Choice> RuleScopes { get; } = new[]
    {
        new Choice(RuleScope.Name, "scope_name"),
        new Choice(RuleScope.Extension, "scope_ext"),
        new Choice(RuleScope.NameAndExtension, "scope_both"),
    };

    public static IReadOnlyList<Choice> NumberPlacements { get; } = new[]
    {
        new Choice(NumberPlacement.ReplaceName, "place_replace"),
        new Choice(NumberPlacement.Prefix, "place_prefix"),
        new Choice(NumberPlacement.Suffix, "place_suffix"),
    };

    public static IReadOnlyList<Choice> InsertPositions { get; } = new[]
    {
        new Choice(InsertPosition.Start, "inspos_start"),
        new Choice(InsertPosition.End, "inspos_end"),
        new Choice(InsertPosition.AtIndex, "inspos_index"),
    };

    public static IReadOnlyList<Choice> RemoveModes { get; } = new[]
    {
        new Choice(RemoveMode.FirstChars, "remmode_first"),
        new Choice(RemoveMode.LastChars, "remmode_last"),
        new Choice(RemoveMode.Range, "remmode_range"),
        new Choice(RemoveMode.BetweenDelimiters, "remmode_between"),
    };

    public static IReadOnlyList<Choice> CaseModes { get; } = new[]
    {
        new Choice(CaseMode.Title, "casemode_title"),
        new Choice(CaseMode.Sentence, "casemode_sentence"),
        new Choice(CaseMode.Lower, "casemode_lower"),
        new Choice(CaseMode.Upper, "casemode_upper"),
    };

    public static IReadOnlyList<Choice> ExportFormats { get; } = new[]
    {
        new Choice(TreeExportFormat.Text, "fmt_text"),
        new Choice(TreeExportFormat.Markdown, "fmt_md"),
        new Choice(TreeExportFormat.Json, "fmt_json"),
        new Choice(TreeExportFormat.Html, "fmt_html"),
        new Choice(TreeExportFormat.Excel, "fmt_excel"),
        new Choice(TreeExportFormat.Word, "fmt_word"),
    };

    public static IReadOnlyList<Choice> SubtitleModes { get; } = new[]
    {
        new Choice(SubtitleMatchMode.ByEpisode, "submode_episode"),
        new Choice(SubtitleMatchMode.ByOrder, "submode_order"),
    };
}
