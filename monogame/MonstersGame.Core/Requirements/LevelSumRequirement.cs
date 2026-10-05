namespace MonstersGame.Core.Requirements;

/// <summary>Regla de un <see cref="RequirementSet"/> en modo <see cref="RequirementMode.LevelSum"/>: suma minima de Nivel entre candidatos que cumplan <see cref="Filter"/>.</summary>
public sealed class LevelSumRequirement
{
    public TargetFilter Filter { get; }
    public int MinLevelSum { get; }

    public LevelSumRequirement(TargetFilter filter, int minLevelSum)
    {
        Filter = filter;
        MinLevelSum = minLevelSum;
    }
}
