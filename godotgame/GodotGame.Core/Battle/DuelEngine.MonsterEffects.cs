using GodotGame.Core.Effects;
using GodotGame.Core.Effects.Monster;
using GodotGame.Core.Entities;

namespace GodotGame.Core.Battle;

/// <summary>Un efecto de Monstruo que un jugador puede activar ahora mismo (ver <see cref="DuelEngine.GetActivatableEffects"/>).</summary>
public sealed record ActivatableEffect(CardRef Card, int EffectIndex, MonsterEffect Effect)
{
    public string Label =>
        $"{Card.Card.Name} — {MonsterEffectCatalog.TypeLabel(Effect.Type)} ({CardRef.ZoneName(Card.Zone)})"
        + (string.IsNullOrWhiteSpace(Effect.Text) ? "" : $": {Effect.Text}");
}

/// <summary>
/// Efectos por datos en el motor (de Monstruos y de Magias/Trampas):
/// activacion manual (Encendido, Rapido, No clasificado), activacion de una
/// Magia/Trampa, ventana de efectos Disparados/Volteo tras cada accion,
/// ventana de respuesta a un ataque, resolucion en la Cadena y elecciones de
/// los jugadores a mitad de efecto.
///
/// La resolucion se escribe como corrutinas (<c>IEnumerable&lt;ChoiceRequest&gt;</c>):
/// cuando un paso necesita una decision la devuelve, el motor la publica en
/// <see cref="DuelState.PendingChoice"/> y se detiene hasta que la UI o la IA
/// responden con <see cref="AnswerYesNo"/>/<see cref="AnswerCards"/>/<see cref="AnswerOption"/>.
/// Si nada necesita decision, todo se resuelve en la misma llamada (como antes).
/// </summary>
public sealed partial class DuelEngine
{
    private readonly Stack<IEnumerator<ChoiceRequest>> _routines = new();
    private bool _pumping;

    private enum EndTurnStage { None, HandLimit, Finish }
    private EndTurnStage _endTurnStage;

    /// <summary>Un ataque declarado que espera a que el defensor responda (y a que se resuelva lo que active).</summary>
    private sealed record PendingAttack(int AttackerZone, int TargetZone, Card Attacker, Card? Target);
    private PendingAttack? _pendingAttack;

    /// <summary>Jugador que tiene la ventana de respuesta a un ataque (puede activar sin ser su turno y sin Cadena).</summary>
    private PlayerSide? _windowSide;

    /// <summary>Eventos de Invocacion que ya tuvieron su ventana de respuesta.</summary>
    private readonly HashSet<TriggerEvent> _summonWindowSeen = new(ReferenceEqualityComparer.Instance);

    // ------------------------------------------------------------ Elecciones

    /// <summary>Responde una <see cref="ChoiceKind.YesNo"/> pendiente.</summary>
    public ActionResult AnswerYesNo(bool yes)
    {
        if (State.PendingChoice is not { Kind: ChoiceKind.YesNo } choice) return ActionResult.Fail("No hay ninguna pregunta pendiente.");
        choice.AnswerYesNo(yes);
        State.PendingChoice = null;
        Pump();
        return ActionResult.Ok();
    }

    /// <summary>Responde una <see cref="ChoiceKind.SelectOption"/> pendiente.</summary>
    public ActionResult AnswerOption(int option)
    {
        if (State.PendingChoice is not { Kind: ChoiceKind.SelectOption } choice) return ActionResult.Fail("No hay ninguna opcion pendiente.");
        if (option < 0 || option >= choice.Options.Count) return ActionResult.Fail("Opcion invalida.");
        choice.AnswerOption(option);
        State.PendingChoice = null;
        Pump();
        return ActionResult.Ok();
    }

    /// <summary>Cierra una <see cref="ChoiceKind.Reveal"/> pendiente (el jugador ya vio las cartas).</summary>
    public ActionResult AcknowledgeReveal()
    {
        if (State.PendingChoice is not { Kind: ChoiceKind.Reveal } choice) return ActionResult.Fail("No hay cartas que mostrar.");
        choice.Acknowledge();
        State.PendingChoice = null;
        Pump();
        return ActionResult.Ok();
    }

    /// <summary>Responde una <see cref="ChoiceKind.SelectCards"/> pendiente con los indices de <see cref="ChoiceRequest.Candidates"/> elegidos.</summary>
    public ActionResult AnswerCards(int[] indices)
    {
        if (State.PendingChoice is not { Kind: ChoiceKind.SelectCards } choice) return ActionResult.Fail("No hay ninguna seleccion pendiente.");
        var error = choice.ValidateCards(indices ?? Array.Empty<int>());
        if (error != null) return ActionResult.Fail(error);
        choice.AnswerCards(indices ?? Array.Empty<int>());
        State.PendingChoice = null;
        Pump();
        return ActionResult.Ok();
    }

    private ActionResult ValidateNoPendingChoice() =>
        State.PendingChoice != null
            ? ActionResult.Fail("Primero responde la eleccion pendiente.")
            : ActionResult.Ok();

    // ------------------------------------------------------------ Bucle de proceso

    private void RunRoutine(IEnumerable<ChoiceRequest> routine)
    {
        _routines.Push(routine.GetEnumerator());
        Pump();
    }

