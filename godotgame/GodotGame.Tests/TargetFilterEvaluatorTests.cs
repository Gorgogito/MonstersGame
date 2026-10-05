using GodotGame.Core.Entities;
using GodotGame.Core.Requirements;

namespace GodotGame.Tests;

public class TargetFilterEvaluatorTests
{
    private static MonsterCard Dragon(int id = 1) =>
        new(id, "Dragon de Prueba", 1000, 1000, 4, "Dragon", MonsterAttribute.Fire, MonsterCategory.Normal);

    private static MonsterCard Warrior(int id = 2) =>
        new(id, "Guerrero de Prueba", 1000, 1000, 4, "Warrior", MonsterAttribute.Earth, MonsterCategory.Fusion);

    [Fact]
    public void Matches_EmptyFilter_MatchesAnyCard()
    {
        var filter = TargetFilter.Any;

        Assert.True(TargetFilterEvaluator.Matches(filter, Dragon()));
        Assert.True(TargetFilterEvaluator.Matches(filter, Warrior()));
    }

    [Fact]
    public void Matches_SingleGroup_RequiresAllConditionsInGroup()
    {
        var filter = new TargetFilter(1, new IReadOnlyList<FilterCondition>[]
        {
            new[]
            {
                new FilterCondition(FilterConditionKind.Type, false, "Dragon"),
                new FilterCondition(FilterConditionKind.Attribute, false, "Fire")
            }
        });

        Assert.True(TargetFilterEvaluator.Matches(filter, Dragon()));
        Assert.False(TargetFilterEvaluator.Matches(filter, Warrior()));
    }

    [Fact]
    public void Matches_MultipleGroups_ActAsOr()
    {
        var filter = new TargetFilter(1, new IReadOnlyList<FilterCondition>[]
        {
            new[] { new FilterCondition(FilterConditionKind.Type, false, "Dragon") },
            new[] { new FilterCondition(FilterConditionKind.Category, false, "Fusion") }
        });

        Assert.True(TargetFilterEvaluator.Matches(filter, Dragon()));
        Assert.True(TargetFilterEvaluator.Matches(filter, Warrior()));

        var neither = new MonsterCard(3, "Otro", 1000, 1000, 4, "Zombie", MonsterAttribute.Dark, MonsterCategory.Normal);
        Assert.False(TargetFilterEvaluator.Matches(filter, neither));
    }

    [Fact]
    public void Matches_Negate_InvertsCondition()
    {
        var filter = new TargetFilter(1, new IReadOnlyList<FilterCondition>[]
        {
            new[] { new FilterCondition(FilterConditionKind.Type, true, "Dragon") }
        });

        Assert.False(TargetFilterEvaluator.Matches(filter, Dragon()));
        Assert.True(TargetFilterEvaluator.Matches(filter, Warrior()));
    }

    [Fact]
    public void Matches_SpecificCard_OnlyMatchesThatId()
    {
        var filter = new TargetFilter(1, new IReadOnlyList<FilterCondition>[]
        {
            new[] { new FilterCondition(FilterConditionKind.SpecificCard, false, "1") }
        });

        Assert.True(TargetFilterEvaluator.Matches(filter, Dragon(1)));
        Assert.False(TargetFilterEvaluator.Matches(filter, Dragon(2)));
    }

    [Fact]
    public void Matches_ControllerSide_ComparesCandidateAgainstFilterOwner()
    {
        var filter = new TargetFilter(1, new IReadOnlyList<FilterCondition>[]
        {
            new[] { new FilterCondition(FilterConditionKind.ControllerSide, false, "Opponent") }
        });

        Assert.True(TargetFilterEvaluator.Matches(filter, Dragon(), PlayerSide.Cpu, PlayerSide.Human));
        Assert.False(TargetFilterEvaluator.Matches(filter, Dragon(), PlayerSide.Human, PlayerSide.Human));
    }
}
