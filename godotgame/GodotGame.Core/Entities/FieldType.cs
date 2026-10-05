using GodotGame.Core.Requirements;

namespace GodotGame.Core.Entities;

/// <summary>
/// Catalogo extensible por datos de "tipo de Campo" (Bosque, Oceano,
/// Volcanico, etc.), analogo a como <c>Types</c> ya administra el Tipo de
/// Monstruo desde el editor sin recompilar. Referenciado por una
/// <see cref="SpellCard"/> con <see cref="SpellCard.SubType"/> =
/// <see cref="SpellSubType.Field"/>.
///
/// El modificador que aporta se calcula EN VIVO (ver <see cref="Battle.EffectiveStats"/>):
/// no hay estado por instancia como en Equip, se consulta "quien tiene el
/// Campo activo ahora mismo" en cada calculo de ATK/DEF efectivo. Por eso un
/// Monstruo que entra al Campo despues de activada la Carta de Campo tambien
/// recibe el bono, y uno que sale lo pierde, sin bookkeeping.
/// </summary>
public sealed class FieldType
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }

    /// <summary>Color de referencia para la presentacion visual (Fase 6), en formato <c>#RRGGBB</c>.</summary>
    public string Color { get; }

    /// <summary>Ruta de la imagen de fondo (Fase 6). Vacio = sin imagen propia.</summary>
    public string BackgroundImage { get; }

    /// <summary>Clave hacia un perfil visual reutilizable (Fase 6, mismo mecanismo que <c>EffectDefinition.VisualProfileKey</c>).</summary>
    public string VisualEffectsKey { get; }

    /// <summary>
    /// A que Monstruos afecta el modificador. Null = todos (mismo convenio
    /// que en cualquier otro consumidor del motor de predicados).
    /// </summary>
    public TargetFilter? AffectedFilter { get; }

    public int StatModifierAmount { get; }
    public FieldStatKind StatModifierStat { get; }

    /// <summary>
    /// Terreno elemental "de verdad" (adaptacion de esfuerzo medio inspirada
    /// en Forbidden Memories): un segundo filtro/monto INDEPENDIENTE del
    /// principal, tipicamente en signo opuesto (ej. un Campo de Fuego da
    /// <see cref="StatModifierAmount"/> a los Monstruos de Atributo Fuego via
    /// <see cref="AffectedFilter"/>, y <see cref="OpposedStatModifierAmount"/>
    /// -negativo- a los de Atributo Agua via <see cref="OpposedFilter"/>).
    /// Null = sin penalizacion (comportamiento identico al de antes de esta
    /// adaptacion). Se evalua con el mismo <see cref="StatModifierStat"/> que
    /// el modificador principal -- son dos caras del mismo terreno, no dos
    /// terrenos independientes.
    /// </summary>
    public TargetFilter? OpposedFilter { get; }
    public int OpposedStatModifierAmount { get; }

    public FieldType(
        string id,
        string name,
        string description,
        string color,
        string backgroundImage,
        string visualEffectsKey,
        TargetFilter? affectedFilter,
        int statModifierAmount,
        FieldStatKind statModifierStat,
        TargetFilter? opposedFilter = null,
        int opposedStatModifierAmount = 0)
    {
        Id = id;
        Name = name;
        Description = description;
        Color = color;
        BackgroundImage = backgroundImage;
        VisualEffectsKey = visualEffectsKey;
        AffectedFilter = affectedFilter;
        StatModifierAmount = statModifierAmount;
        StatModifierStat = statModifierStat;
        OpposedFilter = opposedFilter;
        OpposedStatModifierAmount = opposedStatModifierAmount;
    }
}