    /// <summary>
    /// Avanza todo lo que pueda avanzar sin una decision de un jugador:
    /// corrutinas en curso, pase automatico de Prioridad (si esta activado),
    /// ventana de efectos Disparados y la End Phase diferida.
    /// </summary>
    private void Pump()
    {
        if (_pumping) return;
        _pumping = true;
        try
        {
            while (true)
            {
                if (State.IsOver)
                {
                    _routines.Clear();
                    State.PendingChoice = null;
                    return;
                }
                if (State.PendingChoice != null) return;

                if (_routines.Count > 0)
                {
                    var top = _routines.Peek();
                    if (!top.MoveNext()) { _routines.Pop(); continue; }
                    if (top.Current is { Answered: false } request) { State.PendingChoice = request; return; }
                    continue;
                }

                if (State.Chain.Count > 0)
                {
                    if (!State.ChainResolving && _config.AutoPassWhenNoResponse
                        && State.ChainPendingResponder is { } responder && !HasChainResponse(responder))
                    {
                        PassInternal();
                        continue;
                    }
                    return;
                }

                if (TryOpenSummonWindow()) continue;

                if (State.TriggerEvents.Count > 0)
                {
                    OpenTriggerWindow();
                    continue;
                }

                if (_pendingAttack != null)
                {
                    ContinuePendingAttack();
                    continue;
                }

                if (_endTurnStage != EndTurnStage.None)
                {
                    ContinueEndTurn();
                    continue;
                }

                return;
            }
        }
        finally
        {
            _pumping = false;
        }
    }

    // ------------------------------------------------------------ Activacion manual

    /// <summary>
    /// Efectos de Monstruo que <paramref name="side"/> puede activar ahora: de
    /// Encendido y No clasificados en su propia Main Phase sin Cadena abierta;
    /// Rapidos en su turno o cuando tiene la Prioridad en una Cadena.
    /// </summary>
    public IReadOnlyList<ActivatableEffect> GetActivatableEffects(PlayerSide side)
    {
        var result = new List<ActivatableEffect>();
        if (State.IsOver || State.PendingChoice != null || State.ChainResolving || _endTurnStage != EndTurnStage.None
            || State.PendingDiscardCount > 0 || _routines.Count > 0)
            return result;

        var player = State.GetPlayer(side);
        var zones = new[]
        {
            (CardZone.MonsterZone, EffectZone.Field), (CardZone.SpellTrapZone, EffectZone.Field), (CardZone.FieldZone, EffectZone.Field),
            (CardZone.Hand, EffectZone.Hand), (CardZone.Graveyard, EffectZone.Graveyard), (CardZone.Banished, EffectZone.Banished)
        };
        foreach (var (zone, effectZone) in zones)
        {
            var seen = new HashSet<(Card, int)>(ReferenceTupleComparer.Instance);
            foreach (var card in CardQuery.Enumerate(player, side, zone))
            {
                var effects = card.Card.Effects;
                if (effects.Count == 0) continue;
                if (zone is CardZone.MonsterZone or CardZone.SpellTrapZone or CardZone.FieldZone && !card.IsFaceUp(State)) continue;

                for (int i = 0; i < effects.Count; i++)
                {
                    var effect = effects[i];
                    if (!effect.IsManual || effect.ActivationZone != effectZone) continue;
                    // Copias identicas en mano/Cementerio: basta con ofrecer una.
                    if (zone is CardZone.Hand or CardZone.Graveyard or CardZone.Banished && !seen.Add((card.Card, i))) continue;
                    if (!CanActivateManual(side, card, effect, i)) continue;
                    result.Add(new ActivatableEffect(card, i, effect));
                }
            }
        }
        return result;
    }

    /// <summary>Activa un efecto devuelto por <see cref="GetActivatableEffects"/>.</summary>
    public ActionResult ActivateMonsterEffect(ActivatableEffect effect)
    {
        if (State.IsOver) return ActionResult.Fail("El duelo ha terminado.");
        var pending = ValidateNoPendingChoice();
        if (!pending.Success) return pending;

        var side = effect.Card.Side;
        var live = GetActivatableEffects(side).FirstOrDefault(e =>
            ReferenceEquals(e.Card.Card, effect.Card.Card) && e.Card.Zone == effect.Card.Zone && e.EffectIndex == effect.EffectIndex
            && (e.Card.Zone != CardZone.MonsterZone || e.Card.Index == effect.Card.Index));
        if (live == null) return ActionResult.Fail("Ese efecto no se puede activar ahora.");

        var activation = new EffectActivation(live.Card.Card, live.Effect, live.EffectIndex, side, live.Card) { RespondingTo = TopLinkInfo() };
        RunRoutine(ActivationRoutine(activation));
        return ActionResult.Ok();
    }

    /// <summary>Lo que se sabe del eslabon superior de la Cadena (al que responderia una activacion ahora).</summary>
    private ChainLinkInfo? TopLinkInfo()
    {
        if (State.Chain.Count == 0) return null;
        var top = State.Chain[^1];
        return new ChainLinkInfo(State.Chain.Count - 1, top.Controller, top.CardIn(State),
            top.MonsterEffect is { } effect && effect.Effect.Type != MonsterEffectType.Activation && effect.Source is MonsterCard);
    }

