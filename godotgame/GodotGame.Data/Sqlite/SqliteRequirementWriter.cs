using Microsoft.Data.Sqlite;
using GodotGame.Data.Loaders;

namespace GodotGame.Data.Sqlite;

/// <summary>
/// Escribe <c>TargetFilters</c>/<c>TargetFilterConditions</c> y
/// <c>RequirementSets</c>/<c>RequirementSlots</c>/<c>LevelSumRequirements</c>
/// a partir de los DTO editables del editor de cartas (<see cref="FilterDto"/>,
/// <see cref="FusionSlotDto"/>). Opera sobre una conexion ya abierta para que
/// el llamador (<see cref="SqliteCardWriter"/>) pueda componer varias
/// escrituras relacionadas a una misma carta.
///
/// Simplificacion deliberada: cada guardado inserta filas NUEVAS en vez de
/// reutilizar/actualizar las anteriores (no borra el filtro/requisito viejo
/// de una carta que se reedita). Es mas simple y evita el riesgo de borrar
/// por error algo que otra carta todavia referencia, a costa de dejar filas
/// huerfanas en <c>TargetFilters</c>/<c>RequirementSets</c> tras varias
/// ediciones — no afectan el comportamiento del juego (nada las referencia),
/// solo ocupan espacio. Una limpieza de huerfanos queda para mas adelante.
/// </summary>
public static class SqliteRequirementWriter
{
    /// <summary>Escribe un <see cref="FilterDto"/> como <c>TargetFilters</c>+<c>TargetFilterConditions</c>. Devuelve null si esta vacio (sin restriccion).</summary>
    public static int? WriteFilter(SqliteConnection connection, FilterDto? filter)
    {
        if (filter == null || filter.IsEmpty) return null;

        int filterId;
        using (var insertFilter = connection.CreateCommand())
        {
            insertFilter.CommandText = "INSERT INTO TargetFilters (Name) VALUES (''); SELECT last_insert_rowid();";
            filterId = Convert.ToInt32((long)insertFilter.ExecuteScalar()!);
        }

        for (int groupIndex = 0; groupIndex < filter.OrGroups.Count; groupIndex++)
        {
            foreach (var condition in filter.OrGroups[groupIndex])
            {
                using var insertCondition = connection.CreateCommand();
                insertCondition.CommandText = "INSERT INTO TargetFilterConditions (FilterId, GroupIndex, Kind, Negate, Value) VALUES ($FilterId, $GroupIndex, $Kind, $Negate, $Value)";
                insertCondition.Parameters.AddWithValue("$FilterId", filterId);
                insertCondition.Parameters.AddWithValue("$GroupIndex", groupIndex);
                insertCondition.Parameters.AddWithValue("$Kind", condition.Kind);
                insertCondition.Parameters.AddWithValue("$Negate", condition.Negate ? 1 : 0);
                insertCondition.Parameters.AddWithValue("$Value", condition.Value);
                insertCondition.ExecuteNonQuery();
            }
        }

        return filterId;
    }

    /// <summary>Escribe una fila <c>TargetFilters</c> sin condiciones (equivalente a "cualquiera"), para columnas FilterId que son NOT NULL.</summary>
    private static int WriteEmptyFilter(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO TargetFilters (Name) VALUES (''); SELECT last_insert_rowid();";
        return Convert.ToInt32((long)command.ExecuteScalar()!);
    }

    /// <summary>Escribe un <c>RequirementSet</c> en modo <c>MaterialSlots</c> (Fusion generica) y devuelve su Id. Null si no hay huecos configurados.</summary>
    public static int? WriteMaterialSlotsRequirementSet(SqliteConnection connection, IReadOnlyList<FusionSlotDto> slots)
    {
        if (slots.Count == 0) return null;

        int requirementSetId = InsertRequirementSet(connection, "MaterialSlots");

        for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            var slot = slots[slotIndex];
            int? filterId = WriteFilter(connection, slot.Filter);

            using var insertSlot = connection.CreateCommand();
            insertSlot.CommandText = "INSERT INTO RequirementSlots (RequirementSetId, SlotIndex, FilterId, MinCount, MaxCount) VALUES ($SetId, $SlotIndex, $FilterId, $MinCount, $MaxCount)";
            insertSlot.Parameters.AddWithValue("$SetId", requirementSetId);
            insertSlot.Parameters.AddWithValue("$SlotIndex", slotIndex);
            // Un hueco sin filtro propio (cualquier Monstruo) todavia necesita una fila
            // en TargetFilters porque RequirementSlots.FilterId es NOT NULL.
            insertSlot.Parameters.AddWithValue("$FilterId", filterId ?? WriteEmptyFilter(connection));
            insertSlot.Parameters.AddWithValue("$MinCount", Math.Max(1, slot.MinCount));
            insertSlot.Parameters.AddWithValue("$MaxCount", Math.Max(slot.MinCount, slot.MaxCount));
            insertSlot.ExecuteNonQuery();
        }

        return requirementSetId;
    }

    /// <summary>Escribe un <c>RequirementSet</c> en modo <c>LevelSum</c> (Ritual) y devuelve su Id.</summary>
    public static int WriteLevelSumRequirementSet(SqliteConnection connection, FilterDto? filter, int minLevelSum)
    {
        int requirementSetId = InsertRequirementSet(connection, "LevelSum");
        // LevelSumRequirements.FilterId tambien es NOT NULL: un filtro vacio igual escribe su fila "Any".
        int filterId = WriteFilter(connection, filter) ?? WriteEmptyFilter(connection);

        using var insertLevelSum = connection.CreateCommand();
        insertLevelSum.CommandText = "INSERT INTO LevelSumRequirements (RequirementSetId, FilterId, MinLevelSum) VALUES ($SetId, $FilterId, $MinLevelSum)";
        insertLevelSum.Parameters.AddWithValue("$SetId", requirementSetId);
        insertLevelSum.Parameters.AddWithValue("$FilterId", filterId);
        insertLevelSum.Parameters.AddWithValue("$MinLevelSum", minLevelSum);
        insertLevelSum.ExecuteNonQuery();

        return requirementSetId;
    }

    private static int InsertRequirementSet(SqliteConnection connection, string mode)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO RequirementSets (Mode) VALUES ($Mode); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$Mode", mode);
        return Convert.ToInt32((long)command.ExecuteScalar()!);
    }
}
