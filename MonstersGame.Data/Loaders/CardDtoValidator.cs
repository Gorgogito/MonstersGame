using MonstersGame.Core.Effects;
using MonstersGame.Core.Entities;

namespace MonstersGame.Data.Loaders;

/// <summary>
/// Valida un <see cref="CardDto"/> contra las reglas de MonstersGame antes de
/// dejarlo guardar (seccion 11 del analisis de adaptacion: "el editor no
/// deberia permitir guardar una carta invalida, pero los mensajes deben
/// indicar claramente que corregir"). Usa <see cref="CardDtoMapper"/> para
/// entender "carta valida" exactamente igual que el motor del juego.
/// </summary>
public static class CardDtoValidator
{
    /// <summary>
    /// Valida <paramref name="dto"/>. <paramref name="allCards"/> es el
    /// catalogo completo actual (para chequear duplicados de Id/Nombre y
    /// referencias cruzadas como el Monstruo de una Magia de Ritual);
    /// <paramref name="originalId"/> es el Id que tenia la carta antes de
    /// editarla (para no chocar consigo misma), o null si es nueva.
    /// </summary>
    public static List<string> Validate(CardDto dto, IReadOnlyList<CardDto> allCards, int? originalId)
    {
        var errors = new List<string>();

        if (dto.Id <= 0)
            errors.Add("El Id debe ser un numero mayor que 0.");
        if (string.IsNullOrWhiteSpace(dto.Name))
            errors.Add("El nombre no puede estar vacio.");

        // Al editar, se excluye la carta original (por su Id de antes de
        // editar) para no chocar consigo misma; al crear una nueva no se
        // excluye nada, cualquier coincidencia de Id/nombre es un choque real.
        var others = originalId.HasValue
            ? allCards.Where(c => c.Id != originalId.Value).ToList()
            : allCards.ToList();
        if (others.Any(c => c.Id == dto.Id))
            errors.Add($"Ya existe otra carta con Id {dto.Id}.");
        if (!string.IsNullOrWhiteSpace(dto.Name) && others.Any(c => string.Equals(c.Name.Trim(), dto.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            errors.Add($"Ya existe otra carta llamada \"{dto.Name}\" (el reglamento trata a las cartas con el mismo nombre como la misma carta).");

        switch (dto.Kind.Trim().ToLowerInvariant())
        {
            case "monster":
                ValidateMonster(dto, errors);
                break;
            case "spell":
                ValidateSpell(dto, others, errors);
                break;
            case "trap":
                ValidateTrap(dto, errors);
                break;
            default:
                errors.Add($"Kind \"{dto.Kind}\" invalido: debe ser Monster, Spell o Trap.");
                break;
        }

        return errors;
    }

    private static void ValidateMonster(CardDto dto, List<string> errors)
    {
        if (dto.Attack < 0) errors.Add("El ATK no puede ser negativo.");
        if (dto.Defense < 0) errors.Add("La DEF no puede ser negativa.");
        if (dto.Level is < 1 or > 12) errors.Add("El Nivel debe estar entre 1 y 12.");
        // El Tipo ya no es un enum fijo: el catalogo de Tipos vive en la
        // tabla Types y se administra desde el editor (ver TypeRepository),
        // asi que cualquier texto no vacio es un Tipo valido aqui.
        if (string.IsNullOrWhiteSpace(dto.Type)) errors.Add("El Tipo no puede estar vacio.");
        if (!Enum.TryParse<MonsterAttribute>(dto.Attribute, ignoreCase: true, out _))
            errors.Add($"Atributo \"{dto.Attribute}\" no reconocido.");
        if (!Enum.TryParse<MonsterCategory>(dto.Category, ignoreCase: true, out _))
            errors.Add($"Categoria \"{dto.Category}\" no reconocida (debe ser Normal, Effect, Fusion o Ritual).");

        ValidateEffectId(dto.EffectId, errors);
    }

    private static void ValidateSpell(CardDto dto, List<CardDto> others, List<string> errors)
    {
        if (!Enum.TryParse<SpellSubType>(dto.SubType, ignoreCase: true, out var subType))
        {
            errors.Add($"SubType \"{dto.SubType}\" no reconocido para una Magia.");
            return;
        }

        if (subType == SpellSubType.Ritual)
        {
            if (dto.RequiredRitualLevel < 1)
                errors.Add("Una Magia de Ritual necesita exigir Sacrificar al menos Nivel 1.");

            var linked = others.FirstOrDefault(c => c.Id == dto.RitualMonsterId
                && c.Kind.Equals("Monster", StringComparison.OrdinalIgnoreCase));
            if (linked == null)
                errors.Add($"RitualMonsterId {dto.RitualMonsterId} no corresponde a ningun Monstruo del catalogo.");
            else if (!string.Equals(linked.Category, nameof(MonsterCategory.Ritual), StringComparison.OrdinalIgnoreCase))
                errors.Add($"\"{linked.Name}\" existe, pero su Categoria no es Ritual: cambiala primero para poder vincularla aqui.");
        }
        else
        {
            ValidateEffectId(dto.EffectId, errors);
        }
    }

    private static void ValidateTrap(CardDto dto, List<string> errors)
    {
        if (!Enum.TryParse<TrapSubType>(dto.SubType, ignoreCase: true, out _))
            errors.Add($"SubType \"{dto.SubType}\" no reconocido para una Trampa.");

        ValidateEffectId(dto.EffectId, errors);
    }

    private static void ValidateEffectId(string effectId, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(effectId)) return;
        if (EffectRegistry.Get(effectId) == null)
            errors.Add($"EffectId \"{effectId}\" no esta registrado en EffectRegistry. Dejalo vacio o usa uno de los ya implementados.");
    }
}
