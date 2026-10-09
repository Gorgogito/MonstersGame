using GodotGame.Core.Battle;
using GodotGame.Core.Effects;
using GodotGame.Core.Effects.Monster;
using GodotGame.Core.Entities;
using GodotGame.Core.Rules;
using GodotGame.Tests.TestSupport;

namespace GodotGame.Tests;

/// <summary>
/// Efectos de Monstruo compuestos por datos (Continuo, de Encendido,
/// Disparado, Rapido, de Volteo y No clasificado), probados con cartas
/// modeladas sobre los ejemplos reales (Mundo Oscuro, ¡Peligro!...).
/// </summary>
public class MonsterEffectTests
{
    // ------------------------------------------------------------------ helpers

    private static EffectStep Step(string kind, params (string, string)[] p) => new(kind, EffectActionParams.Of(p));

    private static EffectStep OptionalStep(string kind, params (string, string)[] p) => new(kind, EffectActionParams.Of(p), optional: true);

    private static EffectStep When(string condition, bool negate, string kind, params (string, string)[] p) =>
        new(kind, EffectActionParams.Of(p), conditions: new[] { new StepCondition(condition, negate, EffectActionParams.Empty) });

    private static MonsterCard Monster(int id, string name, int level, string type, params MonsterEffect[] effects) =>
        new(id, name, attack: 1000 + level * 100, defense: 500, level: level, type, MonsterAttribute.Dark,
            category: MonsterCategory.Effect, effects: effects);

    private static readonly MonsterCard Searchable = new(9001, "Puertas de Prueba", 0, 0, 1, "Fiend", MonsterAttribute.Dark);

    /// <summary>"Si esta carta es descartada al Cementerio por efecto de una carta: Invoca esta carta de Modo Especial."</summary>
    private static MonsterCard DarkWorldSummoner() => Monster(9100, "Guerrillero del Mundo de Prueba", 4, "Fiend",
        new MonsterEffect(MonsterEffectType.Trigger, new[] { Step("special_summon_self") }, EffectEvent.DiscardedByCardEffect));

    /// <summary>Encendido desde el Campo: "Tu adversario descarta 1 carta."</summary>
    private static MonsterCard ForcedDiscarder() => Monster(9101, "Verdugo de Prueba", 4, "Fiend",
        new MonsterEffect(MonsterEffectType.Ignition, new[] { Step("discard", ("Who", "Opponent"), ("Count", "1"), ("Chooser", "Owner")) }));

    /// <summary>Encendido desde el Campo: "Descarta 1 carta."</summary>
    private static MonsterCard SelfDiscarder() => Monster(9102, "Ermitaño de Prueba", 4, "Fiend",
        new MonsterEffect(MonsterEffectType.Ignition, new[] { Step("discard", ("Who", "Controller"), ("Count", "1"), ("Chooser", "Owner")) }));

    private static ActivatableEffect Find(DuelEngine engine, PlayerSide side, string name) =>
        engine.GetActivatableEffects(side).Single(e => e.Card.Card.Name == name);

    /// <summary>Pasa la Prioridad solo hasta que la Cadena actual empieza a resolverse (no responde elecciones).</summary>
    private static void PassBoth(DuelEngine engine)
    {
        engine.PassPriority();
        if (engine.State.Chain.Count > 0 && engine.State.PendingChoice == null) engine.PassPriority();
    }

    /// <summary>Juega todo hasta que no quede nada pendiente: pasa la Prioridad y responde cada eleccion con la primera opcion valida.</summary>
    private static void Drive(DuelEngine engine)
    {
        for (int guard = 0; guard < 100; guard++)
        {
            var choice = engine.State.PendingChoice;
            if (choice != null)
            {
                switch (choice.Kind)
                {
                    case ChoiceKind.YesNo: engine.AnswerYesNo(true); break;
                    case ChoiceKind.SelectOption: engine.AnswerOption(0); break;
                    default: engine.AnswerCards(Enumerable.Range(0, Math.Max(choice.Min, Math.Min(1, choice.Max))).ToArray()); break;
                }
                continue;
            }
            if (engine.State.Chain.Count > 0) { engine.PassPriority(); continue; }
            return;
        }
        throw new InvalidOperationException("Drive no termino.");
    }

