using GodotGame.Data.Loaders;

namespace GodotGame.Tests.Data;

/// <summary>
/// Confirma que el catalogo inicial copiado desde monogame/ (Epica III del
/// plan de adaptacion a Godot: "el archivo monstersgame.db se copia como
/// dato, no como codigo") sigue siendo un catalogo real y cargable por la
/// capa de persistencia portada -- no solo bytes identicos, sino datos que
/// GodotGame.Data efectivamente puede leer.
/// </summary>
public class SeedCatalogTests
{
    private static string SeedDbPath =>
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "GodotGame", "Data", "monstersgame.db");

    [Fact]
    public void SeedDatabase_Exists()
    {
        Assert.True(File.Exists(SeedDbPath), $"No se encontro el catalogo semilla en {Path.GetFullPath(SeedDbPath)}");
    }

    [Fact]
    public void SeedDatabase_LoadsRealCards()
    {
        var cards = new SqliteCardLoader(SeedDbPath).LoadCards();

        Assert.NotEmpty(cards);
    }
}
