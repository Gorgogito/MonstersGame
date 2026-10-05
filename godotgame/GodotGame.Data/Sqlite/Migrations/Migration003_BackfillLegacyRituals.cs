using Microsoft.Data.Sqlite;
using GodotGame.Data.Sqlite;

namespace GodotGame.Data.Sqlite.Migrations;

/// <summary>
/// Traduce cada Magia de Ritual existente (<c>RequiredRitualLevel &gt; 0</c>) a
/// un <c>RequirementSet</c> en modo <c>LevelSum</c> con un filtro vacio
/// (equivalente a "cualquier monstruo"), y enlaza <c>Cards.RequirementSetId</c>.
///
/// Ninguna carta se pierde ni cambia de comportamiento: el umbral de Nivel
/// migrado es exactamente el mismo que ya tenia. Las columnas legacy
/// (<c>RitualMonsterId</c>, <c>RequiredRitualLevel</c>) se conservan sin tocar.
/// </summary>
internal sealed class Migration003_BackfillLegacyRituals : ISchemaMigration
{
    public int TargetVersion => 3;

    public void Apply(SqliteConnection connection)
    {
        var pending = new List<(int CardId, int RequiredLevel)>();
        using (var select = connection.CreateCommand())
        {
            select.CommandText = """
                SELECT Id, RequiredRitualLevel FROM Cards
                WHERE SubType = 'Ritual' AND RequiredRitualLevel > 0 AND RequirementSetId IS NULL
                """;
            using var reader = select.ExecuteReader();
            while (reader.Read())
                pending.Add((reader.GetInt32(0), reader.GetInt32(1)));
        }

        foreach (var (cardId, requiredLevel) in pending)
        {
            int filterId = InsertEmptyFilter(connection);
            int requirementSetId = InsertLevelSumRequirementSet(connection, filterId, requiredLevel);

            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE Cards SET RequirementSetId = $RequirementSetId WHERE Id = $CardId";
            update.Parameters.AddWithValue("$RequirementSetId", requirementSetId);
            update.Parameters.AddWithValue("$CardId", cardId);
            update.ExecuteNonQuery();
        }
    }

    private static int InsertEmptyFilter(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO TargetFilters (Name) VALUES (''); SELECT last_insert_rowid();";
        return Convert.ToInt32((long)command.ExecuteScalar()!);
    }

    private static int InsertLevelSumRequirementSet(SqliteConnection connection, int filterId, int minLevelSum)
    {
        int requirementSetId;
        using (var insertSet = connection.CreateCommand())
        {
            insertSet.CommandText = "INSERT INTO RequirementSets (Mode) VALUES ('LevelSum'); SELECT last_insert_rowid();";
            requirementSetId = Convert.ToInt32((long)insertSet.ExecuteScalar()!);
        }

        using (var insertLevelSum = connection.CreateCommand())
        {
            insertLevelSum.CommandText = "INSERT INTO LevelSumRequirements (RequirementSetId, FilterId, MinLevelSum) VALUES ($RequirementSetId, $FilterId, $MinLevelSum)";
            insertLevelSum.Parameters.AddWithValue("$RequirementSetId", requirementSetId);
            insertLevelSum.Parameters.AddWithValue("$FilterId", filterId);
            insertLevelSum.Parameters.AddWithValue("$MinLevelSum", minLevelSum);
            insertLevelSum.ExecuteNonQuery();
        }

        return requirementSetId;
    }
}
