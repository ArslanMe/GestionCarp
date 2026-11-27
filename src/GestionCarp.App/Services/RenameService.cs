using System;
using System.Collections.Generic;
using GestionCarp.Core.Rename;

namespace GestionCarp.App.Services;

/// <summary>
/// Punto único para ejecutar y deshacer renombrados. Avisa a todas las pestañas cuando cambian
/// archivos en disco, para que actualicen sus listas.
/// </summary>
public sealed class RenameService
{
    public RenameService(UndoHistory history)
    {
        History = history;
        History.Changed += (_, _) => HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public UndoHistory History { get; }

    /// <summary>Se lanza tras renombrar o deshacer, con las operaciones realizadas en orden.</summary>
    public event EventHandler<IReadOnlyList<RenameOperation>>? FilesChanged;

    public event EventHandler? HistoryChanged;

    public bool CanUndo => History.CanUndo;

    public string UndoDescription => History.Last is { } last
        ? $"Deshacer «{last.Description}» ({last.Timestamp:dd/MM HH:mm})"
        : "No hay nada que deshacer";

    public RenameResult Execute(IReadOnlyList<RenameOperation> operations, string description)
    {
        var result = RenameExecutor.Execute(operations, description);
        if (result.Success && result.Batch is not null)
        {
            History.Push(result.Batch);
            FilesChanged?.Invoke(this, result.Executed);
        }
        return result;
    }

    public RenameResult Undo()
    {
        var result = History.UndoLast();
        if (result.Success)
            FilesChanged?.Invoke(this, result.Executed);
        return result;
    }
}
