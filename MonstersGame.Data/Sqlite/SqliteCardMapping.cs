using Microsoft.Data.Sqlite;
using MonstersGame.Data.Loaders;

namespace MonstersGame.Data.Sqlite;

/// <summary>
/// Conversion compartida entre una fila de la tabla <c>Cards</c> y un
/// <see cref="CardDto"/>, usada tanto por <see cref="SqliteCardLoader"/>
/// (solo lectura, para el juego) como por <see cref="SqliteCardWriter"/>
/// (lectura y escritura, para el editor).
/// </summary>
internal static class SqliteCardMapping
{
    public static CardDto ReadDto(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Kind = reader.GetString(1),
        Name = reader.GetString(2),
        Attack = reader.GetInt32(3),
        Defense = reader.GetInt32(4),
        Level = reader.GetInt32(5),
        Type = reader.GetString(6),
        Attribute = reader.GetString(7),
        Image = reader.GetString(8),
        Description = reader.GetString(9),
        EffectId = reader.GetString(10),
        SubType = reader.GetString(11),
        Category = reader.GetString(12),
        RitualMonsterId = reader.GetInt32(13),
        RequiredRitualLevel = reader.GetInt32(14)
    };

    public static void BindParameters(SqliteCommand command, CardDto dto)
    {
        command.Parameters.AddWithValue("$Id", dto.Id);
        command.Parameters.AddWithValue("$Kind", dto.Kind);
        command.Parameters.AddWithValue("$Name", dto.Name);
        command.Parameters.AddWithValue("$Attack", dto.Attack);
        command.Parameters.AddWithValue("$Defense", dto.Defense);
        command.Parameters.AddWithValue("$Level", dto.Level);
        command.Parameters.AddWithValue("$Type", dto.Type);
        command.Parameters.AddWithValue("$Attribute", dto.Attribute);
        command.Parameters.AddWithValue("$Image", dto.Image);
        command.Parameters.AddWithValue("$Description", dto.Description);
        command.Parameters.AddWithValue("$EffectId", dto.EffectId);
        command.Parameters.AddWithValue("$SubType", dto.SubType);
        command.Parameters.AddWithValue("$Category", dto.Category);
        command.Parameters.AddWithValue("$RitualMonsterId", dto.RitualMonsterId);
        command.Parameters.AddWithValue("$RequiredRitualLevel", dto.RequiredRitualLevel);
    }
}
