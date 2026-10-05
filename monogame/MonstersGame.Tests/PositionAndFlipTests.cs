using MonstersGame.Core.Entities;
using MonstersGame.Core.Rules;
using MonstersGame.Tests.TestSupport;

namespace MonstersGame.Tests;

public class PositionAndFlipTests
{
    [Fact]
    public void ChangePosition_OnMonsterSummonedThisTurn_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.Level4Strong);
        engine.NormalSummon(0, BattlePosition.Attack);

        var result = engine.ChangePosition(0, BattlePosition.DefenseFaceUp);

        Assert.False(result.Success);
    }

    [Fact]
    public void ChangePosition_OnALaterTurn_Succeeds()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.Level4Strong);
        engine.NormalSummon(0, BattlePosition.Attack);

        engine.EndTurn(); // turno de la CPU
        engine.EndTurn(); // vuelve al humano: se resetean las banderas de turno

        var result = engine.ChangePosition(0, BattlePosition.DefenseFaceUp);

        Assert.True(result.Success);
        Assert.Equal(BattlePosition.DefenseFaceUp, human.MonsterZones[0]!.Position);
    }

    [Fact]
    public void ChangePosition_Twice_SameTurn_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong);

        var first = engine.ChangePosition(0, BattlePosition.DefenseFaceUp);
        var second = engine.ChangePosition(0, BattlePosition.Attack);

        Assert.True(first.Success);
        Assert.False(second.Success);
    }

    [Fact]
    public void ChangePosition_ToDefenseFaceDown_IsRejected()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong);

        var result = engine.ChangePosition(0, BattlePosition.DefenseFaceDown);

        Assert.False(result.Success);
    }

    [Fact]
    public void ChangePosition_AfterAttacking_Fails()
    {
        var config = new DuelConfig { FirstPlayerSkipsFirstBattle = false };
        var (engine, human, cpu) = TestDuelFactory.Create(config);
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong);
        engine.AdvancePhase(); // Main1 -> Battle

        var attack = engine.DeclareAttack(0, -1); // el rival no controla monstruos: ataque directo
        var reposition = engine.ChangePosition(0, BattlePosition.DefenseFaceUp);

        Assert.True(attack.Success);
        Assert.False(reposition.Success);
    }

    [Fact]
    public void Attacking_FaceDownDefender_FlipsItFaceUpBeforeDamage()
    {
        var config = new DuelConfig { FirstPlayerSkipsFirstBattle = false };
        var (engine, human, cpu) = TestDuelFactory.Create(config);
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong); // 1500/1200
        TestDuelFactory.PlaceOnField(cpu, 0, TestCards.HighDefense, BattlePosition.DefenseFaceDown); // 300/2500
        engine.AdvancePhase(); // Main1 -> Battle

        var result = engine.DeclareAttack(0, 0);

        Assert.True(result.Success);
        // ATK 1500 < DEF 2500: el defensor se voltea pero no es destruido, el
        // atacante recibe la diferencia como daño de batalla.
        Assert.Equal(BattlePosition.DefenseFaceUp, cpu.MonsterZones[0]!.Position);
        Assert.NotNull(cpu.MonsterZones[0]);
        Assert.Equal(8000 - 1000, human.LifePoints);
    }
}
