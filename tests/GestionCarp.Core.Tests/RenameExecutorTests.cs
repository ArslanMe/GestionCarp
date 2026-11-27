using GestionCarp.Core.Rename;

namespace GestionCarp.Core.Tests;

public class RenameExecutorTests
{
    [Fact]
    public void RenamesFilesAndFoldersAndUndoes()
    {
        using var t = new TempFolder();
        t.File("Season 1/ep1.mkv", "uno");
        t.File("Season 1/ep2.mkv", "dos");

        var ops = new[]
        {
            new RenameOperation(t.Combine("Season 1"), t.Combine("Temporada 1"), true),
            new RenameOperation(t.Combine("Season 1/ep1.mkv"), t.Combine("Season 1/Cap01.mkv"), false),
            new RenameOperation(t.Combine("Season 1/ep2.mkv"), t.Combine("Season 1/Cap02.mkv"), false),
        };

        var history = new UndoHistory(t.Combine("hist/historial.json"));
        var result = RenameExecutor.Execute(ops, "prueba");
        Assert.True(result.Success, result.Error);
        history.Push(result.Batch!);

        Assert.Equal(new[] { "Cap01.mkv", "Cap02.mkv" }, t.Names("Temporada 1"));
        Assert.Equal("uno", File.ReadAllText(t.Combine("Temporada 1/Cap01.mkv")));

        // Las rutas de la lista se pueden actualizar con las operaciones ejecutadas.
        Assert.Equal(t.Combine("Temporada 1/Cap02.mkv"), PathRebaser.Rebase(t.Combine("Season 1/ep2.mkv"), result.Executed));

        // El historial persiste en disco.
        var reloaded = new UndoHistory(t.Combine("hist/historial.json"));
        Assert.True(reloaded.CanUndo);

        var undo = reloaded.UndoLast();
        Assert.True(undo.Success, undo.Error);
        Assert.Equal(new[] { "ep1.mkv", "ep2.mkv" }, t.Names("Season 1"));
        Assert.False(reloaded.CanUndo);
        Assert.Equal(t.Combine("Season 1/ep2.mkv"), PathRebaser.Rebase(t.Combine("Temporada 1/Cap02.mkv"), undo.Executed));
    }

    [Fact]
    public void RollsBackWhenSomethingFails()
    {
        using var t = new TempFolder();
        t.File("a.txt");
        t.File("b.txt");
        t.File("ocupado.txt");

        var ops = new[]
        {
            new RenameOperation(t.Combine("a.txt"), t.Combine("nuevo.txt"), false),
            new RenameOperation(t.Combine("b.txt"), t.Combine("ocupado.txt"), false),
        };

        var result = RenameExecutor.Execute(ops, "falla");
        Assert.False(result.Success);
        Assert.Equal(new[] { "a.txt", "b.txt", "ocupado.txt" }, t.Names());
    }

    [Fact]
    public void CaseOnlyRenameWorks()
    {
        using var t = new TempFolder();
        t.File("serie.mkv");
        var result = RenameExecutor.Execute(new[] { new RenameOperation(t.Combine("serie.mkv"), t.Combine("Serie.mkv"), false) }, "mayúsculas");
        Assert.True(result.Success, result.Error);
        Assert.Equal(new[] { "Serie.mkv" }, t.Names());
    }
}
