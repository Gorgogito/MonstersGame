using GodotGame.Core.Battle;
using GodotGame.Core.Effects;
using GodotGame.Core.Effects.Monster;
using GodotGame.Core.Entities;
using GodotGame.Tests.TestSupport;

namespace GodotGame.Tests;

/// <summary>
/// Trampas por datos con las mecanicas de "Castigo/Lavado de Cerebro del
/// Mundo Oscuro", "Romper la Mente", "Virus Devastador", "Dinomischus
/// Paleozoico" y "Drenaje de Habilidad": negar Invocaciones, sustituir una
/// destruccion desde el Cementerio, declarar un nombre, vigilar robos,
/// Invocar una Trampa como monstruo y negar efectos de monstruos.
/// </summary>
public class TrapEffectTests
{
    private static EffectStep Step(string kind, params (string, string)[] p) => new(kind, EffectActionParams.Of(p));

    private static EffectStep When(string condition, string kind, params (string, string)[] p) =>
        new(kind, EffectActionParams.Of(p), conditions: new[] { new StepCondition(condition, false, EffectActionParams.Empty) });

    private static StepCondition Cond(string kind, bool negate = false, params (string, string)[] p) => new(kind, negate, EffectActionParams.Of(p));

    private static TrapCard Trap(int id, string name, TrapSubType subType, params MonsterEffect[] effects) => new(id, name, subType, effects: effects);

    private static MonsterEffect OnActivate(EffectStep[] steps, EffectActionParams? target = null, EffectStep[]? costs = null, StepCondition[]? conditions = null) =>
        new(MonsterEffectType.Activation, steps, target: target, costs: costs, activationConditions: conditions);

    private static MonsterCard Fiend(int id, string name, int attack = 1500, params MonsterEffect[] effects) =>
        new(id, name, attack, 500, 4, "Demonio", MonsterAttribute.Dark, effects.Length > 0 ? MonsterCategory.Effect : MonsterCategory.Normal, effects: effects);

    private static void Drive(DuelEngine engine, Func<ChoiceRequest, int[]?>? pick = null)
    {
        for (int guard = 0; guard < 200; guard++)
        {
            var choice = engine.State.PendingChoice;
            if (choice != null)
            {
                var custom = pick?.Invoke(choice);
                switch (choice.Kind)
                {
                    case ChoiceKind.YesNo: engine.AnswerYesNo(custom == null || custom[0] == 1); break;
                    case ChoiceKind.SelectOption: engine.AnswerOption(custom?[0] ?? 0); break;
                    case ChoiceKind.Reveal: engine.AcknowledgeReveal(); break;
                    default: engine.AnswerCards(custom ?? Enumerable.Range(0, Math.Max(choice.Min, Math.Min(1, choice.Max))).ToArray()); break;
                }
                continue;
            }
            if (engine.State.Chain.Count > 0) { engine.PassPriority(); continue; }
            return;
        }
        throw new InvalidOperationException("Drive no termino.");
    }

    private static void NextTurn(DuelEngine engine)
    {
        engine.EndTurn();
        Drive(engine);
        if (engine.State.PendingDiscardCount > 0)
            engine.DiscardForEndPhase(Enumerable.Range(0, engine.State.PendingDiscardCount).ToArray());
        Drive(engine);
    }

    private static int[]? ActivateInWindow(ChoiceRequest choice) => choice.IsResponseWindow ? new[] { 1 } : null;

    // ------------------------------------------------------------------ Negar Invocaciones

    private static TrapCard Punishment() => Trap(9800, "Castigo de Prueba", TrapSubType.Normal,
        OnActivate(new[] { Step("negate_summon"), When("previous_step_succeeded", "discard", ("Who", "Controller"), ("Count", "1"), ("CardKind", "Monster"), ("Type", "Demonio")) },
            conditions: new[] { Cond("responding_to_summon") }),
        new MonsterEffect(MonsterEffectType.Continuous, new[]
        {
            Step("destruction_substitute", ("ByBattle", "true"), ("ByOpponentEffect", "true"), ("OncePerTurn", "true"), ("Side", "Own"), ("CardKind", "Monster"), ("NameContains", "Mundo Oscuro"))
        }));

