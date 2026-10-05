using Microsoft.Data.Sqlite;
using MonstersGame.Core.Entities;
using MonstersGame.Core.Requirements;

namespace MonstersGame.Data.Sqlite;

/// <summary>Carga un <see cref="FieldType"/> por Id desde la tabla <c>FieldTypes</c>.</summary>
public sealed class SqliteFieldTypeLoader
{
    private readonly SqliteConnection _connection;
    private readonly SqliteRequirementLoader _requirementLoader;

    public SqliteFieldTypeLoader(SqliteConnection connection)
    {
        _connection = connection;
        _requirementLoader = new SqliteRequirementLoader(connection);
    }

    public FieldType? LoadFieldType(string id)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT Name, Description, Color, BackgroundImage, VisualEffectsKey, AffectedFilterId, StatModifierAmount, StatModifierStat,
                   OpposedFilterId, OpposedStatModifierAmount
            FROM FieldTypes WHERE Id = $Id
            """;
        command.Parameters.AddWithValue("$Id", id);

        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;

        string name = reader.GetString(0);
        string description = reader.GetString(1);
        string color = reader.GetString(2);
        string backgroundImage = reader.GetString(3);
        string visualEffectsKey = reader.GetString(4);
        TargetFilter? affectedFilter = reader.IsDBNull(5) ? null : _requirementLoader.LoadFilter(reader.GetInt32(5));
        int statModifierAmount = reader.GetInt32(6);
        var statModifierStat = Enum.Parse<FieldStatKind>(reader.GetString(7), ignoreCase: true);
        TargetFilter? opposedFilter = reader.IsDBNull(8) ? null : _requirementLoader.LoadFilter(reader.GetInt32(8));
        int opposedStatModifierAmount = reader.GetInt32(9);

        return new FieldType(id, name, description, color, backgroundImage, visualEffectsKey, affectedFilter, statModifierAmount, statModifierStat,
            opposedFilter, opposedStatModifierAmount);
    }
}
