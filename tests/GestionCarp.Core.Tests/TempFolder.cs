namespace GestionCarp.Core.Tests;

/// <summary>Carpeta temporal que se borra al terminar la prueba.</summary>
public sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gc_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string relative, string content = "x")
    {
        string full = Combine(relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        System.IO.File.WriteAllText(full, content);
        return full;
    }

    public string Dir(string relative)
    {
        string full = Combine(relative);
        Directory.CreateDirectory(full);
        return full;
    }

    public string Combine(string relative)
        => System.IO.Path.Combine(Path, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public string[] Names(string relative = "")
        => Directory.EnumerateFileSystemEntries(Combine(relative)).Select(System.IO.Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray()!;

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch { }
    }
}