    private static void AnswerAllYes(DuelEngine engine)
    {
        while (engine.State.PendingChoice is { Kind: ChoiceKind.YesNo })
            engine.AnswerYesNo(true);
    }

    // ------------------------------------------------------------------ Encendido

    [Fact]
    public void Ignition_FromHand_WithDiscardSelfCost_SearchesTheDeck_ThroughTheChain()
    {
        var archives = Monster(9200, "Archivos de Prueba", 3, "Fiend",
            new MonsterEffect(MonsterEffectType.Ignition,
                new[] { Step("add_to_hand", ("From", "Deck"), ("CardId", Searchable.Id.ToString())) },
                activationZone: EffectZone.Hand,
                costs: new[] { Step("discard_self") }));
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(archives);
        human.Deck.Insert(5, Searchable);

        Assert.True(engine.ActivateMonsterEffect(Find(engine, PlayerSide.Human, archives.Name)).Success);

        Assert.Single(engine.State.Chain);
        Assert.Contains(archives, human.Graveyard); // costo pagado al activar
        Assert.DoesNotContain(Searchable, human.Hand);

        TestDuelFactory.CloseChain(engine);

        Assert.Contains(Searchable, human.Hand);
        Assert.DoesNotContain(Searchable, human.Deck);
    }

    [Fact]
    public void Ignition_IsNotOffered_OutsideYourMainPhase()
    {
        var (engine, _, cpu) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(cpu, 0, ForcedDiscarder());

        Assert.Empty(engine.GetActivatableEffects(PlayerSide.Cpu)); // es el turno del humano
    }

    [Fact]
    public void OncePerTurn_BlocksASecondActivation_UntilTheNextTurn()
    {
        var drawer = Monster(9201, "Bibliotecario de Prueba", 4, "Spellcaster",
            new MonsterEffect(MonsterEffectType.Ignition, new[] { Step("draw", ("Count", "1")) }, oncePerTurn: true));
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, drawer);

        engine.ActivateMonsterEffect(Find(engine, PlayerSide.Human, drawer.Name));
        TestDuelFactory.CloseChain(engine);

