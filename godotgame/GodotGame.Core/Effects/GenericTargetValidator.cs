using GodotGame.Core.Battle;
using GodotGame.Core.Entities;
using GodotGame.Core.Requirements;

namespace GodotGame.Core.Effects;

/// <summary>
/// Valida un <see cref="EffectTarget"/> a partir unicamente de datos (Zona +
/// <see cref="TargetFilter"/> opcional), para efectos cuyo objetivo no esta
/// respaldado por ninguna accion que ya sepa validarse a si misma. Reutiliza
/// <see cref="TargetFilterEvaluator"/>, el mismo motor de predicados que
/// Fusion/Ritual/Equip/Field.
/// </summary>
internal static class GenericTargetValidator
{
    public static bool IsValid(DuelState state, Player controller, EffectTarget target, EffectTargetKind kind, TargetFilter? filter)
    {
        return kind switch
        {
            EffectTargetKind.MonsterZone => IsValidMonsterZoneTarget(state, controller, target, filter),
            EffectTargetKind.OwnGraveyard => IsValidOwnGraveyardTarget(controller, target, filter),
            _ => false
        };
    }

    private static bool IsValidMonsterZoneTarget(DuelState state, Player controller, EffectTarget target, TargetFilter? filter)
    {
        var player = state.GetPlayer(target.Side);
        if (target.ZoneIndex < 0 || target.ZoneIndex >= player.MonsterZones.Length) return false;
        var instance = player.MonsterZones[target.ZoneIndex];
        if (instance == null) return false;
        return filter == null || TargetFilterEvaluator.Matches(filter, instance.Card, target.Side, controller.Side);
    }

    private static bool IsValidOwnGraveyardTarget(Player controller, EffectTarget target, TargetFilter? filter)
    {
        if (target.Side != controller.Side) return false;
        if (target.ZoneIndex < 0 || target.ZoneIndex >= controller.Graveyard.Count) return false;
        if (controller.Graveyard[target.ZoneIndex] is not MonsterCard monster) return false;
        return filter == null || TargetFilterEvaluator.Matches(filter, monster, target.Side, controller.Side);
    }
}
