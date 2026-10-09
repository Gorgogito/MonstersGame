using GodotGame.Core.Entities;
using GodotGame.Core.Requirements;

namespace GodotGame.Data.Loaders;

/// <summary>
/// Convierte un <see cref="CardDto"/> (forma de archivo) en la entidad de
/// dominio correspondiente. Publica y compartida entre el cargador del juego
/// y el editor de cartas, para que ambos entiendan "carta valida" exactamente
/// de la misma forma sin duplicar logica.
/// </summary>
public static class CardDtoMapper
{
    /// <param name="ritualRequirement">
    /// Requisito de Ritual ya resuelto (ver <see cref="Sqlite.SqliteRequirementLoader"/>),
    /// solo relevante si <paramref name="dto"/> es una Magia de Ritual. Se
    /// resuelve fuera de este metodo porque requiere acceso a la base de
    /// datos y este mapeo debe permanecer puro (lo comparte tambien el editor).
    /// </param>
    /// <param name="equipTargetFilter">Objetivos permitidos ya resueltos, solo relevante si <paramref name="dto"/> es una Magia de Equipo. Mismo motivo que <paramref name="ritualRequirement"/>.</param>
    /// <param name="fieldType">Tipo de Campo ya resuelto, solo relevante si <paramref name="dto"/> es una Magia de Campo. Mismo motivo que <paramref name="ritualRequirement"/>.</param>
    public static Card ToCard(
        CardDto dto,
        RequirementSet? ritualRequirement = null,
        TargetFilter? equipTargetFilter = null,
        int equipAttackModifier = 0,
        int equipDefenseModifier = 0,
        ModifierDuration equipDuration = ModifierDuration.WhileEquipped,
        int equipDurationTurns = 0,
        FieldType? fieldType = null)
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
                description: dto.Description,
                requirement: ritualRequirement,
                equipTargetFilter: equipTargetFilter,
                equipAttackModifier: equipAttackModifier,
                equipDefenseModifier: equipDefenseModifier,
                equipDuration: equipDuration,
                equipDurationTurns: equipDurationTurns,
                fieldType: fieldType,
                effects: MonsterEffectMapper.ToEffects(dto.MonsterEffects)),
            "trap" => new TrapCard(dto.Id, dto.Name, ParseEnum(dto.SubType, TrapSubType.Normal), dto.EffectId, dto.Image, dto.Description,
                MonsterEffectMapper.ToEffects(dto.MonsterEffects)),
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
                description: dto.Description,
                guardianStar1: ParseOptionalStar(dto.GuardianStar1),
                guardianStar2: ParseOptionalStar(dto.GuardianStar2),
                effects: MonsterEffectMapper.ToEffects(dto.MonsterEffects))
        };
    }

    public static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
    }

    /// <summary>Estrella Guardiana del DTO, o null si esta vacia/no se reconoce (la carta usara la de por defecto de su Atributo).</summary>
    private static GuardianStar? ParseOptionalStar(string? value) =>
        Enum.TryParse<GuardianStar>(value, ignoreCase: true, out var star) ? star : null;
}
