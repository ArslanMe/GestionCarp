using System;
using System.Collections.Generic;
using GestionCarp.Core.Localization;
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
        ? L.F("svc_undo", last.Description, last.Timestamp.ToString("g", Loc.Current.Culture))
        : L.T("hist_empty");

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

    /// <summary>Registra en el historial algo ya hecho fuera del ejecutor (por ejemplo, crear carpetas).</summary>
    public void Record(RenameResult result)
    {
        if (!result.Success || result.Batch is null) return;
        History.Push(result.Batch);
        FilesChanged?.Invoke(this, result.Executed);
    }

    /// <summary>Pregunta, deshace lo último y muestra el resultado.</summary>
    public void UndoWithConfirmation()
    {
        if (!Dialogs.Confirm(UndoDescription + "?")) return;
        var result = Undo();
        if (!result.Success)
            Dialogs.Error(result.Error ?? L.T("err_unknown"));
        else if (result.Notice is not null)
            Dialogs.Info(result.Notice);
    }

    public RenameResult Undo()
    {
        var result = History.UndoLast();
        if (result.Success)
            FilesChanged?.Invoke(this, result.Executed);
        return result;
    }
}