        Assert.Empty(engine.GetActivatableEffects(PlayerSide.Human));
    }

    // ------------------------------------------------------------------ Disparados: descartes

    [Fact]
    public void DiscardedByOpponentsCardEffect_TriggersSpecialSummonOfItself()
    {
        var summoner = DarkWorldSummoner();
        var (engine, human, cpu) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, ForcedDiscarder());
        cpu.Hand.Add(summoner);

        engine.ActivateMonsterEffect(Find(engine, PlayerSide.Human, "Verdugo de Prueba"));
        PassBoth(engine); // la CPU descarta su unica carta (eleccion forzada)

        // El efecto Disparado (obligatorio) abre una Cadena nueva.
        Assert.Single(engine.State.Chain);
        Assert.Same(summoner, engine.State.Chain[0].MonsterEffect!.Source);
        TestDuelFactory.CloseChain(engine);

        Assert.Contains(cpu.MonsterZones, m => m?.Card == summoner);
        Assert.DoesNotContain(summoner, cpu.Graveyard);
    }

    [Fact]
    public void DiscardAsACost_DoesNotCountAsDiscardedByCardEffect()
    {
        var summoner = Monster(9300, "Soldado del Mundo de Prueba", 4, "Fiend",
            new MonsterEffect(MonsterEffectType.Trigger, new[] { Step("special_summon_self") }, EffectEvent.DiscardedByCardEffect),
            new MonsterEffect(MonsterEffectType.Ignition, new[] { Step("draw") }, activationZone: EffectZone.Hand, costs: new[] { Step("discard_self") }));
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(summoner);

        engine.ActivateMonsterEffect(Find(engine, PlayerSide.Human, summoner.Name));
        TestDuelFactory.CloseChain(engine);

        Assert.Empty(engine.State.Chain);
        Assert.Contains(summoner, human.Graveyard);
    }

    [Fact]
    public void DrawOneOrTwo_DependsOnWhetherTheOpponentDiscardedIt()
    {
        // "Si esta carta es descartada al Cementerio por efecto de una carta: roba 1 carta o,
        //  si fue descartada de tu mano por efecto de una carta del adversario, roba 2 cartas en su lugar."
        MonsterCard Drawer() => Monster(9301, "Explorador de Prueba", 2, "Fiend",
            new MonsterEffect(MonsterEffectType.Trigger, new[]
            {
                When("discarded_by_opponent", true, "draw", ("Count", "1")),
                When("discarded_by_opponent", false, "draw", ("Count", "2")),
            }, EffectEvent.DiscardedByCardEffect));

        // Descartada por el propio efecto: roba 1.
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, SelfDiscarder());
        human.Hand.Add(Drawer());
        engine.ActivateMonsterEffect(Find(engine, PlayerSide.Human, "Ermitaño de Prueba"));
        TestDuelFactory.CloseChain(engine);
        TestDuelFactory.CloseChain(engine);
        Assert.Single(human.Hand);

        // Descartada por el adversario: roba 2.
        var (engine2, human2, cpu2) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human2, 0, ForcedDiscarder());
        cpu2.Hand.Add(Drawer());
        engine2.ActivateMonsterEffect(Find(engine2, PlayerSide.Human, "Verdugo de Prueba"));
        TestDuelFactory.CloseChain(engine2);
        TestDuelFactory.CloseChain(engine2);
        Assert.Equal(2, cpu2.Hand.Count);
    }

    [Fact]
    public void OptionalTrigger_AsksItsController_AndDecliningDoesNothing()
    {
        var optional = Monster(9302, "Opcional de Prueba", 4, "Fiend",
            new MonsterEffect(MonsterEffectType.Trigger, new[] { Step("special_summon_self") }, EffectEvent.DiscardedByCardEffect, optional: true));
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, SelfDiscarder());
        human.Hand.Add(optional);

        engine.ActivateMonsterEffect(Find(engine, PlayerSide.Human, "Ermitaño de Prueba"));
        TestDuelFactory.CloseChain(engine);

        Assert.NotNull(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.YesNo, engine.State.PendingChoice!.Kind);
        Assert.False(engine.AdvancePhase().Success); // el duelo espera la respuesta

        engine.AnswerYesNo(false);
        Assert.Empty(engine.State.Chain);
        Assert.Contains(optional, human.Graveyard);
    }

    [Fact]
    public void HandLimitDiscard_TriggersDiscarded_ButNotDiscardedByCardEffect()
    {
        var anyDiscard = Monster(9303, "Tsuchinoko de Prueba", 3, "Reptile",
            new MonsterEffect(MonsterEffectType.Trigger, new[] { Step("special_summon_self") }, EffectEvent.Discarded));
        var byEffect = DarkWorldSummoner();
        var (engine, human, _) = TestDuelFactory.Create();
        for (int i = 0; i < 5; i++) human.Hand.Add(TestCards.Level4Weak);
        human.Hand.Add(anyDiscard);
        human.Hand.Add(byEffect);
        engine.AdvancePhase(); // Main1 -> Main2 (primer turno, sin batalla)

        engine.EndTurn();
        Assert.Equal(1, engine.State.PendingDiscardCount);
        engine.DiscardForEndPhase(new[] { 5 }); // descarta anyDiscard

        Assert.Single(engine.State.Chain);
        TestDuelFactory.CloseChain(engine);
        Assert.Contains(human.MonsterZones, m => m?.Card == anyDiscard);
        Assert.Equal(1, engine.State.ActiveIndex); // el turno termino tras resolver el efecto
    }

    // ------------------------------------------------------------------ ¡Peligro!

    [Fact]
    public void Danger_RevealsDiscardsRandom_ThenSummonsACopyAndDraws()
    {
        // "Puedes mostrar esta carta en tu mano; tu adversario elige al azar 1 carta en toda tu mano,
        //  y despues tu descartas la carta elegida. Despues, si la carta descartada no fue X,
        //  Invoca de Modo Especial, desde tu mano, 1 X y, si lo haces, roba 1 carta."
        var danger = Monster(9400, "¡Peligro de Prueba!", 8, "Beast",
            new MonsterEffect(MonsterEffectType.Ignition, new[]
            {
                Step("discard", ("Who", "Controller"), ("Count", "1"), ("Chooser", "Random")),
                When("last_affected_not_source_name", false, "special_summon", ("From", "Hand"), ("SameNameAsSource", "true")),
                When("previous_step_succeeded", false, "draw", ("Count", "1")),
            }, activationZone: EffectZone.Hand, costs: new[] { Step("reveal_self") }));
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(danger);
        human.Hand.Add(TestCards.Level4Strong);
        engine.State.Rng = new Random(1);

        engine.ActivateMonsterEffect(Find(engine, PlayerSide.Human, danger.Name));
        TestDuelFactory.CloseChain(engine);

        bool discardedDanger = human.Graveyard.Contains(danger);
        if (discardedDanger)
        {
            Assert.DoesNotContain(human.MonsterZones, m => m?.Card == danger);
            Assert.Single(human.Hand); // queda la otra carta, sin robar
        }
        else
        {
            Assert.Contains(TestCards.Level4Strong, human.Graveyard);
            Assert.Contains(human.MonsterZones, m => m?.Card == danger);
            Assert.Single(human.Hand); // robo 1
        }
    }

    // ------------------------------------------------------------------ No clasificado

    [Fact]
    public void Unclassified_SummonsItselfFromTheGraveyard_ByReturningAMonster_WithoutAChain()
    {
        // "Puedes Invocar esta carta de Modo Especial (desde tu Cementerio) devolviendo a la mano
        //  1 monstruo de Nivel 7 o menor que controles."
        var king = Monster(9500, "Rey Supremo de Prueba", 8, "Fiend",
            new MonsterEffect(MonsterEffectType.Unclassified, new[] { Step("special_summon_self") },
                activationZone: EffectZone.Graveyard,
                costs: new[] { Step("return_to_hand", ("From", "MonsterZone"), ("Side", "Own"), ("LevelMax", "7")) }));
        var (engine, human, _) = TestDuelFactory.Create();
        human.Graveyard.Add(king);
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Weak);

        Assert.True(engine.ActivateMonsterEffect(Find(engine, PlayerSide.Human, king.Name)).Success);

        Assert.Empty(engine.State.Chain);
        Assert.Contains(human.MonsterZones, m => m?.Card == king);
        Assert.Contains(TestCards.Level4Weak, human.Hand);
    }

    [Fact]
    public void Unclassified_IsNotOffered_WithoutAMonsterToReturn()
    {
        var king = Monster(9501, "Rey Supremo de Prueba", 8, "Fiend",
            new MonsterEffect(MonsterEffectType.Unclassified, new[] { Step("special_summon_self") },
                activationZone: EffectZone.Graveyard,
                costs: new[] { Step("return_to_hand", ("From", "MonsterZone"), ("Side", "Own"), ("LevelMax", "7")) }));
        var (engine, human, _) = TestDuelFactory.Create();
        human.Graveyard.Add(king);

        Assert.Empty(engine.GetActivatableEffects(PlayerSide.Human));
    }

    // ------------------------------------------------------------------ Batalla

    [Fact]
    public void DestroyedByBattle_SearchesALevelFourOrLowerMonster()
    {
        var scout = Monster(9600, "Vigía de Prueba", 2, "Fiend",
            new MonsterEffect(MonsterEffectType.Trigger,
                new[] { Step("add_to_hand", ("From", "Deck"), ("CardKind", "Monster"), ("LevelMax", "4")) },
                EffectEvent.DestroyedByBattle));
        var (engine, human, cpu) = TestDuelFactory.Create(firstPlayerIndex: 1);
        TestDuelFactory.PlaceOnField(cpu, 0, TestCards.Level4Strong);
        TestDuelFactory.PlaceOnField(human, 0, scout);
        engine.EndTurn(); // turno de la CPU -> humano... vuelve a empezar con el humano activo
        engine.EndTurn();
        // Ahora es turno de la CPU (indice 1) y puede batallar.
        Assert.Equal(1, engine.State.ActiveIndex);
        engine.AdvancePhase();
        int handBefore = human.Hand.Count;

        engine.DeclareAttack(0, 0);
        Assert.Single(engine.State.Chain);
        PassBoth(engine);
        var choice = engine.State.PendingChoice!;
        Assert.Equal(PlayerSide.Human, choice.Chooser);
        Assert.All(choice.Candidates, c => Assert.True(c.Card is MonsterCard { Level: <= 4 }));
        engine.AnswerCards(new[] { 0 });

        Assert.Equal(handBefore + 1, human.Hand.Count);
        Assert.Contains(scout, human.Graveyard);
    }

    [Fact]
    public void InflictsBattleDamage_OptionalDiscard_TriggersDarkWorldByOwnEffect()
    {
        var hunter = Monster(9601, "Cazador de Prueba", 3, "Fiend",
            new MonsterEffect(MonsterEffectType.Trigger, new[] { Step("discard", ("Who", "Controller"), ("Count", "1")) },
                EffectEvent.InflictsBattleDamage, optional: true));
        var summoner = DarkWorldSummoner();
        var (engine, human, _) = TestDuelFactory.Create(firstPlayerIndex: 1);
        engine.EndTurn(); // pasa al humano, que ya puede batallar
        TestDuelFactory.PlaceOnField(human, 0, hunter);
        human.Hand.Clear();
        human.Hand.Add(summoner);
        engine.AdvancePhase();

        engine.DeclareAttack(0, -1);
        Assert.Equal(ChoiceKind.YesNo, engine.State.PendingChoice!.Kind);
        engine.AnswerYesNo(true);
        TestDuelFactory.CloseChain(engine); // descarta (eleccion forzada)
        TestDuelFactory.CloseChain(engine); // Disparado del Mundo Oscuro

        Assert.Contains(human.MonsterZones, m => m?.Card == summoner);
    }

    // ------------------------------------------------------------------ Volteo

    [Fact]
    public void FlipEffect_StartsAChainWhenFlipSummoned()
    {
        var flipper = Monster(9700, "Volteador de Prueba", 3, "Fiend",
            new MonsterEffect(MonsterEffectType.Flip, new[] { Step("destroy", ("From", "MonsterZone"), ("Side", "Opponent")) }));
        var (engine, human, cpu) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, flipper, BattlePosition.DefenseFaceDown);
        TestDuelFactory.PlaceOnField(cpu, 0, TestCards.Level4Strong);

        engine.FlipSummon(0);

        Assert.Single(engine.State.Chain);
        TestDuelFactory.CloseChain(engine);
        Assert.Null(cpu.MonsterZones[0]);
        Assert.Contains(TestCards.Level4Strong, cpu.Graveyard);
    }

    // ------------------------------------------------------------------ Objetivos

    [Fact]
    public void Target_IsChosenOnActivation_AndGainsAttackAfterTheSummon()
    {
        // "Selecciona 1 monstruo Demonio en el Campo; Invoca esta carta de Modo Especial,
        //  y despues ese objetivo (si lo hay) gana 500 ATK."
        var lord = Monster(9800, "Señor de Prueba", 6, "Fiend",
            new MonsterEffect(MonsterEffectType.Trigger,
                new[] { Step("special_summon_self"), Step("modify_stats", ("Apply", "Targets"), ("Attack", "500")) },
                EffectEvent.DiscardedByCardEffect,
                target: EffectActionParams.Of(("From", "MonsterZone"), ("Side", "Both"), ("Type", "Fiend"))));
        var (engine, human, _) = TestDuelFactory.Create();
        var discarder = TestDuelFactory.PlaceOnField(human, 0, SelfDiscarder()); // Fiend
        human.Hand.Add(lord);

        engine.ActivateMonsterEffect(Find(engine, PlayerSide.Human, "Ermitaño de Prueba"));
        TestDuelFactory.CloseChain(engine);
        TestDuelFactory.CloseChain(engine); // el unico Demonio fue elegido solo (forzado)

        Assert.Contains(human.MonsterZones, m => m?.Card == lord);
        Assert.Equal(discarder.Card.Attack + 500, EffectiveStats.EffectiveAttack(discarder, engine.State, human));
    }

    [Fact]
    public void Target_WithSeveralCandidates_WaitsForTheChoice()
    {
        var destroyer = Monster(9801, "Demoledor de Prueba", 4, "Fiend",
            new MonsterEffect(MonsterEffectType.Ignition, new[] { Step("destroy", ("UseTargets", "true")) },
                target: EffectActionParams.Of(("From", "MonsterZone"), ("Side", "Opponent"))));
        var (engine, human, cpu) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, destroyer);
        TestDuelFactory.PlaceOnField(cpu, 0, TestCards.Level4Weak);
        TestDuelFactory.PlaceOnField(cpu, 1, TestCards.Level4Strong);

        engine.ActivateMonsterEffect(Find(engine, PlayerSide.Human, destroyer.Name));
        var choice = engine.State.PendingChoice!;
        Assert.Equal(ChoiceKind.SelectCards, choice.Kind);
        Assert.Equal(2, choice.Candidates.Count);
        int strong = choice.Candidates.ToList().FindIndex(c => c.Card == TestCards.Level4Strong);
        engine.AnswerCards(new[] { strong });
        TestDuelFactory.CloseChain(engine);

        Assert.NotNull(cpu.MonsterZones[0]);
        Assert.Null(cpu.MonsterZones[1]);
    }

    // ------------------------------------------------------------------ Rapido

    [Fact]
    public void QuickEffect_CanRespondToASpell_AndNegateIt()
    {
        var negator = Monster(9900, "Negador de Prueba", 4, "Spellcaster",
            new MonsterEffect(MonsterEffectType.Quick, new[] { Step("negate_activation") }, oncePerTurn: true));
        var (engine, human, cpu) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(cpu, 0, negator);
        human.Hand.Add(TestCards.DrawEffectSpell);
        int deck = human.Deck.Count;

        engine.ActivateSpell(0);
        var quick = Find(engine, PlayerSide.Cpu, negator.Name);
        Assert.True(engine.ActivateMonsterEffect(quick).Success);
        Assert.Equal(2, engine.State.Chain.Count);
        TestDuelFactory.CloseChain(engine);

        Assert.Equal(deck, human.Deck.Count); // el robo fue negado
    }

    // ------------------------------------------------------------------ Continuo

    [Fact]
    public void Continuous_StatModifier_BuffsMatchingMonsters_OnlyWhileFaceUp()
    {
        var general = Monster(9950, "General de Prueba", 4, "Fiend",
            new MonsterEffect(MonsterEffectType.Continuous, new[]
            {
                Step("stat_modifier", ("Apply", "AllMatching"), ("Side", "Own"), ("Type", "Fiend"), ("Attack", "500"), ("ExcludeSource", "true"))
            }));
        var (engine, human, _) = TestDuelFactory.Create();
        var source = TestDuelFactory.PlaceOnField(human, 0, general);
        var fiend = TestDuelFactory.PlaceOnField(human, 1, TestCards.Level2Fodder); // Fiend
        var warrior = TestDuelFactory.PlaceOnField(human, 2, TestCards.Level4Weak);

        Assert.Equal(900, EffectiveStats.EffectiveAttack(fiend, engine.State, human));
        Assert.Equal(1000, EffectiveStats.EffectiveAttack(warrior, engine.State, human));
        Assert.Equal(general.Attack, EffectiveStats.EffectiveAttack(source, engine.State, human));

        source.Position = BattlePosition.DefenseFaceDown;
        Assert.Equal(400, EffectiveStats.EffectiveAttack(fiend, engine.State, human));
    }

    [Fact]
    public void Continuous_BattleIndestructible_SurvivesALosingBattle()
    {
        var wall = new MonsterCard(9951, "Muro Eterno de Prueba", 0, 0, 4, "Rock", MonsterAttribute.Earth, MonsterCategory.Effect,
            effects: new[] { new MonsterEffect(MonsterEffectType.Continuous, new[] { Step("battle_indestructible") }) });
        var (engine, human, cpu) = TestDuelFactory.Create(firstPlayerIndex: 1);
        engine.EndTurn();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong);
        TestDuelFactory.PlaceOnField(cpu, 0, wall, BattlePosition.DefenseFaceUp);
        engine.AdvancePhase();

        engine.DeclareAttack(0, 0);

        Assert.NotNull(cpu.MonsterZones[0]);
    }

    // ------------------------------------------------------------------ Campo rival / Destierro

    [Fact]
    public void SummonToTheOpponentsField_ThenSpecialSummonedByEffect_MakesThemDiscard()
    {
        // "Invoca esta carta de Modo Especial al Campo de tu adversario en Posicion de Defensa.
        //  Si esta carta es Invocada de Modo Especial por efecto de una carta: tu adversario descarta 1 carta."
        var leader = Monster(9960, "Lider de Prueba", 1, "Fiend",
            new MonsterEffect(MonsterEffectType.Trigger, new[] { Step("special_summon_self", ("ToField", "Opponent"), ("Position", "Defense")) }, EffectEvent.DiscardedByCardEffect),
            new MonsterEffect(MonsterEffectType.Trigger, new[] { Step("discard", ("Who", "Opponent"), ("Count", "1")) }, EffectEvent.SpecialSummonedByEffect));
        var (engine, human, cpu) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, SelfDiscarder());
        human.Hand.Add(leader);
        human.Hand.Add(TestCards.Level4Weak);

        engine.ActivateMonsterEffect(Find(engine, PlayerSide.Human, "Ermitaño de Prueba"));
        PassBoth(engine);
        // Dos cartas en mano: el humano elige descartar a "Lider".
        var choice = engine.State.PendingChoice!;
        engine.AnswerCards(new[] { choice.Candidates.ToList().FindIndex(c => c.Card == leader) });
        PassBoth(engine); // Lider va al Campo de la CPU en Defensa
        Assert.Contains(cpu.MonsterZones, m => m?.Card == leader && m.Position == BattlePosition.DefenseFaceUp);

        Assert.Equal(PlayerSide.Cpu, engine.State.Chain.Single().Controller); // su segundo efecto lo controla la CPU
        Drive(engine); // asi que descarta el humano
        Assert.Empty(human.Hand);
        Assert.Contains(TestCards.Level4Weak, human.Graveyard);
    }

    [Fact]
    public void Banished_TriggersItsOwnSpecialSummon()
    {
        var guard = Monster(9970, "Guarda de Prueba", 4, "Fiend",
            new MonsterEffect(MonsterEffectType.Trigger, new[] { Step("special_summon_self") }, EffectEvent.Banished, optional: true));
        var banisher = Monster(9971, "Desterrador de Prueba", 4, "Spellcaster",
            new MonsterEffect(MonsterEffectType.Ignition, new[] { Step("banish", ("From", "Graveyard"), ("Side", "Own")) }));
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, banisher);
        human.Graveyard.Add(guard);

        engine.ActivateMonsterEffect(Find(engine, PlayerSide.Human, banisher.Name));
        TestDuelFactory.CloseChain(engine);
        Assert.Contains(guard, human.Banished);
        AnswerAllYes(engine);
        TestDuelFactory.CloseChain(engine);

        Assert.Contains(human.MonsterZones, m => m?.Card == guard);
        Assert.Empty(human.Banished);
    }

    // ------------------------------------------------------------------ IA

    [Fact]
    public void CpuAI_ActivatesIgnitionEffects_AndAnswersItsOwnChoices()
    {
        var (engine, human, cpu) = TestDuelFactory.Create(new DuelConfig { AutoPassWhenNoResponse = true }, firstPlayerIndex: 1);
        var ai = new GodotGame.Core.AI.BasicCpuAI(new GodotGame.Core.Services.FusionService(Array.Empty<FusionRecipe>(), new GodotGame.Core.Services.CardDatabase(TestCards.All)));
        TestDuelFactory.PlaceOnField(cpu, 0, ForcedDiscarder());
        cpu.Hand.Clear();
        human.Hand.Add(TestCards.Level4Weak);
        human.Hand.Add(TestCards.Level8TwoTributes);

        // La CPU activa el efecto; el humano (dueño de la mano) tiene que elegir que descartar.
        for (int i = 0; i < 5 && engine.State.PendingChoice == null && human.Hand.Count == 2; i++) ai.Step(engine, 1);
        Assert.Equal(PlayerSide.Human, engine.State.PendingChoice!.Chooser);
        engine.AnswerCards(new[] { 0 });

        Assert.Single(human.Hand);
        Assert.Same(TestCards.Level8TwoTributes, human.Hand[0]);
    }

    [Fact]
    public void CpuAI_WhenForcedToDiscard_GivesUpItsWeakestCard()
    {
        var (engine, human, cpu) = TestDuelFactory.Create(new DuelConfig { AutoPassWhenNoResponse = true });
        var ai = new GodotGame.Core.AI.BasicCpuAI(new GodotGame.Core.Services.FusionService(Array.Empty<FusionRecipe>(), new GodotGame.Core.Services.CardDatabase(TestCards.All)));
        TestDuelFactory.PlaceOnField(human, 0, ForcedDiscarder());
        cpu.Hand.Add(TestCards.Level8TwoTributes);
        cpu.Hand.Add(TestCards.ZeroAttack);

        engine.ActivateMonsterEffect(Find(engine, PlayerSide.Human, "Verdugo de Prueba"));
        Assert.Equal(PlayerSide.Cpu, engine.State.PendingChoice!.Chooser);
        ai.Step(engine, 1);

        Assert.Contains(TestCards.ZeroAttack, cpu.Graveyard);
        Assert.Contains(TestCards.Level8TwoTributes, cpu.Hand);
    }

    // ------------------------------------------------------------------ Pase automatico

    [Fact]
    public void AutoPass_ResolvesTheChainWhenNobodyCanRespond()
    {
        var drawer = Monster(9980, "Lector de Prueba", 4, "Spellcaster",
            new MonsterEffect(MonsterEffectType.Ignition, new[] { Step("draw", ("Count", "1")) }));
        var (engine, human, _) = TestDuelFactory.Create(new DuelConfig { AutoPassWhenNoResponse = true });
        TestDuelFactory.PlaceOnField(human, 0, drawer);

        engine.ActivateMonsterEffect(Find(engine, PlayerSide.Human, drawer.Name));

        Assert.Empty(engine.State.Chain);
        Assert.Single(human.Hand);
    }
}
