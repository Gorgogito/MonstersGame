namespace GodotGame.Core.Entities;

/// <summary>Carta de Trampa. Igual que <see cref="SpellCard"/>, sin efectos aun (Bloque 5).</summary>
public sealed class TrapCard : Card
{
    public override CardKind Kind => CardKind.Trap;

    /// <summary>Subtipo (Normal, Continua, de Contraefecto).</summary>
    public TrapSubType SubType { get; }

    /// <summary>
    /// Velocidad de Hechizo: 2 para Normal/Continua, 3 para Contraefecto.
    /// Derivada del subtipo, no configurable directamente.
    /// </summary>
    public int SpellSpeed => SubType == TrapSubType.Counter ? 3 : 2;

    /// <summary>Clave del efecto asociado, resoluble por datos externos.</summary>
    public string EffectId { get; }

    public TrapCard(int id, string name, TrapSubType subType, string effectId = "", string image = "", string description = "")
        : base(id, name, image, description)
    {
        SubType = subType;
        EffectId = effectId ?? string.Empty;
    }
}
