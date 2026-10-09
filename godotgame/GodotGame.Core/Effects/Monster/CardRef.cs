using GodotGame.Core.Battle;
using GodotGame.Core.Entities;

namespace GodotGame.Core.Effects.Monster;

/// <summary>
/// Una carta concreta en un lugar concreto, en el momento en que se la eligio.
/// Al resolverse se vuelve a buscar (ver <see cref="CardMover.Locate"/>), por
/// si el tablero cambio entretanto ("si lo hay").
/// </summary>
public sealed record CardRef(Card Card, PlayerSide Side, CardZone Zone, int Index)
{
    /// <summary>La instancia de Monstruo, si la carta esta en una Zona de Monstruos.</summary>
    public CardInstance? MonsterInstance(DuelState state) =>
        Zone == CardZone.MonsterZone ? state.GetPlayer(Side).MonsterZones[Index] : null;

    /// <summary>La instancia de Magia/Trampa, si la carta esta en una Zona de Magia/Trampa o del Campo.</summary>
    public SpellTrapInstance? SpellTrapInstance(DuelState state) => Zone switch
    {
        CardZone.SpellTrapZone => state.GetPlayer(Side).SpellTrapZones[Index],
        CardZone.FieldZone => state.GetPlayer(Side).FieldZone,
        _ => null
    };

    /// <summary>Verdadero si la carta esta boca arriba (en mano/Cementerio/Destierro se considera visible).</summary>
    public bool IsFaceUp(DuelState state) => Zone switch
    {
        CardZone.MonsterZone => MonsterInstance(state)?.IsFaceUp ?? false,
        CardZone.SpellTrapZone or CardZone.FieldZone => SpellTrapInstance(state)?.FaceUp ?? false,
        CardZone.Deck => false,
        _ => true
    };

    public static string ZoneName(CardZone zone) => zone switch
    {
        CardZone.Hand => "Mano",
        CardZone.Deck => "Deck",
        CardZone.Graveyard => "Cementerio",
        CardZone.Banished => "Desterrada",
        CardZone.MonsterZone => "Zona de Monstruos",
        CardZone.SpellTrapZone => "Zona de Magia/Trampa",
        CardZone.FieldZone => "Zona del Campo",
        _ => zone.ToString()
    };
}