    private bool CanActivateManual(PlayerSide side, CardRef card, MonsterEffect effect, int effectIndex)
    {
        bool myTurn = State.ActivePlayer.Side == side;
        switch (effect.Type)
        {
            case MonsterEffectType.Ignition:
            case MonsterEffectType.Unclassified:
                if (!myTurn || State.Chain.Count > 0 || State.Phase is not (DuelPhase.Main1 or DuelPhase.Main2)) return false;
                break;
            case MonsterEffectType.Quick:
                if (State.Chain.Count > 0)
                {
                    if (State.ChainPendingResponder != side || effect.SpellSpeed < TopChainLinkSpeed()) return false;
                }
                else if (_windowSide == side) { }
                else if (!myTurn || State.Phase is not (DuelPhase.Main1 or DuelPhase.Battle or DuelPhase.Main2)) return false;
                break;
            default:
                return false;
        }

        var activation = new EffectActivation(card.Card, effect, effectIndex, side, card) { RespondingTo = TopLinkInfo() };
        return CanActivate(activation);
    }

    /// <summary>Una vez por turno, condiciones de activacion, costos pagables y objetivos disponibles.</summary>
    private bool CanActivate(EffectActivation activation)
    {
        var effect = activation.Effect;
        if (effect.OncePerTurn && State.UsedOncePerTurn.Contains(OncePerTurnKey(activation))) return false;

        var ctx = new MonsterEffectContext(State, activation);
        if (!MonsterEffectCatalog.AllMet(effect.ActivationConditions, ctx)) return false;

        activation.PayingCost = true;
        try
        {
            foreach (var cost in effect.Costs)
            {
                var info = MonsterEffectCatalog.Step(cost.ActionKind);
                if (info == null) return false;
                if (!info.Factory().CanRun(ctx, cost.Params)) return false;
            }
        }
        finally
        {
            activation.PayingCost = false;
        }

        if (effect.Target is { } target)
        {
            var query = new CardQuery(target, "MonsterZone", RelativeSide.Both);
            int available = query.Candidates(State, activation.Controller, activation.Source, activation.SourceRef).Count;
            if (query.Min > 0 && available < query.Min) return false;
        }

        // Una Magia/Trampa no se puede activar si su efecto no podria hacer
        // nada (ej. "Invoca por Fusion ..." sin materiales): se mira el primer
        // paso obligatorio y sin condiciones que no dependa de los objetivos.
        if (effect.Type == MonsterEffectType.Activation)
        {
            var first = effect.Steps.FirstOrDefault(s => !s.Optional && s.Conditions.Count == 0);
            if (first != null && !first.Params.GetBool("UseTargets") && !first.Params.GetBool("UseLastAffected")
                && MonsterEffectCatalog.Step(first.ActionKind) is { } info && !info.Factory().CanRun(ctx, first.Params))
                return false;
        }
        return true;
    }

    private string OncePerTurnKey(EffectActivation activation) => $"{activation.Controller}:{activation.OncePerTurnKey}";

    // ------------------------------------------------------------ Activacion (comun)

    /// <summary>
    /// Activa el efecto: paga costos, elige objetivos y lo agrega a la Cadena
    /// (o, si es No clasificado, lo aplica en el acto sin Cadena).
    /// </summary>
    private IEnumerable<ChoiceRequest> ActivationRoutine(EffectActivation activation, int chainZoneIndex = -1, EffectTarget? legacyTarget = null)
    {
        var effect = activation.Effect;
        var ctx = new MonsterEffectContext(State, activation);
        var player = State.GetPlayer(activation.Controller);
        bool cardActivation = effect.Type == MonsterEffectType.Activation;
        activation.RespondingTo ??= TopLinkInfo();

        if (cardActivation)
        {
            Log.Add($"{player.Name} activa {activation.Source.Name}.");
            if (chainZoneIndex >= 0) State.Events.Enqueue(new SpellTrapActivatedEvent(activation.Controller, chainZoneIndex, activation.Source));
            CardMover.RecordInPlace(State, EffectEvent.CardActivated, activation.Source, activation.Controller,
                chainZoneIndex >= 0 ? CardZone.SpellTrapZone : CardZone.FieldZone, new MoveCause(CauseKind.Rule, activation.Controller, activation.Source));
        }
        else
        {
            Log.Add($"{player.Name} activa el efecto de {activation.Source.Name} ({MonsterEffectCatalog.TypeLabel(effect.Type)}).");
            State.Events.Enqueue(new MonsterEffectActivatedEvent(activation.Controller, activation.Source, activation.SourceRef.Zone, activation.SourceRef.Index, effect.Type));
        }
        if (effect.OncePerTurn) State.UsedOncePerTurn.Add(OncePerTurnKey(activation));

        // Costos.
        activation.PayingCost = true;
        foreach (var cost in effect.Costs)
        {
            if (!MonsterEffectCatalog.AllMet(cost.Conditions, ctx)) continue;
            var info = MonsterEffectCatalog.Step(cost.ActionKind);
            if (info == null) continue;
            var step = info.Factory();
            if (!step.CanRun(ctx, cost.Params))
            {
                Log.Add($"No se puede pagar el costo de {activation.Source.Name}: el efecto no se activa.");
                activation.PayingCost = false;
                AbortCardActivation(activation, chainZoneIndex);
                yield break;
            }
            foreach (var request in step.Run(ctx, cost.Params)) yield return request;
        }
        activation.PayingCost = false;

        // Objetivos ("selecciona ...").
        if (effect.Target is { } target)
        {
            var query = new CardQuery(target, "MonsterZone", RelativeSide.Both);
            var candidates = query.Candidates(State, activation.Controller, activation.Source, ctx.CurrentSourceRef() ?? activation.SourceRef);
            if (candidates.Count < query.Min)
            {
                Log.Add($"{activation.Source.Name} no tiene objetivos validos.");
                AbortCardActivation(activation, chainZoneIndex);
                yield break;
            }
            var choice = ctx.SelectCards(query.Chooser, activation.Controller, $"{activation.Source.Name}: selecciona el/los objetivo(s).",
                candidates, query.Min, query.Count, TargetPurpose(effect));
            if (!choice.Answered) yield return choice;
            activation.Targets.AddRange(choice.SelectedCards);
            if (activation.Targets.Count > 0)
                Log.Add($"Objetivo(s): {string.Join(", ", activation.Targets.Select(t => t.Card.Name))}.");
        }

        if (effect.Type == MonsterEffectType.Unclassified)
        {
            // Sin Cadena: se aplica en el acto (ej. Invocarse pagando una condicion).
            activation.IsProcedure = true;
            foreach (var request in ResolveMonsterEffectRoutine(activation)) yield return request;
            yield break;
        }

        State.Chain.Add(new ChainLink { Controller = activation.Controller, ZoneIndex = chainZoneIndex, MonsterEffect = activation, Target = legacyTarget });
        State.ChainConsecutivePasses = 0;
        State.ChainPendingResponder = Opponent(activation.Controller);
        Log.Add(cardActivation
            ? $"{player.Name} encadena {activation.Source.Name} (Eslabon {State.Chain.Count})."
            : $"{player.Name} encadena el efecto de {activation.Source.Name} (Eslabon {State.Chain.Count}).");
    }

