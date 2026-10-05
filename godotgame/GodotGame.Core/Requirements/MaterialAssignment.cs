using GodotGame.Core.Entities;

namespace GodotGame.Core.Requirements;

/// <summary>Resultado de una coincidencia exitosa contra un <see cref="RequirementSet"/>: las cartas concretas que lo satisfacen.</summary>
public sealed record MaterialAssignment(IReadOnlyList<MonsterCard> UsedMaterials);
