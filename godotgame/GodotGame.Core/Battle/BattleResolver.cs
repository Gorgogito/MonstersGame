using GodotGame.Core.Entities;
using GodotGame.Core.Rules;

namespace GodotGame.Core.Battle;

/// <summary>Resultado del calculo de daño de una batalla entre monstruos.</summary>
public readonly struct BattleOutcome
{
    /// <summary>El monstruo atacante es destruido.</summary>
    public bool AttackerDestroyed { get; init; }

    /// <summary>El monstruo defensor es destruido.</summary>
    public bool DefenderDestroyed { get; init; }

    /// <summary>Daño aplicado a los LP del jugador atacante.</summary>
    public int DamageToAttacker { get; init; }

    /// <summary>Daño aplicado a los LP del jugador atacado.</summary>
    public int DamageToDefender { get; init; }
}

/// <summary>
/// Calcula el resultado de una batalla segun las "Reglas de Batallas de
/// Monstruos" (paginas 38-39 del reglamento). Es una clase pura sin estado:
/// recibe atacante y defensor y devuelve el desenlace.
///
/// Decision asumida (inspirada en Forbidden Memories y el TCG clasico): NO se
/// aplica daño de penetracion salvo que una carta lo indique (ninguna en esta
/// version), acorde con la regla general del reglamento.
/// </summary>
public static class BattleResolver
{
    /// <summary>Ataque directo a los LP del adversario (pagina 39).</summary>
    public static BattleOutcome ResolveDirectAttack(CardInstance attacker, DuelState state, Player attackerController)
    {
        return new BattleOutcome { DamageToDefender = EffectiveStats.EffectiveAttack(attacker, state, attackerController) };
    }

    /// <summary>Los numeros que se enfrentan en una batalla, ya con el bono de Estrella Guardiana incluido, y ese bono por separado (para mostrarlo).</summary>
    public readonly record struct BattleValues(int AttackerValue, int DefenderValue, int AttackerStarBonus, int DefenderStarBonus);

    /// <summary>
    /// ATK efectivo del atacante contra ATK o DEF efectivo del defensor (segun
    /// su posicion), mas +<see cref="GuardianStars.Bonus"/> para quien tenga
    /// ventaja de Estrella Guardiana. El defensor ya debe estar boca arriba.
    /// </summary>
    public static BattleValues CombatValues(
        CardInstance attacker, CardInstance defender,
        DuelState state, Player attackerController, Player defenderController)
    {
        var (attackerBonus, defenderBonus) = GuardianStars.BattleBonuses(attacker, defender);
        int atk = EffectiveStats.EffectiveAttack(attacker, state, attackerController) + attackerBonus;
        int defenderValue = defender.IsDefending
            ? EffectiveStats.EffectiveDefense(defender, state, defenderController)
            : EffectiveStats.EffectiveAttack(defender, state, defenderController);
        return new BattleValues(atk, defenderValue + defenderBonus, attackerBonus, defenderBonus);
    }

    /// <summary>
    /// Resuelve una batalla contra un monstruo. El defensor puede estar en
    /// ataque o en defensa. Si esta boca abajo, se asume que ya fue volteado
    /// por el motor (la DEF es visible) antes de llamar a este metodo.
    /// </summary>
    public static BattleOutcome Resolve(
        CardInstance attacker, CardInstance defender,
        DuelState state, Player attackerController, Player defenderController)
    {
        var values = CombatValues(attacker, defender, state, attackerController, defenderController);
        int atk = values.AttackerValue;

        if (defender.IsDefending)
        {
            int def = values.DefenderValue;

            // ATK vs DEF (pagina 39).
            if (atk > def)
            {
                // Victoria: defensor destruido, sin daño.
                return new BattleOutcome { DefenderDestroyed = true };
            }
            if (atk == def)
            {
                // Empate: ninguno destruido, sin daño.
                return new BattleOutcome();
            }
            // Derrota: atacante no destruido (regla general), el atacante recibe
            // la diferencia DEF - ATK como daño de batalla.
            return new BattleOutcome { DamageToAttacker = def - atk };
        }

        // El defensor esta en Posicion de Ataque: ATK vs ATK (pagina 38).
        int defAtk = values.DefenderValue;

        // Regla "Monstruos con 0 ATK" (pagina 46): no destruyen nada en batalla.
        if (atk > defAtk)
        {
            return new BattleOutcome
            {
                DefenderDestroyed = true,
                DamageToDefender = atk - defAtk
            };
        }
        if (atk == defAtk)
        {
            // Empate: ambos destruidos, sin daño. Si ambos son 0 ATK, ninguno
            // se destruye (regla de 0 ATK).
            bool bothZero = atk == 0 && defAtk == 0;
            return new BattleOutcome
            {
                AttackerDestroyed = !bothZero,
                DefenderDestroyed = !bothZero
            };
        }
        // Derrota del atacante.
        return new BattleOutcome
        {
            AttackerDestroyed = true,
            DamageToAttacker = defAtk - atk
        };
    }
}
