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

    /// <summary>
    /// Solo para eslabones de efectos de Monstruo (Encendido, Disparado,
    /// Rapido, Volteo): la activacion completa. En ese caso <see cref="ZoneIndex"/>
    /// no se usa (vale -1): la carta puede estar en la mano, el Cementerio, etc.
    /// </summary>
    public GodotGame.Core.Effects.Monster.EffectActivation? MonsterEffect { get; init; }

    /// <summary>La carta de este eslabon (Magia/Trampa en su Zona, o el Monstruo cuyo efecto se activo).</summary>
    public Card? CardIn(DuelState state) =>
        MonsterEffect?.Source ?? (ZoneIndex >= 0 ? state.GetPlayer(Controller).SpellTrapZones[ZoneIndex]?.Card : null);
}
