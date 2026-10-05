using MonstersGame.Core.Battle;
using MonstersGame.Core.Effects;
using MonstersGame.Core.Entities;
using MonstersGame.Core.Rules;
using MonstersGame.Tests.TestSupport;

namespace MonstersGame.Tests;

/// <summary>
/// Verifica que cada accion del motor encola el <see cref="DuelEvent"/>
/// correcto en <see cref="DuelState.Events"/> -- la cola que la presentacion
/// (Fase 6) drena en vez de inferir por diff de estado.
/// </summary>
public class DuelEventTests
{
    private static T ExpectSingle<T>(DuelState state) where T : DuelEvent
    {
        var events = state.Events.ToList();
        return Assert.IsType<T>(Assert.Single(events));
    }

    [Fact]
    public void NormalSummon_EnqueuesMonsterSummonedEvent()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.Level4Strong);

        engine.NormalSummon(0, BattlePosition.Attack);

        var evt = ExpectSingle<MonsterSummonedEvent>(engine.State);
        Assert.Equal(PlayerSide.Human, evt.Side);
        Assert.Equal(0, evt.ZoneIndex);
        Assert.Equal(TestCards.Level4Strong, evt.Card);
        Assert.Equal(SummonKind.Normal, evt.Kind);
    }

    [Fact]
    public void NormalSummon_WithTribute_AlsoEnqueuesMonsterDestroyedEventForTheTribute()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Weak);
        human.Hand.Add(TestCards.Level6OneTribute);

        engine.NormalSummon(0, BattlePosition.Attack, tributeZones: new[] { 0 });

        var destroyed = engine.State.Events.OfType<MonsterDestroyedEvent>().Single();
        Assert.Equal(DestructionCause.Cost, destroyed.Cause);
        Assert.Equal(TestCards.Level4Weak, destroyed.Card);
        Assert.Contains(engine.State.Events, e => e is MonsterSummonedEvent);
    }

    [Fact]
    public void FlipSummon_EnqueuesMonsterSummonedEventWithFlipKind()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong, BattlePosition.DefenseFaceDown);

        engine.FlipSummon(0);

        var evt = ExpectSingle<MonsterSummonedEvent>(engine.State);
        Assert.Equal(SummonKind.Flip, evt.Kind);
    }

    [Fact]
    public void Fuse_EnqueuesFusionPerformedEventWithBothMaterials()
    {
        var (engine, human, _) = TestDuelFactory.Create(recipes: new[] { TestCards.ValidFusionRecipe });
        human.Hand.Add(TestCards.FusionMaterialA);
        human.Hand.Add(TestCards.FusionMaterialB);

        engine.Fuse(0, 1);

        var evt = ExpectSingle<FusionPerformedEvent>(engine.State);
        Assert.Equal(TestCards.FusionResult, evt.Result);
        Assert.Equal(2, evt.Materials.Count);
        Assert.Contains(TestCards.FusionMaterialA, evt.Materials);
        Assert.Contains(TestCards.FusionMaterialB, evt.Materials);
    }

    [Fact]
    public void RitualSummon_EnqueuesRitualPerformedEvent_AndCostEventForFieldTributes()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level6OneTribute); // Nivel 6, alcanza para RequiredRitualLevel=6
        human.Hand.Add(TestCards.RitualSummonSpell);
        human.Hand.Add(TestCards.RitualMonster);

        var result = engine.RitualSummon(0, 1, handTributeIndices: Array.Empty<int>(), fieldTributeZones: new[] { 0 });
        Assert.True(result.Success);

        Assert.Contains(engine.State.Events, e => e is RitualPerformedEvent r && r.Result == TestCards.RitualMonster);
        Assert.Contains(engine.State.Events, e => e is MonsterDestroyedEvent { Cause: DestructionCause.Cost } d && d.Card == TestCards.Level6OneTribute);
    }

    [Fact]
    public void ActivateSpell_EnqueuesSpellTrapActivatedEvent()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.DrawEffectSpell);

        engine.ActivateSpell(0);

        var evt = ExpectSingle<SpellTrapActivatedEvent>(engine.State);
        Assert.Equal(TestCards.DrawEffectSpell, evt.Card);
    }

    [Fact]
    public void ActivateField_EnqueuesFieldChangedEvent()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.FieldSpellA);

        engine.ActivateSpell(0);

        var evt = ExpectSingle<FieldChangedEvent>(engine.State);
        Assert.Null(evt.OldFieldTypeId);
    }

    [Fact]
    public void EquipResolution_EnqueuesStatModifierAppliedEvent()
    {
        var equip = new SpellCard(9201, "Equip de Prueba", SpellSubType.Equip, equipAttackModifier: 500, equipDefenseModifier: 200);
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(equip);
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong);

        engine.ActivateSpell(0, new EffectTarget { Side = PlayerSide.Human, ZoneIndex = 0 });
        TestDuelFactory.CloseChain(engine);

        var evt = engine.State.Events.OfType<StatModifierAppliedEvent>().Single();
        Assert.Equal(0, evt.ZoneIndex);
        Assert.Equal(500, evt.AttackAmount);
        Assert.Equal(200, evt.DefenseAmount);
    }

    [Fact]
    public void SpecialSummonFromGraveyard_EnqueuesMonsterSummonedEventWithSpecialKind()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Graveyard.Add(TestCards.Level4Strong);
        human.Hand.Add(TestCards.ReviveEffectSpell);

        engine.ActivateSpell(0, new EffectTarget { Side = PlayerSide.Human, ZoneIndex = 0 });
        TestDuelFactory.CloseChain(engine);

        var evt = engine.State.Events.OfType<MonsterSummonedEvent>().Single();
        Assert.Equal(SummonKind.Special, evt.Kind);
        Assert.Equal(TestCards.Level4Strong, evt.Card);
    }

    [Fact]
    public void DeclareAttack_DestroyingBothMonsters_EnqueuesMonsterDestroyedEventWithBattleCause_ForBoth()
    {
        // Fase 3 del plan de mejoras visuales: DestructionCause.Battle existia
        // en Core pero DuelEngine.DeclareAttack nunca lo encolaba -- la
        // destruccion en combate no tenia forma de reaccionar via DuelEvent,
        // a diferencia de Sacrificio/Efecto (ver los otros tests de esta
        // clase). Empate de ATK: ambos monstruos se destruyen en el mismo
        // ataque, asi que un solo DeclareAttack alcanza para verificar los
        // dos lados.
        var config = new DuelConfig { FirstPlayerSkipsFirstBattle = false };
        var (engine, human, cpu) = TestDuelFactory.Create(config);
        engine.AdvancePhase(); // Main1 -> Battle
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Weak);
        TestDuelFactory.PlaceOnField(cpu, 0, TestCards.Level4Weak, BattlePosition.Attack);

        var result = engine.DeclareAttack(0, 0);

        Assert.True(result.Success);
        var destroyed = engine.State.Events.OfType<MonsterDestroyedEvent>().ToList();
        Assert.Equal(2, destroyed.Count);
        Assert.All(destroyed, d => Assert.Equal(DestructionCause.Battle, d.Cause));
        Assert.Contains(destroyed, d => d.Side == PlayerSide.Human && d.Card == TestCards.Level4Weak);
        Assert.Contains(destroyed, d => d.Side == PlayerSide.Cpu && d.Card == TestCards.Level4Weak);
    }

    [Fact]
    public void DestroyTargetMonster_EnqueuesMonsterDestroyedEventWithEffectCause_AndDetachesItsEquip()
    {
        var (engine, human, cpu) = TestDuelFactory.Create();
        var equip = new SpellCard(9202, "Equip Victima", SpellSubType.Equip, equipAttackModifier: 300);
        var equipInstance = TestDuelFactory.PlaceSpellTrap(human, 0, equip, faceUp: true);
        var monster = TestDuelFactory.PlaceOnField(cpu, 0, TestCards.Level4Weak);
        monster.ActiveModifiers.Add(new ActiveStatModifier(300, 0, ModifierDuration.WhileEquipped, equipSource: equipInstance));
        equipInstance.EquippedMonsterRef = new EffectTarget { Side = PlayerSide.Cpu, ZoneIndex = 0 };

        TestDuelFactory.PlaceSpellTrap(human, 1, TestCards.DestroyEffectTrap, faceUp: false);
        engine.ActivateSetCard(1, new EffectTarget { Side = PlayerSide.Cpu, ZoneIndex = 0 });
        TestDuelFactory.CloseChain(engine);

        var destroyed = engine.State.Events.OfType<MonsterDestroyedEvent>().Single();
        Assert.Equal(DestructionCause.Effect, destroyed.Cause);
        Assert.Equal(TestCards.Level4Weak, destroyed.Card);

        // El bug corregido: la Magia de Equipo del monstruo destruido por un
        // efecto (no por batalla/Sacrificio) tambien debe irse al Cementerio.
        Assert.Null(human.SpellTrapZones[0]);
        Assert.Contains(equip, human.Graveyard);
    }
}
