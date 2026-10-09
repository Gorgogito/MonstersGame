using Microsoft.Data.Sqlite;

namespace GodotGame.Data.Sqlite.Migrations;

/// <summary>
/// Crea <c>MonsterEffects</c>: los efectos de Monstruo compuestos por datos
/// (Continuo, de Encendido, Disparado, Rapido, de Volteo, No clasificado), uno
/// por fila y en orden, cada uno serializado como JSON (ver
/// <see cref="SqliteMonsterEffectStore"/>). Se guardan como JSON en vez de en
/// tablas normalizadas porque son arboles (costos, objetivos, pasos con sus
/// condiciones y parametros) que el editor siempre lee y escribe completos.
/// Puramente aditiva: las cartas existentes quedan sin efectos.
/// </summary>
internal sealed class Migration011_AddMonsterEffects : ISchemaMigration
{
    public int TargetVersion => 11;

    public void Apply(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE MonsterEffects (
                CardId INTEGER NOT NULL REFERENCES Cards(Id) ON DELETE CASCADE,
                Ordinal INTEGER NOT NULL,
                Json TEXT NOT NULL,
                PRIMARY KEY (CardId, Ordinal)
            );
            """;
        command.ExecuteNonQuery();
    }
}
