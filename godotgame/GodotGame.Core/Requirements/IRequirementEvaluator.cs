using GodotGame.Core.Entities;

namespace GodotGame.Core.Requirements;

/// <summary>Estrategia de coincidencia para un modo de <see cref="RequirementSet"/> (mismo patron que <see cref="Effects.IEffectAction"/>).</summary>
public interface IRequirementEvaluator
{
    /// <summary>
    /// Intenta satisfacer <paramref name="set"/> usando <paramref name="candidates"/>
    /// (ej. la mano del jugador). Devuelve verdadero y la asignacion si es posible.
    /// </summary>
    bool TryMatch(RequirementSet set, IReadOnlyList<MonsterCard> candidates, out MaterialAssignment? assignment);
}
