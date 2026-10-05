using GodotGame.Core.AI;
using GodotGame.Core.Battle;
using GodotGame.Core.Entities;
using GodotGame.Core.Rules;
using GodotGame.Core.Services;
using GodotGame.Tests.TestSupport;
using Xunit;

namespace GodotGame.Tests;

public class GuardianStarTests
{
    private static (DuelEngine Engine, Player Human, Player Cpu) CreateInBattlePhase()
    {
        var config = new DuelConfig { FirstPlayerSkipsFirstBattle = false };
        var (engine, human, cpu) = TestDuelFactory.Create(config);
        engine.AdvancePhase(); // Main1 -> Battle
        return (engine, human, cpu);
    }

    private static MonsterCard Monster(int id, int atk, int def, GuardianStar first, GuardianStar second) =>
        new(id, $"Monstruo {id}", atk, def, level: 4, "Warrior", MonsterAttribute.Earth,
            guardianStar1: first, guardianStar2: second);

    [Theory]
    [InlineData(GuardianStar.Sun, GuardianStar.Moon)]
    [InlineData(GuardianStar.Moon, GuardianStar.Venus)]
    [InlineData(GuardianStar.Venus, GuardianStar.Mercury)]
    [InlineData(GuardianStar.Mercury, GuardianStar.Sun)]
    [InlineData(GuardianStar.Mars, GuardianStar.Jupiter)]
    [InlineData(GuardianStar.Jupiter, GuardianStar.Saturn)]
    [InlineData(GuardianStar.Saturn, GuardianStar.Uranus)]
    [InlineData(GuardianStar.Uranus, GuardianStar.Pluto)]
    [InlineData(GuardianStar.Pluto, GuardianStar.Neptune)]
    [InlineData(GuardianStar.Neptune, GuardianStar.Mars)]
    public void Beats_FollowsBothCycles(GuardianStar winner, GuardianStar loser)
    {
        Assert.True(GuardianStars.Beats(winner, loser));
        Assert.False(GuardianStars.Beats(loser, winner));
        Assert.Equal(loser, GuardianStars.BeatsWhich(winner));
    }

    [Fact]
    public void Beats_StarsOfDifferentCyclesNeverBeatEachOther()
    {
        Assert.False(GuardianStars.Beats(GuardianStar.Sun, GuardianStar.Mars));
        Assert.False(GuardianStars.Beats(GuardianStar.Mars, GuardianStar.Sun));
        Assert.False(GuardianStars.Beats(GuardianStar.Sun, GuardianStar.Sun));
    }

    [Fact]
    public void MonsterCard_WithoutStars_UsesAttributeDefaults()
    {
        var card = new MonsterCard(1, "Sin estrellas", 1000, 1000, 4, "Warrior", MonsterAttribute.Light);

        Assert.Equal(GuardianStars.DefaultsFor(MonsterAttribute.Light).First, card.GuardianStar1);
        Assert.Equal(GuardianStars.DefaultsFor(MonsterAttribute.Light).Second, card.GuardianStar2);
        Assert.Equal(card.GuardianStar1, new CardInstance(card, BattlePosition.Attack).GuardianStar);
    }

    [Fact]
    public void StarAdvantage_AddsBonusToAttacker_AndCanTurnTheBattle()
    {
        var (engine, human, cpu) = CreateInBattlePhase();
        TestDuelFactory.PlaceOnField(human, 0, Monster(9001, 1200, 1000, GuardianStar.Sun, GuardianStar.Mars));
        TestDuelFactory.PlaceOnField(cpu, 0, Monster(9002, 1500, 1000, GuardianStar.Moon, GuardianStar.Pluto), BattlePosition.Attack);

        engine.DeclareAttack(0, 0);

        // 1200 + 500 = 1700 > 1500: sin la estrella, el atacante habria perdido.
        Assert.Null(cpu.MonsterZones[0]);
        Assert.NotNull(human.MonsterZones[0]);
        Assert.Equal(8000 - 200, cpu.LifePoints);

        var attack = engine.State.LastAttack!.Value;
        Assert.Equal(1700, attack.AttackerValue);
        Assert.Equal(GuardianStars.Bonus, attack.AttackerStarBonus);
        Assert.Equal(0, attack.DefenderStarBonus);
        Assert.Equal(GuardianStar.Moon, attack.DefenderStar);
    }

