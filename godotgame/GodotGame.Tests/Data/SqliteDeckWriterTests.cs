using GodotGame.Data.Sqlite;
using GodotGame.Tests.Data.TestSupport;

namespace GodotGame.Tests.Data;

public class SqliteDeckWriterTests
{
    [Fact]
    public void SaveDeck_NewDeck_InsertsNameAndCards_InPositionOrder()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteDeckWriter(dir.DbPath);

        int id = writer.SaveDeck(null, "Mazo de Prueba", new[] { 3, 1, 1 });

        var decks = writer.LoadAllDecks();
        var deck = Assert.Single(decks);
        Assert.Equal(id, deck.Id);
        Assert.Equal("Mazo de Prueba", deck.Name);
        Assert.Equal(new[] { 3, 1, 1 }, deck.CardIds);
    }

    [Fact]
    public void SaveDeck_ExistingId_RenamesAndReplacesCards_WithoutDuplicatingTheDeck()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteDeckWriter(dir.DbPath);
        int id = writer.SaveDeck(null, "Nombre Original", new[] { 1, 2 });

        writer.SaveDeck(id, "Nombre Editado", new[] { 5, 5, 6 });

        var deck = Assert.Single(writer.LoadAllDecks());
        Assert.Equal(id, deck.Id);
        Assert.Equal("Nombre Editado", deck.Name);
        Assert.Equal(new[] { 5, 5, 6 }, deck.CardIds);
    }

    [Fact]
    public void SaveDeck_EmptyCardList_SavesADeckWithNoCards()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteDeckWriter(dir.DbPath);

        writer.SaveDeck(null, "Vacio", Array.Empty<int>());

        var deck = Assert.Single(writer.LoadAllDecks());
        Assert.Empty(deck.CardIds);
    }

    [Fact]
    public void DeleteDeck_RemovesTheDeckAndItsCards()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteDeckWriter(dir.DbPath);
        int id = writer.SaveDeck(null, "Para Borrar", new[] { 1, 2, 3 });

        writer.DeleteDeck(id);

        Assert.Empty(writer.LoadAllDecks());
    }

    [Fact]
    public void DeleteDeck_NonExistentId_DoesNothing()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteDeckWriter(dir.DbPath);
        writer.SaveDeck(null, "Sobrevive", new[] { 1 });

        writer.DeleteDeck(999);

        Assert.Single(writer.LoadAllDecks());
    }

    [Fact]
    public void LoadAllDecks_MultipleDecks_AreOrderedByName()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteDeckWriter(dir.DbPath);
        writer.SaveDeck(null, "Zeta", new[] { 1 });
        writer.SaveDeck(null, "Alfa", new[] { 2 });

        var decks = writer.LoadAllDecks();

        Assert.Equal(new[] { "Alfa", "Zeta" }, decks.Select(d => d.Name));
    }

    [Fact]
    public void LoadAllDecks_MissingDatabase_ReturnsEmpty()
    {
        string missing = Path.Combine(Path.GetTempPath(), "GodotGame_NoExiste_" + Guid.NewGuid() + ".db");

        var decks = new SqliteDeckWriter(missing).LoadAllDecks();

        Assert.Empty(decks);
    }
}
