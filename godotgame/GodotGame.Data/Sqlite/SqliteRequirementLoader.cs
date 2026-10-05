using Microsoft.Data.Sqlite;
using GodotGame.Core.Requirements;

namespace GodotGame.Data.Sqlite;

/// <summary>
/// Carga <see cref="TargetFilter"/> y <see cref="RequirementSet"/> desde las
/// tablas TargetFilters/TargetFilterConditions/RequirementSets/RequirementSlots/
/// LevelSumRequirements, resolviendo referencias por Id. Usado por los
/// loaders de Cartas y Fusiones para hidratar los requisitos de Ritual/Fusion
/// genericos.
/// </summary>
public sealed class SqliteRequirementLoader
{
    private readonly SqliteConnection _connection;

    public SqliteRequirementLoader(SqliteConnection connection) => _connection = connection;

    public TargetFilter LoadFilter(int filterId)
    {
        var groups = new SortedDictionary<int, List<FilterCondition>>();

        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT GroupIndex, Kind, Negate, Value FROM TargetFilterConditions WHERE FilterId = $FilterId ORDER BY GroupIndex, Id";
        command.Parameters.AddWithValue("$FilterId", filterId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            int groupIndex = reader.GetInt32(0);
            var kind = Enum.Parse<FilterConditionKind>(reader.GetString(1), ignoreCase: true);
            bool negate = reader.GetInt32(2) != 0;
            string value = reader.GetString(3);

            if (!groups.TryGetValue(groupIndex, out var list))
            {
                list = new List<FilterCondition>();
                groups[groupIndex] = list;
            }
            list.Add(new FilterCondition(kind, negate, value));
        }

        var orGroups = groups.Values.Select(g => (IReadOnlyList<FilterCondition>)g).ToList();
        return new TargetFilter(filterId, orGroups);
    }

    public RequirementSet LoadRequirementSet(int requirementSetId)
    {
        string mode;
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "SELECT Mode FROM RequirementSets WHERE Id = $Id";
            command.Parameters.AddWithValue("$Id", requirementSetId);
            var result = command.ExecuteScalar()
                ?? throw new InvalidOperationException($"RequirementSet {requirementSetId} no existe.");
            mode = (string)result;
        }

        var requirementMode = Enum.Parse<RequirementMode>(mode, ignoreCase: true);

        if (requirementMode == RequirementMode.LevelSum)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT FilterId, MinLevelSum FROM LevelSumRequirements WHERE RequirementSetId = $Id";
            command.Parameters.AddWithValue("$Id", requirementSetId);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
                throw new InvalidOperationException($"RequirementSet {requirementSetId} es LevelSum pero no tiene fila en LevelSumRequirements.");

            var filter = LoadFilter(reader.GetInt32(0));
            int minLevelSum = reader.GetInt32(1);
            return new RequirementSet(requirementSetId, requirementMode, Array.Empty<RequirementSlot>(), new LevelSumRequirement(filter, minLevelSum));
        }
        else
        {
            var slots = new List<RequirementSlot>();
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT SlotIndex, FilterId, MinCount, MaxCount FROM RequirementSlots WHERE RequirementSetId = $Id ORDER BY SlotIndex";
            command.Parameters.AddWithValue("$Id", requirementSetId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                int slotIndex = reader.GetInt32(0);
                var filter = LoadFilter(reader.GetInt32(1));
                int minCount = reader.GetInt32(2);
                int maxCount = reader.GetInt32(3);
                slots.Add(new RequirementSlot(slotIndex, filter, minCount, maxCount));
            }
            return new RequirementSet(requirementSetId, requirementMode, slots, null);
        }
    }
}
