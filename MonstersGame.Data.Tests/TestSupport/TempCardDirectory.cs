namespace MonstersGame.Data.Tests.TestSupport;

/// <summary>Carpeta temporal aislada por prueba, borrada automaticamente al terminar.</summary>
internal sealed class TempCardDirectory : IDisposable
{
    public string Path { get; }

    /// <summary>Ruta de una base SQLite dentro de la carpeta (no existe hasta que algo la crea).</summary>
    public string DbPath => System.IO.Path.Combine(Path, "test.db");

    public TempCardDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MonstersGameDataTests_" + Guid.NewGuid());
        Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch { /* mejor esfuerzo */ }
    }
}