    /// <summary>Una Magia/Trampa que no llego a activarse (costo u objetivos imposibles) va al Cementerio en vez de quedar boca arriba sin efecto.</summary>
    private void AbortCardActivation(EffectActivation activation, int chainZoneIndex)
    {
        if (activation.Effect.Type != MonsterEffectType.Activation || chainZoneIndex < 0) return;
        var player = State.GetPlayer(activation.Controller);
        if (!ReferenceEquals(player.SpellTrapZones[chainZoneIndex]?.Card, activation.Source)) return;
        CardMover.SendToGraveyard(State, new CardRef(activation.Source, activation.Controller, CardZone.SpellTrapZone, chainZoneIndex), MoveCause.Rule);
        Log.Add($"{activation.Source.Name} va al Cementerio sin efecto.");
    }

    /// <summary>Para la IA: si los objetivos del efecto salen perdiendo (destruir, desterrar) o ganando (Invocar, ATK).</summary>
    private static ChoicePurpose TargetPurpose(MonsterEffect effect)
    {
        foreach (var step in effect.Steps)
        {
            if (!step.Params.GetBool("UseTargets") && step.Params.GetString("Apply") != nameof(ApplyTo.Targets)) continue;
            return step.ActionKind switch
            {
                "destroy" or "banish" or "return_to_hand" or "send_to_graveyard" => ChoicePurpose.Harm,
                "modify_stats" => step.Params.GetInt("Attack") + step.Params.GetInt("Defense") >= 0 ? ChoicePurpose.Benefit : ChoicePurpose.Harm,
                _ => ChoicePurpose.Benefit
            };
        }
        return ChoicePurpose.Neutral;
    }

    /// <summary>Ejecuta los pasos del efecto en orden (condiciones "si ...", pasos opcionales "puedes ...").</summary>
    private IEnumerable<ChoiceRequest> ResolveMonsterEffectRoutine(EffectActivation activation)
    {
        var ctx = new MonsterEffectContext(State, activation);
        activation.LastStepSucceeded = true;

        // "Niega los efectos de los monstruos boca arriba": se resuelve sin efecto si sigue boca arriba en el Campo.
        if (activation.Source is MonsterCard && activation.Effect.Type != MonsterEffectType.Activation
            && ctx.CurrentSourceRef() is { Zone: CardZone.MonsterZone } onField && onField.IsFaceUp(State)
            && LastingEffects.MonsterEffectsNegated(State))
        {
            Log.Add($"El efecto de {activation.Source.Name} está negado.");
            yield break;
        }

        foreach (var step in activation.Effect.Steps)
        {
            if (!MonsterEffectCatalog.AllMet(step.Conditions, ctx))
            {
                activation.LastStepSucceeded = false;
                continue;
            }

            var info = MonsterEffectCatalog.Step(step.ActionKind);
            if (info == null) continue;
            var implementation = info.Factory();
            if (!implementation.CanRun(ctx, step.Params))
            {
                activation.LastStepSucceeded = false;
                continue;
            }

            if (step.Optional)
            {
                var ask = ChoiceRequest.YesNo(activation.Controller, $"{activation.Source.Name}: ¿{info.Label}?", activation.Source);
                yield return ask;
                if (!ask.Yes)
                {
                    activation.LastStepSucceeded = false;
                    continue;
                }
            }

            foreach (var request in implementation.Run(ctx, step.Params)) yield return request;

            CheckLifePoints();
            if (State.IsOver) yield break;
        }
    }

    // ------------------------------------------------------------ Disparados / Volteo

