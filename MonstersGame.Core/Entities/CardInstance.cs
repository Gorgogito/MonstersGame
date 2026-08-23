namespace MonstersGame.Core.Entities;

/// <summary>
/// Representa una carta concreta situada en el campo, con su estado de juego
/// (posicion de batalla, si esta boca arriba, banderas de turno).
///
/// Se distingue de <see cref="Card"/> porque la misma definicion de carta puede
/// existir multiples veces y cada copia en el campo tiene su propio estado.
/// </summary>
public sealed class CardInstance
{
    /// <summary>Definicion de la carta (datos inmutables).</summary>
    public MonsterCard Card { get; }

    /// <summary>Posicion de batalla actual.</summary>
    public BattlePosition Position { get; set; }

    /// <summary>Indica si el monstruo ya ataco en el turno actual.</summary>
    public bool HasAttackedThisTurn { get; set; }

    /// <summary>Indica si fue invocado/colocado este turno (no puede cambiar posicion).</summary>
    public bool SummonedThisTurn { get; set; }

    /// <summary>Indica si ya cambio de posicion de batalla este turno.</summary>
    public bool PositionChangedThisTurn { get; set; }

    public CardInstance(MonsterCard card, BattlePosition position)
    {
        Card = card;
        Position = position;
    }

    /// <summary>Verdadero si la carta esta boca arriba.</summary>
    public bool IsFaceUp => Position != BattlePosition.DefenseFaceDown;

    /// <summary>Verdadero si la carta esta en alguna posicion de defensa.</summary>
    public bool IsDefending =>
        Position is BattlePosition.DefenseFaceUp or BattlePosition.DefenseFaceDown;

    /// <summary>Reinicia las banderas de estado al comenzar un nuevo turno del controlador.</summary>
    public void ResetTurnFlags()
    {
        HasAttackedThisTurn = false;
        SummonedThisTurn = false;
        PositionChangedThisTurn = false;
    }
}
