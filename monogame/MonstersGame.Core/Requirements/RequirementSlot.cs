namespace MonstersGame.Core.Requirements;

/// <summary>Un hueco de material dentro de un <see cref="RequirementSet"/> en modo <see cref="RequirementMode.MaterialSlots"/>.</summary>
public sealed class RequirementSlot
{
    public int SlotIndex { get; }
    public TargetFilter Filter { get; }
    public int MinCount { get; }
    public int MaxCount { get; }

    public RequirementSlot(int slotIndex, TargetFilter filter, int minCount, int maxCount)
    {
        SlotIndex = slotIndex;
        Filter = filter;
        MinCount = minCount;
        MaxCount = maxCount;
    }
}
