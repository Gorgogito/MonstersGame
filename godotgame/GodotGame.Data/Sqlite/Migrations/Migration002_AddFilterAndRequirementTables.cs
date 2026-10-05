using Microsoft.Data.Sqlite;
using GodotGame.Data.Sqlite;

namespace GodotGame.Data.Sqlite.Migrations;

/// <summary>
/// Crea las tablas del motor de predicados (<c>TargetFilters</c>/<c>TargetFilterConditions</c>)
/// y de requisitos (<c>RequirementSets</c>/<c>RequirementSlots</c>/<c>LevelSumRequirements</c>),
/// agrega <c>Cards.RequirementSetId</c> (para Ritual) y recrea <c>Fusions</c>
/// con <c>Id</c> propio y <c>RequirementSetId</c> (para recetas genericas).
///
/// Puramente aditiva: ninguna fila existente pierde datos ni cambia de
/// significado (las columnas nuevas son NULL por defecto).
/// </summary>
internal sealed class Migration002_AddFilterAndRequirementTables : ISchemaMigration
{
    public int TargetVersion => 2;

    public void Apply(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE TargetFilters (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE TargetFilterConditions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FilterId INTEGER NOT NULL REFERENCES TargetFilters(Id) ON DELETE CASCADE,
                GroupIndex INTEGER NOT NULL,
                Kind TEXT NOT NULL,
                Negate INTEGER NOT NULL DEFAULT 0,
                Value TEXT NOT NULL
            );

            CREATE TABLE RequirementSets (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Mode TEXT NOT NULL
            );

            CREATE TABLE RequirementSlots (
                RequirementSetId INTEGER NOT NULL REFERENCES RequirementSets(Id) ON DELETE CASCADE,
                SlotIndex INTEGER NOT NULL,
                FilterId INTEGER NOT NULL REFERENCES TargetFilters(Id),
                MinCount INTEGER NOT NULL DEFAULT 1,
                MaxCount INTEGER NOT NULL DEFAULT 1,
                PRIMARY KEY (RequirementSetId, SlotIndex)
            );

            CREATE TABLE LevelSumRequirements (
                RequirementSetId INTEGER PRIMARY KEY REFERENCES RequirementSets(Id) ON DELETE CASCADE,
                FilterId INTEGER NOT NULL REFERENCES TargetFilters(Id),
                MinLevelSum INTEGER NOT NULL
            );

            ALTER TABLE Cards ADD COLUMN RequirementSetId INTEGER NULL REFERENCES RequirementSets(Id);

            CREATE TABLE Fusions_new (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MaterialA INTEGER NULL,
                MaterialB INTEGER NULL,
                Result INTEGER NOT NULL,
                RequirementSetId INTEGER NULL REFERENCES RequirementSets(Id)
            );

            INSERT INTO Fusions_new (MaterialA, MaterialB, Result)
                SELECT MaterialA, MaterialB, Result FROM Fusions;

            DROP TABLE Fusions;
            ALTER TABLE Fusions_new RENAME TO Fusions;
            """;
        command.ExecuteNonQuery();
    }
}
