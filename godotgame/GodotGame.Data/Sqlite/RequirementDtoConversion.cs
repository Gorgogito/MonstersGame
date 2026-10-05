using GodotGame.Core.Requirements;
using GodotGame.Data.Loaders;

namespace GodotGame.Data.Sqlite;

/// <summary>
/// Convierte los objetos ya resueltos del motor de predicados
/// (<see cref="TargetFilter"/>, <see cref="RequirementSlot"/>) de vuelta a
/// sus DTO editables, para que el editor pueda recargar y reeditar una carta
/// que ya tiene un filtro/receta guardados.
/// </summary>
internal static class RequirementDtoConversion
{
    public static FilterDto ToFilterDto(TargetFilter filter)
    {
        var dto = new FilterDto();
        foreach (var group in filter.OrGroups)
            dto.OrGroups.Add(group.Select(c => new FilterConditionDto { Kind = c.Kind.ToString(), Negate = c.Negate, Value = c.Value }).ToList());
        return dto;
    }

    public static FusionSlotDto ToFusionSlotDto(RequirementSlot slot) => new()
    {
        Filter = ToFilterDto(slot.Filter),
        MinCount = slot.MinCount,
        MaxCount = slot.MaxCount
    };
}
