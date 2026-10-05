namespace GodotGame.Data.Sqlite;

/// <summary>Un mazo guardado, con su Id de base de datos (para poder editarlo o borrarlo despues).</summary>
public sealed record SavedDeck(int Id, string Name, List<int> CardIds);

/// <summary>
/// Lee y escribe mazos completos en las tablas <c>Decks</c>/<c>DeckCards</c> de
/// una base SQLite — el equivalente de <see cref="SqliteCardWriter"/> pero
/// para mazos, usado por el editor de mazos de <c>GodotGame.CardEditor</c>.
/// </summary>
public sealed class SqliteDeckWriter
{
    private readonly string _dbPath;

    public SqliteDeckWriter(string dbPath) => _dbPath = dbPath;

    public List<SavedDeck> LoadAllDecks()
    {
        var decks = new List<SavedDeck>();
        if (!File.Exists(_dbPath)) return decks;

        SqliteSchema.EnsureCreated(_dbPath);
        using var connection = SqliteSchema.OpenConnection(_dbPath);

        using var deckCommand = connection.CreateCommand();
        deckCommand.CommandText = "SELECT Id, Name FROM Decks ORDER BY Name";
        var rows = new List<(int Id, string Name)>();
        using (var reader = deckCommand.ExecuteReader())
            while (reader.Read())
                rows.Add((reader.GetInt32(0), reader.GetString(1)));

        foreach (var (id, name) in rows)
        {
            using var cardsCommand = connection.CreateCommand();
            cardsCommand.CommandText = "SELECT CardId FROM DeckCards WHERE DeckId = $DeckId ORDER BY Position";
            cardsCommand.Parameters.AddWithValue("$DeckId", id);

            var cardIds = new List<int>();
            using var cardsReader = cardsCommand.ExecuteReader();
            while (cardsReader.Read())
                cardIds.Add(cardsReader.GetInt32(0));

            decks.Add(new SavedDeck(id, name, cardIds));
        }

        return decks;
    }

    /// <summary>
    /// Guarda un mazo: si <paramref name="id"/> es null inserta uno nuevo, si
    /// no actualiza el nombre y reemplaza por completo la lista de cartas
    /// (se borran las anteriores e insertan las nuevas, no se intenta un
    /// diff). Devuelve el Id del mazo guardado.
    /// </summary>
    public int SaveDeck(int? id, string name, IReadOnlyList<int> cardIds)
    {
        SqliteSchema.EnsureCreated(_dbPath);
        using var connection = SqliteSchema.OpenConnection(_dbPath);

        int deckId;
        if (id.HasValue)
        {
            deckId = id.Value;
            using var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = "UPDATE Decks SET Name = $Name WHERE Id = $Id";
            updateCommand.Parameters.AddWithValue("$Name", name);
            updateCommand.Parameters.AddWithValue("$Id", deckId);
            updateCommand.ExecuteNonQuery();

            using var clearCommand = connection.CreateCommand();
            clearCommand.CommandText = "DELETE FROM DeckCards WHERE DeckId = $Id";
            clearCommand.Parameters.AddWithValue("$Id", deckId);
            clearCommand.ExecuteNonQuery();
        }
        else
        {
            using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = "INSERT INTO Decks (Name) VALUES ($Name); SELECT last_insert_rowid();";
            insertCommand.Parameters.AddWithValue("$Name", name);
            deckId = checked((int)(long)insertCommand.ExecuteScalar()!);
        }

        for (int position = 0; position < cardIds.Count; position++)
        {
            using var cardCommand = connection.CreateCommand();
            cardCommand.CommandText = "INSERT INTO DeckCards (DeckId, Position, CardId) VALUES ($DeckId, $Position, $CardId)";
            cardCommand.Parameters.AddWithValue("$DeckId", deckId);
            cardCommand.Parameters.AddWithValue("$Position", position);
            cardCommand.Parameters.AddWithValue("$CardId", cardIds[position]);
            cardCommand.ExecuteNonQuery();
        }

        return deckId;
    }

    /// <summary>Borra un mazo. Sus filas de <c>DeckCards</c> se van con el, por <c>ON DELETE CASCADE</c>.</summary>
    public void DeleteDeck(int id)
    {
        SqliteSchema.EnsureCreated(_dbPath);
        using var connection = SqliteSchema.OpenConnection(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Decks WHERE Id = $Id";
        command.Parameters.AddWithValue("$Id", id);
        command.ExecuteNonQuery();
    }
}
