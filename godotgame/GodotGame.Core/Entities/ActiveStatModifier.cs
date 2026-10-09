namespace GodotGame.Core.Entities;

/// <summary>
/// Un modificador de ATK/DEF vigente sobre un <see cref="CardInstance"/> en
/// el Campo (hoy solo lo produce equipar una Magia de Equipo; una Carta de
/// Campo se sumara en la Fase 4 como un modificador calculado en vivo, no uno
/// de estos). Admite valores negativos: <see cref="AttackAmount"/>/
/// <see cref="DefenseAmount"/> son enteros con signo, no hay un tipo separado
/// para penalizaciones.
/// </summary>
public sealed class ActiveStatModifier
{
    public int AttackAmount { get; }
    public int DefenseAmount { get; }

    /// <summary>Niveles ganados (o perdidos) por un efecto ("esta carta gana 1 Nivel").</summary>
    public int LevelAmount { get; init; }
    public ModifierDuration Duration { get; }

    /// <summary>Solo relevante si <see cref="Duration"/> es <see cref="ModifierDuration.ForNTurns"/>: cuantas Fases Finales del controlador le quedan.</summary>
    public int RemainingTurns { get; set; }

    /// <summary>
    /// Solo relevante si <see cref="Duration"/> es <see cref="ModifierDuration.WhileEquipped"/>:
    /// la instancia de la Magia de Equipo que lo origino, para poder
    /// encontrarlo si esa carta especifica se elimina del Campo por otra via
    /// que no sea "el Monstruo equipado se va" (esa ya limpia el modificador
    /// junto con toda la instancia del Monstruo).
    /// </summary>
    public SpellTrapInstance? EquipSource { get; }

    public ActiveStatModifier(int attackAmount, int defenseAmount, ModifierDuration duration, int remainingTurns = 0, SpellTrapInstance? equipSource = null)
    {
        AttackAmount = attackAmount;
        DefenseAmount = defenseAmount;
        Duration = duration;
        RemainingTurns = remainingTurns;
        EquipSource = equipSource;
    }
}
