using Microsoft.Data.Sqlite;

namespace MonstersGame.Data.Sqlite.Migrations;

/// <summary>
/// Agrega a <c>FieldTypes</c> un segundo modificador INDEPENDIENTE del
/// principal (<c>OpposedFilterId</c>/<c>OpposedStatModifierAmount</c>), para
/// terreno elemental "de verdad" (adaptacion de esfuerzo medio inspirada en
/// Forbidden Memories): un Campo puede dar un bonus a quien cumple
/// <c>AffectedFilterId</c> Y una penalizacion independiente a quien cumple
/// <c>OpposedFilterId</c> (tipicamente el Atributo opuesto), en vez de un
/// unico filtro/monto. Puramente aditiva -- ver <see cref="Migration007_AddFieldTypes"/>
/// para el resto de columnas; ningun <c>FieldType</c> existente cambia de
/// comportamiento (<c>OpposedFilterId</c> nulo = sin penalizacion, igual que
/// antes de esta migracion).
/// </summary>
internal sealed class Migration008_AddFieldTypeOpposedModifier : ISchemaMigration
{
    public int TargetVersion => 8;

    public void Apply(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            ALTER TABLE FieldTypes ADD COLUMN OpposedFilterId INTEGER NULL REFERENCES TargetFilters(Id);
            ALTER TABLE FieldTypes ADD COLUMN OpposedStatModifierAmount INTEGER NOT NULL DEFAULT 0;
            """;
        command.ExecuteNonQuery();
    }
}
