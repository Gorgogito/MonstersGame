namespace GodotGame.Data.Sqlite;

/// <summary>
/// Lee y escribe el catalogo de Tipos de Monstruo (tabla <c>Types</c>) —
/// texto libre administrado por el usuario desde el editor de cartas, no un
/// enum del codigo (ver <see cref="GodotGame.Core.Entities.MonsterCard.Type"/>).
/// </summary>
public sealed class SqliteTypeWriter
{
    private readonly string _dbPath;

    public SqliteTypeWriter(string dbPath) => _dbPath = dbPath;

    public List<string> LoadAll()
    {
        // A diferencia de Cards/Decks (donde "no existe la base" significa
        // legitimamente "catalogo vacio"), Types siempre debe devolver al
        // menos los Tipos clasicos sembrados por SqliteSchema — si no,
        // ni siquiera se podria crear la primera carta de un catalogo nuevo.
        SqliteSchema.EnsureCreated(_dbPath);
        using var connection = SqliteSchema.OpenConnection(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM Types ORDER BY Name";

        var names = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            names.Add(reader.GetString(0));
        return names;
    }

    /// <summary>Agrega un Tipo nuevo. Sin efecto si ya existe (mismo nombre exacto).</summary>
    public void Add(string name)
    {
        SqliteSchema.EnsureCreated(_dbPath);
        using var connection = SqliteSchema.OpenConnection(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO Types (Name) VALUES ($Name)";
        command.Parameters.AddWithValue("$Name", name);
        command.ExecuteNonQuery();
    }

    /// <summary>Cuantas cartas del catalogo tienen este Tipo ahora mismo (para no dejar borrar/renombrar sin avisar).</summary>
    public int CountCardsUsing(string name)
    {
        SqliteSchema.EnsureCreated(_dbPath);
        using var connection = SqliteSchema.OpenConnection(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Cards WHERE Type = $Name";
        command.Parameters.AddWithValue("$Name", name);
        return checked((int)(long)command.ExecuteScalar()!);
    }

    public void Delete(string name)
    {
        SqliteSchema.EnsureCreated(_dbPath);
        using var connection = SqliteSchema.OpenConnection(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Types WHERE Name = $Name";
        command.Parameters.AddWithValue("$Name", name);
        command.ExecuteNonQuery();
    }

    /// <summary>Renombra el Tipo y actualiza cualquier carta que lo tuviera asignado, para no dejar cartas con un Tipo huerfano.</summary>
    public void Rename(string oldName, string newName)
    {
        SqliteSchema.EnsureCreated(_dbPath);
        using var connection = SqliteSchema.OpenConnection(_dbPath);

        using (var renameCommand = connection.CreateCommand())
        {
            renameCommand.CommandText = "UPDATE Types SET Name = $New WHERE Name = $Old";
            renameCommand.Parameters.AddWithValue("$New", newName);
            renameCommand.Parameters.AddWithValue("$Old", oldName);
            renameCommand.ExecuteNonQuery();
        }

        using (var updateCardsCommand = connection.CreateCommand())
        {
            updateCardsCommand.CommandText = "UPDATE Cards SET Type = $New WHERE Type = $Old";
            updateCardsCommand.Parameters.AddWithValue("$New", newName);
            updateCardsCommand.Parameters.AddWithValue("$Old", oldName);
            updateCardsCommand.ExecuteNonQuery();
        }
    }
}
