using GodotGame.Core.Requirements;

namespace GodotGame.Core.Entities;

/// <summary>
/// Receta de fusion. Dos formas conviven:
///
/// - Legacy (2 materiales exactos por Id, <see cref="Requirement"/> null):
///   <see cref="MaterialAId"/>/<see cref="MaterialBId"/> identifican el par
///   exacto; el emparejamiento es independiente del orden (A+B == B+A). Es la
///   version original inspirada en Forbidden Memories (fusion directa, sin
///   "Polimerizacion").
/// - Generica (<see cref="Requirement"/> no null, modo <see cref="RequirementMode.MaterialSlots"/>):
///   N huecos con <see cref="TargetFilter"/> propio (carta especifica, tipo,
///   categoria, atributo, cantidad). <see cref="MaterialAId"/>/<see cref="MaterialBId"/>
///   no tienen significado en este caso (quedan en 0).
/// </summary>
public sealed class FusionRecipe
{
    public int Id { get; }
    public int MaterialAId { get; }
    public int MaterialBId { get; }
    public int ResultId { get; }

    /// <summary>No nulo solo para recetas genericas (ver clase). Null = receta legacy de 2 materiales exactos.</summary>
    public RequirementSet? Requirement { get; }

    public FusionRecipe(int materialAId, int materialBId, int resultId, int id = 0, RequirementSet? requirement = null)
    {
        Id = id;
        MaterialAId = materialAId;
        MaterialBId = materialBId;
        ResultId = resultId;
        Requirement = requirement;
    }

    /// <summary>Indica si esta receta corresponde al par de ids dado (en cualquier orden).</summary>
    public bool Matches(int first, int second)
    {
        return (MaterialAId == first && MaterialBId == second)
            || (MaterialAId == second && MaterialBId == first);
    }
}
