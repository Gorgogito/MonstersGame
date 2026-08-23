using MonstersGame.Data.Loaders;

namespace MonstersGame.Data.Sqlite;

/// <summary>
/// Lee y escribe el catalogo de cartas en la tabla <c>Cards</c> de una base
/// SQLite. Mismo contrato publico que <see cref="JsonCardLoader"/>'s writer
/// (<see cref="Loaders.JsonCardWriter"/>) para que <c>MonstersGame.CardEditor</c>
/// pueda usar cualquiera de los dos sin cambiar su propio codigo.
/// </summary>
public sealed class SqliteCardWriter
{
    private readonly string _dbPath;

    public SqliteCardWriter(string dbPath) => _dbPath = dbPath;

    public List<CardDto> LoadAllDtos()
    {
        var dtos = new List<CardDto>();
        if (!File.Exists(_dbPath)) return dtos;

        SqliteSchema.EnsureCreated(_dbPath);
        using var connection = SqliteSchema.OpenConnection(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Kind, Name, Attack, Defense, Level, Type, Attribute, Image, Description, EffectId, SubType, Category, RitualMonsterId, RequiredRitualLevel FROM Cards ORDER BY Id";

        using var reader = command.ExecuteReader();
        while (reader.Read())
            dtos.Add(SqliteCardMapping.ReadDto(reader));

        return dtos;
    }

    /// <summary>Inserta o reemplaza (por Id) la carta en la base.</summary>
    public void SaveCard(CardDto dto)
    {
        SqliteSchema.EnsureCreated(_dbPath);
        using var connection = SqliteSchema.OpenConnection(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Cards (Id, Kind, Name, Attack, Defense, Level, Type, Attribute, Image, Description, EffectId, SubType, Category, RitualMonsterId, RequiredRitualLevel)
            VALUES ($Id, $Kind, $Name, $Attack, $Defense, $Level, $Type, $Attribute, $Image, $Description, $EffectId, $SubType, $Category, $RitualMonsterId, $RequiredRitualLevel)
            ON CONFLICT(Id) DO UPDATE SET
                Kind = excluded.Kind, Name = excluded.Name, Attack = excluded.Attack, Defense = excluded.Defense,
                Level = excluded.Level, Type = excluded.Type, Attribute = excluded.Attribute, Image = excluded.Image,
                Description = excluded.Description, EffectId = excluded.EffectId, SubType = excluded.SubType,
                Category = excluded.Category, RitualMonsterId = excluded.RitualMonsterId, RequiredRitualLevel = excluded.RequiredRitualLevel
            """;
        SqliteCardMapping.BindParameters(command, dto);
        command.ExecuteNonQuery();
    }

    public void DeleteCard(int id)
    {
        SqliteSchema.EnsureCreated(_dbPath);
        using var connection = SqliteSchema.OpenConnection(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Cards WHERE Id = $Id";
        command.Parameters.AddWithValue("$Id", id);
        command.ExecuteNonQuery();
    }
}