    [Fact]
    public void SetTrap_NegatesTheOpponentsSummon_AndItsSummonEffectsDoNotTrigger()
    {
        // El monstruo de la CPU tiene "si es Invocado: roba 1 carta": al negar la Invocacion no roba.
        var drawer = Fiend(9801, "Invocador Robador", 1500,
            new MonsterEffect(MonsterEffectType.Trigger, new[] { Step("draw", ("Count", "1")) }, EffectEvent.Summoned));
        var (engine, human, cpu) = TestDuelFactory.Create();
        TestDuelFactory.PlaceSpellTrap(human, 0, Punishment(), faceUp: false);
        human.Hand.Add(Fiend(9802, "Demonio de Mano"));
        NextTurn(engine);
        cpu.Hand.Add(drawer);
        int cpuDeck = cpu.Deck.Count;

        Assert.True(engine.NormalSummon(cpu.Hand.Count - 1, BattlePosition.Attack).Success);
        var window = engine.State.PendingChoice!;
        Assert.True(window.IsResponseWindow);
        Assert.Equal(PlayerSide.Human, window.Chooser);
        Drive(engine, ActivateInWindow);

        Assert.All(cpu.MonsterZones, m => Assert.Null(m));
        Assert.Contains(drawer, cpu.Graveyard);
        Assert.Equal(cpuDeck, cpu.Deck.Count);
        Assert.Empty(human.Hand); // descarto al Demonio
    }

    [Fact]
    public void SummonWindow_DoesNotAppear_WithoutACardThatRespondsToSummons()
    {
        var (engine, human, cpu) = TestDuelFactory.Create();
        TestDuelFactory.PlaceSpellTrap(human, 0, TestCards.NormalTrap, faceUp: false);
        NextTurn(engine);
        cpu.Hand.Add(TestCards.Level4Strong);

        Assert.True(engine.NormalSummon(cpu.Hand.Count - 1, BattlePosition.Attack).Success);
        Assert.Null(engine.State.PendingChoice);
    }

    [Fact]
    public void GraveyardSubstitute_IsBanishedInsteadOfAnArchetypeMonsterDestroyedInBattle_OncePerTurn()
    {
        var (engine, human, cpu) = TestDuelFactory.Create();
        var punishment = Punishment();
        human.Graveyard.Add(punishment);
        TestDuelFactory.PlaceOnField(human, 0, Fiend(9803, "Soldado del Mundo Oscuro", 1000));
        TestDuelFactory.PlaceOnField(human, 1, Fiend(9804, "Soldado Comun", 1000));
        NextTurn(engine);
        TestDuelFactory.PlaceOnField(cpu, 0, TestCards.Level8TwoTributes);
        TestDuelFactory.PlaceOnField(cpu, 1, TestCards.Level6OneTribute);
        engine.AdvancePhase();

        Assert.True(engine.DeclareAttack(0, 0).Success);
        Drive(engine);
        Assert.NotNull(human.MonsterZones[0]);          // protegido
        Assert.Contains(punishment, human.Banished);

        Assert.True(engine.DeclareAttack(1, 1).Success);
        Drive(engine);
        Assert.Null(human.MonsterZones[1]);             // el que no es "Mundo Oscuro" se destruye
    }

    // ------------------------------------------------------------------ Lavado de Cerebro

