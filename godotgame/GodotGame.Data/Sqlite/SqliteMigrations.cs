using Microsoft.Data.Sqlite;
using GodotGame.Data.Sqlite.Migrations;

namespace GodotGame.Data.Sqlite;

/// <summary>Una migracion incremental de esquema, identificada por la version de destino.</summary>
public interface ISchemaMigration
{
    /// <summary>Version de <c>PRAGMA user_version</c> que deja la base tras aplicarse.</summary>
    int TargetVersion { get; }

    /// <summary>Aplica los cambios de esquema/datos de esta migracion. Se ejecuta dentro de una transaccion.</summary>
    void Apply(SqliteConnection connection);
}

/// <summary>
/// Aplica, en orden, las migraciones pendientes sobre una base ya creada por
/// <see cref="SqliteSchema.EnsureCreated"/>. Usa <c>PRAGMA user_version</c>
/// (entero nativo de SQLite) en vez de una tabla de version propia.
///
/// El catalogo creado por <see cref="SqliteSchema.EnsureCreated"/> es la
/// version 1 (baseline). Las migraciones reales (filtros de objetivo,
/// requisitos de Fusion/Ritual, efectos data-driven, tipos de Campo) se
/// agregan aqui en fases posteriores sin cambiar este mecanismo.
/// </summary>
public static class SqliteMigrations
{
    private static readonly ISchemaMigration[] Migrations =
    {
        new Migration002_AddFilterAndRequirementTables(),
        new Migration003_BackfillLegacyRituals(),
        new Migration004_AddEffectDefinitionTables(),
        new Migration005_BackfillLegacyEffectDefinitions(),
        new Migration006_AddEquipColumns(),
        new Migration007_AddFieldTypes(),
        new Migration008_AddFieldTypeOpposedModifier(),
        new Migration009_SeedElementalFieldTypes(),
        new Migration010_AddGuardianStars(),
        new Migration011_AddMonsterEffects(),
    };

    public static void ApplyPending(SqliteConnection connection)
    {
        long currentVersion = GetUserVersion(connection);

        foreach (var migration in Migrations.OrderBy(m => m.TargetVersion))
        {
            if (migration.TargetVersion <= currentVersion) continue;

            using var transaction = connection.BeginTransaction();
            migration.Apply(connection);
            SetUserVersion(connection, migration.TargetVersion, transaction);
            transaction.Commit();
        }
    }

    private static long GetUserVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return (long)command.ExecuteScalar()!;
    }

    private static void SetUserVersion(SqliteConnection connection, int version, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // PRAGMA no admite parametros; version viene del codigo (TargetVersion de una ISchemaMigration), no de entrada externa.
        command.CommandText = $"PRAGMA user_version = {version};";
        command.ExecuteNonQuery();
    }
}
