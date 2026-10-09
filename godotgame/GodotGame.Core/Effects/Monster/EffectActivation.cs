using GodotGame.Core.Battle;
using GodotGame.Core.Entities;

namespace GodotGame.Core.Effects.Monster;

/// <summary>
/// Una activacion concreta de un <see cref="MonsterEffect"/>: de que carta,
/// quien la controla, que evento la disparo (si es Disparado), que objetivos
/// se eligieron, y el estado que los pasos se van pasando entre si al
/// resolverse ("y, si lo haces...", "la carta descartada").
/// </summary>
public sealed class EffectActivation
{
    public Card Source { get; }
    public MonsterEffect Effect { get; }
    public int EffectIndex { get; }
    public PlayerSide Controller { get; }

    /// <summary>Donde estaba esta carta al activarse (para "Invoca esta carta", "excepto esta carta").</summary>
    public CardRef SourceRef { get; set; }

    /// <summary>Evento que disparo el efecto (solo Disparado/Volteo).</summary>
    public TriggerEvent? Trigger { get; init; }

    /// <summary>Objetivos elegidos al activar ("selecciona ...").</summary>
    public List<CardRef> Targets { get; } = new();

    /// <summary>Verdadero mientras se pagan los costos (los movimientos cuentan como costo, no como efecto).</summary>
    public bool PayingCost { get; set; }

    /// <summary>
    /// Verdadero para un efecto No clasificado (procedimiento de Invocacion):
    /// sus movimientos no cuentan como "por efecto de una carta".
    /// </summary>
    public bool IsProcedure { get; set; }

    /// <summary>Si el ultimo paso ejecutado se aplico ("y, si lo haces, ...").</summary>
    public bool LastStepSucceeded { get; set; }

    /// <summary>Cartas afectadas por el ultimo paso ejecutado (ej. la carta descartada).</summary>
    public List<Card> LastAffected { get; } = new();

    /// <summary>Solo al resolverse en la Cadena: niega el eslabon al que responde (efecto Rapido de negacion).</summary>
    public Action? NegateRespondedLink { get; set; }

    public EffectActivation(Card source, MonsterEffect effect, int effectIndex, PlayerSide controller, CardRef sourceRef)
    {
        Source = source;
        Effect = effect;
        EffectIndex = effectIndex;
        Controller = controller;
        SourceRef = sourceRef;
    }

    /// <summary>Clave de "una vez por turno": por nombre de carta e indice de efecto, como en el reglamento.</summary>
    public string OncePerTurnKey => $"{Source.Name.Trim().ToLowerInvariant()}#{EffectIndex}";

    /// <summary>
    /// Verdadero si esta carta fue descartada de la mano de su dueño al
    /// Cementerio por el efecto de una carta de su adversario (condicion de
    /// los monstruos "del Mundo Oscuro").
    /// </summary>
    public bool DiscardedByOpponent =>
        Trigger is { } t
        && t.FromZone == CardZone.Hand
        && t.ToZone == CardZone.Graveyard
        && t.Cause.Kind == CauseKind.Effect
        && t.Cause.By is { } by
        && by != t.Controller;

    public bool DiscardedByCardEffect =>
        Trigger is { } t && t.FromZone == CardZone.Hand && t.Cause.Kind == CauseKind.Effect;
}

/// <summary>Todo lo que un paso de efecto de Monstruo necesita para ejecutarse.</summary>
public sealed class MonsterEffectContext
{
    public DuelState State { get; }
    public EffectActivation Activation { get; }

    public MonsterEffectContext(DuelState state, EffectActivation activation)
    {
        State = state;
        Activation = activation;
    }

    public Player Controller => State.GetPlayer(Activation.Controller);
    public PlayerSide ControllerSide => Activation.Controller;
    public PlayerSide OpponentSide => Activation.Controller == PlayerSide.Human ? PlayerSide.Cpu : PlayerSide.Human;
    public Player Opponent => State.GetPlayer(OpponentSide);
    public Card Source => Activation.Source;

    /// <summary>Causa de los movimientos que haga el paso actual: costo mientras se paga, efecto al resolverse.</summary>
    public MoveCause Cause => new(
        Activation.PayingCost ? CauseKind.Cost : Activation.IsProcedure ? CauseKind.Rule : CauseKind.Effect,
        Activation.Controller, Activation.Source);

    /// <summary>Donde esta "esta carta" ahora mismo (null si ya no se encuentra donde estaba).</summary>
    public CardRef? CurrentSourceRef() => CardMover.Locate(State, Activation.SourceRef);

    /// <summary>Resuelve quien elige segun <paramref name="chooser"/>; <paramref name="owner"/> es el dueño de las cartas cuando aplica.</summary>
    public PlayerSide ChooserSide(ChooserKind chooser, PlayerSide owner) => chooser switch
    {
        ChooserKind.Opponent => OpponentSide,
        ChooserKind.Owner => owner,
        _ => ControllerSide
    };

    /// <summary>
    /// Crea una eleccion de cartas. Si es "al azar" o no hay nada que decidir,
    /// la responde aca mismo (sin preguntarle a nadie): el llamador solo hace
    /// <c>yield return</c> si <see cref="ChoiceRequest.Answered"/> es falso.
    /// </summary>
    public ChoiceRequest SelectCards(ChooserKind chooser, PlayerSide owner, string prompt, IReadOnlyList<CardRef> candidates, int min, int max, ChoicePurpose purpose)
    {
        var request = ChoiceRequest.Cards(ChooserSide(chooser, owner), prompt, Source, candidates, min, max, purpose);
        if (chooser == ChooserKind.Random)
        {
            var indices = Enumerable.Range(0, request.Candidates.Count).OrderBy(_ => State.Rng.Next()).Take(request.Max).ToList();
            request.AnswerCards(indices);
        }
        else if (request.IsForced)
        {
            request.AnswerCards(Enumerable.Range(0, request.Max).ToList());
        }
        return request;
    }

    public void Log(string message) => State.Log.Add(message);
}
