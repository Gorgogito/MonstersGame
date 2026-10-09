using GodotGame.Core.Entities;

namespace GodotGame.Core.Battle;

/// <summary>
/// Como se produjo una Invocacion (para <see cref="MonsterSummonedEvent"/>).
/// Fusion y Ritual tienen su propio evento mas rico (<see cref="FusionPerformedEvent"/>,
/// <see cref="RitualPerformedEvent"/>) en vez de este: no se duplica el
/// evento para el mismo momento.
/// </summary>
public enum SummonKind { Normal, Flip, Special }

/// <summary>
/// Por que se destruyo/removio un Monstruo (para <see cref="MonsterDestroyedEvent"/>).
/// <c>Battle</c> esta reservado para cuando <see cref="DuelEngine.DeclareAttack"/>
/// tambien emita eventos (ver <see cref="DuelState.LastAttack"/>, que por
/// ahora sigue siendo la unica fuente para la presentacion de un ataque) —
/// todavia no lo usa nada, a proposito.
/// </summary>
public enum DestructionCause { Battle, Effect, Cost }

/// <summary>
/// Un evento estructurado de una accion del motor, para que la presentacion
/// (capa visual) reaccione sin tener que inferir nada comparando estado
/// (a diferencia de como funciona hoy <see cref="DuelState.LastAttack"/>).
/// Se encolan en <see cref="DuelState.Events"/> y la presentacion los drena
/// cada frame -- a diferencia de <c>LastAttack</c> (un solo slot, se
/// sobreescribe), una cola no pierde eventos si ocurren varios en el mismo
/// instante logico.
/// </summary>
public abstract record DuelEvent(PlayerSide Side);

/// <summary>Un Monstruo entro al Campo (Invocacion Normal, por Volteo o Especial). Fusion/Ritual usan su propio evento.</summary>
public sealed record MonsterSummonedEvent(PlayerSide Side, int ZoneIndex, MonsterCard Card, SummonKind Kind) : DuelEvent(Side);

/// <summary>Un Monstruo abandono el Campo hacia el Cementerio.</summary>
public sealed record MonsterDestroyedEvent(PlayerSide Side, int ZoneIndex, MonsterCard Card, DestructionCause Cause) : DuelEvent(Side);

/// <summary>Una Magia o Trampa se activo (se agrego como nuevo eslabon de la Cadena).</summary>
public sealed record SpellTrapActivatedEvent(PlayerSide Side, int ZoneIndex, Card Card) : DuelEvent(Side);

/// <summary>Una Fusion se completo con exito.</summary>
public sealed record FusionPerformedEvent(PlayerSide Side, int ZoneIndex, IReadOnlyList<MonsterCard> Materials, MonsterCard Result) : DuelEvent(Side);

/// <summary>Una Invocacion Ritual se completo con exito.</summary>
public sealed record RitualPerformedEvent(PlayerSide Side, int ZoneIndex, MonsterCard Result) : DuelEvent(Side);

/// <summary>La Carta de Campo de un jugador cambio (activacion nueva, reemplazando la anterior si habia).</summary>
public sealed record FieldChangedEvent(PlayerSide Side, string? OldFieldTypeId, string? NewFieldTypeId) : DuelEvent(Side);

/// <summary>Se activo el efecto de una carta (Monstruo, o efecto de Encendido/Disparado/Rapido de una Magia/Trampa) desde el Campo, la mano, el Cementerio o el Destierro.</summary>
public sealed record MonsterEffectActivatedEvent(PlayerSide Side, Card Card, GodotGame.Core.Effects.Monster.CardZone Zone, int ZoneIndex, GodotGame.Core.Effects.Monster.MonsterEffectType EffectType) : DuelEvent(Side);

/// <summary>Una carta salio de la mano al Cementerio por un descarte.</summary>
public sealed record CardDiscardedEvent(PlayerSide Side, Card Card) : DuelEvent(Side);

/// <summary>Una carta fue desterrada.</summary>
public sealed record CardBanishedEvent(PlayerSide Side, Card Card) : DuelEvent(Side);

/// <summary>Se aplico un modificador de ATK/DEF a un Monstruo del Campo (hoy, solo al equiparse una Magia de Equipo).</summary>
public sealed record StatModifierAppliedEvent(PlayerSide Side, int ZoneIndex, int AttackAmount, int DefenseAmount, string Source) : DuelEvent(Side);
