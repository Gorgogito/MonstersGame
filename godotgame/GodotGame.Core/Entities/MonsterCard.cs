namespace GodotGame.Core.Entities;

/// <summary>
/// Carta de Monstruo. Contiene los atributos descritos en el reglamento
/// (pagina 6-7): ATK, DEF, Nivel, Tipo y Atributo.
/// </summary>
public sealed class MonsterCard : Card
{
    public override CardKind Kind => CardKind.Monster;

    /// <summary>Puntos de Ataque originales impresos en la carta.</summary>
    public int Attack { get; }

    /// <summary>Puntos de Defensa originales impresos en la carta.</summary>
    public int Defense { get; }

    /// <summary>Nivel (1-12). Determina los Sacrificios necesarios para invocar.</summary>
    public int Level { get; }

    /// <summary>
    /// Tipo de Monstruo (Dragon, Guerrero, etc.). Texto libre, no un enum: el
    /// catalogo de Tipos vive en la base de datos (tabla <c>Types</c>) y se
    /// administra desde el editor de cartas, no en el codigo — no tiene
    /// ninguna sinergia de reglas propia todavia, es puramente descriptivo.
    /// </summary>
    public string Type { get; }

    public MonsterAttribute Attribute { get; }

    /// <summary>
    /// Categoria del monstruo (pagina 6-10): determina si puede Invocarse de
    /// Modo Normal/por Sacrificio (Normal y Efecto) o solo de Modo Especial
    /// (Fusion y Ritual).
    /// </summary>
    public MonsterCategory Category { get; }

    /// <summary>
    /// Las dos Estrellas Guardianas entre las que elige su controlador al
    /// Invocarlo (ver <see cref="GodotGame.Core.Rules.GuardianStars"/>). Si no
    /// se indican, se toman las de por defecto de su Atributo.
    /// </summary>
    public GuardianStar GuardianStar1 { get; }
    public GuardianStar GuardianStar2 { get; }

    /// <summary>
    /// Clave del efecto asociado (solo relevante si <see cref="Category"/> es
    /// <see cref="MonsterCategory.Effect"/>). En esta version solo se resuelve
    /// para Efectos de Volteo; el resto de categorias de efecto de monstruo
    /// (Continuo, de Encendido, Rapido) quedan para una iteracion posterior.
    /// </summary>
    public string EffectId { get; }

    /// <summary>
    /// Efectos de Monstruo compuestos por datos (Continuo, de Encendido,
    /// Disparado, Rapido, de Volteo o No clasificado; ver
    /// <see cref="GodotGame.Core.Effects.Monster.MonsterEffect"/>). Independiente
    /// del <see cref="EffectId"/> heredado (efecto de Volteo de un solo paso).
    /// </summary>
    public IReadOnlyList<GodotGame.Core.Effects.Monster.MonsterEffect> Effects { get; }

    /// <summary>
    /// Numero de Sacrificios necesarios para la Invocacion Normal (pagina 20):
    /// Nivel 1-4 = 0, Nivel 5-6 = 1, Nivel 7+ = 2. No aplica a Fusion/Ritual,
    /// que nunca se Invocan de Modo Normal.
    /// </summary>
    public int RequiredTributes => Level >= 7 ? 2 : Level >= 5 ? 1 : 0;

    public MonsterCard(
        int id,
        string name,
        int attack,
        int defense,
        int level,
        string type,
        MonsterAttribute attribute,
        MonsterCategory category = MonsterCategory.Normal,
        string effectId = "",
        string image = "",
        string description = "",
        GuardianStar? guardianStar1 = null,
        GuardianStar? guardianStar2 = null,
        IReadOnlyList<GodotGame.Core.Effects.Monster.MonsterEffect>? effects = null)
        : base(id, name, image, description)
    {
        Attack = attack;
        Defense = defense;
        Level = level;
        Type = type ?? string.Empty;
        Attribute = attribute;
        Category = category;
        EffectId = effectId ?? string.Empty;
        Effects = effects ?? Array.Empty<GodotGame.Core.Effects.Monster.MonsterEffect>();

        var defaults = GodotGame.Core.Rules.GuardianStars.DefaultsFor(attribute);
        GuardianStar1 = guardianStar1 ?? defaults.First;
        GuardianStar2 = guardianStar2 ?? defaults.Second;
    }
}