    [Fact]
    public void Trap_ReturnsAnArchetypeMonster_AndChangesTheMonsterEffect_ToARandomDiscard()
    {
        var brainwash = Trap(9805, "Lavado de Prueba", TrapSubType.Normal,
            OnActivate(new[] { Step("return_to_hand", ("UseTargets", "true")), When("previous_step_succeeded", "replace_responded_effect", ("Count", "1"), ("Random", "true")) },
                target: EffectActionParams.Of(("From", "MonsterZone"), ("Side", "Both"), ("CardKind", "Monster"), ("NameContains", "Mundo Oscuro"), ("Count", "1"), ("Min", "1")),
                conditions: new[]
                {
                    Cond("responding_to_activation", false, ("Who", "Opponent"), ("MonsterEffect", "true"), ("NormalSpell", "false"), ("NormalTrap", "false")),
                    Cond("count_at_least", false, ("From", "Hand"), ("Side", "Own"), ("Amount", "3")),
                }));
        var cpuDrawer = Fiend(9806, "Robador Rival", 1500,
            new MonsterEffect(MonsterEffectType.Ignition, new[] { Step("draw", ("Count", "2")) }));
        var (engine, human, cpu) = TestDuelFactory.Create();
        TestDuelFactory.PlaceSpellTrap(human, 0, brainwash, faceUp: false);
        var guard = Fiend(9807, "Guardia del Mundo Oscuro");
        TestDuelFactory.PlaceOnField(human, 0, guard);
        human.Hand.AddRange(new Card[] { TestCards.Level4Weak, TestCards.Level4Weak, TestCards.Level4Weak });
        NextTurn(engine);
        TestDuelFactory.PlaceOnField(cpu, 0, cpuDrawer);
        int cpuDeck = cpu.Deck.Count;

        Assert.True(engine.ActivateMonsterEffect(engine.GetActivatableEffects(PlayerSide.Cpu).Single()).Success);
        Assert.True(engine.ActivateSetCard(0).Success);
        Drive(engine);

        Assert.All(human.MonsterZones, m => Assert.Null(m));   // el guardia volvio a la mano
        Assert.Equal(cpuDeck, cpu.Deck.Count);                  // la CPU no robo
        Assert.Equal(3, human.Hand.Count);                      // +1 devuelto, -1 descartado al azar
        Assert.Equal(2, human.Graveyard.Count);                 // la Trampa + la carta descartada
    }

    // ------------------------------------------------------------------ Romper la Mente

    private static TrapCard MindCrush() => Trap(9808, "Romper de Prueba", TrapSubType.Normal,
        OnActivate(new[] { Step("declare_card_discard", ("Who", "Opponent"), ("PenaltyIfMissing", "true")) }));

    [Fact]
    public void DeclaredCard_InTheOpponentsHand_DiscardsEveryCopy()
    {
        var (engine, human, cpu) = TestDuelFactory.Create();
        TestDuelFactory.PlaceSpellTrap(human, 0, MindCrush(), faceUp: false);
        human.Hand.Add(TestCards.Level4Weak);
        cpu.Hand.AddRange(new Card[] { TestCards.Level4Strong, TestCards.Level4Strong, TestCards.HighDefense });
        NextTurn(engine);
        cpu.Hand.Add(new SpellCard(9809, "Robo Rival", SpellSubType.Normal, effects: new[] { OnActivate(new[] { Step("draw", ("Count", "1")) }) }));

        engine.ActivateSpell(cpu.Hand.Count - 1);
        Assert.True(engine.ActivateSetCard(0).Success);
        while (engine.State.PendingChoice == null) engine.PassPriority();
        var declare = engine.State.PendingChoice!;
        Assert.Equal("declare_name", declare.Tag);
        int strong = declare.Options.ToList().IndexOf(TestCards.Level4Strong.Name);
        Drive(engine, choice => choice.Tag == "declare_name" ? new[] { strong } : null);

        Assert.Equal(2, cpu.Graveyard.Count(c => c == TestCards.Level4Strong));
        Assert.DoesNotContain(TestCards.Level4Strong, cpu.Hand);
        Assert.Contains(TestCards.Level4Weak, human.Hand);
    }

