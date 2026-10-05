using GodotGame.Core.Entities;

namespace GodotGame.Core.Battle;

/// <summary>
/// Cuando un Monstruo abandona el Campo por cualquier via (batalla,
/// Sacrificio, o un efecto que lo destruye directamente), cualquier Magia de
/// Equipo que estuviera equipada a esa Zona (de cualquiera de los dos
/// jugadores: el motor de filtros ya permite equipar al Monstruo del rival)
/// se envia al Cementerio -- una Magia de Equipo sin Monstruo al que estar
/// equipada no permanece en el Campo. El modificador en si no necesita
/// limpieza aparte: vive en <see cref="CardInstance.ActiveModifiers"/> de la
/// instancia que se esta descartando junto con ella.
///
/// Extraido de <see cref="DuelEngine"/> a una clase compartida porque
/// <c>DestroyTargetMonsterAction</c> (en <c>GodotGame.Core.Effects</c>)
/// tambien destruye Monstruos directamente y necesitaba la misma limpieza
/// -- antes solo la aplicaban los 3 sitios de <see cref="DuelEngine"/> que
/// ya la llamaban (Sacrificio de Ritual, Sacrificio de Invocacion Normal,
/// destruccion en batalla), dejando huerfana la Magia de Equipo cuando el
/// Monstruo se destruia por un efecto en vez de por esas 3 vias.
/// </summary>
internal static class EquipCleanup
{
    public static void DetachEquipsTargeting(DuelState state, PlayerSide side, int zoneIndex)
    {
        foreach (var owner in state.Players)
        {
            for (int i = 0; i < owner.SpellTrapZones.Length; i++)
            {
                var instance = owner.SpellTrapZones[i];
                if (instance?.EquippedMonsterRef is not { } target) continue;
                if (target.Side != side || target.ZoneIndex != zoneIndex) continue;

                owner.SpellTrapZones[i] = null;
                owner.SendToGraveyard(instance.Card);
                state.Log.Add($"{instance.Card.Name} va al Cementerio: el Monstruo que tenia Equipado abandono el Campo.");
            }
        }
    }
}
