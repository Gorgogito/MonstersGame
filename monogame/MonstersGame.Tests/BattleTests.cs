using MonstersGame.Core.Battle;
using MonstersGame.Core.Entities;
using MonstersGame.Core.Rules;
using MonstersGame.Tests.TestSupport;

namespace MonstersGame.Tests;

public class BattleTests
{
    private static (DuelEngine Engine, Player Human, Player Cpu) CreateInBattlePhase()
    {
        var config = new DuelConfig { FirstPlayerSkipsFirstBattle = false };
        var (engine, human, cpu) = TestDuelFactory.Create(config);
        engine.AdvancePhase(); // Main1 -> Battle
        return (engine, human, cpu);
    }

    [Fact]
    public void AttackVsAttack_HigherAtkWins_DealsExcessDamage()
    {
        var (engine, human, cpu) = CreateInBattlePhase();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong); // 1500
        TestDuelFactory.PlaceOnField(cpu, 0, TestCards.Level4Weak, BattlePosition.Attack); // 1000

        var result = engine.DeclareAttack(0, 0);

        Assert.True(result.Success);
        Assert.Null(cpu.MonsterZones[0]);
        Assert.NotNull(human.MonsterZones[0]);
        Assert.Equal(8000 - 500, cpu.LifePoints);
        Assert.Equal(8000, human.LifePoints);
    }

    [Fact]
    public void AttackVsAttack_Tie_DestroysBothNoDamage()
    {
        var (engine, human, cpu) = CreateInBattlePhase();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Weak);
        TestDuelFactory.PlaceOnField(cpu, 0, TestCards.Level4Weak, BattlePosition.Attack);

        var result = engine.DeclareAttack(0, 0);

        Assert.True(result.Success);
        Assert.Null(human.MonsterZones[0]);
        Assert.Null(cpu.MonsterZones[0]);
        Assert.Equal(8000, human.LifePoints);
        Assert.Equal(8000, cpu.LifePoints);
    }

    [Fact]
    public void AttackVsAttack_LowerAtkLoses_AttackerDestroyedAndDamaged()
    {
        var (engine, human, cpu) = CreateInBattlePhase();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Weak); // 1000
        TestDuelFactory.PlaceOnField(cpu, 0, TestCards.Level4Strong, BattlePosition.Attack); // 1500

        var result = engine.DeclareAttack(0, 0);

        Assert.True(result.Success);
        Assert.Null(human.MonsterZones[0]);
        Assert.NotNull(cpu.MonsterZones[0]);
        Assert.Equal(8000 - 500, human.LifePoints);
        Assert.Equal(8000, cpu.LifePoints);
    }

    [Fact]
    public void AttackVsDefense_HigherAtkWins_DestroysDefenderNoDamage()
    {
        var (engine, human, cpu) = CreateInBattlePhase();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level6OneTribute); // ATK 2200
        TestDuelFactory.PlaceOnField(cpu, 0, TestCards.Level4Weak, BattlePosition.DefenseFaceUp); // DEF 1000 (menor al ATK del atacante)

        var result = engine.DeclareAttack(0, 0);

        Assert.True(result.Success);
        Assert.Null(cpu.MonsterZones[0]);
        Assert.Equal(8000, human.LifePoints);
        Assert.Equal(8000, cpu.LifePoints);
    }

    [Fact]
    public void AttackVsDefense_Tie_NobodyDestroyedNoDamage()
    {
        var (engine, human, cpu) = CreateInBattlePhase();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong); // ATK 1500
        var defenderCard = new MonsterCard(9001, "Defensor Igualado", attack: 100, defense: 1500, level: 4, "Rock", MonsterAttribute.Earth);
        TestDuelFactory.PlaceOnField(cpu, 0, defenderCard, BattlePosition.DefenseFaceUp);

        var result = engine.DeclareAttack(0, 0);

        Assert.True(result.Success);
        Assert.NotNull(human.MonsterZones[0]);
        Assert.NotNull(cpu.MonsterZones[0]);
        Assert.Equal(8000, human.LifePoints);
        Assert.Equal(8000, cpu.LifePoints);
    }

    [Fact]
    public void AttackVsDefense_LowerAtkThanDef_NobodyDestroyed_AttackerTakesDamage()
    {
        var (engine, human, cpu) = CreateInBattlePhase();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong); // ATK 1500
        TestDuelFactory.PlaceOnField(cpu, 0, TestCards.HighDefense, BattlePosition.DefenseFaceUp); // DEF 2500

        var result = engine.DeclareAttack(0, 0);

        Assert.True(result.Success);
        Assert.NotNull(human.MonsterZones[0]);
        Assert.NotNull(cpu.MonsterZones[0]);
        Assert.Equal(8000 - 1000, human.LifePoints);
        Assert.Equal(8000, cpu.LifePoints);
    }

    [Fact]
    public void ZeroAttack_VsZeroAttack_NeitherIsDestroyed()
    {
        var (engine, human, cpu) = CreateInBattlePhase();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.ZeroAttack);
        TestDuelFactory.PlaceOnField(cpu, 0, TestCards.ZeroAttack, BattlePosition.Attack);

        var result = engine.DeclareAttack(0, 0);

        Assert.True(result.Success);
        Assert.NotNull(human.MonsterZones[0]);
        Assert.NotNull(cpu.MonsterZones[0]);
    }

    [Fact]
    public void DirectAttack_WhenOpponentHasNoMonsters_DealsFullAttackAsDamage()
    {
        var (engine, human, cpu) = CreateInBattlePhase();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong); // 1500

        var result = engine.DeclareAttack(0, -1);

        Assert.True(result.Success);
        Assert.Equal(8000 - 1500, cpu.LifePoints);
    }

    [Fact]
    public void DirectAttack_WhenOpponentHasMonsters_IsRejected()
    {
        var (engine, human, cpu) = CreateInBattlePhase();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong);
        TestDuelFactory.PlaceOnField(cpu, 0, TestCards.Level4Weak, BattlePosition.Attack);

        var result = engine.DeclareAttack(0, -1);

        Assert.False(result.Success);
        Assert.Equal(8000, cpu.LifePoints);
    }

    [Fact]
    public void Monster_CannotAttackTwiceInSameTurn()
    {
        var (engine, human, cpu) = CreateInBattlePhase();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong);

        var first = engine.DeclareAttack(0, -1);
        var second = engine.DeclareAttack(0, -1);

        Assert.True(first.Success);
        Assert.False(second.Success);
    }

    [Fact]
    public void DeclareAttack_TargetedAttack_RecordsLastAttackWithDestructionOutcome()
    {
        var (engine, human, cpu) = CreateInBattlePhase();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong); // 1500
        TestDuelFactory.PlaceOnField(cpu, 0, TestCards.Level4Weak, BattlePosition.Attack); // 1000

        engine.DeclareAttack(0, 0);

        var attack = engine.State.LastAttack;
        Assert.NotNull(attack);
        Assert.Equal(0, attack!.Value.AttackerZone);
        Assert.Equal(0, attack.Value.DefenderZone);
        Assert.Equal(TestCards.Level4Strong.Id, attack.Value.AttackerCard.Id);
        Assert.Equal(TestCards.Level4Weak.Id, attack.Value.DefenderCard?.Id);
        Assert.False(attack.Value.AttackerDestroyed);
        Assert.True(attack.Value.DefenderDestroyed);
    }

    [Fact]
    public void DeclareAttack_DirectAttack_RecordsLastAttackWithoutDefender()
    {
        var (engine, human, cpu) = CreateInBattlePhase();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong);

        engine.DeclareAttack(0, -1);

        var attack = engine.State.LastAttack;
        Assert.NotNull(attack);
        Assert.Equal(-1, attack!.Value.DefenderZone);
        Assert.Null(attack.Value.DefenderCard);
        Assert.Null(attack.Value.DefenderPosition);
        Assert.False(attack.Value.DefenderDestroyed);
    }

    [Fact]
    public void DeclareAttack_AgainstFaceDownDefender_LastAttackReportsFlippedPosition()
    {
        var (engine, human, cpu) = CreateInBattlePhase();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level6OneTribute); // ATK 2200
        TestDuelFactory.PlaceOnField(cpu, 0, TestCards.Level4Weak, BattlePosition.DefenseFaceDown);

        engine.DeclareAttack(0, 0);

        var attack = engine.State.LastAttack;
        Assert.NotNull(attack);
        Assert.Equal(BattlePosition.DefenseFaceUp, attack!.Value.DefenderPosition);
        Assert.True(attack.Value.DefenderDestroyed);
    }
}
