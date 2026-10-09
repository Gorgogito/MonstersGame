using GodotGame.Core.Battle;
using GodotGame.Core.Entities;

namespace GodotGame.Core.Effects.Monster;

/// <summary>
/// Consulta EN VIVO los efectos Continuos de las cartas boca arriba en el
/// Campo (igual que la Carta de Campo en <see cref="EffectiveStats"/>): no
/// guardan estado, asi que dejan de aplicarse en cuanto su carta abandona el
/// Campo o queda boca abajo. Cuentan los Monstruos boca arriba y las Magias/
/// Trampas boca arriba en la Zona de Magia/Trampa o en la Zona del Campo.
/// </summary>
public static class ContinuousEffects
{
    /// <summary>Un efecto Continuo vigente: de que carta, donde esta y (si es una Magia de Equipo) a que monstruo esta equipada.</summary>
    private sealed record ActiveEffect(PlayerSide Side, CardRef Source, CardInstance? Monster, EffectTarget? EquippedTo, MonsterEffect Effect);

    private static IEnumerable<ActiveEffect> Active(DuelState state)
    {
        foreach (var player in state.Players)
        {
            for (int zone = 0; zone < player.MonsterZones.Length; zone++)
            {
                var instance = player.MonsterZones[zone];
                if (instance is not { IsFaceUp: true }) continue;
                var source = new CardRef(instance.Card, player.Side, CardZone.MonsterZone, zone);
                foreach (var effect in ActiveOf(state, instance.Card, player.Side, source))
                    yield return new ActiveEffect(player.Side, source, instance, null, effect);
            }

            for (int zone = 0; zone < player.SpellTrapZones.Length; zone++)
            {
                var instance = player.SpellTrapZones[zone];
                if (instance is not { FaceUp: true }) continue;
                var source = new CardRef(instance.Card, player.Side, CardZone.SpellTrapZone, zone);
                foreach (var effect in ActiveOf(state, instance.Card, player.Side, source))
                    yield return new ActiveEffect(player.Side, source, null, instance.EquippedMonsterRef, effect);
            }

            if (player.FieldZone is { FaceUp: true } field)
            {
                var source = new CardRef(field.Card, player.Side, CardZone.FieldZone, 0);
                foreach (var effect in ActiveOf(state, field.Card, player.Side, source))
                    yield return new ActiveEffect(player.Side, source, null, null, effect);
            }
        }
    }

    private static IEnumerable<MonsterEffect> ActiveOf(DuelState state, Card card, PlayerSide side, CardRef source)
    {
        var effects = card.Effects;
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i].Type != MonsterEffectType.Continuous) continue;
            if (effects[i].ActivationConditions.Count > 0)
            {
                var activation = new EffectActivation(card, effects[i], i, side, source);
                if (!MonsterEffectCatalog.AllMet(effects[i].ActivationConditions, new MonsterEffectContext(state, activation))) continue;
            }
            yield return effects[i];
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

    /// <summary>Verdadero si un efecto Continuo dice que este monstruo no puede ser destruido en batalla.</summary>
    public static bool IsBattleIndestructible(CardInstance instance, DuelState state) => HasPassive(instance, state, "battle_indestructible");

    /// <summary>Verdadero si un efecto Continuo le permite a este monstruo atacar directamente.</summary>
    public static bool CanAttackDirectly(CardInstance instance, DuelState state) => HasPassive(instance, state, "direct_attack");

    private static bool HasPassive(CardInstance instance, DuelState state, string kind)
    {
        var targetRef = Find(state, instance);
        if (targetRef == null) return false;
        return Active(state).Any(a => a.Effect.Steps.Any(s => s.ActionKind == kind && Applies(state, a, s.Params, instance, targetRef)));
    }

    private static bool Applies(DuelState state, ActiveEffect active, EffectActionParams p, CardInstance target, CardRef targetRef)
    {
        switch (p.GetEnum("Apply", ApplyTo.Self))
        {
            case ApplyTo.Self:
                return ReferenceEquals(active.Monster, target);
            case ApplyTo.Equipped:
                return active.EquippedTo is { } equipped && equipped.Side == targetRef.Side && equipped.ZoneIndex == targetRef.Index;
        }

        if (p.GetBool("ExcludeSource") && ReferenceEquals(active.Monster, target)) return false;
        if (!CardQuery.Sides(p.GetEnum("Side", RelativeSide.Own), active.Side).Contains(targetRef.Side)) return false;
        if (!target.IsFaceUp) return false;
        var query = new CardQuery(p.With("From", "MonsterZone"));
        return query.Matches(state, targetRef, active.Source.Card);
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
