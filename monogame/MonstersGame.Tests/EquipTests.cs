using MonstersGame.Core.Battle;
using MonstersGame.Core.Effects;
using MonstersGame.Core.Entities;
using MonstersGame.Core.Requirements;
using MonstersGame.Tests.TestSupport;

namespace MonstersGame.Tests;

public class EquipTests
{
    private static readonly TargetFilter DragonOnly = new(1, new IReadOnlyList<FilterCondition>[]
    {
        new[] { new FilterCondition(FilterConditionKind.Type, false, "Dragon") }
    });

    private static readonly TargetFilter OpponentOnly = new(2, new IReadOnlyList<FilterCondition>[]
    {
        new[] { new FilterCondition(FilterConditionKind.ControllerSide, false, "Opponent") }
    });

    [Fact]
    public void Activate_WithTargetFilter_RejectsNonMatchingTarget()
    {
        var equip = new SpellCard(9001, "Solo para Dragones", SpellSubType.Equip, equipTargetFilter: DragonOnly, equipAttackModifier: 500);
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(equip);
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong); // Warrior, no Dragon

        var result = engine.ActivateSpell(0, new EffectTarget { Side = PlayerSide.Human, ZoneIndex = 0 });

        Assert.False(result.Success);
    }

    [Fact]
    public void Activate_WithTargetFilter_AcceptsMatchingTarget()
    {
        var equip = new SpellCard(9002, "Solo para Dragones", SpellSubType.Equip, equipTargetFilter: DragonOnly, equipAttackModifier: 500);
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(equip);
        var dragon = TestDuelFactory.PlaceOnField(human, 0, TestCards.FusionMaterialA); // Type = Dragon

        var result = engine.ActivateSpell(0, new EffectTarget { Side = PlayerSide.Human, ZoneIndex = 0 });
        TestDuelFactory.CloseChain(engine);

        Assert.True(result.Success);
        Assert.Equal(TestCards.FusionMaterialA.Attack + 500, EffectiveStats.EffectiveAttack(dragon, engine.State, human));
    }

    [Fact]
    public void Activate_WithOpponentFilter_CanEquipRivalsMonster()
    {
        var equip = new SpellCard(9003, "Grillete", SpellSubType.Equip, equipTargetFilter: OpponentOnly, equipAttackModifier: -500);
        var (engine, human, cpu) = TestDuelFactory.Create();
        human.Hand.Add(equip);
        var rival = TestDuelFactory.PlaceOnField(cpu, 0, TestCards.Level4Strong);

        var result = engine.ActivateSpell(0, new EffectTarget { Side = PlayerSide.Cpu, ZoneIndex = 0 });
        TestDuelFactory.CloseChain(engine);

        Assert.True(result.Success);
        Assert.Equal(TestCards.Level4Strong.Attack - 500, EffectiveStats.EffectiveAttack(rival, engine.State, human));
    }

    [Fact]
    public void Resolve_NegativeModifiers_ReduceEffectiveStats()
    {
        var equip = new SpellCard(9004, "Cadenas", SpellSubType.Equip, equipAttackModifier: -300, equipDefenseModifier: -200);
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(equip);
        var monster = TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong);

        engine.ActivateSpell(0, new EffectTarget { Side = PlayerSide.Human, ZoneIndex = 0 });
        TestDuelFactory.CloseChain(engine);

        Assert.Equal(TestCards.Level4Strong.Attack - 300, EffectiveStats.EffectiveAttack(monster, engine.State, human));
        Assert.Equal(TestCards.Level4Strong.Defense - 200, EffectiveStats.EffectiveDefense(monster, engine.State, human));
    }

    [Fact]
    public void Modifier_UntilEndOfTurn_IsRemovedWhenTheControllersTurnEnds()
    {
        var equip = new SpellCard(9005, "Impulso Temporal", SpellSubType.Equip,
            equipAttackModifier: 700, equipDuration: ModifierDuration.UntilEndOfTurn);
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(equip);
        var monster = TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong);

        engine.ActivateSpell(0, new EffectTarget { Side = PlayerSide.Human, ZoneIndex = 0 });
        TestDuelFactory.CloseChain(engine);
        Assert.Equal(TestCards.Level4Strong.Attack + 700, EffectiveStats.EffectiveAttack(monster, engine.State, human));

        engine.EndTurn(); // Fase Final del turno de Human: el modificador temporal expira aqui

        Assert.Equal(TestCards.Level4Strong.Attack, EffectiveStats.EffectiveAttack(monster, engine.State, human));
        // La Magia de Equipo en si sigue en el Campo (solo expira el modificador, no la carta).
        Assert.Contains(human.SpellTrapZones, z => z?.Card == equip);
    }

    [Fact]
    public void Modifier_ForNTurns_SurvivesUntilTheConfiguredNumberOfTheControllersTurnsPass()
    {
        var equip = new SpellCard(9006, "Impulso de Dos Turnos", SpellSubType.Equip,
            equipAttackModifier: 400, equipDuration: ModifierDuration.ForNTurns, equipDurationTurns: 2);
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(equip);
        var monster = TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong);

        engine.ActivateSpell(0, new EffectTarget { Side = PlayerSide.Human, ZoneIndex = 0 });
        TestDuelFactory.CloseChain(engine);

        engine.EndTurn(); // fin del turno 1 de Human (cuenta 1 de 2) -- turno de Cpu
        engine.EndTurn(); // fin del turno de Cpu -- no descuenta (no es su modificador)
        Assert.Equal(TestCards.Level4Strong.Attack + 400, EffectiveStats.EffectiveAttack(monster, engine.State, human));

        engine.EndTurn(); // fin del 2do turno de Human (cuenta 2 de 2): expira
        engine.EndTurn(); // turno de Cpu

        Assert.Equal(TestCards.Level4Strong.Attack, EffectiveStats.EffectiveAttack(monster, engine.State, human));
    }

    [Fact]
    public void Modifier_WhileEquipped_SurvivesAcrossManyTurns()
    {
        var equip = new SpellCard(9007, "Permanente", SpellSubType.Equip, equipAttackModifier: 500);
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(equip);
        var monster = TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong);

        engine.ActivateSpell(0, new EffectTarget { Side = PlayerSide.Human, ZoneIndex = 0 });
        TestDuelFactory.CloseChain(engine);

        for (int i = 0; i < 6; i++) engine.EndTurn();

        Assert.Equal(TestCards.Level4Strong.Attack + 500, EffectiveStats.EffectiveAttack(monster, engine.State, human));
    }
}
