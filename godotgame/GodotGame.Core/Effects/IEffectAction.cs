using GodotGame.Core.Battle;
using GodotGame.Core.Entities;

namespace GodotGame.Core.Effects;

/// <summary>
/// Una accion atomica de efecto (patron Strategy): la unidad minima con la
/// que se compone el efecto de una carta. Deliberadamente pequena e
/// independiente entre si, para no terminar con un arbol de <c>switch</c> por
/// carta (ver seccion 7 del analisis de adaptacion).
/// </summary>
public interface IEffectAction
{
    /// <summary>
    /// Resuelve la accion. Debe ser defensiva: si el objetivo (u otro estado
    /// que asumia) ya no es valido porque el tablero cambio entre la
    /// activacion y la resolucion, no debe lanzar excepciones, simplemente no
    /// hacer nada.
    /// </summary>
    void Resolve(EffectContext context);
}

/// <summary>
/// Que clase de cosa espera <see cref="EffectTarget.ZoneIndex"/> para una
/// accion en concreto. Se lo expone la carta a la UI (que no conoce reglas)
/// para que sepa que control mostrar: click en una Zona de Monstruos, o un
/// explorador del Cementerio propio.
/// </summary>
public enum EffectTargetKind
{
    MonsterZone,
    OwnGraveyard
}

/// <summary>
/// Una <see cref="IEffectAction"/> que necesita que el jugador elija un
/// <see cref="EffectTarget"/> en el momento de activar la carta (no al
/// resolverla, igual que en el reglamento real).
/// </summary>
public interface ITargetedEffectAction : IEffectAction
{
    /// <summary>Que tipo de objetivo espera, para que la UI muestre el control correcto.</summary>
    EffectTargetKind TargetKind { get; }

    /// <summary>Valida un objetivo propuesto contra el estado actual del duelo.</summary>
    bool IsValidTarget(DuelState state, Player controller, EffectTarget target);
}
