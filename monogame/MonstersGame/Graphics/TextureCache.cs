using Microsoft.Xna.Framework.Graphics;
using MonstersGame.Core.Entities;

namespace MonstersGame.Graphics;

/// <summary>
/// Carga bajo demanda y cachea las texturas reales de arte (Bloque 11):
/// imagen por carta, insignia por Atributo, y la estrella de Nivel. Se cargan
/// con <see cref="Texture2D.FromStream"/> directo sobre el PNG — no se usa el
/// Content Pipeline (mgcb), asi que no hace falta compilar nada de antemano.
/// Si un archivo falta o esta corrupto, se cachea <c>null</c> para esa clave
/// y el renderer sigue con el placeholder de siempre — una carta sin imagen
/// nunca debe romper el juego.
/// </summary>
public sealed class TextureCache
{
    private readonly GraphicsDevice _device;
    private readonly string _cardsDir;
    private readonly string _attributesDir;
    private readonly string _fieldsDir;
    private readonly string _levelStarPath;
    private readonly string _cardBackPath;
    private readonly Dictionary<string, Texture2D?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public TextureCache(GraphicsDevice device, string artRoot)
    {
        _device = device;
        _cardsDir = Path.Combine(artRoot, "Cards");
        _attributesDir = Path.Combine(artRoot, "Attributes");
        _fieldsDir = Path.Combine(artRoot, "Fields");
        _levelStarPath = Path.Combine(artRoot, "Level", "level.png");
        _cardBackPath = Path.Combine(_cardsDir, "CardBack.jpg");
    }

    /// <summary>Textura de arte de una carta a partir de <see cref="Card.Image"/>, o null si no hay archivo (carta sin imagen, o Image vacio).</summary>
    public Texture2D? CardArt(Card card) =>
        string.IsNullOrWhiteSpace(card.Image) ? null : LoadFromFile(Path.Combine(_cardsDir, card.Image));

    /// <summary>Insignia del Atributo de un Monstruo, o null si no hay archivo para ese Atributo.</summary>
    public Texture2D? AttributeIcon(MonsterAttribute attribute) =>
        LoadFromFile(Path.Combine(_attributesDir, AttributeFileName(attribute)));

    /// <summary>Imagen de fondo de un <c>FieldType</c> a partir de <see cref="FieldType.BackgroundImage"/>, o null si no hay archivo (Vacio/sin archivo = degrada al color de tema).</summary>
    public Texture2D? FieldBackground(string backgroundImage) =>
        string.IsNullOrWhiteSpace(backgroundImage) ? null : LoadFromFile(Path.Combine(_fieldsDir, backgroundImage));

    /// <summary>La estrella de Nivel (se dibuja una vez por punto de Nivel), o null si falta el archivo.</summary>
    public Texture2D? LevelStar() => LoadFromFile(_levelStarPath);

    /// <summary>El reverso real de la carta (Bloque 17), o null si falta el archivo (degrada al placeholder generico).</summary>
    public Texture2D? CardBack() => LoadFromFile(_cardBackPath);

    private static string AttributeFileName(MonsterAttribute attribute) => attribute switch
    {
        MonsterAttribute.Dark => "Oscuridad.png",
        MonsterAttribute.Light => "Luz.png",
        MonsterAttribute.Earth => "Tierra.png",
        MonsterAttribute.Fire => "Fuego.png",
        MonsterAttribute.Water => "Agua.png",
        MonsterAttribute.Wind => "Viento.png",
        MonsterAttribute.Divine => "Divinidad.png",
        _ => "Oscuridad.png"
    };

    private Texture2D? LoadFromFile(string path)
    {
        if (_cache.TryGetValue(path, out var cached)) return cached;

        Texture2D? texture = null;
        if (File.Exists(path))
        {
            try
            {
                using var stream = File.OpenRead(path);
                texture = Texture2D.FromStream(_device, stream);
            }
            catch (Exception)
            {
                texture = null; // archivo presente pero ilegible: se degrada al placeholder, no se interrumpe el juego
            }
        }

        _cache[path] = texture;
        return texture;
    }
}