    [Fact]
    public void DeclaredCard_NotInTheHand_YouDiscardOneAtRandom()
    {
        var (engine, human, cpu) = TestDuelFactory.Create();
        TestDuelFactory.PlaceSpellTrap(human, 0, MindCrush(), faceUp: false);
        human.Hand.Add(TestCards.Level4Weak);
        cpu.Hand.Add(TestCards.HighDefense);
        NextTurn(engine);
        cpu.Hand.Add(new SpellCard(9810, "Robo Rival 2", SpellSubType.Normal, effects: new[] { OnActivate(new[] { Step("draw", ("Count", "1")) }) }));

        engine.ActivateSpell(cpu.Hand.Count - 1);
        engine.ActivateSetCard(0);
        while (engine.State.PendingChoice == null) engine.PassPriority();
        int missing = engine.State.PendingChoice!.Options.ToList().IndexOf(TestCards.Level8TwoTributes.Name);
        Drive(engine, choice => choice.Tag == "declare_name" ? new[] { missing } : null);

        Assert.Contains(TestCards.Level4Weak, human.Graveyard);
    }

    // ------------------------------------------------------------------ Virus

    [Fact]
    public void Virus_TributeCost_DestroysWeakMonsters_OnFieldInHandAndInLaterDraws()
    {
        var virus = Trap(9811, "Virus de Prueba", TrapSubType.Normal,
            OnActivate(new[]
                {
                    Step("reveal_hand", ("Who", "Opponent")),
                    Step("destroy_all", ("From", "MonsterZone,Hand"), ("Side", "Opponent"), ("CardKind", "Monster"), ("AttackMax", "1500")),
                    Step("watch_draws", ("Who", "Opponent"), ("Turns", "3"), ("CardKind", "Monster"), ("AttackMax", "1500")),
                },
                costs: new[] { Step("tribute", ("Side", "Own"), ("CardKind", "Monster"), ("Attribute", "Dark"), ("AttackMin", "2000"), ("Count", "1"), ("Min", "1")) }));
        var (engine, human, cpu) = TestDuelFactory.Create();
        var darkBoss = new MonsterCard(9812, "Jefe Oscuro", 2500, 2000, 7, "Demonio", MonsterAttribute.Dark);
        TestDuelFactory.PlaceOnField(human, 0, darkBoss);
        TestDuelFactory.PlaceSpellTrap(human, 0, virus, faceUp: false);
        TestDuelFactory.PlaceOnField(cpu, 0, TestCards.Level4Weak);    // 1000: destruido
        TestDuelFactory.PlaceOnField(cpu, 1, TestCards.Level6OneTribute); // 2200: sobrevive
        cpu.Hand.Clear();
        cpu.Hand.AddRange(new Card[] { TestCards.Level2Fodder, TestCards.Level8TwoTributes });
        cpu.Deck.Insert(0, TestCards.ZeroAttack);

        Assert.True(engine.ActivateSetCard(0).Success);
        Drive(engine);

        Assert.Contains(darkBoss, human.Graveyard);
        Assert.Null(cpu.MonsterZones[0]);
        Assert.NotNull(cpu.MonsterZones[1]);
        Assert.Contains(TestCards.Level2Fodder, cpu.Graveyard);
        Assert.Contains(TestCards.Level8TwoTributes, cpu.Hand);

        NextTurn(engine); // la CPU roba la Larva (0 ATK): destruida
        Assert.Contains(TestCards.ZeroAttack, cpu.Graveyard);
        Assert.Single(engine.State.DrawWatches);
    }

    // ------------------------------------------------------------------ Paleozoico

