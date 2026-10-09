using GodotGame.Core.Entities;
using GodotGame.Core.Effects.Monster;
using GodotGame.Core.Requirements;

namespace GodotGame.Core.Battle;

/// <summary>
/// Calcula el ATK/DEF "efectivo" de una carta en el campo (valor impreso mas
/// modificadores vigentes: Equip, Carta de Campo, etc.). Es una clase pura sin
/// estado, igual que <see cref="BattleResolver"/>.
///
/// Suma <see cref="CardInstance.ActiveModifiers"/> (Equip, guardado por
/// instancia) mas el modificador de la Carta de Campo de CADA jugador, si la
/// tiene (Field: cada <see cref="Player"/> tiene su propia Zona del Campo en
/// este proyecto, ver <see cref="Player.FieldZone"/> -- no es una unica Zona
/// compartida). El de Field se calcula EN VIVO, consultando
/// <see cref="Player.FieldZone"/> en cada llamada; no hay estado guardado por
/// instancia como en Equip, asi que un Monstruo que entra al Campo despues de
/// activada la Carta de Campo tambien lo recibe automaticamente.
/// </summary>
public static class EffectiveStats
{
    public static int EffectiveAttack(CardInstance instance, DuelState state, Player controller) =>
        Math.Max(0, instance.Card.Attack
            + instance.ActiveModifiers.Sum(m => m.AttackAmount)
            + FieldModifierSum(instance, state, controller, FieldStatKind.Attack)
            + ContinuousEffects.StatBonus(instance, state).Attack);

    public static int EffectiveDefense(CardInstance instance, DuelState state, Player controller) =>
        Math.Max(0, instance.Card.Defense
            + instance.ActiveModifiers.Sum(m => m.DefenseAmount)
            + FieldModifierSum(instance, state, controller, FieldStatKind.Defense)
            + ContinuousEffects.StatBonus(instance, state).Defense);

    private static int FieldModifierSum(CardInstance instance, DuelState state, Player controller, FieldStatKind stat)
    {
        int sum = 0;
        foreach (var fieldOwner in state.Players)
        {
            var fieldType = (fieldOwner.FieldZone?.Card as SpellCard)?.FieldType;
            if (fieldType == null) continue;
            if (fieldType.StatModifierStat != stat && fieldType.StatModifierStat != FieldStatKind.Both) continue;

            if (fieldType.StatModifierAmount != 0
                && (fieldType.AffectedFilter == null
                    || TargetFilterEvaluator.Matches(fieldType.AffectedFilter, instance.Card, controller.Side, fieldOwner.Side)))
                sum += fieldType.StatModifierAmount;

            // Terreno elemental (adaptacion de esfuerzo medio): segundo
            // filtro/monto INDEPENDIENTE del principal, tipicamente en signo
            // opuesto -- ver FieldType.OpposedFilter. A diferencia del
            // principal, aqui SI hace falta el filtro (sin el, "penalizar a
            // todos" no tiene sentido como terreno elemental).
            if (fieldType.OpposedFilter != null && fieldType.OpposedStatModifierAmount != 0
                && TargetFilterEvaluator.Matches(fieldType.OpposedFilter, instance.Card, controller.Side, fieldOwner.Side))
                sum += fieldType.OpposedStatModifierAmount;
        }
        return sum;
    }
}
