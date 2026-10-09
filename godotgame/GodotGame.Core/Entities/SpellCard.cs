using GodotGame.Core.Requirements;

namespace GodotGame.Core.Entities;

/// <summary>
/// Carta Magica. Su efecto puede venir de <see cref="Card.Effects"/> (efectos
/// compuestos por datos: al activarse, Continuo, de Encendido, Disparado...)
/// o, en las cartas antiguas, del <see cref="EffectId"/> heredado.
///
/// El subtipo decide como se juega (ver <c>SpellTrapCatalog</c>):
/// Normal (se resuelve y va al Cementerio), Continua (se queda boca arriba),
/// de Equipo (se equipa a un Monstruo), de Campo (Zona del Campo, afecta a
/// ambos jugadores), de Juego Rapido (Velocidad 2: en el turno rival si se
/// Coloco antes) y de Ritual (Invocacion Ritual).
/// </summary>
public sealed class SpellCard : Card
{
    public override CardKind Kind => CardKind.Spell;

    /// <summary>Subtipo (Normal, Ritual, Continua, de Equipo, de Campo, de Juego Rapido).</summary>
    public SpellSubType SubType { get; }

    /// <summary>
    /// Velocidad de Hechizo: 1 para todas las Magicas salvo las de Juego
    /// Rapido, que son 2. Derivada del subtipo, no configurable directamente.
    /// </summary>
    public int SpellSpeed => SubType == SpellSubType.QuickPlay ? 2 : 1;

    /// <summary>
    /// Clave del efecto asociado. Se resuelve contra <c>EffectRegistry</c> (la
    /// via de escape del sistema de efectos, Bloque 5): mientras no exista un
    /// formato declarativo en JSON, los efectos reales se registran en codigo.
    /// </summary>
    public string EffectId { get; }

    /// <summary>
    /// Solo relevante si <see cref="SubType"/> es <see cref="SpellSubType.Ritual"/>:
    /// Id del Monstruo de Ritual que esta Carta Magica puede invocar.
    /// </summary>
    public int RitualMonsterId { get; }

    /// <summary>
    /// Solo relevante si <see cref="SubType"/> es <see cref="SpellSubType.Ritual"/>:
    /// suma minima de Niveles que deben sumar los monstruos Sacrificados.
    ///
    /// Se conserva de solo-lectura/historico tras la migracion a
    /// <see cref="Requirement"/> (columna original <c>RequiredRitualLevel</c>
    /// de la tabla <c>Cards</c>): el motor ya no lo consume para validar el
    /// Ritual, solo <see cref="Requirement"/>.
    /// </summary>
    public int RequiredRitualLevel { get; }

    /// <summary>
    /// Solo relevante si <see cref="SubType"/> es <see cref="SpellSubType.Ritual"/>:
    /// requisito de Sacrificio en el modelo generico (modo <see cref="Requirements.RequirementMode.LevelSum"/>).
    /// Null si la carta todavia no fue migrada (no deberia ocurrir tras la
    /// migracion de respaldo de Rituales existentes).
    /// </summary>
    public RequirementSet? Requirement { get; }

    /// <summary>
    /// Solo relevante si <see cref="SubType"/> es <see cref="SpellSubType.Equip"/>:
    /// que Monstruos son objetivo legal. Null = cualquier Monstruo en el
    /// Campo (mismo convenio que un <see cref="TargetFilter"/> vacio en
    /// cualquier otro consumidor del motor de predicados).
    /// </summary>
    public TargetFilter? EquipTargetFilter { get; }

    /// <summary>Solo relevante si <see cref="SubType"/> es <see cref="SpellSubType.Equip"/>: modificador de ATK al equiparse (admite negativos).</summary>
    public int EquipAttackModifier { get; }

    /// <summary>Solo relevante si <see cref="SubType"/> es <see cref="SpellSubType.Equip"/>: modificador de DEF al equiparse (admite negativos).</summary>
    public int EquipDefenseModifier { get; }

    /// <summary>Solo relevante si <see cref="SubType"/> es <see cref="SpellSubType.Equip"/>: duracion del modificador que produce.</summary>
    public ModifierDuration EquipDuration { get; }

    /// <summary>Solo relevante si <see cref="EquipDuration"/> es <see cref="ModifierDuration.ForNTurns"/>: cuantos turnos dura.</summary>
    public int EquipDurationTurns { get; }

    /// <summary>
    /// Solo relevante si <see cref="SubType"/> es <see cref="SpellSubType.Field"/>:
    /// el tipo de Campo que esta Carta activa. Null si la carta todavia no
    /// fue configurada desde el editor (una Carta de Campo sin
    /// <see cref="FieldType"/> se activa igual, simplemente sin modificador
    /// ni tema visual).
    /// </summary>
    public FieldType? FieldType { get; }

    public SpellCard(
        int id,
        string name,
        SpellSubType subType,
        string effectId = "",
        int ritualMonsterId = 0,
        int requiredRitualLevel = 0,
        string image = "",
        string description = "",
        RequirementSet? requirement = null,
        TargetFilter? equipTargetFilter = null,
        int equipAttackModifier = 0,
        int equipDefenseModifier = 0,
        ModifierDuration equipDuration = ModifierDuration.WhileEquipped,
        int equipDurationTurns = 0,
        FieldType? fieldType = null,
        IReadOnlyList<GodotGame.Core.Effects.Monster.MonsterEffect>? effects = null)
        : base(id, name, image, description, effects)
    {
        SubType = subType;
        EffectId = effectId ?? string.Empty;
        RitualMonsterId = ritualMonsterId;
        RequiredRitualLevel = requiredRitualLevel;
        Requirement = requirement;
        EquipTargetFilter = equipTargetFilter;
        EquipAttackModifier = equipAttackModifier;
        EquipDefenseModifier = equipDefenseModifier;
        EquipDuration = equipDuration;
        EquipDurationTurns = equipDurationTurns;
        FieldType = fieldType;
    }
}
