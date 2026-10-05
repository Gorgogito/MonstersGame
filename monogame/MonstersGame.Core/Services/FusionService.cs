using MonstersGame.Core.Entities;
using MonstersGame.Core.Requirements;

namespace MonstersGame.Core.Services;

/// <summary>
/// Resuelve fusiones entre cartas. Las recetas se cargan desde datos externos
/// (no estan dispersas por el codigo). El servicio solo consulta la tabla de
/// recetas y la base de cartas para producir el resultado.
/// </summary>
public sealed class FusionService
{
    private static readonly MaterialSlotRequirementEvaluator SlotEvaluator = new();

    private readonly List<FusionRecipe> _recipes;
    private readonly CardDatabase _database;

    public FusionService(IEnumerable<FusionRecipe> recipes, CardDatabase database)
    {
        _recipes = recipes.ToList();
        _database = database;
    }

    public int RecipeCount => _recipes.Count;

    /// <summary>
    /// Intenta fusionar dos cartas. Devuelve la carta de monstruo resultante o
    /// null si no existe una receta valida para ese par. Solo resuelve recetas
    /// legacy de 2 materiales exactos (ver <see cref="TryFuseMany"/> para
    /// recetas genericas por filtro).
    /// </summary>
    public MonsterCard? TryFuse(MonsterCard a, MonsterCard b)
    {
        var recipe = _recipes.FirstOrDefault(r => r.Matches(a.Id, b.Id));
        if (recipe == null) return null;
        return _database.GetMonster(recipe.ResultId);
    }

    /// <summary>Indica si existe una fusion para el par de cartas dado.</summary>
    public bool CanFuse(MonsterCard a, MonsterCard b) => TryFuse(a, b) != null;

    /// <summary>
    /// Intenta fusionar usando una receta generica (<see cref="FusionRecipe.Requirement"/>
    /// no nulo, modo <see cref="RequirementMode.MaterialSlots"/>): de entre
    /// TODAS las recetas cuyos huecos queden satisfechos por <paramref name="candidates"/>,
    /// devuelve la que consuma MAS candidatos (a igualdad de consumo, la
    /// primera en orden de registro). Devuelve la carta resultante y, por
    /// <paramref name="usedMaterials"/>, las cartas concretas consumidas.
    ///
    /// Preferir el mayor consumo (no solo la primera receta satisfecha) es
    /// necesario porque una receta de menos huecos puede "tapar" a otra mas
    /// especifica que aprovecharia todos los candidatos: si el llamador
    /// selecciono N cartas esperando consumirlas todas (ver <c>DuelEngine.FuseMany</c>),
    /// una coincidencia parcial con la primera receta encontrada rechazaria
    /// una fusion valida que si existia con otra receta.
    /// </summary>
    public MonsterCard? TryFuseMany(IReadOnlyList<MonsterCard> candidates, out IReadOnlyList<MonsterCard> usedMaterials)
    {
        MonsterCard? bestResult = null;
        IReadOnlyList<MonsterCard> bestUsed = Array.Empty<MonsterCard>();

        foreach (var recipe in _recipes)
        {
            if (recipe.Requirement is not { Mode: RequirementMode.MaterialSlots }) continue;
            if (!SlotEvaluator.TryMatch(recipe.Requirement, candidates, out var assignment) || assignment == null) continue;

            var result = _database.GetMonster(recipe.ResultId);
            if (result == null) continue;

            if (assignment.UsedMaterials.Count > bestUsed.Count)
            {
                bestResult = result;
                bestUsed = assignment.UsedMaterials;
            }
        }

        usedMaterials = bestUsed;
        return bestResult;
    }
}
