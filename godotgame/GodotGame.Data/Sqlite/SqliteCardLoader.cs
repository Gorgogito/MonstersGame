using GodotGame.Core.Entities;
using GodotGame.Core.Requirements;
using GodotGame.Data.Sqlite;

namespace GodotGame.Data.Loaders;

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
        var requirementLoader = new SqliteRequirementLoader(connection);
        var fieldTypeLoader = new SqliteFieldTypeLoader(connection);
        var monsterEffects = SqliteMonsterEffectStore.LoadAll(connection);

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Kind, Name, Attack, Defense, Level, Type, Attribute, Image, Description, EffectId, SubType, Category,
                   RitualMonsterId, RequiredRitualLevel, RequirementSetId,
                   EquipTargetFilterId, EquipAttackModifier, EquipDefenseModifier, EquipDuration, EquipDurationTurns,
                   FieldTypeId, GuardianStar1, GuardianStar2
            FROM Cards ORDER BY Id
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var dto = SqliteCardMapping.ReadDto(reader);
            dto.GuardianStar1 = reader.GetString(22);
            dto.GuardianStar2 = reader.GetString(23);
            dto.MonsterEffects = monsterEffects.GetValueOrDefault(dto.Id) ?? new List<MonsterEffectDto>();
            RequirementSet? ritualRequirement = reader.IsDBNull(15) ? null : requirementLoader.LoadRequirementSet(reader.GetInt32(15));
            TargetFilter? equipTargetFilter = reader.IsDBNull(16) ? null : requirementLoader.LoadFilter(reader.GetInt32(16));
            int equipAttackModifier = reader.GetInt32(17);
            int equipDefenseModifier = reader.GetInt32(18);
            var equipDuration = Enum.Parse<ModifierDuration>(reader.GetString(19), ignoreCase: true);
            int equipDurationTurns = reader.GetInt32(20);
            FieldType? fieldType = reader.IsDBNull(21) ? null : fieldTypeLoader.LoadFieldType(reader.GetString(21));

            cards.Add(CardDtoMapper.ToCard(dto, ritualRequirement,
                equipTargetFilter, equipAttackModifier, equipDefenseModifier, equipDuration, equipDurationTurns,
                fieldType));
        }

        return cards;
    }
}
