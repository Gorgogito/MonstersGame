using GodotGame.Core.Entities;

namespace GodotGame.Core.Requirements;

/// <summary>
/// Resuelve un <see cref="RequirementSet"/> en modo <see cref="RequirementMode.MaterialSlots"/>:
/// asignacion greedy, hueco por hueco, del mas al menos restrictivo (menos
/// candidatos posibles primero) para minimizar fallos de asignacion.
/// </summary>
public sealed class MaterialSlotRequirementEvaluator : IRequirementEvaluator
{
    public bool TryMatch(RequirementSet set, IReadOnlyList<MonsterCard> candidates, out MaterialAssignment? assignment)
    {
        assignment = null;
        if (set.Mode != RequirementMode.MaterialSlots) return false;

        var remaining = candidates.ToList();
        var used = new List<MonsterCard>();

        var orderedSlots = set.Slots
            .OrderBy(slot => remaining.Count(c => TargetFilterEvaluator.Matches(slot.Filter, c)))
            .ToList();

        foreach (var slot in orderedSlots)
        {
            var matched = remaining
                .Where(c => TargetFilterEvaluator.Matches(slot.Filter, c))
                .Take(slot.MaxCount)
                .ToList();

            if (matched.Count < slot.MinCount) return false;

            foreach (var card in matched) remaining.Remove(card);
            used.AddRange(matched);
        }

        assignment = new MaterialAssignment(used);
        return true;
    }
}
