using GodotGame.Core.Entities;
using GodotGame.Core.Rules;
using Microsoft.Data.Sqlite;

namespace GodotGame.Data.Sqlite.Migrations;

/// <summary>
/// Agrega a <c>Cards</c> las dos Estrellas Guardianas de cada Monstruo (ver
/// <see cref="GuardianStars"/>) y las rellena en los Monstruos existentes con
/// el par por defecto de su Atributo, para que el editor las muestre ya
/// cargadas y se puedan ajustar carta por carta. Cartas que no son Monstruo
/// quedan con texto vacio (no aplica).
/// </summary>
internal sealed class Migration010_AddGuardianStars : ISchemaMigration
{
    public int TargetVersion => 10;

    public void Apply(SqliteConnection connection)
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                ALTER TABLE Cards ADD COLUMN GuardianStar1 TEXT NOT NULL DEFAULT '';
                ALTER TABLE Cards ADD COLUMN GuardianStar2 TEXT NOT NULL DEFAULT '';
                """;
            command.ExecuteNonQuery();
        }

        foreach (var attribute in Enum.GetValues<MonsterAttribute>())
        {
            var (first, second) = GuardianStars.DefaultsFor(attribute);
            using var update = connection.CreateCommand();
            update.CommandText = """
                UPDATE Cards SET GuardianStar1 = $First, GuardianStar2 = $Second
                WHERE Kind = 'Monster' AND Attribute = $Attribute COLLATE NOCASE AND GuardianStar1 = ''
                """;
            update.Parameters.AddWithValue("$First", first.ToString());
            update.Parameters.AddWithValue("$Second", second.ToString());
            update.Parameters.AddWithValue("$Attribute", attribute.ToString());
            update.ExecuteNonQuery();
        }
    }
}
