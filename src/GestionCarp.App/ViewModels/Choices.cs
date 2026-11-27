using System.Collections.Generic;
using GestionCarp.Core.Rename;
using GestionCarp.Core.Rename.Rules;
using GestionCarp.Core.Subtitles;
using GestionCarp.Core.Tree;

namespace GestionCarp.App.ViewModels;

public sealed record Choice(object Value, string Label);

/// <summary>Textos en español para los desplegables de opciones.</summary>
public static class Choices
{
    public static IReadOnlyList<Choice> RuleScopes { get; } = new[]
    {
        new Choice(RuleScope.Name, "Solo el nombre"),
        new Choice(RuleScope.Extension, "Solo la extensión"),
        new Choice(RuleScope.NameAndExtension, "Nombre y extensión"),
    };

    public static IReadOnlyList<Choice> NumberPlacements { get; } = new[]
    {
        new Choice(NumberPlacement.ReplaceName, "Sustituir el nombre completo"),
        new Choice(NumberPlacement.Prefix, "Añadir al principio"),
        new Choice(NumberPlacement.Suffix, "Añadir al final"),
    };

    public static IReadOnlyList<Choice> InsertPositions { get; } = new[]
    {
        new Choice(InsertPosition.Start, "Al principio"),
        new Choice(InsertPosition.End, "Al final"),
        new Choice(InsertPosition.AtIndex, "En una posición"),
    };

    public static IReadOnlyList<Choice> RemoveModes { get; } = new[]
    {
        new Choice(RemoveMode.FirstChars, "Los primeros caracteres"),
        new Choice(RemoveMode.LastChars, "Los últimos caracteres"),
        new Choice(RemoveMode.Range, "Un tramo desde una posición"),
        new Choice(RemoveMode.BetweenDelimiters, "Todo lo que esté entre delimitadores"),
    };

    public static IReadOnlyList<Choice> CaseModes { get; } = new[]
    {
        new Choice(CaseMode.Title, "Tipo Título"),
        new Choice(CaseMode.Sentence, "Tipo oración"),
        new Choice(CaseMode.Lower, "minúsculas"),
        new Choice(CaseMode.Upper, "MAYÚSCULAS"),
    };

    public static IReadOnlyList<Choice> ExportFormats { get; } = new[]
    {
        new Choice(TreeExportFormat.Text, "Texto (árbol)"),
        new Choice(TreeExportFormat.Markdown, "Markdown"),
        new Choice(TreeExportFormat.Json, "JSON"),
    };

    public static IReadOnlyList<Choice> SubtitleModes { get; } = new[]
    {
        new Choice(SubtitleMatchMode.ByEpisode, "Por número de episodio (S01E05, 1x05, Cap 5…)"),
        new Choice(SubtitleMatchMode.ByOrder, "Por orden alfabético en cada carpeta"),
    };
}
