using Microsoft.Data.Sqlite;

namespace GodotGame.Data.Sqlite.Migrations;

/// <summary>
/// Backfill de 6 <c>FieldTypes</c> de ejemplo, uno por Atributo de Monstruo
/// salvo Divino (que tampoco tiene terreno propio en el genero de
/// referencia): demuestra el modificador opuesto de <see cref="Migration008_AddFieldTypeOpposedModifier"/>
/// de punta a punta -- cada uno da +500 ATK a su Atributo y -500 ATK al
/// opuesto (Fuego/Agua, Viento/Tierra, Luz/Oscuridad) -- sin obligar a
/// autoria manual desde cero. Son datos de ejemplo completamente editables o
/// borrables desde <c>FieldTypeEditorForm</c> despues; no los usa ninguna
/// carta del catalogo por defecto. Se insertan solo si el Id todavia no
/// existe, para no duplicar si esta migracion corriera dos veces sobre la
/// misma base.
/// </summary>
internal sealed class Migration009_SeedElementalFieldTypes : ISchemaMigration
{
    public int TargetVersion => 9;

    private static readonly (string Id, string Name, string Color, string Attribute, string Opposed)[] Terrains =
    {
        ("terrain_fire", "Terreno de Fuego", "#B4321E", "Fire", "Water"),
        ("terrain_water", "Terreno de Agua", "#1E5A82", "Water", "Fire"),
        ("terrain_wind", "Terreno de Viento", "#2E6E50", "Wind", "Earth"),
        ("terrain_earth", "Terreno de Tierra", "#6E5A32", "Earth", "Wind"),
        ("terrain_light", "Terreno de Luz", "#A08C3C", "Light", "Dark"),
        ("terrain_dark", "Terreno de Oscuridad", "#46325A", "Dark", "Light"),
    };

    public void Apply(SqliteConnection connection)
    {
        foreach (var terrain in Terrains)
        {
            if (AlreadyExists(connection, terrain.Id)) continue;

            int affectedFilterId = InsertAttributeFilter(connection, terrain.Attribute);
            int opposedFilterId = InsertAttributeFilter(connection, terrain.Opposed);

            using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO FieldTypes (Id, Name, Description, Color, BackgroundImage, VisualEffectsKey,
                                         AffectedFilterId, StatModifierAmount, StatModifierStat,
                                         OpposedFilterId, OpposedStatModifierAmount)
                VALUES ($Id, $Name, $Description, $Color, '', '',
                        $AffectedFilterId, 500, 'Attack',
                        $OpposedFilterId, -500)
                """;
            insert.Parameters.AddWithValue("$Id", terrain.Id);
            insert.Parameters.AddWithValue("$Name", terrain.Name);
            insert.Parameters.AddWithValue("$Description", $"+500 ATK a Monstruos de Atributo {terrain.Attribute}, -500 ATK a los de Atributo {terrain.Opposed}.");
            insert.Parameters.AddWithValue("$Color", terrain.Color);
            insert.Parameters.AddWithValue("$AffectedFilterId", affectedFilterId);
            insert.Parameters.AddWithValue("$OpposedFilterId", opposedFilterId);
            insert.ExecuteNonQuery();
        }
    }

    private static bool AlreadyExists(SqliteConnection connection, string id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM FieldTypes WHERE Id = $Id";
        command.Parameters.AddWithValue("$Id", id);
        return (long)command.ExecuteScalar()! > 0;
    }

    /// <summary>Crea un <c>TargetFilter</c> de un solo grupo/condicion (<c>Kind = Attribute</c>) y devuelve su Id.</summary>
    private static int InsertAttributeFilter(SqliteConnection connection, string attribute)
    {
        int filterId;
        using (var insertFilter = connection.CreateCommand())
        {
            insertFilter.CommandText = "INSERT INTO TargetFilters (Name) VALUES (''); SELECT last_insert_rowid();";
            filterId = checked((int)(long)insertFilter.ExecuteScalar()!);
        }
        using (var insertCondition = connection.CreateCommand())
        {
            insertCondition.CommandText = "INSERT INTO TargetFilterConditions (FilterId, GroupIndex, Kind, Negate, Value) VALUES ($FilterId, 0, 'Attribute', 0, $Value)";
            insertCondition.Parameters.AddWithValue("$FilterId", filterId);
            insertCondition.Parameters.AddWithValue("$Value", attribute);
            insertCondition.ExecuteNonQuery();
        }
        return filterId;
    }
}
