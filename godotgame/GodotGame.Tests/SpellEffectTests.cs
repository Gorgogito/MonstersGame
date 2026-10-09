using GodotGame.Core.Battle;
using GodotGame.Core.Effects;
using GodotGame.Core.Effects.Monster;
using GodotGame.Core.Entities;
using GodotGame.Tests.TestSupport;

namespace GodotGame.Tests;

/// <summary>
/// Efectos por datos de Magias y Trampas (Normal, Continua, de Equipo, de
/// Campo, de Juego Rapido) y las mecanicas nuevas de los ejemplos del Mundo
/// Oscuro: Disparados sobre "otra carta", efectos desde el Cementerio,
/// "una vez por turno" de activacion, ventana de respuesta a un ataque,
/// Invocacion por Fusion por efecto, cambio del efecto activado, etc.
/// </summary>
public class SpellEffectTests
{
    // ------------------------------------------------------------------ helpers

    private static EffectStep Step(string kind, params (string, string)[] p) => new(kind, EffectActionParams.Of(p));

    private static EffectStep OptionalStep(string kind, params (string, string)[] p) => new(kind, EffectActionParams.Of(p), optional: true);

    private static EffectStep When(string condition, string kind, params (string, string)[] p) =>
        new(kind, EffectActionParams.Of(p), conditions: new[] { new StepCondition(condition, false, EffectActionParams.Empty) });

    private static StepCondition Cond(string kind, bool negate = false, params (string, string)[] p) => new(kind, negate, EffectActionParams.Of(p));

    private static MonsterEffect OnActivate(EffectStep[] steps, EffectActionParams? target = null, bool oncePerTurn = false,
        EffectStep[]? costs = null, StepCondition[]? conditions = null) =>
        new(MonsterEffectType.Activation, steps, oncePerTurn: oncePerTurn, target: target, costs: costs, activationConditions: conditions);

    private static SpellCard Spell(int id, string name, SpellSubType subType, params MonsterEffect[] effects) =>
        new(id, name, subType, effects: effects);

    private static MonsterCard Fiend(int id, string name, int level = 4, params MonsterEffect[] effects) =>
        new(id, name, attack: 1000 + level * 100, defense: 500, level: level, "Demonio", MonsterAttribute.Dark,
            category: effects.Length > 0 ? MonsterCategory.Effect : MonsterCategory.Normal, effects: effects);

    private static readonly MonsterCard Filler = TestCards.Level4Weak;

    /// <summary>Juega todo hasta que no quede nada pendiente: pasa la Prioridad y responde cada eleccion con la primera opcion valida.</summary>
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

    /// <summary>Termina el turno del jugador activo (descartando si hace falta) y deja al siguiente en su Main Phase 1.</summary>
    private static void NextTurn(DuelEngine engine)
    {
        engine.EndTurn();
        Drive(engine);
        if (engine.State.PendingDiscardCount > 0)
            engine.DiscardForEndPhase(Enumerable.Range(0, engine.State.PendingDiscardCount).ToArray());
        Drive(engine);
    }

    // ------------------------------------------------------------------ Normal

    [Fact]
    public void NormalSpell_ResolvesThroughTheChain_ThenGoesToTheGraveyard()
    {
        // "Cada jugador roba 1 carta, y despues cada jugador descarta 1 carta."
        var deals = Spell(9500, "Tratos de Prueba", SpellSubType.Normal,
            OnActivate(new[] { Step("draw", ("Who", "Both"), ("Count", "1")), Step("discard", ("Who", "Both"), ("Count", "1"), ("Chooser", "Owner")) }));
        var (engine, human, cpu) = TestDuelFactory.Create();
        human.Hand.Add(deals);
        human.Hand.Add(Filler);
        cpu.Hand.Add(Filler);

        Assert.True(engine.ActivateSpell(0).Success);
        Assert.Single(engine.State.Chain);
        Assert.True(human.SpellTrapZones[0]!.FaceUp);

        Drive(engine);

        Assert.Contains(deals, human.Graveyard);
        Assert.Null(human.SpellTrapZones[0]);
        Assert.Single(human.Hand);   // 1 + 1 robada - 1 descartada
        Assert.Single(cpu.Hand);
        Assert.Single(cpu.Graveyard);
    }

    [Fact]
    public void TargetedSpell_DestroysASetCard_ThenDiscards()
    {
        // "Selecciona 1 carta Colocada en el Campo; destruye ese objetivo, y despues descarta 1 carta."
        var lightning = Spell(9501, "Relampago de Prueba", SpellSubType.Normal,
            OnActivate(new[] { Step("destroy", ("UseTargets", "true")), Step("discard", ("Who", "Controller"), ("Count", "1")) },
                target: EffectActionParams.Of(("From", "MonsterZone,SpellTrapZone"), ("Side", "Both"), ("Face", "FaceDown"), ("Count", "1"), ("Min", "1"))));
        var (engine, human, cpu) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(cpu, 0, TestCards.Level4Strong, BattlePosition.DefenseFaceDown);
        TestDuelFactory.PlaceOnField(cpu, 1, TestCards.Level6OneTribute); // boca arriba: no es objetivo valido
        human.Hand.Add(lightning);
        human.Hand.Add(Filler);

        Assert.True(engine.ActivateSpell(0).Success);
        Drive(engine);

        Assert.Null(cpu.MonsterZones[0]);
        Assert.NotNull(cpu.MonsterZones[1]);
        Assert.Contains(Filler, human.Graveyard);
        Assert.Contains(lightning, human.Graveyard);
    }

