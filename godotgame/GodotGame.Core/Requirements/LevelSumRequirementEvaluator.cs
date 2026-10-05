using GodotGame.Core.Entities;

namespace GodotGame.Core.Requirements;

/// <summary>
/// Resuelve un <see cref="RequirementSet"/> en modo <see cref="RequirementMode.LevelSum"/>:
/// filtra candidatos elegibles y elige el subconjunto minimo (greedy por
/// Nivel descendente) cuya suma alcance <see cref="LevelSumRequirement.MinLevelSum"/>.
/// </summary>
public sealed class LevelSumRequirementEvaluator : IRequirementEvaluator
{
    public bool TryMatch(RequirementSet set, IReadOnlyList<MonsterCard> candidates, out MaterialAssignment? assignment)
    {
        assignment = null;
        if (set.Mode != RequirementMode.LevelSum || set.LevelSum == null) return false;

        var eligible = candidates
            .Where(c => TargetFilterEvaluator.Matches(set.LevelSum.Filter, c))
            .OrderByDescending(c => c.Level)
            .ToList();

        var chosen = new List<MonsterCard>();
        int sum = 0;
        foreach (var card in eligible)
        {
            if (sum >= set.LevelSum.MinLevelSum) break;
            chosen.Add(card);
            sum += card.Level;
        }

        if (sum < set.LevelSum.MinLevelSum) return false;

        assignment = new MaterialAssignment(chosen);
        return true;
    }
}
