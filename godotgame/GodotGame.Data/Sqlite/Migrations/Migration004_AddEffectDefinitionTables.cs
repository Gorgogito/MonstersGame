using Microsoft.Data.Sqlite;

namespace GodotGame.Data.Sqlite.Migrations;

/// <summary>
/// Crea las tablas del sistema de efectos data-driven: <c>EffectDefinitions</c>
/// (Trigger + metadatos), <c>EffectConditions</c>, <c>EffectTargetSpecs</c>
/// (reutiliza <c>TargetFilters</c>, ver <see cref="Migration002_AddFilterAndRequirementTables"/>)
/// y <c>EffectActionSteps</c>. Puramente aditiva.
/// </summary>
internal sealed class Migration004_AddEffectDefinitionTables : ISchemaMigration
{
    public int TargetVersion => 4;

    public void Apply(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE EffectDefinitions (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Trigger TEXT NOT NULL,
                VisualProfileKey TEXT NOT NULL DEFAULT '',
                IsActive INTEGER NOT NULL DEFAULT 1
            );

            CREATE TABLE EffectConditions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EffectDefinitionId TEXT NOT NULL REFERENCES EffectDefinitions(Id) ON DELETE CASCADE,
                ConditionKind TEXT NOT NULL,
                ParamsJson TEXT NOT NULL DEFAULT '{}'
            );

            CREATE TABLE EffectTargetSpecs (
                EffectDefinitionId TEXT PRIMARY KEY REFERENCES EffectDefinitions(Id) ON DELETE CASCADE,
                TargetKind TEXT NOT NULL,
                FilterId INTEGER NULL REFERENCES TargetFilters(Id),
                Required INTEGER NOT NULL DEFAULT 1
            );

            CREATE TABLE EffectActionSteps (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EffectDefinitionId TEXT NOT NULL REFERENCES EffectDefinitions(Id) ON DELETE CASCADE,
                StepOrder INTEGER NOT NULL,
                ActionKind TEXT NOT NULL,
                ParamsJson TEXT NOT NULL DEFAULT '{}'
            );
            """;
        command.ExecuteNonQuery();
    }
}