    [Fact]
    public void Spell_WithoutValidTargets_CannotBeActivated_AndStaysInHand()
    {
        var lightning = Spell(9502, "Relampago sin Objetivo", SpellSubType.Normal,
            OnActivate(new[] { Step("destroy", ("UseTargets", "true")) },
                target: EffectActionParams.Of(("From", "SpellTrapZone"), ("Side", "Both"), ("Face", "FaceDown"), ("Count", "1"), ("Min", "1"))));
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(lightning);

        Assert.False(engine.ActivateSpell(0).Success);
        Assert.Contains(lightning, human.Hand);
        Assert.Null(human.SpellTrapZones[0]);
    }

    [Fact]
    public void OncePerTurnActivation_BlocksASecondCopyTheSameTurn()
    {
        // "Selecciona 1 monstruo Demonio de Nivel 4 o menor en tu Cementerio; Invocalo ... Solo puedes activar 1 por turno."
        MonsterEffect Charge() => OnActivate(new[] { Step("special_summon", ("UseTargets", "true")) }, oncePerTurn: true,
            target: EffectActionParams.Of(("From", "Graveyard"), ("Side", "Own"), ("CardKind", "Monster"), ("Type", "Demonio"), ("LevelMax", "4"), ("Count", "1"), ("Min", "1")));
        var first = Spell(9503, "Carga de Prueba", SpellSubType.Normal, Charge());
        var second = Spell(9503, "Carga de Prueba", SpellSubType.Normal, Charge());
        var (engine, human, _) = TestDuelFactory.Create();
        human.Graveyard.Add(Fiend(9600, "Demonio A"));
        human.Graveyard.Add(Fiend(9601, "Demonio B"));
        human.Hand.Add(first);
        human.Hand.Add(second);

        Assert.True(engine.ActivateSpell(0).Success);
        Drive(engine);
        Assert.NotNull(human.MonsterZones[0]);

        var again = engine.ActivateSpell(0);
        Assert.False(again.Success);
        Assert.Contains("por turno", again.Message);
        Assert.Contains(second, human.Hand);
    }

    // ------------------------------------------------------------------ Continua / Campo

    [Fact]
    public void ContinuousSpell_TriggersWhenAFiendIsDiscardedByAnArchetypeCard()
    {
        // "Si un monstruo Demonio es descartado de tu mano por efecto de una carta 'Mundo Oscuro' o del adversario: puedes descartar 1 carta, y despues roba 2."
        var archives = Spell(9504, "Archivos de Prueba", SpellSubType.Continuous,
            new MonsterEffect(MonsterEffectType.Trigger,
                new[] { Step("discard", ("Who", "Controller"), ("Count", "1")), When("previous_step_succeeded", "draw", ("Who", "Controller"), ("Count", "2")) },
                EffectEvent.DiscardedByCardEffect, optional: true, oncePerTurn: true,
                activationConditions: new[] { Cond("event_caused_by", false, ("ByOpponent", "true"), ("SourceNameContains", "Mundo Oscuro")) },
                subject: EventSubject.AnyCard,
                eventFilter: EffectActionParams.Of(("Side", "Own"), ("CardKind", "Monster"), ("Type", "Demonio"))));
        var dealsDw = Spell(9505, "Tratos del Mundo Oscuro", SpellSubType.Normal,
            OnActivate(new[] { Step("discard", ("Who", "Controller"), ("Count", "1"), ("CardKind", "Monster")) }));
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceSpellTrap(human, 0, archives, faceUp: true);
        var fiend = Fiend(9602, "Demonio Descartable");
        human.Hand.Add(dealsDw);
        human.Hand.Add(fiend);
        human.Hand.Add(Filler);
        int deck = human.Deck.Count;

        Assert.True(engine.ActivateSpell(0).Success);
        // Descarta al Demonio (el unico monstruo), Archivos pregunta, descarta Filler y roba 2.
        Drive(engine);

        Assert.Contains(fiend, human.Graveyard);
        Assert.Contains(Filler, human.Graveyard);
        Assert.Equal(deck - 2, human.Deck.Count);
        Assert.Equal(2, human.Hand.Count);
        Assert.Same(archives, human.SpellTrapZones[0]!.Card); // la Continua sigue en el Campo
    }

