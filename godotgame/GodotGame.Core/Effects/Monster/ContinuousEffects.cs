using GodotGame.Core.Battle;
using GodotGame.Core.Entities;

namespace GodotGame.Core.Effects.Monster;

/// <summary>
/// Consulta EN VIVO los efectos Continuos de los Monstruos boca arriba (igual
/// que la Carta de Campo en <see cref="EffectiveStats"/>): no guardan estado,
/// asi que dejan de aplicarse en cuanto su Monstruo abandona el Campo o queda
/// boca abajo.
/// </summary>
public static class ContinuousEffects
{
    private sealed record ActiveEffect(PlayerSide Side, int Zone, CardInstance Instance, MonsterEffect Effect, int EffectIndex);

    private static IEnumerable<ActiveEffect> Active(DuelState state)
    {
        foreach (var player in state.Players)
        {
            for (int zone = 0; zone < player.MonsterZones.Length; zone++)
            {
                var instance = player.MonsterZones[zone];
                if (instance is not { IsFaceUp: true }) continue;
                var effects = instance.Card.Effects;
                for (int i = 0; i < effects.Count; i++)
                {
                    if (effects[i].Type != MonsterEffectType.Continuous) continue;
                    if (effects[i].ActivationConditions.Count > 0)
                    {
                        var activation = new EffectActivation(instance.Card, effects[i], i, player.Side, new CardRef(instance.Card, player.Side, CardZone.MonsterZone, zone));
                        if (!MonsterEffectCatalog.AllMet(effects[i].ActivationConditions, new MonsterEffectContext(state, activation))) continue;
                    }
                    yield return new ActiveEffect(player.Side, zone, instance, effects[i], i);
                }
            }
        }
    }

    /// <summary>Suma de ATK/DEF que los efectos Continuos le dan a <paramref name="target"/>.</summary>
    public static (int Attack, int Defense) StatBonus(CardInstance target, DuelState? state)
    {
        if (state == null) return (0, 0);
        var targetRef = Find(state, target);
        if (targetRef == null) return (0, 0);

        int attack = 0, defense = 0;
        foreach (var active in Active(state))
        {
            foreach (var step in active.Effect.Steps)
            {
                if (step.ActionKind != "stat_modifier") continue;
                if (!Applies(state, active, step.Params, target, targetRef)) continue;
                attack += step.Params.GetInt("Attack");
                defense += step.Params.GetInt("Defense");
            }
        }
        return (attack, defense);
    }

    /// <summary>Verdadero si un efecto Continuo de esta misma carta dice que no puede ser destruida en batalla.</summary>
    public static bool IsBattleIndestructible(CardInstance instance, DuelState state) => HasSelfPassive(instance, state, "battle_indestructible");

    /// <summary>Verdadero si un efecto Continuo de esta misma carta le permite atacar directamente.</summary>
    public static bool CanAttackDirectly(CardInstance instance, DuelState state) => HasSelfPassive(instance, state, "direct_attack");

    private static bool HasSelfPassive(CardInstance instance, DuelState state, string kind) =>
        Active(state).Any(a => ReferenceEquals(a.Instance, instance) && a.Effect.Steps.Any(s => s.ActionKind == kind));

    private static bool Applies(DuelState state, ActiveEffect active, EffectActionParams p, CardInstance target, CardRef targetRef)
    {
        if (p.GetEnum("Apply", ApplyTo.Self) == ApplyTo.Self)
            return ReferenceEquals(active.Instance, target);

        if (p.GetBool("ExcludeSource") && ReferenceEquals(active.Instance, target)) return false;
        if (!CardQuery.Sides(p.GetEnum("Side", RelativeSide.Own), active.Side).Contains(targetRef.Side)) return false;
        if (!target.IsFaceUp) return false;
        var query = new CardQuery(p.With("From", "MonsterZone"));
        return query.Matches(state, targetRef, active.Instance.Card);
    }

    private static CardRef? Find(DuelState state, CardInstance target)
    {
        foreach (var player in state.Players)
            for (int zone = 0; zone < player.MonsterZones.Length; zone++)
                if (ReferenceEquals(player.MonsterZones[zone], target))
                    return new CardRef(target.Card, player.Side, CardZone.MonsterZone, zone);
        return null;
    }
}
