namespace MonstersGame.Data.Loaders;

/// <summary>
/// DTOs de deserializacion JSON. Se mantienen separados de las entidades de
/// dominio para que el formato de archivo pueda evolucionar sin afectar al
/// modelo del juego. <see cref="CardDto"/> es publico porque tambien es el
/// modelo de edicion que usa <c>MonstersGame.CardEditor</c>.
/// </summary>
public sealed class CardDto
{
    public int Id { get; set; }
    public string Kind { get; set; } = "Monster";
    public string Name { get; set; } = "";
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int Level { get; set; }
    public string Type { get; set; } = "Unknown";
    public string Attribute { get; set; } = "Dark";
    public string Image { get; set; } = "";
    public string Description { get; set; } = "";
    public string EffectId { get; set; } = "";
    /// <summary>Solo para Kind = "Spell" (SpellSubType) o "Trap" (TrapSubType).</summary>
    public string SubType { get; set; } = "Normal";
    /// <summary>Solo para Kind = "Monster" (MonsterCategory): Normal, Effect, Fusion o Ritual.</summary>
    public string Category { get; set; } = "Normal";
    /// <summary>Solo para Kind = "Spell" con SubType = "Ritual": Id del Monstruo de Ritual asociado.</summary>
    public int RitualMonsterId { get; set; }
    /// <summary>Solo para Kind = "Spell" con SubType = "Ritual": suma minima de Niveles a Sacrificar.</summary>
    public int RequiredRitualLevel { get; set; }
}

internal sealed class DeckDto
{
    public string Name { get; set; } = "Mazo";
    public List<int> Cards { get; set; } = new();
}

internal sealed class FusionDto
{
    public int MaterialA { get; set; }
    public int MaterialB { get; set; }
    public int Result { get; set; }
}