    [Fact]
    public void StarAdvantage_AddsBonusToDefenderDefense()
    {
        var (engine, human, cpu) = CreateInBattlePhase();
        TestDuelFactory.PlaceOnField(human, 0, Monster(9001, 1400, 1000, GuardianStar.Moon, GuardianStar.Mars));
        TestDuelFactory.PlaceOnField(cpu, 0, Monster(9002, 500, 1200, GuardianStar.Sun, GuardianStar.Pluto), BattlePosition.DefenseFaceUp);

        engine.DeclareAttack(0, 0);

        // DEF 1200 + 500 = 1700 > ATK 1400: el defensor aguanta y el atacante recibe 300.
        Assert.NotNull(cpu.MonsterZones[0]);
        Assert.Equal(8000 - 300, human.LifePoints);
        Assert.Equal(GuardianStars.Bonus, engine.State.LastAttack!.Value.DefenderStarBonus);
    }

    [Fact]
    public void ChooseGuardianStar_OnlyOneOfTheCardsStars_OnlyOnceAndOnlyWhenJustSummoned()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        var instance = TestDuelFactory.PlaceOnField(human, 0, Monster(9001, 1000, 1000, GuardianStar.Sun, GuardianStar.Mars));

        Assert.False(engine.ChooseGuardianStar(PlayerSide.Human, 0, GuardianStar.Mars).Success); // no se invoco este turno

        instance.SummonedThisTurn = true;
        Assert.False(engine.ChooseGuardianStar(PlayerSide.Human, 0, GuardianStar.Moon).Success); // no es suya
        Assert.True(engine.ChooseGuardianStar(PlayerSide.Human, 0, GuardianStar.Mars).Success);
        Assert.Equal(GuardianStar.Mars, instance.GuardianStar);
        Assert.False(engine.ChooseGuardianStar(PlayerSide.Human, 0, GuardianStar.Sun).Success); // ya eligio
    }

    [Fact]
    public void BestAgainst_PicksTheStarThatBeatsVisibleOpponents()
    {
        var card = Monster(9001, 1000, 1000, GuardianStar.Sun, GuardianStar.Mars);
        var opponentMoon = new CardInstance(Monster(9002, 1000, 1000, GuardianStar.Jupiter, GuardianStar.Moon), BattlePosition.Attack)
        {
            GuardianStar = GuardianStar.Jupiter
        };

        Assert.Equal(GuardianStar.Mars, GuardianStars.BestAgainst(card, new[] { opponentMoon }));
        Assert.Equal(GuardianStar.Sun, GuardianStars.BestAgainst(card, Array.Empty<CardInstance>()));
    }

    [Fact]
    public void CpuAi_ChoosesTheBestStarForItsJustSummonedMonster()
    {
        var (engine, human, cpu) = TestDuelFactory.Create();
        var opponent = TestDuelFactory.PlaceOnField(human, 0, Monster(9002, 1000, 1000, GuardianStar.Jupiter, GuardianStar.Moon));
        var mine = TestDuelFactory.PlaceOnField(cpu, 0, Monster(9001, 1000, 1000, GuardianStar.Sun, GuardianStar.Mars));
        mine.SummonedThisTurn = true;

        bool acted = new BasicCpuAI(new FusionService(Array.Empty<FusionRecipe>(), new CardDatabase(TestCards.All))).Step(engine, 1);

        Assert.True(acted);
        Assert.True(mine.GuardianStarChosen);
        Assert.Equal(GuardianStar.Mars, mine.GuardianStar); // Marte vence a Jupiter
        Assert.Equal(GuardianStar.Jupiter, opponent.GuardianStar);
    }
}
