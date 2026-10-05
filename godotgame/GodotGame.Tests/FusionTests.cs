using GodotGame.Core.Entities;
using GodotGame.Tests.TestSupport;

namespace GodotGame.Tests;

public class FusionTests
{
    [Fact]
    public void Fuse_WithValidRecipe_ProducesResultAndSendsMaterialsToGraveyard()
    {
        var (engine, human, _) = TestDuelFactory.Create(recipes: new[] { TestCards.ValidFusionRecipe });
        human.Hand.Add(TestCards.FusionMaterialA);
        human.Hand.Add(TestCards.FusionMaterialB);

        var result = engine.Fuse(0, 1);

        Assert.True(result.Success);
        Assert.Empty(human.Hand);
        Assert.Contains(TestCards.FusionMaterialA, human.Graveyard);
        Assert.Contains(TestCards.FusionMaterialB, human.Graveyard);
        Assert.Equal(TestCards.FusionResult, human.MonsterZones[0]!.Card);
    }

    [Fact]
    public void Fuse_WithoutMatchingRecipe_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create(); // sin recetas registradas
        human.Hand.Add(TestCards.FusionMaterialA);
        human.Hand.Add(TestCards.FusionMaterialB);

        var result = engine.Fuse(0, 1);

        Assert.False(result.Success);
        Assert.Equal(2, human.Hand.Count); // ninguna carta se consume si la fusion falla
        Assert.Empty(human.Graveyard);
    }

    [Fact]
    public void Fuse_SameCardTwice_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create(recipes: new[] { TestCards.ValidFusionRecipe });
        human.Hand.Add(TestCards.FusionMaterialA);

        var result = engine.Fuse(0, 0);

        Assert.False(result.Success);
    }

    [Fact]
    public void Fuse_CountsAsTheTurnMonsterPlay()
    {
        var (engine, human, _) = TestDuelFactory.Create(recipes: new[] { TestCards.ValidFusionRecipe });
        human.Hand.Add(TestCards.FusionMaterialA);
        human.Hand.Add(TestCards.FusionMaterialB);
        human.Hand.Add(TestCards.Level4Strong);

        var fuse = engine.Fuse(0, 1);
        var summon = engine.NormalSummon(0, BattlePosition.Attack); // la unica carta restante en mano

        Assert.True(fuse.Success);
        Assert.False(summon.Success);
    }

    [Fact]
    public void Fuse_RecipeOrderIsIrrelevant()
    {
        var (engine, human, _) = TestDuelFactory.Create(recipes: new[] { TestCards.ValidFusionRecipe });
        human.Hand.Add(TestCards.FusionMaterialB);
        human.Hand.Add(TestCards.FusionMaterialA);

        var result = engine.Fuse(0, 1);

        Assert.True(result.Success);
        Assert.Equal(TestCards.FusionResult, human.MonsterZones[0]!.Card);
    }
}
