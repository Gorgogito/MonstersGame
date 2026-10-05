using Microsoft.Data.Sqlite;
using GodotGame.Data.Loaders;
using GodotGame.Data.Sqlite;
using GodotGame.Tests.Data.TestSupport;

namespace GodotGame.Tests.Data;

public class SqliteDeckLoaderTests
{
    private static void InsertDeck(string dbPath, string name, params int[] cardIds)
    {
        SqliteSchema.EnsureCreated(dbPath);
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using var deckCommand = connection.CreateCommand();
        deckCommand.CommandText = "INSERT INTO Decks (Name) VALUES ($Name); SELECT last_insert_rowid();";
        deckCommand.Parameters.AddWithValue("$Name", name);
        long deckId = (long)deckCommand.ExecuteScalar()!;

        for (int i = 0; i < cardIds.Length; i++)
        {
            using var cardCommand = connection.CreateCommand();
            cardCommand.CommandText = "INSERT INTO DeckCards (DeckId, Position, CardId) VALUES ($DeckId, $Position, $CardId)";
            cardCommand.Parameters.AddWithValue("$DeckId", deckId);
            cardCommand.Parameters.AddWithValue("$Position", i);
            cardCommand.Parameters.AddWithValue("$CardId", cardIds[i]);
            cardCommand.ExecuteNonQuery();
        }
    }

    [Fact]
    public void LoadDecks_ReadsNameAndCardIds_InPositionOrder()
    {
        using var dir = new TempCardDirectory();
        InsertDeck(dir.DbPath, "Furia de Dragones", 1, 1, 4, 6, 6, 6);

        var decks = new SqliteDeckLoader(dir.DbPath).LoadDecks();

        var deck = Assert.Single(decks);
        Assert.Equal("Furia de Dragones", deck.Name);
        Assert.Equal(new[] { 1, 1, 4, 6, 6, 6 }, deck.CardIds);
    }

    [Fact]
    public void LoadDecks_MultipleDecks_AreOrderedByName()
    {
        using var dir = new TempCardDirectory();
        InsertDeck(dir.DbPath, "Zeta", 1);
        InsertDeck(dir.DbPath, "Alfa", 2);

        var decks = new SqliteDeckLoader(dir.DbPath).LoadDecks();

        Assert.Equal(new[] { "Alfa", "Zeta" }, decks.Select(d => d.Name));
    }

    [Fact]
    public void LoadDecks_MissingDatabase_ReturnsEmpty()
    {
        string missing = Path.Combine(Path.GetTempPath(), "GodotGame_NoExiste_" + Guid.NewGuid() + ".db");

        var decks = new SqliteDeckLoader(missing).LoadDecks();

        Assert.Empty(decks);
    }
}
