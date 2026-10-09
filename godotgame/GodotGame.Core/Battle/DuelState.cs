using GodotGame.Core.Entities;

namespace GodotGame.Core.Battle;

/// <summary>
/// Estado completo de un duelo en curso. Es un contenedor de datos: el motor
/// (<see cref="DuelEngine"/>) es quien lo modifica aplicando las reglas. La UI
/// solo lee este estado para dibujar.
/// </summary>
public sealed class DuelState
{
    /// <summary>Los dos duelistas. Indice 0 = humano, 1 = CPU.</summary>
    public Player[] Players { get; }

    /// <summary>Indice del jugador activo (cuyo turno transcurre).</summary>
    public int ActiveIndex { get; set; }

    /// <summary>Indice del jugador que comenzo el duelo (para reglas de primer turno).</summary>
    public int FirstPlayerIndex { get; set; }

    /// <summary>Numero de turno global (comienza en 1).</summary>
    public int TurnNumber { get; set; } = 1;

    /// <summary>Fase actual del turno.</summary>
    public DuelPhase Phase { get; set; } = DuelPhase.Draw;

    /// <summary>Registro de eventos.</summary>
    public GameLog Log { get; } = new();

    /// <summary>
    /// El ultimo ataque declarado con exito (por cualquiera de los dos
    /// jugadores), con los datos exactos de que zonas participaron y que se
    /// destruyo. No es un historial: se sobrescribe en cada ataque nuevo. Solo
    /// lo consume la presentacion para animar el enfrentamiento (ver
    /// <see cref="AttackInfo"/>); ninguna regla depende de el.
    /// </summary>
    public AttackInfo? LastAttack { get; set; }

    /// <summary>
    /// Eventos estructurados de acciones del motor (Invocacion, destruccion,
    /// Fusion, Ritual, Magia/Trampa activada, Campo cambiado, modificador
    /// aplicado), en el orden en que ocurrieron. A diferencia de
    /// <see cref="LastAttack"/> es una cola, no un slot unico: la
    /// presentacion la drena completa cada frame, asi que varios eventos en
    /// el mismo instante logico no se pisan entre si. Ver <see cref="DuelEvent"/>.
    /// </summary>
    public Queue<DuelEvent> Events { get; } = new();

    /// <summary>Verdadero cuando el duelo ha terminado.</summary>
    public bool IsOver { get; set; }

    /// <summary>Lado ganador (valido solo si <see cref="IsOver"/>). Null = empate.</summary>
    public PlayerSide? Winner { get; set; }

    /// <summary>
    /// Cartas que el jugador activo todavia debe elegir y descartar por el
    /// limite de mano antes de que el turno termine realmente. Mientras sea
    /// mayor que 0, <see cref="DuelEngine.EndTurn"/> no avanza de turno: hay
    /// que resolverlo con <see cref="DuelEngine.DiscardForEndPhase"/>.
    /// </summary>
    public int PendingDiscardCount { get; set; }

    /// <summary>
    /// Pila de la Cadena en curso (el ultimo elemento es el eslabon mas
    /// reciente, el que se resuelve primero). Vacia cuando no hay ninguna
    /// Cadena abierta.
    /// </summary>
    public List<ChainLink> Chain { get; } = new();

    /// <summary>
    /// A que jugador le toca decidir si responde a la Cadena o pasa la
    /// Prioridad. Null cuando <see cref="Chain"/> esta vacia.
    /// </summary>
    public PlayerSide? ChainPendingResponder { get; set; }

    /// <summary>
    /// Cuantos "paso" consecutivos se han dado desde el ultimo eslabon
    /// agregado. Al llegar a 2 (ambos jugadores declinaron seguidos) la
    /// Cadena se resuelve.
    /// </summary>
    public int ChainConsecutivePasses { get; set; }

    /// <summary>
    /// Decision pendiente de un jugador en medio de un efecto (ver
    /// <see cref="GodotGame.Core.Effects.Monster.ChoiceRequest"/>). Mientras no
    /// sea null el duelo esta en pausa esperando la respuesta.
    /// </summary>
    public GodotGame.Core.Effects.Monster.ChoiceRequest? PendingChoice { get; set; }

    /// <summary>Eventos sobre cartas concretas que todavia no se revisaron en busca de efectos Disparados.</summary>
    public List<GodotGame.Core.Effects.Monster.TriggerEvent> TriggerEvents { get; } = new();

    /// <summary>Efectos "una vez por turno" ya usados este turno (por jugador + nombre de carta + indice de efecto).</summary>
    public HashSet<string> UsedOncePerTurn { get; } = new();

    /// <summary>Verdadero mientras la Cadena se esta resolviendo (nadie puede responder ni pasar).</summary>
    public bool ChainResolving { get; set; }

    /// <summary>Azar del duelo (elecciones "al azar" de los efectos). Reemplazable para pruebas deterministas.</summary>
    public Random Rng { get; set; } = new();

    public DuelState(Player human, Player cpu)
    {
        Players = new[] { human, cpu };
    }

    public Player ActivePlayer => Players[ActiveIndex];
    public Player InactivePlayer => Players[1 - ActiveIndex];

    /// <summary>Verdadero si es el primer turno del duelo jugado por el jugador inicial.</summary>
    public bool IsFirstTurnOfStartingPlayer =>
        TurnNumber == 1 && ActiveIndex == FirstPlayerIndex;

    public Player GetPlayer(PlayerSide side) =>
        Players[0].Side == side ? Players[0] : Players[1];
}