    /// <summary>
    /// Revisa los eventos acumulados y activa los efectos Disparados/de
    /// Volteo que correspondan: primero los del jugador del turno (obligatorios
    /// antes que opcionales), despues los del adversario. Los opcionales le
    /// preguntan a su controlador. Cada uno entra como un eslabon nuevo.
    /// </summary>
    private void OpenTriggerWindow()
    {
        var events = State.TriggerEvents.ToList();
        State.TriggerEvents.Clear();
        _summonWindowSeen.Clear();
        State.PendingSummons.Clear();
        State.PendingSummonsBy = null;

        var pending = new List<EffectActivation>();
        foreach (var evt in events)
        {
            // "Si esta carta ...": los efectos de la propia carta del evento.
            var card = evt.Card;
            for (int i = 0; i < card.Effects.Count; i++)
            {
                var effect = card.Effects[i];
                if (!effect.IsTriggered || effect.Subject != EventSubject.ThisCard || effect.TriggerEvent != evt.Kind) continue;
                if (pending.Any(p => ReferenceEquals(p.Source, card) && p.EffectIndex == i && p.Trigger == evt)) continue;

                var source = LocateTriggerSource(evt) ?? new CardRef(card, evt.Controller, evt.ToZone, -1);
                pending.Add(new EffectActivation(card, effect, i, evt.Controller, source) { Trigger = evt });
            }

            // "Si un monstruo ... es ...": efectos de otras cartas que vigilan este evento.
            foreach (var (watcher, index, effect) in Watchers(evt.Kind))
            {
                if (pending.Any(p => ReferenceEquals(p.Source, watcher.Card) && p.EffectIndex == index && p.Effect.Subject == EventSubject.AnyCard)) continue;
                if (!WatchedEventMatches(watcher, effect, evt)) continue;
                pending.Add(new EffectActivation(watcher.Card, effect, index, watcher.Side, watcher) { Trigger = evt });
            }
        }
        if (pending.Count == 0) return;

        var turnSide = State.ActivePlayer.Side;
        var ordered = pending
            .OrderBy(a => a.Controller == turnSide ? 0 : 1)
            .ThenBy(a => a.Effect.Optional ? 1 : 0)
            .ToList();
        _routines.Push(TriggerWindowRoutine(ordered).GetEnumerator());
    }

    private IEnumerable<ChoiceRequest> TriggerWindowRoutine(List<EffectActivation> activations)
    {
        foreach (var activation in activations)
        {
            if (State.IsOver) yield break;
            if (!CanActivate(activation)) continue;

            if (activation.Effect.Optional)
            {
                string evt = MonsterEffectCatalog.EventLabel(activation.Effect.TriggerEvent).ToLowerInvariant();
                var ask = ChoiceRequest.YesNo(activation.Controller, $"{activation.Source.Name} {evt}. ¿Activas su efecto?", activation.Source);
                yield return ask;
                if (!ask.Yes) continue;
            }

            foreach (var request in ActivationRoutine(activation)) yield return request;
        }
    }

    /// <summary>Cartas (de ambos jugadores) con un efecto Disparado sobre "otra carta" para este evento, que estan en su zona de activacion.</summary>
    private IEnumerable<(CardRef Card, int Index, MonsterEffect Effect)> Watchers(EffectEvent kind)
    {
        foreach (var player in State.Players)
        {
            foreach (var zone in new[] { CardZone.MonsterZone, CardZone.SpellTrapZone, CardZone.FieldZone, CardZone.Hand, CardZone.Graveyard, CardZone.Banished })
            {
                foreach (var card in CardQuery.Enumerate(player, player.Side, zone))
                {
                    var effects = card.Card.Effects;
                    for (int i = 0; i < effects.Count; i++)
                    {
                        var effect = effects[i];
                        if (effect.Type != MonsterEffectType.Trigger || effect.Subject != EventSubject.AnyCard || effect.TriggerEvent != kind) continue;
                        bool inZone = effect.ActivationZone switch
                        {
                            EffectZone.Field => zone is CardZone.MonsterZone or CardZone.SpellTrapZone or CardZone.FieldZone && card.IsFaceUp(State),
                            EffectZone.Hand => zone == CardZone.Hand,
                            EffectZone.Graveyard => zone == CardZone.Graveyard,
                            EffectZone.Banished => zone == CardZone.Banished,
                            _ => false
                        };
                        if (inZone) yield return (card, i, effect);
                    }
                }
            }
        }
    }

    /// <summary>Si la carta del evento cumple el filtro del efecto que lo vigila (lado relativo a quien controla el efecto).</summary>
    private bool WatchedEventMatches(CardRef watcher, MonsterEffect effect, TriggerEvent evt)
    {
        var filter = effect.EventFilter ?? EffectActionParams.Empty;
        var query = new CardQuery(filter.With("From", evt.ToZone.ToString()), evt.ToZone.ToString(), RelativeSide.Both);
        if (!CardQuery.Sides(query.Side, watcher.Side).Contains(evt.Controller)) return false;
        if (query.ExcludeSource && ReferenceEquals(evt.Card, watcher.Card)) return false;
        return query.Matches(State, new CardRef(evt.Card, evt.Controller, evt.ToZone, -1), watcher.Card);
    }

