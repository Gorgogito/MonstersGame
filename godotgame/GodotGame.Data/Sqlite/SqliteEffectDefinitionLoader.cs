using System.Text.Json;
using Microsoft.Data.Sqlite;
using GodotGame.Core.Effects;
using GodotGame.Core.Requirements;

namespace GodotGame.Data.Sqlite;

/// <summary>
/// Carga <see cref="EffectDefinition"/> desde las tablas
/// EffectDefinitions/EffectConditions/EffectTargetSpecs/EffectActionSteps de
/// una base SQLite, para pasarselas a <see cref="EffectDefinitionResolver.Load"/>.
/// </summary>
public sealed class SqliteEffectDefinitionLoader
{
    private readonly string _dbPath;

    public SqliteEffectDefinitionLoader(string dbPath) => _dbPath = dbPath;

    public IReadOnlyList<EffectDefinition> LoadEffectDefinitions()
    {
        var definitions = new List<EffectDefinition>();
        if (!File.Exists(_dbPath)) return definitions;

        SqliteSchema.EnsureCreated(_dbPath);
        using var connection = SqliteSchema.OpenConnection(_dbPath);
        var requirementLoader = new SqliteRequirementLoader(connection);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Trigger, VisualProfileKey, IsActive FROM EffectDefinitions";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            string id = reader.GetString(0);
            string name = reader.GetString(1);
            var trigger = Enum.Parse<EffectTrigger>(reader.GetString(2), ignoreCase: true);
            string visualProfileKey = reader.GetString(3);
            bool isActive = reader.GetInt32(4) != 0;

            var conditions = LoadConditions(connection, id);
            var targetSpec = LoadTargetSpec(connection, requirementLoader, id);
            var actionSteps = LoadActionSteps(connection, id);

            definitions.Add(new EffectDefinition(id, name, trigger, visualProfileKey, isActive, conditions, targetSpec, actionSteps));
        }

        return definitions;
    }

    private static List<EffectConditionSpec> LoadConditions(SqliteConnection connection, string effectDefinitionId)
    {
        var conditions = new List<EffectConditionSpec>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT ConditionKind, ParamsJson FROM EffectConditions WHERE EffectDefinitionId = $Id ORDER BY Id";
        command.Parameters.AddWithValue("$Id", effectDefinitionId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
            conditions.Add(new EffectConditionSpec(reader.GetString(0), ParseParams(reader.GetString(1))));

        return conditions;
    }

    private static EffectTargetSpec? LoadTargetSpec(SqliteConnection connection, SqliteRequirementLoader requirementLoader, string effectDefinitionId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT TargetKind, FilterId, Required FROM EffectTargetSpecs WHERE EffectDefinitionId = $Id";
        command.Parameters.AddWithValue("$Id", effectDefinitionId);

        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;

        var targetKind = Enum.Parse<EffectTargetKind>(reader.GetString(0), ignoreCase: true);
        TargetFilter? filter = reader.IsDBNull(1) ? null : requirementLoader.LoadFilter(reader.GetInt32(1));
        bool required = reader.GetInt32(2) != 0;

        return new EffectTargetSpec(targetKind, filter, required);
    }

    private static List<EffectActionStepSpec> LoadActionSteps(SqliteConnection connection, string effectDefinitionId)
    {
        var steps = new List<EffectActionStepSpec>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT ActionKind, ParamsJson FROM EffectActionSteps WHERE EffectDefinitionId = $Id ORDER BY StepOrder";
        command.Parameters.AddWithValue("$Id", effectDefinitionId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
            steps.Add(new EffectActionStepSpec(reader.GetString(0), ParseParams(reader.GetString(1))));

        return steps;
    }

    private static EffectActionParams ParseParams(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}") return EffectActionParams.Empty;
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        return values == null ? EffectActionParams.Empty : new EffectActionParams(values);
    }
}
