using GodotGame.Core.Effects;
using GodotGame.Core.Entities;

namespace GodotGame.Data.Loaders;

/// <summary>
/// Valida un <see cref="CardDto"/> contra las reglas de GodotGame antes de
/// dejarlo guardar (seccion 9 del analisis de evolucion del sistema de
/// cartas: "el editor no deberia permitir guardar una carta invalida, y los
/// mensajes deben indicar claramente que corregir"). Usa <see cref="CardDtoMapper"/>
/// para entender "carta valida" exactamente igual que el motor del juego.
/// </summary>
public static class CardDtoValidator
{
    private static readonly string[] ValidFilterKinds = { "SpecificCard", "Type", "Category", "Attribute", "ControllerSide", "Any" };

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
        foreach (var star in new[] { dto.GuardianStar1, dto.GuardianStar2 })
            if (!string.IsNullOrWhiteSpace(star) && !Enum.TryParse<GuardianStar>(star, ignoreCase: true, out _))
                errors.Add($"Estrella Guardiana \"{star}\" no reconocida.");
        if (!Enum.TryParse<MonsterCategory>(dto.Category, ignoreCase: true, out var category))
            errors.Add($"Categoria \"{dto.Category}\" no reconocida (debe ser Normal, Effect, Fusion o Ritual).");
        else if (category == MonsterCategory.Fusion)
            ValidateFusionMaterials(dto, errors);

        ValidateEffectIdOrComposedEffect(dto, errors);
    }

    private static void ValidateFusionMaterials(CardDto dto, List<string> errors)
    {
        if (dto.FusionMaterials.Count == 0)
        {
            errors.Add("No se puede guardar la carta: falta definir al menos un material de Fusion.");
            return;
        }

        for (int i = 0; i < dto.FusionMaterials.Count; i++)
        {
            var slot = dto.FusionMaterials[i];
            if (slot.MinCount < 1)
                errors.Add($"El hueco de Fusion #{i + 1} necesita una cantidad minima de al menos 1.");
            if (slot.MaxCount < slot.MinCount)
                errors.Add($"El hueco de Fusion #{i + 1} tiene una cantidad maxima menor que la minima.");
            ValidateFilter($"el material de Fusion #{i + 1}", slot.Filter, errors);
        }
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

            ValidateFilter("los Sacrificios de Ritual", dto.RitualFilter, errors);
        }
        else
        {
            ValidateEffectIdOrComposedEffect(dto, errors);

            if (subType == SpellSubType.Equip)
                ValidateEquip(dto, errors);
        }
    }

    private static void ValidateEquip(CardDto dto, List<string> errors)
    {
        ValidateFilter("los objetivos de Equipo", dto.EquipTargetFilter, errors);

        if (!Enum.TryParse<ModifierDuration>(dto.EquipDuration, ignoreCase: true, out var duration))
            errors.Add($"Duracion \"{dto.EquipDuration}\" no reconocida (debe ser WhileEquipped, UntilEndOfTurn o ForNTurns).");
        else if (duration == ModifierDuration.ForNTurns && dto.EquipDurationTurns < 1)
            errors.Add("Una Magia de Equipo con duracion \"ForNTurns\" necesita indicar al menos 1 turno.");
    }

    private static void ValidateTrap(CardDto dto, List<string> errors)
    {
        if (!Enum.TryParse<TrapSubType>(dto.SubType, ignoreCase: true, out _))
            errors.Add($"SubType \"{dto.SubType}\" no reconocido para una Trampa.");

        ValidateEffectIdOrComposedEffect(dto, errors);
    }

    /// <summary>
    /// Si <see cref="CardDto.ComposeCustomEffect"/> esta activo, valida el
    /// efecto compuesto (Trigger reconocido, al menos un paso de accion, cada
    /// ActionKind registrado en <see cref="EffectActionCatalog"/>, objetivo
    /// coherente); si no, valida el <c>EffectId</c> heredado como siempre.
    /// </summary>
    private static void ValidateEffectIdOrComposedEffect(CardDto dto, List<string> errors)
    {
        if (!dto.ComposeCustomEffect)
        {
            ValidateEffectId(dto.EffectId, errors);
            return;
        }

        if (!Enum.TryParse<EffectTrigger>(dto.EffectTrigger, ignoreCase: true, out _))
            errors.Add($"Trigger \"{dto.EffectTrigger}\" no reconocido.");

        if (dto.EffectActionSteps.Count == 0)
        {
            errors.Add("El efecto compuesto necesita al menos un paso de accion.");
        }
        else
        {
            foreach (var step in dto.EffectActionSteps)
                if (!EffectActionCatalog.RegisteredKinds.Contains(step.ActionKind))
                    errors.Add($"La accion \"{step.ActionKind}\" no esta registrada en EffectActionCatalog.");
        }

        if (dto.EffectRequiresTarget)
        {
            if (!Enum.TryParse<EffectTargetKind>(dto.EffectTargetKind, ignoreCase: true, out _))
                errors.Add($"Tipo de objetivo \"{dto.EffectTargetKind}\" no reconocido.");
            ValidateFilter("el objetivo del efecto", dto.EffectTargetFilter, errors);
        }
    }

    private static void ValidateEffectId(string effectId, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(effectId)) return;
        if (EffectRegistry.Get(effectId) == null)
            errors.Add($"EffectId \"{effectId}\" no esta registrado en EffectRegistry. Dejalo vacio o usa uno de los ya implementados.");
    }

    /// <summary>
    /// Valida un <see cref="FilterDto"/> editable: cada grupo OR debe tener
    /// al menos una condicion, y el Tipo/Valor de cada condicion debe ser
    /// reconocible. Un filtro null o completamente vacio es valido (significa
    /// "cualquiera").
    /// </summary>
    private static void ValidateFilter(string context, FilterDto? filter, List<string> errors)
    {
        if (filter == null) return;

        for (int g = 0; g < filter.OrGroups.Count; g++)
        {
            if (filter.OrGroups[g].Count == 0)
            {
                errors.Add($"El filtro de {context} tiene un grupo vacio; quitalo o agregale una condicion.");
                continue;
            }

            foreach (var condition in filter.OrGroups[g])
            {
                if (!ValidFilterKinds.Contains(condition.Kind))
                {
                    errors.Add($"El filtro de {context} usa un tipo de condicion \"{condition.Kind}\" no reconocido.");
                    continue;
                }

                switch (condition.Kind)
                {
                    case "Category" when !Enum.TryParse<MonsterCategory>(condition.Value, ignoreCase: true, out _):
                        errors.Add($"El filtro de {context} tiene una Categoria \"{condition.Value}\" no reconocida.");
                        break;
                    case "Attribute" when !Enum.TryParse<MonsterAttribute>(condition.Value, ignoreCase: true, out _):
                        errors.Add($"El filtro de {context} tiene un Atributo \"{condition.Value}\" no reconocido.");
                        break;
                    case "SpecificCard" when !int.TryParse(condition.Value, out _):
                        errors.Add($"El filtro de {context} tiene un Id de carta \"{condition.Value}\" invalido (debe ser un numero).");
                        break;
                }
            }
        }
    }
}