    /// <summary>Donde quedo la carta tras el evento (para "Invoca esta carta" desde el Cementerio, etc.).</summary>
    private CardRef? LocateTriggerSource(TriggerEvent evt)
    {
        var player = State.GetPlayer(evt.Controller);
        return evt.ToZone switch
        {
            CardZone.MonsterZone or CardZone.SpellTrapZone or CardZone.FieldZone =>
                CardQuery.Enumerate(player, evt.Controller, evt.ToZone).LastOrDefault(c => ReferenceEquals(c.Card, evt.Card)),
            CardZone.Hand or CardZone.Deck or CardZone.Graveyard or CardZone.Banished =>
                CardQuery.Enumerate(player, evt.Controller, evt.ToZone).LastOrDefault(c => ReferenceEquals(c.Card, evt.Card)),
            _ => null
        };
    }

    /// <summary>Registra un evento de fase (Standby/End) para las cartas boca arriba del jugador del turno que lo usan.</summary>
    private void RecordPhaseEvents(EffectEvent kind)
    {
        var player = State.ActivePlayer;
        foreach (var zone in new[] { CardZone.MonsterZone, CardZone.SpellTrapZone, CardZone.FieldZone })
        {
            foreach (var card in CardQuery.Enumerate(player, player.Side, zone))
            {
                if (!card.IsFaceUp(State)) continue;
                if (!card.Card.Effects.Any(e => e.IsTriggered && e.Subject == EventSubject.ThisCard && e.TriggerEvent == kind)) continue;
                CardMover.RecordInPlace(State, kind, card.Card, player.Side, zone, MoveCause.Rule);
            }
        }
    }

    // ------------------------------------------------------------ Cadena

    private void PassInternal()
    {
        State.ChainConsecutivePasses++;
        if (State.ChainConsecutivePasses >= 2)
        {
            _routines.Push(ResolveChainRoutine().GetEnumerator());
            return;
        }
        State.ChainPendingResponder = Opponent(State.ChainPendingResponder!.Value);
    }

    /// <summary>
    /// Verdadero si <paramref name="side"/> tiene algo con lo que responder a
    /// la Cadena abierta (Magia/Trampa Colocada o de Juego Rapido con
    /// Velocidad suficiente, o un efecto Rapido de Monstruo). Se usa para el
    /// pase automatico de Prioridad (<see cref="Rules.DuelConfig.AutoPassWhenNoResponse"/>).
    /// </summary>
    public bool HasChainResponse(PlayerSide side)
    {
        if (State.Chain.Count == 0) return false;
        int required = Math.Max(2, TopChainLinkSpeed());
        var player = State.GetPlayer(side);

        for (int zone = 0; zone < player.SpellTrapZones.Length; zone++)
        {
            var instance = player.SpellTrapZones[zone];
            if (instance == null || instance.FaceUp) continue;
            int speed = instance.Card switch
            {
                SpellCard { SubType: SpellSubType.QuickPlay } when instance.SetThisTurn => 0,
                SpellCard { SubType: not (SpellSubType.Ritual or SpellSubType.Field) } s => s.SpellSpeed,
                TrapCard t when !instance.SetThisTurn => t.SpellSpeed,
                _ => 0
            };
            if (speed < required) continue;
            if (instance.Card.ActivationEffect == null || CanActivateSetCardNow(player, zone)) return true;
        }

        bool myTurn = State.ActivePlayer.Side == side;
        if (myTurn && player.FirstFreeSpellTrapZone() != -1
            && player.Hand.Any(c => c is SpellCard { SubType: not (SpellSubType.Ritual or SpellSubType.Field) } s && s.SpellSpeed >= required))
            return true;

        return GetActivatableEffects(side).Any(e => e.Effect.Type == MonsterEffectType.Quick);
    }

    /// <summary>Si la carta Colocada en esa zona (con efecto por datos) cumple ahora sus condiciones, costos y objetivos.</summary>
    private bool CanActivateSetCardNow(Player player, int zone)
    {
        var instance = player.SpellTrapZones[zone];
        if (instance?.Card.ActivationEffect is not { } effect) return false;
        var activation = new EffectActivation(instance.Card, effect, IndexOf(instance.Card, effect), player.Side,
            new CardRef(instance.Card, player.Side, CardZone.SpellTrapZone, zone)) { RespondingTo = TopLinkInfo() };
        return CanActivate(activation);
    }

    // ------------------------------------------------------------ Ventana de respuesta a un ataque

    /// <summary>Algo que el defensor puede activar cuando le declaran un ataque.</summary>
    private sealed record WindowOption(string Label, Func<IEnumerable<ChoiceRequest>> Start);

