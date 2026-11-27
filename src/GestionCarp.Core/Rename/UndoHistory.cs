using System.Text.Json;

namespace GestionCarp.Core.Rename;

/// <summary>Historial de renombrados que se pueden deshacer. Se guarda en disco para sobrevivir a reinicios.</summary>
public sealed class UndoHistory
{
    private const int MaxEntries = 50;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string? _filePath;
    private readonly List<RenameBatch> _batches = new();

    public UndoHistory(string? filePath = null)
    {
        _filePath = filePath;
        Load();
    }

    public event EventHandler? Changed;

    public IReadOnlyList<RenameBatch> Batches => _batches;

    public bool CanUndo => _batches.Count > 0;

    public RenameBatch? Last => _batches.Count > 0 ? _batches[^1] : null;

    public void Push(RenameBatch batch)
    {
        if (batch.Operations.Count == 0) return;
        _batches.Add(batch);
        if (_batches.Count > MaxEntries)
            _batches.RemoveRange(0, _batches.Count - MaxEntries);
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Deshace el último lote. Si falla, el lote se queda en el historial.</summary>
    public RenameResult UndoLast()
    {
        var batch = Last ?? throw new InvalidOperationException("No hay nada que deshacer.");
        var result = RenameExecutor.Undo(batch);
        if (result.Success)
        {
            _batches.RemoveAt(_batches.Count - 1);
            Save();
            Changed?.Invoke(this, EventArgs.Empty);
        }
        return result;
    }

    private void Load()
    {
        if (_filePath is null || !File.Exists(_filePath)) return;
        try
        {
            var loaded = JsonSerializer.Deserialize<List<RenameBatch>>(File.ReadAllText(_filePath), JsonOptions);
            if (loaded is not null) _batches.AddRange(loaded);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Historial dañado o inaccesible: se empieza de cero.
        }
    }

    private void Save()
    {
        if (_filePath is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(_batches, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Si no se puede guardar, el historial sigue funcionando en memoria.
        }
    }
}
