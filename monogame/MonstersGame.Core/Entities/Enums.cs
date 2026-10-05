namespace MonstersGame.Core.Entities;

/// <summary>Clase de carta. Permite ampliar a Magias y Trampas sin refactorizar.</summary>
public enum CardKind
{
    Monster,
    Spell,
    Trap
}

/// <summary>Atributo del monstruo (pagina 7 del reglamento).</summary>
public enum MonsterAttribute
{
    Dark,
    Earth,
    Fire,
    Light,
    Water,
    Wind,
    Divine
}

/// <summary>
/// Posicion de batalla de un monstruo en el campo (pagina 4 y 20 del reglamento).
/// </summary>
public enum BattlePosition
{
    Attack,           // Ataque boca arriba
    DefenseFaceUp,    // Defensa boca arriba
    DefenseFaceDown   // Defensa boca abajo (Colocada)
}

/// <summary>
/// Fases del turno (pagina 30 del reglamento). Se incluyen todas para
/// extensibilidad aunque la version inicial use principalmente Main1 y Battle.
/// </summary>
public enum DuelPhase
{
    Draw,
    Standby,
    Main1,
    Battle,
    Main2,
    End
}

/// <summary>Identifica a cada uno de los dos duelistas.</summary>
public enum PlayerSide
{
    Human,
    Cpu
}

/// <summary>Subtipo de Carta Magica (seccion "Carta Magicas" del reglamento).</summary>
public enum SpellSubType
{
    Normal,
    Ritual,
    Continuous,
    Equip,
    Field,
    QuickPlay
}

/// <summary>Subtipo de Carta de Trampa (seccion "Cartas de Trampa" del reglamento).</summary>
public enum TrapSubType
{
    Normal,
    Continuous,
    Counter
}

/// <summary>
/// Categoria de un Monstruo (seccion "Que es una Carta de Monstruo"). Normal y
/// Efecto se Invocan de Modo Normal/por Sacrificio; Fusion y Ritual solo de
/// Modo Especial, mediante su propio procedimiento de invocacion.
/// </summary>
public enum MonsterCategory
{
    Normal,
    Effect,
    Fusion,
    Ritual
}
