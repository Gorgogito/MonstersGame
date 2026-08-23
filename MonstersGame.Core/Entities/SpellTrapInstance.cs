namespace MonstersGame.Core.Entities;

/// <summary>
/// Una Carta Magica o de Trampa concreta situada en el Campo (Zona de Magia y
/// Trampas o Zona del Campo), con su propio estado de juego. Analoga a
/// <see cref="CardInstance"/> pero para cartas que no son de Monstruo.
/// </summary>
public sealed class SpellTrapInstance
{
    /// <summary>La carta (siempre <see cref="SpellCard"/> o <see cref="TrapCard"/>).</summary>
    public Card Card { get; }

    /// <summary>Boca arriba (activa) o boca abajo (Colocada, sin activar todavia).</summary>
    public bool FaceUp { get; set; }

    /// <summary>
    /// Verdadero si la carta fue Colocada en el turno actual. Una Carta de
    /// Trampa no puede activarse en el mismo turno en que fue Colocada.
    /// </summary>
    public bool SetThisTurn { get; set; }

    public SpellTrapInstance(Card card, bool faceUp)
    {
        Card = card;
        FaceUp = faceUp;
    }

    /// <summary>Reinicia las banderas de estado al comenzar un nuevo turno del controlador.</summary>
    public void ResetTurnFlags() => SetThisTurn = false;
}
