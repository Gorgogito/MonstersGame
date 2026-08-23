namespace MonstersGame.Core.Entities;

/// <summary>
/// Carta Magica. El campo <see cref="EffectId"/> apunta a un efecto resoluble
/// por un sistema de efectos (aun no implementado: Bloque 5 de la adaptacion).
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
    /// </summary>
    public int RequiredRitualLevel { get; }

    public SpellCard(
        int id,
        string name,
        SpellSubType subType,
        string effectId = "",
        int ritualMonsterId = 0,
        int requiredRitualLevel = 0,
        string image = "",
        string description = "")
        : base(id, name, image, description)
    {
        SubType = subType;
        EffectId = effectId ?? string.Empty;
        RitualMonsterId = ritualMonsterId;
        RequiredRitualLevel = requiredRitualLevel;
    }
}
