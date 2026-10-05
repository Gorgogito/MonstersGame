using GodotGame.Core.Entities;
using GodotGame.Core.Rules;
using GodotGame.Tests.TestSupport;

namespace GodotGame.Tests;

public class EdgeCaseTests
{
    [Fact]
    public void Tribute_ToAnEmptyZone_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.Level6OneTribute);

        var result = engine.NormalSummon(0, BattlePosition.Attack, tributeZones: new[] { 2 });

        Assert.False(result.Success);
        Assert.Single(human.Hand);
    }

    [Fact]
    public void SetMonster_AtLevel5OrAbove_AlsoRequiresTribute()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.Level6OneTribute);

        // Sin monstruos en el campo no hay nada que sacrificar: el
        // auto-seleccionador de tributos no encuentra las zonas requeridas.
        var withoutTribute = engine.SetMonster(0);
        Assert.False(withoutTribute.Success);
        Assert.Single(human.Hand);

        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Weak);
        var withTribute = engine.SetMonster(0, tributeZones: new[] { 0 });

        Assert.True(withTribute.Success);
        Assert.Equal(BattlePosition.DefenseFaceDown, human.MonsterZones[0]!.Position);
    }

    [Fact]
    public void NormalSummon_InvalidHandIndex_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.Level4Strong);

        var result = engine.NormalSummon(5, BattlePosition.Attack);

        Assert.False(result.Success);
    }

    [Fact]
    public void DeclareAttack_WithDefendingMonster_Fails()
    {
        var config = new DuelConfig { FirstPlayerSkipsFirstBattle = false };
        var (engine, human, _) = TestDuelFactory.Create(config);
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong, BattlePosition.DefenseFaceUp);
        engine.AdvancePhase();

        var result = engine.DeclareAttack(0, -1);

        Assert.False(result.Success);
    }

    [Fact]
    public void DeclareAttack_FromEmptyZone_Fails()
    {
        var config = new DuelConfig { FirstPlayerSkipsFirstBattle = false };
        var (engine, _, _) = TestDuelFactory.Create(config);
        engine.AdvancePhase();

        var result = engine.DeclareAttack(0, -1);

        Assert.False(result.Success);
    }

    [Fact]
    public void MonsterZones_AreCappedAtFive()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        for (int i = 0; i < 5; i++)
            TestDuelFactory.PlaceOnField(human, i, TestCards.Level4Weak);

        Assert.Equal(-1, human.FirstFreeMonsterZone());
        Assert.Equal(5, human.MonsterCount);
    }
}
