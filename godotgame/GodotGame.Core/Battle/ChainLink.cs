using GodotGame.Core.Effects;
using GodotGame.Core.Entities;

namespace GodotGame.Core.Battle;

/// <summary>
/// Un eslabon de la Cadena: identifica quien la activo y en que Zona de
/// Magia/Trampa quedo la carta (ya boca arriba desde el momento de la
/// activacion). La carta y su subtipo se leen en vivo desde esa zona al
/// resolver, en vez de duplicarse aqui.
/// </summary>
public readonly struct ChainLink
{
    public required PlayerSide Controller { get; init; }
    public required int ZoneIndex { get; init; }

    /// <summary>Objetivo elegido al activar, si el efecto de la carta lo requiere.</summary>
    public EffectTarget? Target { get; init; }
}
