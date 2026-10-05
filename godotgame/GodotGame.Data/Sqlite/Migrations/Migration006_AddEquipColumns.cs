using Microsoft.Data.Sqlite;

namespace GodotGame.Data.Sqlite.Migrations;

/// <summary>
/// Agrega a <c>Cards</c> las columnas que necesita una Magia de Equipo:
/// objetivos permitidos (reutiliza <c>TargetFilters</c>), modificadores de
/// ATK/DEF (con signo) y duracion. Puramente aditiva: toda columna nueva
/// tiene un default neutro (0 / 'WhileEquipped'), asi que ninguna carta
/// existente (que no sea Equip) se ve afectada.
/// </summary>
internal sealed class Migration006_AddEquipColumns : ISchemaMigration
{
    public int TargetVersion => 6;

    public void Apply(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            ALTER TABLE Cards ADD COLUMN EquipTargetFilterId INTEGER NULL REFERENCES TargetFilters(Id);
            ALTER TABLE Cards ADD COLUMN EquipAttackModifier INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE Cards ADD COLUMN EquipDefenseModifier INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE Cards ADD COLUMN EquipDuration TEXT NOT NULL DEFAULT 'WhileEquipped';
            ALTER TABLE Cards ADD COLUMN EquipDurationTurns INTEGER NOT NULL DEFAULT 0;
            """;
        command.ExecuteNonQuery();
    }
}
