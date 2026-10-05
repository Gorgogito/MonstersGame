using MonstersGame.Core.Entities;
using MonstersGame.Core.Rules;
using MonstersGame.Tests.TestSupport;

namespace MonstersGame.Tests;

public class FlipSummonTests
{
    [Fact]
    public void FlipSummon_OnASetMonsterFromAnEarlierTurn_FlipsItToAttack()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong, BattlePosition.DefenseFaceDown);

        var result = engine.FlipSummon(0);

        Assert.True(result.Success);
        Assert.Equal(BattlePosition.Attack, human.MonsterZones[0]!.Position);
    }

    [Fact]
    public void FlipSummon_OnAMonsterSetThisSameTurn_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.Level4Strong);
        engine.SetMonster(0);

        var result = engine.FlipSummon(0);

        Assert.False(result.Success);
        Assert.Equal(BattlePosition.DefenseFaceDown, human.MonsterZones[0]!.Position);
    }

    [Fact]
    public void FlipSummon_OnAFaceUpMonster_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong, BattlePosition.Attack);

        var result = engine.FlipSummon(0);

        Assert.False(result.Success);
    }

    [Fact]
    public void FlipSummon_OnEmptyZone_Fails()
    {
        var (engine, _, _) = TestDuelFactory.Create();

        var result = engine.FlipSummon(0);

        Assert.False(result.Success);
    }

    [Fact]
    public void FlipSummon_CountsAsTheTurnPositionChange_BlockingChangePosition()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong, BattlePosition.DefenseFaceDown);
        engine.FlipSummon(0);

        var reposition = engine.ChangePosition(0, BattlePosition.DefenseFaceUp);

        Assert.False(reposition.Success);
    }

    [Fact]
    public void FlipSummon_ThenAttack_IsAllowedTheSameTurn()
    {
        var config = new DuelConfig { FirstPlayerSkipsFirstBattle = false };
        var (engine, human, cpu) = TestDuelFactory.Create(config);
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong, BattlePosition.DefenseFaceDown);
        engine.FlipSummon(0);
        engine.AdvancePhase(); // Main1 -> Battle

        var attack = engine.DeclareAttack(0, -1);

        Assert.True(attack.Success);
        Assert.Equal(8000 - 1500, cpu.LifePoints);
    }

    [Fact]
    public void ChangePosition_OnAFaceDownMonster_IsRejected_MustUseFlipSummon()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong, BattlePosition.DefenseFaceDown);

        var result = engine.ChangePosition(0, BattlePosition.Attack);

        Assert.False(result.Success);
        Assert.Equal(BattlePosition.DefenseFaceDown, human.MonsterZones[0]!.Position);
    }
}
