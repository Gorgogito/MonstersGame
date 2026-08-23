using System.Text.Json;
using MonstersGame.Data.Loaders;

namespace MonstersGame.Data.Sqlite;

/// <summary>
/// Migra el catalogo de cartas, mazos y fusiones desde el formato JSON
/// anterior (un archivo por carta en <c>Cards/</c>, un archivo por mazo en
/// <c>Decks/</c>, y <c>Fusions/fusions.json</c>) a una base SQLite unica.
/// Reutilizable: sirve tanto para la migracion inicial del Bloque 9 como para
/// re-sembrar una base nueva a partir de un volcado JSON existente.
/// </summary>
public static class JsonToSqliteMigrator
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <param name="jsonDataRoot">Carpeta que contiene Cards/, Decks/ y Fusions/fusions.json.</param>
    /// <param name="dbPath">Ruta del archivo .db a crear (se sobrescribe si ya existe).</param>
    public static void Migrate(string jsonDataRoot, string dbPath)
    {
        if (File.Exists(dbPath))
        {
            // Microsoft.Data.Sqlite agrupa conexiones (connection pooling): el
            // archivo puede seguir bloqueado por el pool aunque ya se haya
            // llamado Dispose() en toda conexion previa. Hay que vaciar el
            // pool antes de poder borrar el archivo.
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }
        SqliteSchema.EnsureCreated(dbPath);

        using var connection = SqliteSchema.OpenConnection(dbPath);

        MigrateCards(jsonDataRoot, connection);
        MigrateDecks(jsonDataRoot, connection);
        MigrateFusions(jsonDataRoot, connection);
    }

    private static void MigrateCards(string jsonDataRoot, Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        var writer = new JsonCardWriter(Path.Combine(jsonDataRoot, "Cards"));
        foreach (var dto in writer.LoadAllDtos())
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Cards (Id, Kind, Name, Attack, Defense, Level, Type, Attribute, Image, Description, EffectId, SubType, Category, RitualMonsterId, RequiredRitualLevel)
                VALUES ($Id, $Kind, $Name, $Attack, $Defense, $Level, $Type, $Attribute, $Image, $Description, $EffectId, $SubType, $Category, $RitualMonsterId, $RequiredRitualLevel)
                """;
            SqliteCardMapping.BindParameters(command, dto);
            command.ExecuteNonQuery();
        }
    }

    private static void MigrateDecks(string jsonDataRoot, Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        string decksDir = Path.Combine(jsonDataRoot, "Decks");
        if (!Directory.Exists(decksDir)) return;

        foreach (var file in Directory.GetFiles(decksDir, "*.json").OrderBy(f => f))
        {
            var dto = JsonSerializer.Deserialize<DeckDto>(File.ReadAllText(file), Options);
            if (dto == null) continue;

            using var deckCommand = connection.CreateCommand();
            deckCommand.CommandText = "INSERT INTO Decks (Name) VALUES ($Name); SELECT last_insert_rowid();";
            deckCommand.Parameters.AddWithValue("$Name", dto.Name);
            long deckId = (long)deckCommand.ExecuteScalar()!;

            for (int position = 0; position < dto.Cards.Count; position++)
            {
                using var cardCommand = connection.CreateCommand();
                cardCommand.CommandText = "INSERT INTO DeckCards (DeckId, Position, CardId) VALUES ($DeckId, $Position, $CardId)";
                cardCommand.Parameters.AddWithValue("$DeckId", deckId);
                cardCommand.Parameters.AddWithValue("$Position", position);
                cardCommand.Parameters.AddWithValue("$CardId", dto.Cards[position]);
                cardCommand.ExecuteNonQuery();
            }
        }
    }

    private static void MigrateFusions(string jsonDataRoot, Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        string path = Path.Combine(jsonDataRoot, "Fusions", "fusions.json");
        if (!File.Exists(path)) return;

        var dtos = JsonSerializer.Deserialize<List<FusionDto>>(File.ReadAllText(path), Options) ?? new List<FusionDto>();
        foreach (var dto in dtos)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Fusions (MaterialA, MaterialB, Result) VALUES ($A, $B, $Result)";
            command.Parameters.AddWithValue("$A", dto.MaterialA);
            command.Parameters.AddWithValue("$B", dto.MaterialB);
            command.Parameters.AddWithValue("$Result", dto.Result);
            command.ExecuteNonQuery();
        }
    }
}
