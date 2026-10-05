using Microsoft.Xna.Framework;
using MonstersGame.Core.Battle;
using MonstersGame.Core.Entities;
using MonstersGame.Graphics;

namespace MonstersGame.Screens.Duel;

/// <summary>
/// Drena <see cref="DuelState.Events"/> cada frame y mantiene los destellos
/// activos por Zona -- mismo patron que el "clash" de batalla que ya existia
/// en <see cref="DuelScreen"/> (un timer + una funcion de envolvente), ahora
/// generalizado para N Zonas concurrentes en vez de una sola. Cada Zona puede
/// tener a su vez varios destellos superpuestos (hasta <see cref="MaxFlashesPerZone"/>):
/// un evento nuevo en una Zona que ya tenia uno activo se AGREGA en vez de
/// pisarlo (Fase 2 del plan de mejoras visuales) -- antes, un segundo evento
/// en la misma Zona antes de que terminara el primero lo reemplazaba sin
/// transicion. No dibuja nada por si mismo: <see cref="DuelScreen"/> consulta
/// <see cref="TryGetOverlay"/> al dibujar cada Zona y decide como pintarlo.
///
/// Deliberadamente separado de <see cref="DuelState.LastAttack"/>/el "clash"
/// existente: esta capa no toca la presentacion del ataque en batalla, para
/// no mezclar ese codigo ya probado con esto (el clash tiene su propio
/// arreglo de concurrencia, ver <c>DuelScreen.ClashInstance</c>).
/// </summary>
public sealed class EventAnimationController
{
    public enum ZoneKind { Monster, SpellTrap, Field }

    /// <summary>Tope de destellos superpuestos por Zona -- evita crecimiento sin limite si una resolucion de Cadena dispara muchos eventos sobre la misma Zona en un mismo frame; el mas viejo se descarta primero.</summary>
    private const int MaxFlashesPerZone = 4;

    private readonly record struct ZoneRef(ZoneKind Kind, PlayerSide Side, int Index);
    private readonly record struct ActiveFlash(Color Color, float Remaining, float Total);

    private readonly Dictionary<ZoneRef, List<ActiveFlash>> _active = new();
    private readonly List<(ZoneKind Kind, PlayerSide Side, int Index, VisualProfile Profile)> _newThisFrame = new();

    /// <summary>Se invoca una vez por frame: drena eventos nuevos y hace avanzar los timers de los destellos activos.</summary>
    public void Update(DuelState state, float dt)
    {
        _newThisFrame.Clear();

        while (state.Events.Count > 0)
        {
            var evt = state.Events.Dequeue();
            var zone = ZoneRefFor(evt);
            if (zone == null) continue;

            var profile = VisualProfileCatalog.Resolve(VisualProfileCatalog.ResolveKey(evt, state));
            AddFlash(zone.Value, profile);
            _newThisFrame.Add((zone.Value.Kind, zone.Value.Side, zone.Value.Index, profile));
        }

        if (_active.Count == 0) return;
        foreach (var key in _active.Keys.ToList())
        {
            var flashes = _active[key];
            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                var flash = flashes[i];
                float remaining = flash.Remaining - dt;
                if (remaining <= 0f) flashes.RemoveAt(i);
                else flashes[i] = flash with { Remaining = remaining };
            }
            if (flashes.Count == 0) _active.Remove(key);
        }
    }

    private void AddFlash(ZoneRef zone, VisualProfile profile)
    {
        if (!_active.TryGetValue(zone, out var flashes))
        {
            flashes = new List<ActiveFlash>();
            _active[zone] = flashes;
        }
        if (flashes.Count >= MaxFlashesPerZone) flashes.RemoveAt(0);
        flashes.Add(new ActiveFlash(profile.FlashColor, profile.Duration, profile.Duration));
    }

    /// <summary>
    /// Color y opacidad del destello mas fuerte activo en una Zona (el que
    /// tenga mayor proporcion de vida restante -- en la practica, casi
    /// siempre el disparado mas recientemente), o false si no hay ninguno.
    /// Combina varios destellos superpuestos en un unico color/alpha para no
    /// romper el contrato con <c>DuelScreen.DrawEventOverlay</c>, que sigue
    /// pintando un solo rectangulo translucido por Zona.
    /// </summary>
    public bool TryGetOverlay(ZoneKind kind, PlayerSide side, int index, out Color color, out float alpha)
    {
        if (_active.TryGetValue(new ZoneRef(kind, side, index), out var flashes) && flashes.Count > 0)
        {
            var strongest = flashes[0];
            float strongestAlpha = AlphaOf(strongest);
            for (int i = 1; i < flashes.Count; i++)
            {
                float a = AlphaOf(flashes[i]);
                if (a > strongestAlpha) { strongest = flashes[i]; strongestAlpha = a; }
            }
            color = strongest.Color;
            alpha = strongestAlpha;
            return true;
        }
        color = Color.White;
        alpha = 0f;
        return false;
    }

    private static float AlphaOf(ActiveFlash flash) => MathHelper.Clamp(flash.Remaining / flash.Total, 0f, 1f);

    /// <summary>
    /// Los disparos que empezaron a animar recien en este frame (uno por
    /// evento drenado de <see cref="DuelState.Events"/>, en el mismo orden),
    /// para que otro consumidor (particulas, sonido -- Fases 3 y 7 del plan
    /// de mejoras visuales) reaccione a "esto acaba de pasar" sin tener que
    /// re-drenar la cola el mismo frame. Se recalcula en cada <see cref="Update"/>;
    /// si nadie la lee en un frame dado, se pierde (no se acumula).
    /// </summary>
    public IReadOnlyList<(ZoneKind Kind, PlayerSide Side, int Index, VisualProfile Profile)> DrainNewTriggers() => _newThisFrame;

    private static ZoneRef? ZoneRefFor(DuelEvent evt) => evt switch
    {
        MonsterSummonedEvent e => new ZoneRef(ZoneKind.Monster, e.Side, e.ZoneIndex),
        MonsterDestroyedEvent e => new ZoneRef(ZoneKind.Monster, e.Side, e.ZoneIndex),
        FusionPerformedEvent e => new ZoneRef(ZoneKind.Monster, e.Side, e.ZoneIndex),
        RitualPerformedEvent e => new ZoneRef(ZoneKind.Monster, e.Side, e.ZoneIndex),
        StatModifierAppliedEvent e => new ZoneRef(ZoneKind.Monster, e.Side, e.ZoneIndex),
        SpellTrapActivatedEvent e => new ZoneRef(ZoneKind.SpellTrap, e.Side, e.ZoneIndex),
        FieldChangedEvent e => new ZoneRef(ZoneKind.Field, e.Side, 0),
        _ => null
    };
}
