using GodotGame.Core.Entities;
using GodotGame.Core.Rules;
using GodotGame.Tests.TestSupport;

namespace GodotGame.Tests;

public class SummoningTests
{
    [Fact]
    public void NormalSummon_Level4_RequiresNoTribute()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.Level4Strong);

        var result = engine.NormalSummon(0, BattlePosition.Attack);

        Assert.True(result.Success);
        Assert.Empty(human.Hand);
        Assert.Equal(TestCards.Level4Strong, human.MonsterZones[0]!.Card);
        Assert.Equal(BattlePosition.Attack, human.MonsterZones[0]!.Position);
    }

    [Fact]
    public void NormalSummon_Level6_WithoutTribute_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.Level6OneTribute);

        var result = engine.NormalSummon(0, BattlePosition.Attack);

        Assert.False(result.Success);
        Assert.Single(human.Hand); // la carta no se juega si el sacrificio falla
    }

    [Fact]
    public void NormalSummon_Level6_WithOneTribute_Succeeds()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Weak);
        human.Hand.Add(TestCards.Level6OneTribute);

        var result = engine.NormalSummon(0, BattlePosition.Attack, tributeZones: new[] { 0 });

        Assert.True(result.Success);
        Assert.Contains(TestCards.Level4Weak, human.Graveyard);
        Assert.Equal(TestCards.Level6OneTribute, human.MonsterZones[0]!.Card);
    }

    [Fact]
    public void NormalSummon_Level8_WithOnlyOneTribute_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Weak);
        human.Hand.Add(TestCards.Level8TwoTributes);

        var result = engine.NormalSummon(0, BattlePosition.Attack, tributeZones: new[] { 0 });

        Assert.False(result.Success);
        Assert.NotNull(human.MonsterZones[0]); // el sacrificio no se consume si falla
    }

    [Fact]
    public void NormalSummon_Level8_WithTwoTributes_Succeeds()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Weak);
        TestDuelFactory.PlaceOnField(human, 1, TestCards.Level4Strong);
        human.Hand.Add(TestCards.Level8TwoTributes);

        var result = engine.NormalSummon(0, BattlePosition.Attack, tributeZones: new[] { 0, 1 });

        Assert.True(result.Success);
        Assert.Equal(2, human.Graveyard.Count);
        Assert.Equal(TestCards.Level8TwoTributes, human.MonsterZones[0]!.Card);
    }

    [Fact]
    public void NormalSummonOrSet_OnlyOncePerTurn()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.Level4Strong);
        human.Hand.Add(TestCards.Level4Weak);

        var first = engine.NormalSummon(0, BattlePosition.Attack);
        var second = engine.SetMonster(0);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Single(human.Hand); // la segunda carta nunca se jugo
    }

    [Fact]
    public void SetMonster_PutsCardFaceDownInDefense()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.Level4Strong);

        var result = engine.SetMonster(0);

        Assert.True(result.Success);
        Assert.Equal(BattlePosition.DefenseFaceDown, human.MonsterZones[0]!.Position);
    }

    [Fact]
    public void NormalSummon_NoFreeMonsterZone_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        for (int i = 0; i < 5; i++)
            TestDuelFactory.PlaceOnField(human, i, TestCards.Level4Weak);
        human.Hand.Add(TestCards.Level4Strong);

        var result = engine.NormalSummon(0, BattlePosition.Attack);

        Assert.False(result.Success);
        Assert.Single(human.Hand);
    }

    [Fact]
    public void NormalSummon_OutsideMainPhase_Fails()
    {
        // FirstPlayerSkipsFirstBattle = false para poder alcanzar la Battle
        // Phase en el turno 1 y probar la validacion de fase.
        var config = new DuelConfig { FirstPlayerSkipsFirstBattle = false };
        var (engine, human, _) = TestDuelFactory.Create(config);
        human.Hand.Add(TestCards.Level4Strong);
        engine.AdvancePhase(); // Main1 -> Battle

        var result = engine.NormalSummon(0, BattlePosition.Attack);

        Assert.False(result.Success);
        Assert.Single(human.Hand);
    }
}
