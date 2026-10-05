using GodotGame.Core.Entities;

namespace GodotGame.Core.Requirements;

/// <summary>
/// Evalua si un <see cref="MonsterCard"/> cumple un <see cref="TargetFilter"/>.
/// Puro y sin estado, igual que <see cref="Battle.BattleResolver"/>.
/// </summary>
public static class TargetFilterEvaluator
{
    /// <summary>
    /// Evalua <paramref name="filter"/> contra <paramref name="card"/>. Un
    /// filtro sin grupos (<see cref="TargetFilter.Any"/> o equivalente)
    /// siempre coincide.
    /// </summary>
    /// <param name="candidateSide">De quien es la carta candidata (para <see cref="FilterConditionKind.ControllerSide"/>).</param>
    /// <param name="filterOwnerSide">De quien es el efecto/carta dueno del filtro (para <see cref="FilterConditionKind.ControllerSide"/>).</param>
    public static bool Matches(TargetFilter filter, MonsterCard card, PlayerSide? candidateSide = null, PlayerSide? filterOwnerSide = null)
    {
        if (filter.OrGroups.Count == 0) return true;

        foreach (var group in filter.OrGroups)
        {
            if (GroupMatches(group, card, candidateSide, filterOwnerSide)) return true;
        }
        return false;
    }

    private static bool GroupMatches(IReadOnlyList<FilterCondition> group, MonsterCard card, PlayerSide? candidateSide, PlayerSide? filterOwnerSide)
    {
        if (group.Count == 0) return false;

        foreach (var condition in group)
        {
            bool result = EvaluateCondition(condition, card, candidateSide, filterOwnerSide);
            if (condition.Negate) result = !result;
            if (!result) return false;
        }
        return true;
    }

    private static bool EvaluateCondition(FilterCondition condition, MonsterCard card, PlayerSide? candidateSide, PlayerSide? filterOwnerSide) =>
        condition.Kind switch
        {
            FilterConditionKind.Any => true,
            FilterConditionKind.SpecificCard => int.TryParse(condition.Value, out int id) && card.Id == id,
            FilterConditionKind.Type => string.Equals(card.Type, condition.Value, StringComparison.OrdinalIgnoreCase),
            FilterConditionKind.Category => Enum.TryParse<MonsterCategory>(condition.Value, ignoreCase: true, out var category) && card.Category == category,
            FilterConditionKind.Attribute => Enum.TryParse<MonsterAttribute>(condition.Value, ignoreCase: true, out var attribute) && card.Attribute == attribute,
            FilterConditionKind.ControllerSide => MatchesControllerSide(condition.Value, candidateSide, filterOwnerSide),
            _ => false
        };

    private static bool MatchesControllerSide(string value, PlayerSide? candidateSide, PlayerSide? filterOwnerSide)
    {
        // Sin contexto de lados (ej. evaluando materiales de Fusion en mano,
        // donde ambos son siempre del mismo jugador) la condicion no aplica.
        if (candidateSide is null || filterOwnerSide is null) return true;

        return value.Trim().ToLowerInvariant() switch
        {
            "owner" => candidateSide == filterOwnerSide,
            "opponent" => candidateSide != filterOwnerSide,
            _ => true
        };
    }
}