    [Fact]
    public void TrapInTheGraveyard_SummonsItselfAsAMonster_WhenATrapIsActivated_AndIsBanishedWhenItLeaves()
    {
        var paleozoic = Trap(9813, "Paleozoico de Prueba", TrapSubType.Normal,
            OnActivate(new[] { Step("draw") }),
            new MonsterEffect(MonsterEffectType.Trigger,
                new[] { Step("special_summon_self_as_monster", ("Type", "Aqua"), ("Attribute", "Water"), ("Level", "2"), ("Attack", "1200"), ("Defense", "0")) },
                EffectEvent.CardActivated, optional: true, activationZone: EffectZone.Graveyard,
                subject: EventSubject.AnyCard, eventFilter: EffectActionParams.Of(("Side", "Both"), ("CardKind", "Trap"))));
        var (engine, human, cpu) = TestDuelFactory.Create();
        human.Graveyard.Add(paleozoic);
        TestDuelFactory.PlaceSpellTrap(human, 0, TestCards.NormalTrap, faceUp: false);
        NextTurn(engine);
        cpu.Hand.Add(new SpellCard(9814, "Robo Rival 3", SpellSubType.Normal, effects: new[] { OnActivate(new[] { Step("draw", ("Count", "1")) }) }));

        engine.ActivateSpell(cpu.Hand.Count - 1);
        engine.ActivateSetCard(0);
        Drive(engine);

        var monster = human.MonsterZones.Single(m => m != null)!;
        Assert.Equal(paleozoic.Name, monster.Card.Name);
        Assert.Equal(1200, monster.Card.Attack);
        Assert.True(monster.UnaffectedByMonsterEffects);
        Assert.DoesNotContain(paleozoic, human.Graveyard);

        // Un efecto de monstruo no la puede seleccionar.
        var destroyer = Fiend(9815, "Destructor Rival", 1500,
            new MonsterEffect(MonsterEffectType.Ignition, new[] { Step("destroy", ("UseTargets", "true")) },
                target: EffectActionParams.Of(("From", "MonsterZone"), ("Side", "Opponent"), ("Count", "1"), ("Min", "1"))));
        TestDuelFactory.PlaceOnField(cpu, 4, destroyer);
        Assert.Empty(engine.GetActivatableEffects(PlayerSide.Cpu));

        // Destruida en batalla: se destierra en vez de ir al Cementerio.
        TestDuelFactory.PlaceOnField(cpu, 3, TestCards.Level8TwoTributes);
        engine.AdvancePhase();
        int zone = Array.FindIndex(human.MonsterZones, m => m != null);
        Assert.True(engine.DeclareAttack(3, zone).Success);
        Drive(engine);
        Assert.Contains(human.Banished, c => c.Name == paleozoic.Name);
        Assert.DoesNotContain(human.Graveyard, c => c.Name == paleozoic.Name);
    }

    // ------------------------------------------------------------------ Drenaje de Habilidad

    [Fact]
    public void SkillDrain_PaysLifePoints_AndNegatesFaceUpMonsterEffects()
    {
        var drain = Trap(9816, "Drenaje de Prueba", TrapSubType.Continuous,
            OnActivate(Array.Empty<EffectStep>(), costs: new[] { Step("pay_lp", ("Amount", "1000")) }),
            new MonsterEffect(MonsterEffectType.Continuous, new[] { Step("negate_monster_effects") }));
        var booster = Fiend(9817, "Potenciador", 1000,
            new MonsterEffect(MonsterEffectType.Continuous, new[] { Step("stat_modifier", ("Apply", "Self"), ("Attack", "1000")) }));
        var drawer = Fiend(9818, "Robador", 1000,
            new MonsterEffect(MonsterEffectType.Ignition, new[] { Step("draw", ("Count", "1")) }));
        var (engine, human, _) = TestDuelFactory.Create();
        var boosted = TestDuelFactory.PlaceOnField(human, 0, booster);
        TestDuelFactory.PlaceOnField(human, 1, drawer);
        TestDuelFactory.PlaceSpellTrap(human, 0, drain, faceUp: false);
        Assert.Equal(2000, EffectiveStats.EffectiveAttack(boosted, engine.State, human));
        NextTurn(engine);
        NextTurn(engine);

        int lp = human.LifePoints;
        Assert.True(engine.ActivateSetCard(0).Success);
        Drive(engine);
        Assert.Equal(lp - 1000, human.LifePoints);
        Assert.True(human.SpellTrapZones[0]!.FaceUp);
        Assert.Equal(1000, EffectiveStats.EffectiveAttack(boosted, engine.State, human));

        int deck = human.Deck.Count;
        var ignition = engine.GetActivatableEffects(PlayerSide.Human).Single(); // se puede activar...
        Assert.True(engine.ActivateMonsterEffect(ignition).Success);
        Drive(engine);
        Assert.Equal(deck, human.Deck.Count);                                  // ...pero no hace nada
    }
}