    [Fact]
    public void ContinuousSpell_DoesNotTrigger_ForAnOwnNonArchetypeCard()
    {
        var archives = Spell(9506, "Archivos de Prueba 2", SpellSubType.Continuous,
            new MonsterEffect(MonsterEffectType.Trigger, new[] { Step("draw", ("Who", "Controller"), ("Count", "2")) },
                EffectEvent.DiscardedByCardEffect,
                activationConditions: new[] { Cond("event_caused_by", false, ("ByOpponent", "true"), ("SourceNameContains", "Mundo Oscuro")) },
                subject: EventSubject.AnyCard, eventFilter: EffectActionParams.Of(("Side", "Own"), ("Type", "Demonio"))));
        var plainDiscard = Spell(9507, "Descarte Comun", SpellSubType.Normal,
            OnActivate(new[] { Step("discard", ("Who", "Controller"), ("Count", "1")) }));
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceSpellTrap(human, 0, archives, faceUp: true);
        human.Hand.Add(plainDiscard);
        human.Hand.Add(Fiend(9603, "Demonio Comun"));
        int deck = human.Deck.Count;

        engine.ActivateSpell(0);
        Drive(engine);

        Assert.Equal(deck, human.Deck.Count);
    }

    [Fact]
    public void FieldSpell_BoostsMatchingMonstersOfBothPlayers_AndHasAnIgnitionFromTheField()
    {
        // "Todos los monstruos Demonio ganan 300 ATK/DEF. Una vez por turno: puedes desterrar 1 monstruo Demonio de tu Cementerio; descarta 1 monstruo Demonio, y despues roba 1 carta."
        var gates = Spell(9508, "Puertas de Prueba", SpellSubType.Field,
            new MonsterEffect(MonsterEffectType.Continuous, new[] { Step("stat_modifier", ("Apply", "AllMatching"), ("Side", "Both"), ("Type", "Demonio"), ("Attack", "300"), ("Defense", "300")) }),
            new MonsterEffect(MonsterEffectType.Ignition,
                new[] { Step("discard", ("Who", "Controller"), ("Count", "1"), ("CardKind", "Monster"), ("Type", "Demonio")), When("previous_step_succeeded", "draw", ("Count", "1")) },
                oncePerTurn: true,
                costs: new[] { Step("banish", ("From", "Graveyard"), ("Side", "Own"), ("CardKind", "Monster"), ("Type", "Demonio"), ("Count", "1"), ("Min", "1")) }));
        var (engine, human, cpu) = TestDuelFactory.Create();
        var myFiend = TestDuelFactory.PlaceOnField(human, 0, Fiend(9604, "Demonio Propio"));
        var theirFiend = TestDuelFactory.PlaceOnField(cpu, 0, Fiend(9605, "Demonio Rival"));
        var warrior = TestDuelFactory.PlaceOnField(cpu, 1, TestCards.Level4Strong);
        human.Hand.Add(gates);
        human.Hand.Add(Fiend(9606, "Demonio en Mano"));
        human.Graveyard.Add(Fiend(9607, "Demonio en Cementerio"));

        Assert.True(engine.ActivateSpell(0).Success);
        Assert.Same(gates, human.FieldZone!.Card);
        Assert.Equal(1400 + 300, EffectiveStats.EffectiveAttack(myFiend, engine.State, human));
        Assert.Equal(1400 + 300, EffectiveStats.EffectiveAttack(theirFiend, engine.State, cpu));
        Assert.Equal(1500, EffectiveStats.EffectiveAttack(warrior, engine.State, cpu));

        var ignition = Assert.Single(engine.GetActivatableEffects(PlayerSide.Human));
        Assert.Equal(CardZone.FieldZone, ignition.Card.Zone);
        int deck = human.Deck.Count;
        Assert.True(engine.ActivateMonsterEffect(ignition).Success);
        Drive(engine);

        Assert.Single(human.Banished);
        Assert.Equal(deck - 1, human.Deck.Count);
        Assert.Empty(engine.GetActivatableEffects(PlayerSide.Human)); // una vez por turno
    }

    // ------------------------------------------------------------------ Cementerio

    [Fact]
    public void GraveyardIgnition_IsNotAvailableTheTurnTheCardWasSentThere()
    {
        // "Selecciona hasta 3 cartas en cualquier Cementerio; destierralas ... Durante tu Main Phase, excepto en el turno en que esta carta fue mandada al Cementerio: puedes desterrar esta carta, y despues ... añade a tu mano 1 Demonio desterrado."
        var puppet = Spell(9509, "Titiritero de Prueba", SpellSubType.Normal,
            OnActivate(new[] { Step("banish", ("UseTargets", "true")) }, oncePerTurn: true,
                target: EffectActionParams.Of(("From", "Graveyard"), ("Side", "Both"), ("Count", "3"), ("Min", "1"))),
            new MonsterEffect(MonsterEffectType.Ignition,
                new[] { Step("add_to_hand", ("From", "Banished"), ("Side", "Own"), ("CardKind", "Monster"), ("Type", "Demonio"), ("Count", "1"), ("Min", "1")) },
                activationZone: EffectZone.Graveyard,
                activationConditions: new[] { Cond("not_sent_to_graveyard_this_turn") },
                costs: new[] { Step("banish_self") }));
        var (engine, human, cpu) = TestDuelFactory.Create();
        var fiend = Fiend(9608, "Demonio Desterrable");
        human.Graveyard.Add(fiend);
        cpu.Graveyard.Add(TestCards.Level4Strong);
        human.Hand.Add(puppet);

        Assert.True(engine.ActivateSpell(0).Success);
        Drive(engine, choice => choice.Kind == ChoiceKind.SelectCards ? Enumerable.Range(0, choice.Candidates.Count).ToArray() : null);

        Assert.Contains(fiend, human.Banished);
        Assert.Contains(puppet, human.Graveyard);
        Assert.Empty(engine.GetActivatableEffects(PlayerSide.Human));

        NextTurn(engine); // CPU
        NextTurn(engine); // Humano otra vez
        var fromGraveyard = Assert.Single(engine.GetActivatableEffects(PlayerSide.Human));
        Assert.Equal(CardZone.Graveyard, fromGraveyard.Card.Zone);
        Assert.True(engine.ActivateMonsterEffect(fromGraveyard).Success);
        Drive(engine);

        Assert.Contains(puppet, human.Banished);
        Assert.Contains(fiend, human.Hand);
    }

