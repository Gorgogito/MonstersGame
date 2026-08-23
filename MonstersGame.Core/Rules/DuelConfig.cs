namespace MonstersGame.Core.Rules;

/// <summary>
/// Parametros configurables del duelo. Centraliza las constantes de reglas
/// para poder ajustarlas sin tocar el motor (p. ej. modos de juego futuros).
/// Valores por defecto tomados del reglamento.
/// </summary>
public sealed class DuelConfig
{
    /// <summary>Life Points iniciales (pagina 28).</summary>
    public int StartingLifePoints { get; init; } = 8000;

    /// <summary>Cartas de la mano inicial (pagina 29).</summary>
    public int StartingHandSize { get; init; } = 5;

    /// <summary>Maximo de cartas en mano al final del turno (pagina 36).</summary>
    public int MaxHandSize { get; init; } = 6;

    /// <summary>
    /// El primer jugador no roba en su primer turno (pagina 31). Configurable
    /// por si se quiere relajar la regla en modos alternativos.
    /// </summary>
    public bool FirstPlayerSkipsFirstDraw { get; init; } = true;

    /// <summary>
    /// El primer jugador no puede atacar en su primer turno (pagina 33).
    /// </summary>
    public bool FirstPlayerSkipsFirstBattle { get; init; } = true;
}