    /// <summary>
    /// Trampas y Magias de Juego Rapido Colocadas (no este turno) y efectos
    /// Rapidos que <paramref name="side"/> puede activar ahora, sin Cadena,
    /// en respuesta a la declaracion de un ataque.
    /// </summary>
    private List<WindowOption> AttackWindowOptions(PlayerSide side, int attackerZone)
    {
        var options = new List<WindowOption>();
        var player = State.GetPlayer(side);
        _windowSide = side;
        try
        {
            for (int zone = 0; zone < player.SpellTrapZones.Length; zone++)
            {
                var instance = player.SpellTrapZones[zone];
                if (instance == null || instance.FaceUp || instance.SetThisTurn) continue;
                bool quick = instance.Card is TrapCard || instance.Card is SpellCard { SubType: SpellSubType.QuickPlay };
                if (!quick) continue;
                int captured = zone;
                var card = instance.Card;

                if (card.ActivationEffect != null)
                {
                    if (!CanActivateSetCardNow(player, zone)) continue;
                    options.Add(new WindowOption($"Activar {card.Name} (Colocada)", () => ActivateSetCardInWindow(player, captured, null)));
                    continue;
                }

                string effectId = card switch { SpellCard s => s.EffectId, TrapCard t => t.EffectId, _ => "" };
                EffectTarget? target = null;
                if (EffectDefinitionResolver.Get(effectId) is ITargetedEffectAction targeted)
                {
                    // Una Trampa heredada con objetivo en el Campo apunta al atacante.
                    if (targeted.TargetKind != EffectTargetKind.MonsterZone) continue;
                    var attackerTarget = new EffectTarget { Side = Opponent(side), ZoneIndex = attackerZone };
                    if (!targeted.IsValidTarget(State, player, attackerTarget)) continue;
                    target = attackerTarget;
                }
                var chosenTarget = target;
                options.Add(new WindowOption($"Activar {card.Name} (Colocada){(chosenTarget != null ? " sobre el atacante" : "")}",
                    () => ActivateSetCardInWindow(player, captured, chosenTarget)));
            }

            foreach (var effect in GetActivatableEffects(side).Where(e => e.Effect.Type == MonsterEffectType.Quick))
            {
                var captured = effect;
                options.Add(new WindowOption($"Efecto de {effect.Label}", () => ActivationRoutine(
                    new EffectActivation(captured.Card.Card, captured.Effect, captured.EffectIndex, side, captured.Card))));
            }
        }
        finally
        {
            _windowSide = null;
        }
        return options;
    }

    private IEnumerable<ChoiceRequest> ActivateSetCardInWindow(Player player, int zone, EffectTarget? target)
    {
        var instance = player.SpellTrapZones[zone];
        if (instance == null || instance.FaceUp) yield break;
        instance.FaceUp = true;
        if (instance.Card.ActivationEffect is { } effect)
        {
            var activation = new EffectActivation(instance.Card, effect, IndexOf(instance.Card, effect), player.Side,
                new CardRef(instance.Card, player.Side, CardZone.SpellTrapZone, zone));
            foreach (var request in ActivationRoutine(activation, zone)) yield return request;
            yield break;
        }
        AddChainLink(player.Side, zone, target);
    }

    /// <summary>
    /// Despues de una Invocacion hecha por un jugador (no por un efecto a
    /// mitad de Cadena): si su adversario tiene Colocada una carta que responde
    /// a Invocaciones ("cuando un monstruo fuera a ser Invocado"), le pregunta
    /// si la activa antes de que se apliquen los efectos "si es Invocado".
    /// </summary>
    private bool TryOpenSummonWindow()
    {
        if (State.Chain.Count > 0) return false;
        var summons = State.TriggerEvents.Where(e => e.Kind == EffectEvent.Summoned && e.Cause.Kind == CauseKind.Rule && !_summonWindowSeen.Contains(e)).ToList();
        if (summons.Count == 0) return false;
        foreach (var evt in summons) _summonWindowSeen.Add(evt);

        var summoner = summons[0].Cause.By ?? summons[0].Controller;
        State.PendingSummonsBy = summoner;
        State.PendingSummons.Clear();
        foreach (var evt in summons)
        {
            var onField = CardQuery.Enumerate(State.GetPlayer(evt.Controller), evt.Controller, CardZone.MonsterZone).LastOrDefault(c => ReferenceEquals(c.Card, evt.Card));
            if (onField != null) State.PendingSummons.Add(onField);
        }
        if (State.PendingSummons.Count == 0) return false;

        var responder = Opponent(summoner);
        var options = SummonWindowOptions(responder);
        if (options.Count == 0) return false;
        string names = string.Join(", ", State.PendingSummons.Select(r => r.Card.Name));
        _routines.Push(WindowRoutine(responder, () => SummonWindowOptions(responder),
            $"{State.GetPlayer(summoner).Name} Invoca a {names}. ¿Activas una carta?", State.PendingSummons[0].Card).GetEnumerator());
        return true;
    }

    /// <summary>Cartas Colocadas (y efectos Rapidos) que responden a una Invocacion: las que tienen la condicion "un monstruo del adversario esta siendo Invocado".</summary>
    private List<WindowOption> SummonWindowOptions(PlayerSide side)
    {
        var options = new List<WindowOption>();
        var player = State.GetPlayer(side);
        _windowSide = side;
        try
        {
            for (int zone = 0; zone < player.SpellTrapZones.Length; zone++)
            {
                var instance = player.SpellTrapZones[zone];
                if (instance == null || instance.FaceUp || instance.SetThisTurn) continue;
                if (instance.Card is not (TrapCard or SpellCard { SubType: SpellSubType.QuickPlay })) continue;
                if (instance.Card.ActivationEffect is not { } effect || !effect.ActivationConditions.Any(c => c.Kind == "responding_to_summon")) continue;
                if (!CanActivateSetCardNow(player, zone)) continue;
                int captured = zone;
                options.Add(new WindowOption($"Activar {instance.Card.Name} (Colocada)", () => ActivateSetCardInWindow(player, captured, null)));
            }
            foreach (var effect in GetActivatableEffects(side).Where(e => e.Effect.Type == MonsterEffectType.Quick && e.Effect.ActivationConditions.Any(c => c.Kind == "responding_to_summon")))
            {
                var captured = effect;
                options.Add(new WindowOption($"Efecto de {effect.Label}", () => ActivationRoutine(
                    new EffectActivation(captured.Card.Card, captured.Effect, captured.EffectIndex, side, captured.Card))));
            }
        }
        finally
        {
            _windowSide = null;
        }
        return options;
    }

