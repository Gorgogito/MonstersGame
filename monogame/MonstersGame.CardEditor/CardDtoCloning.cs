using MonstersGame.Data.Loaders;

namespace MonstersGame.CardEditor;

/// <summary>
/// Copias profundas de <see cref="CardDto"/> y sus sub-objetos editables,
/// compartidas entre <c>MainForm</c> y los paneles que extrajo (Fase 5, paso
/// de refactor puro): sin esto, cargar una carta de la lista al formulario
/// mutaria en el sitio el DTO que <c>CardRepository</c> guarda en memoria.
/// </summary>
internal static class CardDtoCloning
{
    public static FilterDto? CloneFilter(FilterDto? f) => f == null
        ? null
        : new FilterDto { OrGroups = f.OrGroups.Select(g => g.Select(c => new FilterConditionDto { Kind = c.Kind, Negate = c.Negate, Value = c.Value }).ToList()).ToList() };

    public static FusionSlotDto CloneSlot(FusionSlotDto s) =>
        new() { Filter = CloneFilter(s.Filter) ?? new FilterDto(), MinCount = s.MinCount, MaxCount = s.MaxCount };

    public static EffectActionStepDto CloneStep(EffectActionStepDto s) =>
        new() { ActionKind = s.ActionKind, ParamsText = s.ParamsText };

    public static CardDto Clone(CardDto d) => new()
    {
        Id = d.Id,
        Kind = d.Kind,
        Name = d.Name,
        Attack = d.Attack,
        Defense = d.Defense,
        Level = d.Level,
        Type = d.Type,
        Attribute = d.Attribute,
        Image = d.Image,
        Description = d.Description,
        EffectId = d.EffectId,
        SubType = d.SubType,
        Category = d.Category,
        RitualMonsterId = d.RitualMonsterId,
        RequiredRitualLevel = d.RequiredRitualLevel,
        RitualFilter = CloneFilter(d.RitualFilter),
        FusionMaterials = d.FusionMaterials.Select(CloneSlot).ToList(),
        EquipTargetFilter = CloneFilter(d.EquipTargetFilter),
        EquipAttackModifier = d.EquipAttackModifier,
        EquipDefenseModifier = d.EquipDefenseModifier,
        EquipDuration = d.EquipDuration,
        EquipDurationTurns = d.EquipDurationTurns,
        FieldTypeId = d.FieldTypeId,
        ComposeCustomEffect = d.ComposeCustomEffect,
        EffectTrigger = d.EffectTrigger,
        EffectRequiresTarget = d.EffectRequiresTarget,
        EffectTargetKind = d.EffectTargetKind,
        EffectTargetFilter = CloneFilter(d.EffectTargetFilter),
        EffectActionSteps = d.EffectActionSteps.Select(CloneStep).ToList(),
        EffectVisualProfileKey = d.EffectVisualProfileKey
    };
}
