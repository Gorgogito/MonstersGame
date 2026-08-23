using MonstersGame.Core.Entities;
using MonstersGame.Data.Sqlite;

namespace MonstersGame.Data.Loaders;

/// <summary>Carga el catalogo de cartas desde la tabla <c>Cards</c> de una base SQLite.</summary>
public sealed class SqliteCardLoader : ICardLoader
{
    private readonly string _dbPath;

    public SqliteCardLoader(string dbPath) => _dbPath = dbPath;

    public IReadOnlyList<Card> LoadCards()
    {
        var cards = new List<Card>();
        if (!File.Exists(_dbPath)) return cards;

        SqliteSchema.EnsureCreated(_dbPath);
        using var connection = SqliteSchema.OpenConnection(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Kind, Name, Attack, Defense, Level, Type, Attribute, Image, Description, EffectId, SubType, Category, RitualMonsterId, RequiredRitualLevel FROM Cards ORDER BY Id";

        using var reader = command.ExecuteReader();
        while (reader.Read())
            cards.Add(CardDtoMapper.ToCard(SqliteCardMapping.ReadDto(reader)));

        return cards;
    }
}
