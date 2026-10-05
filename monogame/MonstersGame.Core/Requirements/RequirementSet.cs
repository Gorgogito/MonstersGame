namespace MonstersGame.Core.Requirements;

/// <summary>
/// Conjunto de requisitos reutilizado por Fusion (<see cref="RequirementMode.MaterialSlots"/>)
/// y Ritual (<see cref="RequirementMode.LevelSum"/>). Ambos consumen el mismo
/// <see cref="TargetFilter"/> pero tienen forma distinta: Fusion son huecos
/// con multiplicidad fija, Ritual es un umbral agregado sin cardinalidad fija
/// de Sacrificios (forzar Ritual a "huecos" seria una abstraccion incorrecta).
/// </summary>
public sealed class RequirementSet
{
    public int Id { get; }
    public RequirementMode Mode { get; }

    /// <summary>Solo relevante si <see cref="Mode"/> es <see cref="RequirementMode.MaterialSlots"/>.</summary>
    public IReadOnlyList<RequirementSlot> Slots { get; }

    /// <summary>Solo relevante si <see cref="Mode"/> es <see cref="RequirementMode.LevelSum"/>.</summary>
    public LevelSumRequirement? LevelSum { get; }

    public RequirementSet(int id, RequirementMode mode, IReadOnlyList<RequirementSlot> slots, LevelSumRequirement? levelSum)
    {
        Id = id;
        Mode = mode;
        Slots = slots;
        LevelSum = levelSum;
    }
}
