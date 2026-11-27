namespace GestionCarp.Core.Rename;

/// <summary>Un cambio de nombre (o de ubicación) de un archivo o carpeta.</summary>
public sealed record RenameOperation(string SourcePath, string TargetPath, bool IsDirectory)
{
    public RenameOperation Reverse() => new(TargetPath, SourcePath, IsDirectory);
}

/// <summary>Un grupo de cambios realizados juntos; es la unidad que se puede deshacer.</summary>
public sealed class RenameBatch
{
    public string Description { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>Operaciones en el orden en que se ejecutaron.</summary>
    public List<RenameOperation> Operations { get; set; } = new();
}

public sealed class RenameResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public RenameBatch? Batch { get; init; }

    public IReadOnlyList<RenameOperation> Executed => Batch?.Operations ?? (IReadOnlyList<RenameOperation>)Array.Empty<RenameOperation>();
}

/// <summary>
/// Ejecuta los renombrados de forma segura: primero pasa cada elemento a un nombre temporal y luego
/// al definitivo (así funcionan intercambios como A→B y B→A). Si algo falla, revierte todo.
/// </summary>
public static class RenameExecutor
{
    public static RenameResult Execute(IEnumerable<RenameOperation> operations, string description, bool deepestFirst = true)
    {
        var indexed = operations
            .Where(o => !string.Equals(o.SourcePath, o.TargetPath, StringComparison.Ordinal))
            .Select((op, i) => (op, i, depth: Depth(op.SourcePath)))
            .ToList();

        var groups = (deepestFirst
                ? indexed.GroupBy(x => x.depth).OrderByDescending(g => g.Key)
                : indexed.GroupBy(x => x.depth).OrderBy(g => g.Key))
            .Select(g => g.OrderBy(x => x.i).Select(x => x.op).ToList())
            .ToList();

        var moves = new List<(string From, string To, bool IsDirectory)>();
        var executed = new List<RenameOperation>();

        try
        {
            foreach (var group in groups)
            {
                var temps = new List<(RenameOperation Op, string Temp)>();
                foreach (var op in group)
                {
                    if (!PathExists(op.SourcePath))
                        throw new IOException($"No se encuentra: {op.SourcePath}");
                    string temp = Path.Combine(Path.GetDirectoryName(op.SourcePath) ?? "",
                        "~gc_" + Guid.NewGuid().ToString("N")[..12] + ".tmp");
                    Move(op.SourcePath, temp, op.IsDirectory);
                    moves.Add((op.SourcePath, temp, op.IsDirectory));
                    temps.Add((op, temp));
                }

                foreach (var (op, temp) in temps)
                {
                    if (PathExists(op.TargetPath))
                        throw new IOException($"Ya existe: {op.TargetPath}");
                    Move(temp, op.TargetPath, op.IsDirectory);
                    moves.Add((temp, op.TargetPath, op.IsDirectory));
                    executed.Add(op);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            var rollbackErrors = new List<string>();
            for (int i = moves.Count - 1; i >= 0; i--)
            {
                var (from, to, isDirectory) = moves[i];
                try { Move(to, from, isDirectory); }
                catch (Exception rollbackEx) { rollbackErrors.Add($"{to}: {rollbackEx.Message}"); }
            }

            string message = "No se pudo completar el renombrado: " + ex.Message;
            message += rollbackErrors.Count == 0
                ? "\nNo se ha cambiado nada (se revirtieron los cambios)."
                : "\nAlgunos cambios no se pudieron revertir:\n" + string.Join("\n", rollbackErrors);
            return new RenameResult { Success = false, Error = message };
        }

        return new RenameResult
        {
            Success = true,
            Batch = new RenameBatch { Description = description, Timestamp = DateTime.Now, Operations = executed },
        };
    }

    /// <summary>Deshace un lote: aplica las operaciones inversas, de las carpetas exteriores a las interiores.</summary>
    public static RenameResult Undo(RenameBatch batch)
        => Execute(batch.Operations.Select(o => o.Reverse()), "Deshacer: " + batch.Description, deepestFirst: false);

    private static int Depth(string path) => path.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar);

    private static bool PathExists(string path) => File.Exists(path) || Directory.Exists(path);

    private static void Move(string from, string to, bool isDirectory)
    {
        if (isDirectory) Directory.Move(from, to);
        else File.Move(from, to);
    }
}
