using GodotGame.Core.Battle;
using GodotGame.Core.Entities;

namespace GodotGame.Core.Effects;

/// <summary>
/// Todo lo que una <see cref="IEffectAction"/> necesita para resolverse: el
/// estado del duelo, quien la controla, la carta que la origina, el objetivo
/// elegido (si lo requiere) y, solo para Trampas de Contraefecto, la forma de
/// negar la activacion a la que responden.
/// </summary>
public sealed class EffectContext
{
    public required DuelState State { get; init; }
    public required Player Controller { get; init; }
    public required Card Source { get; init; }
    public EffectTarget? Target { get; init; }

    /// <summary>
    /// Solo se establece al resolver una Trampa de Contraefecto: invocarlo
    /// marca como negado el eslabon de la Cadena al que esta respondia.
    /// </summary>
    public Action? NegateRespondedLink { get; init; }

    public Player Opponent =>
        State.GetPlayer(Controller.Side == PlayerSide.Human ? PlayerSide.Cpu : PlayerSide.Human);
}
