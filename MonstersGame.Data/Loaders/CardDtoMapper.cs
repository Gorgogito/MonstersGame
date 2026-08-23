using MonstersGame.Core.Entities;

namespace MonstersGame.Data.Loaders;

/// <summary>
/// Convierte un <see cref="CardDto"/> (forma de archivo) en la entidad de
/// dominio correspondiente. Publica y compartida entre el cargador del juego
/// y el editor de cartas, para que ambos entiendan "carta valida" exactamente
/// de la misma forma sin duplicar logica.
/// </summary>
public static class CardDtoMapper
{
    public static Card ToCard(CardDto dto)
    {
        return dto.Kind.Trim().ToLowerInvariant() switch
        {
            "spell" => new SpellCard(
                dto.Id,
                dto.Name,
                ParseEnum(dto.SubType, SpellSubType.Normal),
                effectId: dto.EffectId,
                ritualMonsterId: dto.RitualMonsterId,
                requiredRitualLevel: dto.RequiredRitualLevel,
                image: dto.Image,
                description: dto.Description),
            "trap" => new TrapCard(dto.Id, dto.Name, ParseEnum(dto.SubType, TrapSubType.Normal), dto.EffectId, dto.Image, dto.Description),
            _ => new MonsterCard(
                dto.Id,
                dto.Name,
                dto.Attack,
                dto.Defense,
                dto.Level,
                string.IsNullOrWhiteSpace(dto.Type) ? "Unknown" : dto.Type,
                ParseEnum(dto.Attribute, MonsterAttribute.Dark),
                category: ParseEnum(dto.Category, MonsterCategory.Normal),
                effectId: dto.EffectId,
                image: dto.Image,
                description: dto.Description)
        };
    }

    public static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
    }
}
