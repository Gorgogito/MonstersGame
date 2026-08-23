namespace MonstersGame.Core.Entities;

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

    protected Card(int id, string name, string image, string description)
    {
        Id = id;
        Name = name;
        Image = image ?? string.Empty;
        Description = description ?? string.Empty;
    }
}
