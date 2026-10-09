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
/// Efectos de Monstruo en el motor: activacion manual (Encendido, Rapido, No
/// clasificado), ventana de efectos Disparados/Volteo tras cada accion,
/// resolucion en la Cadena y elecciones de los jugadores a mitad de efecto.
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

                if (State.TriggerEvents.Count > 0)
                {
                    OpenTriggerWindow();
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
        foreach (var (zone, effectZone) in new[] { (CardZone.MonsterZone, EffectZone.Field), (CardZone.Hand, EffectZone.Hand), (CardZone.Graveyard, EffectZone.Graveyard), (CardZone.Banished, EffectZone.Banished) })
        {
            var seen = new HashSet<(Card, int)>(ReferenceTupleComparer.Instance);
            foreach (var card in CardQuery.Enumerate(player, side, zone))
            {
                if (card.Card is not MonsterCard monster || monster.Effects.Count == 0) continue;
                if (zone == CardZone.MonsterZone && card.MonsterInstance(State) is not { IsFaceUp: true }) continue;

                for (int i = 0; i < monster.Effects.Count; i++)
                {
                    var effect = monster.Effects[i];
                    if (!effect.IsManual || effect.ActivationZone != effectZone) continue;
                    // Copias identicas en mano/Cementerio: basta con ofrecer una.
                    if (zone != CardZone.MonsterZone && !seen.Add((monster, i))) continue;
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

        var activation = new EffectActivation(live.Card.Card, live.Effect, live.EffectIndex, side, live.Card);
        RunRoutine(ActivationRoutine(activation));
        return ActionResult.Ok();
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
                else if (!myTurn || State.Phase is not (DuelPhase.Main1 or DuelPhase.Battle or DuelPhase.Main2)) return false;
                break;
            default:
                return false;
        }

        var activation = new EffectActivation(card.Card, effect, effectIndex, side, card);
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
            if (available < Math.Max(1, query.Min)) return false;
        }
        return true;
    }

    private string OncePerTurnKey(EffectActivation activation) => $"{activation.Controller}:{activation.OncePerTurnKey}";

    // ------------------------------------------------------------ Activacion (comun)

    /// <summary>
    /// Activa el efecto: paga costos, elige objetivos y lo agrega a la Cadena
    /// (o, si es No clasificado, lo aplica en el acto sin Cadena).
    /// </summary>
    private IEnumerable<ChoiceRequest> ActivationRoutine(EffectActivation activation)
    {
        var effect = activation.Effect;
        var ctx = new MonsterEffectContext(State, activation);
        var player = State.GetPlayer(activation.Controller);

        Log.Add($"{player.Name} activa el efecto de {activation.Source.Name} ({MonsterEffectCatalog.TypeLabel(effect.Type)}).");
        if (activation.Source is MonsterCard monster)
            State.Events.Enqueue(new MonsterEffectActivatedEvent(activation.Controller, monster, activation.SourceRef.Zone, activation.SourceRef.Index, effect.Type));
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

        State.Chain.Add(new ChainLink { Controller = activation.Controller, ZoneIndex = -1, MonsterEffect = activation });
        State.ChainConsecutivePasses = 0;
        State.ChainPendingResponder = Opponent(activation.Controller);
        Log.Add($"{player.Name} encadena el efecto de {activation.Source.Name} (Eslabon {State.Chain.Count}).");
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

        var pending = new List<EffectActivation>();
        foreach (var evt in events)
        {
            if (evt.Card is not MonsterCard monster || monster.Effects.Count == 0) continue;
            for (int i = 0; i < monster.Effects.Count; i++)
            {
                var effect = monster.Effects[i];
                if (!effect.IsTriggered || effect.TriggerEvent != evt.Kind) continue;
                if (pending.Any(p => ReferenceEquals(p.Source, monster) && p.EffectIndex == i && p.Trigger == evt)) continue;

                var source = LocateTriggerSource(evt) ?? new CardRef(monster, evt.Controller, evt.ToZone, -1);
                pending.Add(new EffectActivation(monster, effect, i, evt.Controller, source) { Trigger = evt });
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

    /// <summary>Donde quedo la carta tras el evento (para "Invoca esta carta" desde el Cementerio, etc.).</summary>
    private CardRef? LocateTriggerSource(TriggerEvent evt)
    {
        var player = State.GetPlayer(evt.Controller);
        return evt.ToZone switch
        {
            CardZone.MonsterZone => CardQuery.Enumerate(player, evt.Controller, CardZone.MonsterZone).LastOrDefault(c => ReferenceEquals(c.Card, evt.Card)),
            CardZone.Hand or CardZone.Deck or CardZone.Graveyard or CardZone.Banished =>
                CardQuery.Enumerate(player, evt.Controller, evt.ToZone).LastOrDefault(c => ReferenceEquals(c.Card, evt.Card)),
            _ => null
        };
    }

    /// <summary>Registra un evento de fase (Standby/End) para los Monstruos boca arriba del jugador del turno que lo usan.</summary>
    private void RecordPhaseEvents(EffectEvent kind)
    {
        var player = State.ActivePlayer;
        for (int zone = 0; zone < player.MonsterZones.Length; zone++)
        {
            var instance = player.MonsterZones[zone];
            if (instance is not { IsFaceUp: true }) continue;
            if (!instance.Card.Effects.Any(e => e.IsTriggered && e.TriggerEvent == kind)) continue;
            CardMover.RecordInPlace(State, kind, instance.Card, player.Side, CardZone.MonsterZone, MoveCause.Rule);
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

        foreach (var instance in player.SpellTrapZones)
        {
            if (instance == null || instance.FaceUp) continue;
            int speed = instance.Card switch
            {
                SpellCard { SubType: not (SpellSubType.Ritual or SpellSubType.Field) } s => s.SpellSpeed,
                TrapCard t when !instance.SetThisTurn => t.SpellSpeed,
                _ => 0
            };
            if (speed >= required) return true;
        }

        if (player.Hand.Any(c => c is SpellCard { SubType: not (SpellSubType.Ritual or SpellSubType.Field) } s && s.SpellSpeed >= required && player.FirstFreeSpellTrapZone() != -1))
            return true;

        return GetActivatableEffects(side).Any(e => e.Effect.Type == MonsterEffectType.Quick);
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
