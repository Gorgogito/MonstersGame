using Microsoft.Data.Sqlite;

namespace MonstersGame.Data.Sqlite;

/// <summary>
/// Crea (si no existen) las tablas del catalogo en la base SQLite. Idempotente:
/// se puede llamar en cada arranque sin efecto si ya existen.
/// </summary>
public static class SqliteSchema
{
    /// <summary>
    /// Tipos de Monstruo clasicos con los que se siembra la tabla <c>Types</c>
    /// la primera vez que se crea (incluye "DivineBeast", que la lista original
    /// del proyecto no tenia). Solo se usan para esa siembra inicial: una vez
    /// creada la tabla, el catalogo de Tipos lo administra el editor de cartas
    /// (ver <see cref="SqliteTypeWriter"/>), no el codigo.
    /// </summary>
    private static readonly string[] DefaultTypes =
    {
        "Dragon", "Spellcaster", "Warrior", "BeastWarrior", "Beast", "WingedBeast",
        "Fiend", "Zombie", "Machine", "Aqua", "Pyro", "Rock", "Plant", "Insect",
        "Thunder", "Fish", "SeaSerpent", "Reptile", "Dinosaur", "Fairy", "DivineBeast"
    };

    public static void EnsureCreated(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var connection = OpenConnection(dbPath);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS Cards (
                    Id INTEGER PRIMARY KEY,
                    Kind TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Attack INTEGER NOT NULL,
                    Defense INTEGER NOT NULL,
                    Level INTEGER NOT NULL,
                    Type TEXT NOT NULL,
                    Attribute TEXT NOT NULL,
                    Image TEXT NOT NULL,
                    Description TEXT NOT NULL,
                    EffectId TEXT NOT NULL,
                    SubType TEXT NOT NULL,
                    Category TEXT NOT NULL,
                    RitualMonsterId INTEGER NOT NULL,
                    RequiredRitualLevel INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Decks (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS DeckCards (
                    DeckId INTEGER NOT NULL REFERENCES Decks(Id) ON DELETE CASCADE,
                    Position INTEGER NOT NULL,
                    CardId INTEGER NOT NULL,
                    PRIMARY KEY (DeckId, Position)
                );

                CREATE TABLE IF NOT EXISTS Fusions (
                    MaterialA INTEGER NOT NULL,
                    MaterialB INTEGER NOT NULL,
                    Result INTEGER NOT NULL,
                    PRIMARY KEY (MaterialA, MaterialB)
                );

                CREATE TABLE IF NOT EXISTS Types (
                    Name TEXT PRIMARY KEY
                );
                """;
            command.ExecuteNonQuery();
        }

        SeedDefaultTypesIfEmpty(connection);
    }

    /// <summary>
    /// Siembra los Tipos clasicos solo si la tabla esta vacia — no en cada
    /// arranque, para no resucitar un Tipo que el usuario borro a proposito
    /// desde el editor. Si el usuario llega a borrarlos todos, se resiembran
    /// (mejor un catalogo de Tipos por defecto que uno vacio que impide crear
    /// cartas de Monstruo).
    /// </summary>
    private static void SeedDefaultTypesIfEmpty(SqliteConnection connection)
    {
        using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = "SELECT COUNT(*) FROM Types";
            long count = (long)countCommand.ExecuteScalar()!;
            if (count > 0) return;
        }

        foreach (var name in DefaultTypes)
        {
            using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = "INSERT OR IGNORE INTO Types (Name) VALUES ($Name)";
            insertCommand.Parameters.AddWithValue("$Name", name);
            insertCommand.ExecuteNonQuery();
        }
    }

    public static SqliteConnection OpenConnection(string dbPath)
    {
        var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        // SQLite no aplica las claves foraneas (ni el ON DELETE CASCADE de
        // DeckCards) salvo que se pida explicitamente por conexion.
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();

        return connection;
    }
}
