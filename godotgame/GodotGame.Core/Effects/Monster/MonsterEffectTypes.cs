using GodotGame.Core.Entities;

namespace GodotGame.Core.Effects.Monster;

/// <summary>
/// Clasificacion oficial de los efectos de Monstruo. Determina COMO y CUANDO
/// se aplica cada <see cref="MonsterEffect"/>:
///
/// - <see cref="Continuous"/>: no inicia Cadena; se aplica de forma pasiva
///   mientras el Monstruo este boca arriba en el Campo.
/// - <see cref="Ignition"/>: opcional, se activa manualmente solo en tu
///   propia Main Phase (Velocidad de Hechizo 1). Suele tener un costo.
/// - <see cref="Trigger"/>: se activa (obligatorio u opcional) cuando ocurre
///   un evento concreto sobre esta carta, en el turno de cualquier jugador.
/// - <see cref="Quick"/>: Velocidad de Hechizo 2; se activa en el turno de
///   cualquier jugador (incluso como respuesta en una Cadena).
/// - <see cref="Flip"/>: subcategoria de Disparado; obligatorio, se activa al
///   voltearse boca arriba el Monstruo (por ataque, Invocacion por Volteo o efecto).
/// - <see cref="Unclassified"/>: altera reglas de invocacion/posicion sin
///   iniciar Cadena (ej. Invocarse de Modo Especial pagando un costo).
/// </summary>
public enum MonsterEffectType
{
    Continuous,
    Ignition,
    Trigger,
    Quick,
    Flip,
    Unclassified
}

/// <summary>
/// Evento sobre "esta carta" que dispara un efecto <see cref="MonsterEffectType.Trigger"/>
/// (o <see cref="Flipped"/> para un <see cref="MonsterEffectType.Flip"/>).
/// Cada valor esta atado a un punto concreto del motor que lo registra.
/// </summary>
public enum EffectEvent
{
    None,

    /// <summary>Es descartada de la mano al Cementerio por cualquier motivo (costo, efecto o limite de mano).</summary>
    Discarded,

    /// <summary>Es descartada de la mano al Cementerio por el efecto de una carta (no por un costo ni por el limite de mano).</summary>
    DiscardedByCardEffect,

    /// <summary>Es mandada al Cementerio desde cualquier lugar.</summary>
    SentToGraveyard,

    /// <summary>Es destruida (en batalla o por efecto) y mandada al Cementerio.</summary>
    Destroyed,

    /// <summary>Es destruida en batalla y mandada al Cementerio.</summary>
    DestroyedByBattle,

    /// <summary>Es destruida por el efecto de una carta.</summary>
    DestroyedByEffect,

    /// <summary>Es desterrada.</summary>
    Banished,

    /// <summary>Es Invocada de cualquier modo (Normal, por Volteo o Especial).</summary>
    Summoned,

    /// <summary>Es Invocada de Modo Normal (incluye por Sacrificio).</summary>
    NormalSummoned,

    /// <summary>Es Invocada de Modo Especial (por efecto, Fusion o Ritual).</summary>
    SpecialSummoned,

    /// <summary>Es Invocada de Modo Especial por el efecto de una carta.</summary>
    SpecialSummonedByEffect,

    /// <summary>Es volteada boca arriba (Invocacion por Volteo, ataque o efecto). Es el evento de los efectos de Volteo.</summary>
    Flipped,

    /// <summary>Inflige daño de batalla al adversario.</summary>
    InflictsBattleDamage,

    /// <summary>Durante la Standby Phase de su controlador, si esta boca arriba en el Campo.</summary>
    StandbyPhase,

    /// <summary>Durante la End Phase de su controlador, si esta boca arriba en el Campo.</summary>
    EndPhase
}

/// <summary>Desde donde se puede activar un efecto de Encendido/Rapido/No clasificado ("esta carta" debe estar ahi).</summary>
public enum EffectZone
{
    Field,
    Hand,
    Graveyard,
    Banished
}

/// <summary>Una zona concreta donde puede estar una carta.</summary>
public enum CardZone
{
    Hand,
    Deck,
    Graveyard,
    Banished,
    MonsterZone,
    SpellTrapZone,
    FieldZone
}

/// <summary>Por que se movio una carta (para distinguir "por efecto de una carta" de un costo o una regla).</summary>
public enum CauseKind
{
    /// <summary>Regla del juego (limite de mano, materiales de Fusion, Sacrificio de Invocacion).</summary>
    Rule,

    /// <summary>Costo pagado para activar un efecto (no cuenta como "por efecto de una carta").</summary>
    Cost,

    /// <summary>Efecto de una carta al resolverse.</summary>
    Effect,

    /// <summary>Resultado de una batalla.</summary>
    Battle
}

/// <summary>Causa de un movimiento: tipo, quien controlaba el efecto/accion que lo provoco y la carta origen (si la hay).</summary>
public readonly record struct MoveCause(CauseKind Kind, PlayerSide? By = null, Card? Source = null)
{
    public static readonly MoveCause Rule = new(CauseKind.Rule);
    public static readonly MoveCause Battle = new(CauseKind.Battle);
}

/// <summary>
/// Algo que le paso a una carta concreta y que puede disparar sus efectos.
/// <paramref name="Controller"/> es quien la controla/posee DESPUES del evento
/// (el lado de la Zona donde quedo, o el dueño de la mano/Cementerio/Destierro).
/// </summary>
public sealed record TriggerEvent(EffectEvent Kind, Card Card, PlayerSide Controller, CardZone FromZone, CardZone ToZone, MoveCause Cause);
