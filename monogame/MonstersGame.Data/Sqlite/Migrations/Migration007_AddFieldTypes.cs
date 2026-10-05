using Microsoft.Data.Sqlite;

namespace MonstersGame.Data.Sqlite.Migrations;

/// <summary>
/// Crea el catalogo <c>FieldTypes</c> -- extensible por datos, mismo patron
/// que la tabla <c>Types</c> ya usa para el Tipo de Monstruo -- y agrega
/// <c>Cards.FieldTypeId</c> para que una Magia de Campo referencie uno.
/// Puramente aditiva.
/// </summary>
internal sealed class Migration007_AddFieldTypes : ISchemaMigration
{
    public int TargetVersion => 7;

    public void Apply(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE FieldTypes (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Description TEXT NOT NULL DEFAULT '',
                Color TEXT NOT NULL DEFAULT '#FFFFFF',
                BackgroundImage TEXT NOT NULL DEFAULT '',
                VisualEffectsKey TEXT NOT NULL DEFAULT '',
                AffectedFilterId INTEGER NULL REFERENCES TargetFilters(Id),
                StatModifierAmount INTEGER NOT NULL DEFAULT 0,
                StatModifierStat TEXT NOT NULL DEFAULT 'None'
            );

            ALTER TABLE Cards ADD COLUMN FieldTypeId TEXT NULL REFERENCES FieldTypes(Id);
            """;
        command.ExecuteNonQuery();
    }
}