    /// <summary>Pregunta a <paramref name="side"/> que activa (opcion 0 = nada) y lo activa.</summary>
    private IEnumerable<ChoiceRequest> WindowRoutine(PlayerSide side, Func<List<WindowOption>> optionsFor, string prompt, Card source)
    {
        var options = optionsFor();
        if (options.Count == 0) yield break;

        var ask = ChoiceRequest.Pick(side, prompt, source, new[] { "No activar nada" }.Concat(options.Select(o => o.Label)).ToList());
        ask.IsResponseWindow = true;
        yield return ask;
        if (ask.Option <= 0 || ask.Option > options.Count) yield break;

        _windowSide = side;
        foreach (var request in options[ask.Option - 1].Start()) yield return request;
        _windowSide = null;
    }

    /// <summary>Si el defensor tiene algo que activar, pausa el ataque y le pregunta. Devuelve verdadero si el ataque quedo pendiente.</summary>
    private bool OpenAttackWindow(int attackerZone, int targetZone)
    {
        var defender = State.InactivePlayer.Side;
        if (AttackWindowOptions(defender, attackerZone).Count == 0) return false;

        var attacker = State.ActivePlayer.MonsterZones[attackerZone]!;
        attacker.HasAttackedThisTurn = true;
        var target = targetZone >= 0 ? State.InactivePlayer.MonsterZones[targetZone]?.Card : null;
        _pendingAttack = new PendingAttack(attackerZone, targetZone, attacker.Card, target);
        string what = target == null ? "directamente" : (State.InactivePlayer.MonsterZones[targetZone]!.IsFaceUp ? $"a {target.Name}" : "a tu monstruo boca abajo");
        Log.Add($"{State.ActivePlayer.Name} declara un ataque con {attacker.Card.Name} {what}.");
        RunRoutine(AttackWindowRoutine(defender, attackerZone, $"{attacker.Card.Name} ataca {what}. ¿Activas una carta o un efecto?", attacker.Card));
        return true;
    }

    private IEnumerable<ChoiceRequest> AttackWindowRoutine(PlayerSide side, int attackerZone, string prompt, Card attacker)
    {
        var options = AttackWindowOptions(side, attackerZone);
        if (options.Count == 0) yield break;

        var ask = ChoiceRequest.Pick(side, prompt, attacker, new[] { "No activar nada" }.Concat(options.Select(o => o.Label)).ToList());
        ask.IsResponseWindow = true;
        yield return ask;
        if (ask.Option <= 0 || ask.Option > options.Count) yield break;

        _windowSide = side;
        foreach (var request in options[ask.Option - 1].Start()) yield return request;
        _windowSide = null;
    }

    /// <summary>Despues de la ventana (y de la Cadena que se haya formado): el ataque sigue si atacante y objetivo siguen ahi.</summary>
    private void ContinuePendingAttack()
    {
        var pending = _pendingAttack!;
        _pendingAttack = null;
        _windowSide = null;
        if (State.IsOver || State.Phase != DuelPhase.Battle) return;

        var attacker = State.ActivePlayer.MonsterZones[pending.AttackerZone];
        if (attacker == null || !ReferenceEquals(attacker.Card, pending.Attacker) || attacker.Position != BattlePosition.Attack)
        {
            Log.Add($"El ataque de {pending.Attacker.Name} no se realiza.");
            return;
        }
        if (pending.TargetZone >= 0)
        {
            var target = State.InactivePlayer.MonsterZones[pending.TargetZone];
            if (target == null || !ReferenceEquals(target.Card, pending.Target))
            {
                Log.Add($"El objetivo del ataque de {pending.Attacker.Name} ya no está: el ataque se detiene.");
                return;
            }
        }
        else if (State.InactivePlayer.MonsterCount > 0 && !ContinuousEffects.CanAttackDirectly(attacker, State))
        {
            Log.Add($"El adversario ahora controla monstruos: el ataque directo de {pending.Attacker.Name} se detiene.");
            return;
        }
        PerformAttack(pending.AttackerZone, pending.TargetZone);
    }

    // ------------------------------------------------------------ End Phase diferida

    private void ContinueEndTurn()
    {
        var stage = _endTurnStage;
        _endTurnStage = EndTurnStage.None;

        if (stage == EndTurnStage.HandLimit)
        {
            var player = State.ActivePlayer;
            int excess = player.Hand.Count - _config.MaxHandSize;
            if (excess > 0)
            {
                State.PendingDiscardCount = excess;
                Log.Add($"{player.Name} debe descartar {excess} carta(s) (limite de mano).");
                return;
            }
        }

        FinishEndTurn();
    }

    /// <summary>Compara tuplas (carta, indice) por referencia de carta.</summary>
    private sealed class ReferenceTupleComparer : IEqualityComparer<(Card, int)>
    {
        public static readonly ReferenceTupleComparer Instance = new();
        public bool Equals((Card, int) x, (Card, int) y) => ReferenceEquals(x.Item1, y.Item1) && x.Item2 == y.Item2;
        public int GetHashCode((Card, int) obj) => HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item1), obj.Item2);
    }
}
