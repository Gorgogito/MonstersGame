using MonstersGame.Core.Entities;

namespace MonstersGame.Core.Services;

/// <summary>
/// Resuelve fusiones entre cartas. Las recetas se cargan desde datos externos
/// (no estan dispersas por el codigo). El servicio solo consulta la tabla de
/// recetas y la base de cartas para producir el resultado.
/// </summary>
public sealed class FusionService
{
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
    /// null si no existe una receta valida para ese par.
    /// </summary>
    public MonsterCard? TryFuse(MonsterCard a, MonsterCard b)
    {
        var recipe = _recipes.FirstOrDefault(r => r.Matches(a.Id, b.Id));
        if (recipe == null) return null;
        return _database.GetMonster(recipe.ResultId);
    }

    /// <summary>Indica si existe una fusion para el par de cartas dado.</summary>
    public bool CanFuse(MonsterCard a, MonsterCard b) => TryFuse(a, b) != null;
}