    // ------------------------------------------------------------------ Juego Rapido / Trampas / ataque

    [Fact]
    public void QuickPlay_FromHandOnlyInYourTurn_AndNotTheTurnItWasSet()
    {
        var quick = Spell(9510, "Rapida de Prueba", SpellSubType.QuickPlay, OnActivate(new[] { Step("draw", ("Count", "1")) }));
        var cpuSpell = Spell(9511, "Robo de la CPU", SpellSubType.Normal, OnActivate(new[] { Step("draw", ("Count", "1")) }));
        var (engine, human, cpu) = TestDuelFactory.Create(firstPlayerIndex: 1);
        cpu.Hand.Add(cpuSpell);
        human.Hand.Add(quick);

        Assert.True(engine.ActivateSpell(0).Success); // la CPU abre una Cadena en su turno
        Assert.Equal(PlayerSide.Human, engine.State.ChainPendingResponder);
        var fromHand = engine.ActivateSpell(0);
        Assert.False(fromHand.Success);
        Assert.Contains("tu propio turno", fromHand.Message);
        Drive(engine);

        var (engine2, human2, _) = TestDuelFactory.Create();
        human2.Hand.Add(quick);
        Assert.True(engine2.SetSpellOrTrap(0).Success);
        Assert.False(engine2.ActivateSetCard(0).Success);
    }

    [Fact]
    public void SetQuickPlay_CanBeActivated_WhenTheOpponentDeclaresAnAttack()
    {
        // Rapida Colocada en el turno anterior: "destruye 1 monstruo del adversario".
        var quick = Spell(9512, "Defensa Rapida", SpellSubType.QuickPlay,
            OnActivate(new[] { Step("destroy", ("UseTargets", "true")) },
                target: EffectActionParams.Of(("From", "MonsterZone"), ("Side", "Opponent"), ("Count", "1"), ("Min", "1"))));
        var (engine, human, cpu) = TestDuelFactory.Create();
        human.Hand.Add(quick);
        Assert.True(engine.SetSpellOrTrap(0).Success);
        NextTurn(engine); // turno de la CPU
        TestDuelFactory.PlaceOnField(cpu, 0, TestCards.Level4Strong);
        engine.AdvancePhase(); // Battle Phase

        Assert.True(engine.DeclareAttack(0, -1).Success);
        var window = engine.State.PendingChoice;
        Assert.NotNull(window);
        Assert.True(window!.IsResponseWindow);
        Assert.Equal(PlayerSide.Human, window.Chooser);
        Assert.Contains(window.Options, o => o.Contains("Defensa Rapida"));

        int lp = human.LifePoints;
        Drive(engine, choice => choice.IsResponseWindow ? new[] { 1 } : null);

        Assert.Null(cpu.MonsterZones[0]);          // el atacante fue destruido
        Assert.Equal(lp, human.LifePoints);         // y el ataque no se realizo
        Assert.Contains(quick, human.Graveyard);
    }

    [Fact]
    public void Attack_WithoutAResponseAvailable_HasNoWindow()
    {
        var (engine, human, cpu) = TestDuelFactory.Create();
        NextTurn(engine);
        TestDuelFactory.PlaceOnField(cpu, 0, TestCards.Level4Strong);
        engine.AdvancePhase();

        int lp = human.LifePoints;
        Assert.True(engine.DeclareAttack(0, -1).Success);
        Assert.Null(engine.State.PendingChoice);
        Assert.Equal(lp - 1500, human.LifePoints);
    }

    [Fact]
    public void LegacyTrap_InTheAttackWindow_TargetsTheAttacker()
    {
        var (engine, human, cpu) = TestDuelFactory.Create();
        NextTurn(engine);
        TestDuelFactory.PlaceSpellTrap(human, 0, TestCards.DestroyEffectTrap, faceUp: false);
        TestDuelFactory.PlaceOnField(cpu, 2, TestCards.Level4Strong);
        engine.AdvancePhase();

        Assert.True(engine.DeclareAttack(2, -1).Success);
        Assert.Contains(engine.State.PendingChoice!.Options, o => o.Contains("sobre el atacante"));
        int lp = human.LifePoints;
        Drive(engine, choice => choice.IsResponseWindow ? new[] { 1 } : null);

        Assert.Null(cpu.MonsterZones[2]);
        Assert.Equal(lp, human.LifePoints);
    }

    [Fact]
    public void TrapSetInYourTurn_CanBeUsedInTheOpponentsNextTurn()
    {
        var (engine, human, cpu) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.NormalTrap);
        Assert.True(engine.SetSpellOrTrap(0).Success);
        NextTurn(engine);

