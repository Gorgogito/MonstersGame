using MonstersGame.Data.Loaders;

namespace MonstersGame.Data.Sqlite;

/// <summary>
/// Lee y escribe el catalogo <c>FieldTypes</c> -- mismo patron que
/// <see cref="SqliteTypeWriter"/> para el Tipo de Monstruo: un catalogo
/// independiente, administrado desde <c>FieldTypeEditorForm</c>, que
/// cualquier Magia de Campo referencia por Id sin necesitar su propia copia
/// del filtro/modificador.
/// </summary>
public sealed class SqliteFieldTypeWriter
{
    private readonly string _dbPath;

    public SqliteFieldTypeWriter(string dbPath) => _dbPath = dbPath;

    public List<FieldTypeDto> LoadAll()
    {
        SqliteSchema.EnsureCreated(_dbPath);
        using var connection = SqliteSchema.OpenConnection(_dbPath);
        var requirementLoader = new SqliteRequirementLoader(connection);

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, Description, Color, BackgroundImage, VisualEffectsKey, AffectedFilterId, StatModifierAmount, StatModifierStat,
                   OpposedFilterId, OpposedStatModifierAmount
            FROM FieldTypes ORDER BY Name
            """;

        var result = new List<FieldTypeDto>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var dto = new FieldTypeDto
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Description = reader.GetString(2),
                Color = reader.GetString(3),
                BackgroundImage = reader.GetString(4),
                VisualEffectsKey = reader.GetString(5),
                StatModifierAmount = reader.GetInt32(7),
                StatModifierStat = reader.GetString(8),
                OpposedStatModifierAmount = reader.GetInt32(10)
            };
            if (!reader.IsDBNull(6))
                dto.AffectedFilter = RequirementDtoConversion.ToFilterDto(requirementLoader.LoadFilter(reader.GetInt32(6)));
            if (!reader.IsDBNull(9))
                dto.OpposedFilter = RequirementDtoConversion.ToFilterDto(requirementLoader.LoadFilter(reader.GetInt32(9)));
            result.Add(dto);
        }
        return result;
    }

    /// <summary>Inserta o reemplaza (por Id) el tipo de Campo. El filtro anterior (si lo habia) queda huerfano (ver <see cref="SqliteRequirementWriter"/>).</summary>
    public void Save(FieldTypeDto dto)
    {
        SqliteSchema.EnsureCreated(_dbPath);
        using var connection = SqliteSchema.OpenConnection(_dbPath);
        int? filterId = SqliteRequirementWriter.WriteFilter(connection, dto.AffectedFilter);
        int? opposedFilterId = SqliteRequirementWriter.WriteFilter(connection, dto.OpposedFilter);

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO FieldTypes (Id, Name, Description, Color, BackgroundImage, VisualEffectsKey, AffectedFilterId, StatModifierAmount, StatModifierStat,
                                     OpposedFilterId, OpposedStatModifierAmount)
            VALUES ($Id, $Name, $Description, $Color, $BackgroundImage, $VisualEffectsKey, $FilterId, $Amount, $Stat,
                    $OpposedFilterId, $OpposedAmount)
            ON CONFLICT(Id) DO UPDATE SET
                Name = excluded.Name, Description = excluded.Description, Color = excluded.Color,
                BackgroundImage = excluded.BackgroundImage, VisualEffectsKey = excluded.VisualEffectsKey,
                AffectedFilterId = excluded.AffectedFilterId, StatModifierAmount = excluded.StatModifierAmount,
                StatModifierStat = excluded.StatModifierStat, OpposedFilterId = excluded.OpposedFilterId,
                OpposedStatModifierAmount = excluded.OpposedStatModifierAmount
            """;
        command.Parameters.AddWithValue("$Id", dto.Id);
        command.Parameters.AddWithValue("$Name", dto.Name);
        command.Parameters.AddWithValue("$Description", dto.Description);
        command.Parameters.AddWithValue("$Color", dto.Color);
        command.Parameters.AddWithValue("$BackgroundImage", dto.BackgroundImage);
        command.Parameters.AddWithValue("$VisualEffectsKey", dto.VisualEffectsKey);
        command.Parameters.AddWithValue("$FilterId", (object?)filterId ?? DBNull.Value);
        command.Parameters.AddWithValue("$Amount", dto.StatModifierAmount);
        command.Parameters.AddWithValue("$Stat", dto.StatModifierStat);
        command.Parameters.AddWithValue("$OpposedFilterId", (object?)opposedFilterId ?? DBNull.Value);
        command.Parameters.AddWithValue("$OpposedAmount", dto.OpposedStatModifierAmount);
        command.ExecuteNonQuery();
    }

    /// <summary>Cuantas Cartas Magicas de Campo usan este tipo ahora mismo (para no dejar cartas con un tipo huerfano).</summary>
    public int CountCardsUsing(string id)
    {
        SqliteSchema.EnsureCreated(_dbPath);
        using var connection = SqliteSchema.OpenConnection(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Cards WHERE FieldTypeId = $Id";
        command.Parameters.AddWithValue("$Id", id);
        return checked((int)(long)command.ExecuteScalar()!);
    }

    public void Delete(string id)
    {
        SqliteSchema.EnsureCreated(_dbPath);
        using var connection = SqliteSchema.OpenConnection(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM FieldTypes WHERE Id = $Id";
        command.Parameters.AddWithValue("$Id", id);
        command.ExecuteNonQuery();
    }
}
