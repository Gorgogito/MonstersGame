using GodotGame.Core.Entities;

namespace GodotGame.Core.Effects;

/// <summary>
/// Objetivo elegido al activar una carta con efecto que lo requiere. El
/// significado de <see cref="ZoneIndex"/> depende de la accion concreta: una
/// Zona de Monstruos o un indice dentro del Cementerio de <see cref="Side"/>.
/// Se elige en el momento de la activacion (como en el reglamento real) y se
/// vuelve a validar al resolverse, por si el tablero cambio mientras tanto.
/// </summary>
public readonly struct EffectTarget
{
    public required PlayerSide Side { get; init; }
    public required int ZoneIndex { get; init; }
}