        Assert.False(human.SpellTrapZones[0]!.SetThisTurn);
        cpu.Hand.Add(Spell(9513, "Robo Rival", SpellSubType.Normal, OnActivate(new[] { Step("draw", ("Count", "1")) })));
        Assert.True(engine.ActivateSpell(cpu.Hand.Count - 1).Success);
        Assert.True(engine.ActivateSetCard(0).Success); // el humano responde con la Trampa
        Assert.Equal(2, engine.State.Chain.Count);
        Drive(engine);
    }

    // ------------------------------------------------------------------ Reglas del turno

    [Fact]
    public void SummonLock_AllowsTheEffectsSummon_ThenBlocksSummons_ButNotSetting()
    {
        // "Selecciona 1 monstruo 'Mundo Oscuro' en tu Cementerio; Invocalo. No puedes Invocar monstruos el turno en que activas esta carta, excepto por este efecto (pero puedes Colocar)."
        var portal = Spell(9514, "Portal de Prueba", SpellSubType.Normal,
            OnActivate(new[] { Step("special_summon", ("UseTargets", "true")), Step("summon_lock") },
                target: EffectActionParams.Of(("From", "Graveyard"), ("Side", "Own"), ("CardKind", "Monster"), ("NameContains", "Mundo Oscuro"), ("Count", "1"), ("Min", "1")),
                conditions: new[] { Cond("summoned_this_turn", negate: true) }));
        var (engine, human, _) = TestDuelFactory.Create();
        human.Graveyard.Add(Fiend(9609, "Guerrero del Mundo Oscuro"));
        human.Graveyard.Add(Fiend(9610, "Demonio Cualquiera"));
        human.Hand.Add(portal);
        human.Hand.Add(TestCards.Level4Strong);
        human.Hand.Add(TestCards.Level4Weak);

        Assert.True(engine.ActivateSpell(0).Success);
        Drive(engine);

        Assert.Equal("Guerrero del Mundo Oscuro", human.MonsterZones[0]!.Card.Name);
        Assert.False(engine.NormalSummon(0, BattlePosition.Attack).Success);
        Assert.True(engine.SetMonster(0).Success);
    }

    [Fact]
    public void SummonedThisTurnCondition_BlocksTheActivation_AfterANormalSummon()
    {
        var portal = Spell(9515, "Portal Tardio", SpellSubType.Normal,
            OnActivate(new[] { Step("draw") }, conditions: new[] { Cond("summoned_this_turn", negate: true) }));
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.Level4Strong);
        human.Hand.Add(portal);

        Assert.True(engine.NormalSummon(0, BattlePosition.Attack).Success);
        Assert.False(engine.ActivateSpell(0).Success);
    }

    // ------------------------------------------------------------------ Manos

    [Fact]
    public void CardDestruction_DiscardsWholeHands_AndEachDrawsTheSameAmount()
    {
        var destruction = Spell(9516, "Destruccion de Prueba", SpellSubType.Normal,
            OnActivate(new[] { Step("discard", ("Who", "Both"), ("All", "true")), Step("draw", ("Who", "Both"), ("SameAsLastPerPlayer", "true")) }));
        var (engine, human, cpu) = TestDuelFactory.Create();
        human.Hand.Add(destruction);
        human.Hand.AddRange(new Card[] { Filler, Filler, Filler });
        cpu.Hand.AddRange(new Card[] { Filler, Filler });

        engine.ActivateSpell(0);
        Drive(engine);

        Assert.Equal(3, human.Hand.Count);
        Assert.Equal(2, cpu.Hand.Count);
        Assert.Equal(3 + 1, human.Graveyard.Count); // 3 descartadas + la Magia
        Assert.Equal(2, cpu.Graveyard.Count);
    }

    [Fact]
    public void DraggedToTheGrave_ShowsHands_AndEachPicksFromTheOpponentsHand()
    {
        // "Ambos muestran sus manos, cada uno elige 1 carta de la mano de su adversario, y despues se descartan, y despues ambos roban 1."
        var dragged = Spell(9517, "Arrastre de Prueba", SpellSubType.Normal,
            OnActivate(new[]
            {
                Step("reveal_hand", ("Who", "Both")),
                Step("discard", ("Who", "Both"), ("Count", "1"), ("Chooser", "OwnersOpponent")),
                Step("draw", ("Who", "Both"), ("Count", "1")),
            }));
        var (engine, human, cpu) = TestDuelFactory.Create();
        human.Hand.Add(dragged);
        human.Hand.Add(TestCards.Level4Strong);
        cpu.Hand.Add(TestCards.Level8TwoTributes);
        cpu.Hand.Add(Filler);

        Assert.True(engine.ActivateSpell(0).Success);
        engine.PassPriority();
        engine.PassPriority();

        // Se le muestra la mano rival al humano y despues el elige de la mano de la CPU (boca arriba).
        Assert.Equal(ChoiceKind.Reveal, engine.State.PendingChoice!.Kind);
        engine.AcknowledgeReveal();
        var pick = engine.State.PendingChoice!;
        Assert.Equal(PlayerSide.Human, pick.Chooser);
        Assert.True(pick.RevealCandidates);
        Assert.All(pick.Candidates, c => Assert.Equal(PlayerSide.Cpu, c.Side));
        Drive(engine);

        Assert.Contains(TestCards.Level8TwoTributes, cpu.Graveyard);
        Assert.Contains(TestCards.Level4Strong, human.Graveyard);
        Assert.Single(human.Hand);
        Assert.Equal(2, cpu.Hand.Count);
    }

    // ------------------------------------------------------------------ Fusion por efecto

    [Fact]
    public void FusionSummonByEffect_BanishesMaterialsFromFieldAndGraveyard()
    {
        // "Invoca por Fusion 1 Monstruo de Fusion Dragon, desterrando de tu Campo o Cementerio los materiales."
        var accession = Spell(9518, "Accesion de Prueba", SpellSubType.Normal,
            OnActivate(new[] { Step("fusion_summon", ("From", "MonsterZone,Graveyard"), ("MaterialMove", "Banish"), ("Type", "Dragon")) }));
        var (engine, human, _) = TestDuelFactory.Create(recipes: new[] { TestCards.ValidFusionRecipe });
        TestDuelFactory.PlaceOnField(human, 0, TestCards.FusionMaterialA);
        human.Graveyard.Add(TestCards.FusionMaterialB);
        human.Hand.Add(accession);

        Assert.True(engine.ActivateSpell(0).Success);
        Drive(engine);

        var fusion = human.MonsterZones.Single(m => m != null)!;
        Assert.Same(TestCards.FusionResult, fusion.Card);
        Assert.Equal(SummonMethod.Fusion, fusion.SummonMethod);
        Assert.Contains(TestCards.FusionMaterialA, human.Banished);
        Assert.Contains(TestCards.FusionMaterialB, human.Banished);
    }

    [Fact]
    public void FusionSummonByEffect_UsesHandMaterials_OnlyForTheNamedArchetype()
    {
        var accession = Spell(9519, "Accesion Restringida", SpellSubType.Normal,
            OnActivate(new[] { Step("fusion_summon", ("From", "MonsterZone,Graveyard"), ("HandIfNameContains", "Mundo Oscuro")) }));
        var (engine, human, _) = TestDuelFactory.Create(recipes: new[] { TestCards.ValidFusionRecipe });
        human.Hand.Add(accession);
        human.Hand.Add(TestCards.FusionMaterialA);
        human.Graveyard.Add(TestCards.FusionMaterialB);

        // "Fusion de Prueba" no es "Mundo Oscuro": la mano no cuenta y no hay Fusion posible.
        Assert.False(engine.ActivateSpell(0).Success);
    }

    // ------------------------------------------------------------------ Grapha

    private static MonsterCard Overlord() => new(9700, "Señor Supremo de Prueba", 3200, 2300, 10, "Demonio", MonsterAttribute.Dark,
        MonsterCategory.Fusion, effects: new[]
        {
            new MonsterEffect(MonsterEffectType.Quick, new[] { Step("replace_responded_effect", ("Count", "1")) }, oncePerTurn: true,
                activationConditions: new[]
                {
                    Cond("responding_to_activation", false, ("Who", "Opponent"), ("MonsterEffect", "true"), ("NormalSpell", "true"), ("NormalTrap", "true")),
                    Cond("count_at_least", false, ("From", "Hand"), ("Side", "Own"), ("Amount", "1")),
                }),
            new MonsterEffect(MonsterEffectType.Trigger,
                new[]
                {
                    Step("special_summon", ("From", "Banished,Graveyard"), ("Side", "Own"), ("CardId", "9701"), ("Count", "1"), ("Min", "1")),
                    Step("discard", ("Who", "Both"), ("Count", "1")),
                },
                EffectEvent.LeavesField, optional: true,
                activationConditions: new[] { Cond("event_caused_by", false, ("ByOpponent", "true")), Cond("event_summoned_by", false, ("Method", "Fusion")), Cond("event_controlled_by_owner") }),
        });

    private static readonly MonsterCard DragonLord = new(9701, "Señor Dragon de Prueba", 2700, 1800, 8, "Demonio", MonsterAttribute.Dark);

    [Fact]
    public void QuickEffect_ChangesTheOpponentsSpellInto_TheirOpponentDiscards()
    {
        var bigDraw = Spell(9520, "Gran Robo Rival", SpellSubType.Normal, OnActivate(new[] { Step("draw", ("Count", "2")) }));
        var (engine, human, cpu) = TestDuelFactory.Create(firstPlayerIndex: 1);
        TestDuelFactory.PlaceOnField(human, 0, Overlord()).SummonMethod = SummonMethod.Fusion;
        human.Hand.Add(Filler);
        cpu.Hand.Add(bigDraw);
        int cpuDeck = cpu.Deck.Count;

        Assert.True(engine.ActivateSpell(0).Success);
        var quick = Assert.Single(engine.GetActivatableEffects(PlayerSide.Human));
        Assert.True(engine.ActivateMonsterEffect(quick).Success);
        Drive(engine);

        Assert.Equal(cpuDeck, cpu.Deck.Count);       // no robo
        Assert.Contains(Filler, human.Graveyard);    // "tu adversario descarta 1 carta": el humano
        Assert.Contains(bigDraw, cpu.Graveyard);
    }

    [Fact]
    public void QuickEffect_RespondingCondition_IsNotMet_ForTheControllersOwnActivation()
    {
        var myDraw = Spell(9521, "Mi Robo", SpellSubType.Normal, OnActivate(new[] { Step("draw", ("Count", "1")) }));
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, Overlord());
        human.Hand.Add(myDraw);
        human.Hand.Add(Filler);

        engine.ActivateSpell(0);
        engine.PassPriority(); // CPU pasa: vuelve al humano
        Assert.Empty(engine.GetActivatableEffects(PlayerSide.Human));
        Drive(engine);
    }

    [Fact]
    public void LeavesFieldTrigger_FusionSummonedAndDestroyedByTheOpponent_RevivesTheDragonLord()
    {
        var destroyer = Spell(9522, "Destructor Rival", SpellSubType.Normal,
            OnActivate(new[] { Step("destroy", ("UseTargets", "true")) },
                target: EffectActionParams.Of(("From", "MonsterZone"), ("Side", "Opponent"), ("Count", "1"), ("Min", "1"))));
        var (engine, human, cpu) = TestDuelFactory.Create(firstPlayerIndex: 1);
        var overlord = Overlord();
        TestDuelFactory.PlaceOnField(human, 0, overlord).SummonMethod = SummonMethod.Fusion;
        human.Graveyard.Add(DragonLord);
        cpu.Hand.Add(destroyer);
        cpu.Hand.Add(Filler);
        human.Hand.Add(TestCards.Level4Strong);

        Assert.True(engine.ActivateSpell(0).Success);
        Drive(engine);

        Assert.Contains(overlord, human.Graveyard);
        Assert.Contains(human.MonsterZones, m => m?.Card == DragonLord);
        Assert.Contains(TestCards.Level4Strong, human.Graveyard); // cada jugador con cartas descarta 1
        Assert.Contains(Filler, cpu.Graveyard);
    }

    [Fact]
    public void LeavesFieldTrigger_DoesNotActivate_IfItWasNotFusionSummoned()
    {
        var destroyer = Spell(9523, "Destructor Rival 2", SpellSubType.Normal,
            OnActivate(new[] { Step("destroy", ("UseTargets", "true")) },
                target: EffectActionParams.Of(("From", "MonsterZone"), ("Side", "Opponent"), ("Count", "1"), ("Min", "1"))));
        var (engine, human, cpu) = TestDuelFactory.Create(firstPlayerIndex: 1);
        TestDuelFactory.PlaceOnField(human, 0, Overlord()).SummonMethod = SummonMethod.Special;
        human.Graveyard.Add(DragonLord);
        cpu.Hand.Add(destroyer);

        engine.ActivateSpell(0);
        Drive(engine);

        Assert.All(human.MonsterZones, m => Assert.Null(m));
    }

    [Fact]
    public void LookAtARandomCard_ThenSummonItIfItIsAMonster()
    {
        // "... mira 1 carta al azar en la mano de tu adversario y despues, si es un monstruo, puedes Invocarlo de Modo Especial a tu Campo."
        var spy = Spell(9524, "Espia de Prueba", SpellSubType.Normal,
            OnActivate(new[]
            {
                Step("reveal_random_hand", ("Who", "Opponent"), ("Count", "1")),
                OptionalStep("special_summon", ("UseLastAffected", "true"), ("CardKind", "Monster"), ("ToField", "Own"), ("Position", "Attack")),
            }));
        var (engine, human, cpu) = TestDuelFactory.Create();
        human.Hand.Add(spy);
        cpu.Hand.Add(TestCards.Level6OneTribute);

        engine.ActivateSpell(0);
        Drive(engine);

        var stolen = human.MonsterZones.Single(m => m != null)!;
        Assert.Same(TestCards.Level6OneTribute, stolen.Card);
        Assert.Equal(PlayerSide.Cpu, stolen.Owner);
        Assert.Empty(cpu.Hand);
    }

    // ------------------------------------------------------------------ Lilith / Cuervo / Back Jack

    [Fact]
    public void TributeCost_ThenSetATrapFromTheDeck()
    {
        var lilith = new MonsterCard(9702, "Dama de Prueba", 2000, 0, 3, "Demonio", MonsterAttribute.Dark, MonsterCategory.Effect,
            effects: new[]
            {
                new MonsterEffect(MonsterEffectType.Quick,
                    new[] { Step("set_spell_trap", ("From", "Hand,Deck"), ("CardKind", "Trap"), ("Count", "1"), ("Min", "1")) }, oncePerTurn: true,
                    costs: new[] { Step("tribute", ("Count", "1"), ("Min", "1")) })
            });
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, lilith);
        TestDuelFactory.PlaceOnField(human, 1, TestCards.Level2Fodder);
        human.Deck.Insert(3, TestCards.NormalTrap);

        Assert.True(engine.ActivateMonsterEffect(Assert.Single(engine.GetActivatableEffects(PlayerSide.Human))).Success);
        Drive(engine, choice => choice.Kind == ChoiceKind.SelectCards && choice.Candidates.Any(c => c.Card == TestCards.Level2Fodder)
            ? new[] { choice.Candidates.ToList().FindIndex(c => c.Card == TestCards.Level2Fodder) } : null);

        Assert.Contains(TestCards.Level2Fodder, human.Graveyard);
        var set = human.SpellTrapZones.Single(z => z != null)!;
        Assert.Same(TestCards.NormalTrap, set.Card);
        Assert.False(set.FaceUp);
        Assert.True(set.SetThisTurn);
    }

    [Fact]
    public void DiscardAnyNumber_GivesLevelsAndAttackPerCard_UntilEndOfTurn()
    {
        // "Una vez por turno: puedes descartar cualquier numero de cartas; hasta el final del turno gana 1 Nivel y 400 ATK por cada una."
        var raven = new MonsterCard(9703, "Cuervo de Prueba", 1300, 1000, 2, "Demonio", MonsterAttribute.Dark, MonsterCategory.Effect,
            effects: new[]
            {
                new MonsterEffect(MonsterEffectType.Ignition,
                    new[] { Step("modify_stats", ("Apply", "Self"), ("Attack", "400"), ("Level", "1"), ("Scale", "PerLastAffected"), ("Duration", "UntilEndOfTurn")) },
                    oncePerTurn: true, costs: new[] { Step("discard", ("Who", "Controller"), ("AnyNumber", "true")) })
            });
        var (engine, human, _) = TestDuelFactory.Create();
        var instance = TestDuelFactory.PlaceOnField(human, 0, raven);
        human.Hand.AddRange(new Card[] { Filler, Filler, Filler });

        engine.ActivateMonsterEffect(Assert.Single(engine.GetActivatableEffects(PlayerSide.Human)));
        Drive(engine, choice => choice.Kind == ChoiceKind.SelectCards ? new[] { 0, 1 } : null);

        Assert.Equal(1300 + 800, EffectiveStats.EffectiveAttack(instance, engine.State, human));
        Assert.Equal(4, instance.EffectiveLevel);
        Assert.Single(human.Hand);

        NextTurn(engine);
        Assert.Equal(1300, EffectiveStats.EffectiveAttack(instance, engine.State, human));
        Assert.Equal(2, instance.EffectiveLevel);
    }

    [Fact]
    public void QuickEffectFromTheGraveyard_InTheOpponentsTurn_ExcavatesAndSetsANormalTrap()
    {
        var backJack = new MonsterCard(9704, "Rey de Prueba", 0, 0, 1, "Demonio", MonsterAttribute.Dark, MonsterCategory.Effect,
            effects: new[]
            {
                new MonsterEffect(MonsterEffectType.Quick,
                    new[] { Step("excavate", ("IfMatch", "SetOnField"), ("Otherwise", "Bottom"), ("CardKind", "Trap"), ("SubType", "Normal")) },
                    activationZone: EffectZone.Graveyard, oncePerTurn: true,
                    activationConditions: new[] { Cond("is_your_turn", negate: true) },
                    costs: new[] { Step("banish_self") })
            });
        var (engine, human, cpu) = TestDuelFactory.Create(firstPlayerIndex: 1);
        human.Graveyard.Add(backJack);
        human.Deck.Insert(0, TestCards.NormalTrap);
        cpu.Hand.Add(Spell(9525, "Robo de la CPU 2", SpellSubType.Normal, OnActivate(new[] { Step("draw", ("Count", "1")) })));

        Assert.True(engine.ActivateSpell(0).Success);
        var quick = Assert.Single(engine.GetActivatableEffects(PlayerSide.Human));
        Assert.True(engine.ActivateMonsterEffect(quick).Success);
        Drive(engine);

        Assert.Contains(backJack, human.Banished);
        Assert.Same(TestCards.NormalTrap, human.SpellTrapZones.Single(z => z != null)!.Card);
    }

    // ------------------------------------------------------------------ Equipo

    [Fact]
    public void EquipSpell_PassivesApplyToTheEquippedMonster()
    {
        var armor = new SpellCard(9526, "Armadura de Prueba", SpellSubType.Equip, effects: new[]
        {
            new MonsterEffect(MonsterEffectType.Continuous, new[]
            {
                Step("stat_modifier", ("Apply", "Equipped"), ("Attack", "500")),
                Step("battle_indestructible", ("Apply", "Equipped")),
            })
        });
        var (engine, human, cpu) = TestDuelFactory.Create();
        var weak = TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Weak);
        var other = TestDuelFactory.PlaceOnField(human, 1, TestCards.Level4Strong);
        human.Hand.Add(armor);

        Assert.True(engine.ActivateSpell(0, new EffectTarget { Side = PlayerSide.Human, ZoneIndex = 0 }).Success);
        TestDuelFactory.CloseChain(engine);

        Assert.Equal(1500, EffectiveStats.EffectiveAttack(weak, engine.State, human));
        Assert.Equal(1500, EffectiveStats.EffectiveAttack(other, engine.State, human));
        Assert.True(ContinuousEffects.IsBattleIndestructible(weak, engine.State));
        Assert.False(ContinuousEffects.IsBattleIndestructible(other, engine.State));
    }
}
