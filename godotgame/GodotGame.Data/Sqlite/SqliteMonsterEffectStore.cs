using System.Text.Json;
using Microsoft.Data.Sqlite;
using GodotGame.Data.Loaders;

namespace GodotGame.Data.Sqlite;

/// <summary>Lee y escribe la tabla <c>MonsterEffects</c> (un <see cref="MonsterEffectDto"/> en JSON por fila).</summary>
public static class SqliteMonsterEffectStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    /// <summary>Todos los efectos de todas las cartas, por Id de carta y en orden.</summary>
    public static Dictionary<int, List<MonsterEffectDto>> LoadAll(SqliteConnection connection)
    {
        var result = new Dictionary<int, List<MonsterEffectDto>>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT CardId, Json FROM MonsterEffects ORDER BY CardId, Ordinal";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var dto = Deserialize(reader.GetString(1));
            if (dto == null) continue;
            int cardId = reader.GetInt32(0);
            if (!result.TryGetValue(cardId, out var list)) result[cardId] = list = new List<MonsterEffectDto>();
            list.Add(dto);
        }
        return result;
    }

    /// <summary>Reemplaza los efectos de la carta por <paramref name="effects"/> (lista vacia = sin efectos).</summary>
    public static void Save(SqliteConnection connection, int cardId, IReadOnlyList<MonsterEffectDto> effects)
    {
        using (var delete = connection.CreateCommand())
        {
            delete.CommandText = "DELETE FROM MonsterEffects WHERE CardId = $CardId";
            delete.Parameters.AddWithValue("$CardId", cardId);
            delete.ExecuteNonQuery();
        }

        for (int i = 0; i < effects.Count; i++)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO MonsterEffects (CardId, Ordinal, Json) VALUES ($CardId, $Ordinal, $Json)";
            insert.Parameters.AddWithValue("$CardId", cardId);
            insert.Parameters.AddWithValue("$Ordinal", i);
            insert.Parameters.AddWithValue("$Json", JsonSerializer.Serialize(effects[i], Options));
            insert.ExecuteNonQuery();
        }
    }

    private static MonsterEffectDto? Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<MonsterEffectDto>(json, Options); }
        catch (JsonException) { return null; }
    }
}
