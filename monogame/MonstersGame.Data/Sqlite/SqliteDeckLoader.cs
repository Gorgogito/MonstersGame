using MonstersGame.Data.Sqlite;

namespace MonstersGame.Data.Loaders;

/// <summary>Carga los mazos definidos en las tablas <c>Decks</c>/<c>DeckCards</c> de una base SQLite.</summary>
public sealed class SqliteDeckLoader : IDeckLoader
{
    private readonly string _dbPath;

    public SqliteDeckLoader(string dbPath) => _dbPath = dbPath;

    public IReadOnlyList<DeckDefinition> LoadDecks()
    {
        var decks = new List<DeckDefinition>();
        if (!File.Exists(_dbPath)) return decks;

        SqliteSchema.EnsureCreated(_dbPath);
        using var connection = SqliteSchema.OpenConnection(_dbPath);

        using var deckCommand = connection.CreateCommand();
        deckCommand.CommandText = "SELECT Id, Name FROM Decks ORDER BY Name";
        using var deckReader = deckCommand.ExecuteReader();

        var deckRows = new List<(int Id, string Name)>();
        while (deckReader.Read())
            deckRows.Add((deckReader.GetInt32(0), deckReader.GetString(1)));
        deckReader.Close();

        foreach (var (id, name) in deckRows)
        {
            using var cardsCommand = connection.CreateCommand();
            cardsCommand.CommandText = "SELECT CardId FROM DeckCards WHERE DeckId = $DeckId ORDER BY Position";
            cardsCommand.Parameters.AddWithValue("$DeckId", id);

            var cardIds = new List<int>();
            using var cardsReader = cardsCommand.ExecuteReader();
            while (cardsReader.Read())
                cardIds.Add(cardsReader.GetInt32(0));

            decks.Add(new DeckDefinition(name, cardIds));
        }

        return decks;
    }
}
