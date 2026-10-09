namespace GodotGame.Core.Entities;

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

    /// <summary>Modificadores de ATK/DEF vigentes (hoy, solo de Magias de Equipo). Ver <see cref="GodotGame.Core.Battle.EffectiveStats"/>.</summary>
    public List<ActiveStatModifier> ActiveModifiers { get; } = new();

    /// <summary>Estrella Guardiana en uso (una de las dos de la carta). Arranca en la primera hasta que el controlador elija.</summary>
    public GuardianStar GuardianStar { get; set; }

    /// <summary>Verdadero cuando el controlador ya eligio la estrella (ver <c>DuelEngine.ChooseGuardianStar</c>).</summary>
    public bool GuardianStarChosen { get; set; }

    /// <summary>
    /// Dueño de la carta si es distinto de quien la controla (ej. un efecto la
    /// Invoco al Campo del adversario). Null = la controla su dueño. Al salir
    /// del Campo vuelve al Cementerio/mano de su dueño.
    /// </summary>
    public PlayerSide? Owner { get; set; }

    /// <summary>Como entro al Campo (Normal, Colocada, por Volteo, Especial, Fusion o Ritual).</summary>
    public SummonMethod SummonMethod { get; set; } = SummonMethod.Special;

    /// <summary>Nivel ganado/perdido por efectos (ver <see cref="ActiveStatModifier.LevelAmount"/>).</summary>
    public int EffectiveLevel => Math.Max(1, Card.Level + ActiveModifiers.Sum(m => m.LevelAmount));

    public CardInstance(MonsterCard card, BattlePosition position)
    {
        Card = card;
        Position = position;
        GuardianStar = card.GuardianStar1;
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
