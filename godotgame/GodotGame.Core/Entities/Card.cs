namespace GodotGame.Core.Entities;

/// <summary>
/// Carta base del juego. Las subclases concretas (monstruo, magia, trampa)
/// heredan de aqui. El diseno permite agregar nuevas clases de carta sin
/// modificar la logica del motor, que opera contra esta abstraccion.
/// </summary>
public abstract class Card
{
    /// <summary>Identificador unico de la carta dentro de la base de datos.</summary>
    public int Id { get; }

    /// <summary>Nombre mostrado de la carta.</summary>
    public string Name { get; }

    /// <summary>Clase de carta (monstruo / magia / trampa).</summary>
    public abstract CardKind Kind { get; }

    /// <summary>Ruta o clave del recurso grafico (placeholder en esta version).</summary>
    public string Image { get; }

    /// <summary>Texto descriptivo de la carta.</summary>
    public string Description { get; }

    /// <summary>
    /// Efectos compuestos por datos (ver <see cref="GodotGame.Core.Effects.Monster.MonsterEffect"/>):
    /// de Monstruo (Continuo, Encendido, Disparado, Rapido, Volteo, No
    /// clasificado) o de Magia/Trampa (al activarse, Continuo, Encendido,
    /// Disparado, Rapido). Vacio = la carta no tiene efectos por datos.
    /// </summary>
    public IReadOnlyList<GodotGame.Core.Effects.Monster.MonsterEffect> Effects { get; }

    protected Card(int id, string name, string image, string description,
        IReadOnlyList<GodotGame.Core.Effects.Monster.MonsterEffect>? effects = null)
    {
        Id = id;
        Name = name;
        Image = image ?? string.Empty;
        Description = description ?? string.Empty;
        Effects = effects ?? Array.Empty<GodotGame.Core.Effects.Monster.MonsterEffect>();
    }

    /// <summary>El efecto que se aplica al activar la carta (solo Magias/Trampas), o null si no tiene uno por datos.</summary>
    public GodotGame.Core.Effects.Monster.MonsterEffect? ActivationEffect =>
        Effects.FirstOrDefault(e => e.Type == GodotGame.Core.Effects.Monster.MonsterEffectType.Activation);
}
