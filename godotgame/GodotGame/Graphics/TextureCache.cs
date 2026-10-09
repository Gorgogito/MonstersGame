using Godot;
using GodotGame.Core.Entities;
using GodotGame.Data;

namespace GodotGame.Graphics;

/// <summary>
/// Carga bajo demanda y cachea las texturas reales de arte: imagen por
/// carta, insignia por Atributo. Adaptacion Godot de <c>TextureCache</c> de
/// MonoGame -- ahi se cargaba con <c>Texture2D.FromStream</c> sobre el
/// GraphicsDevice de XNA; aca con <see cref="Image.Load"/> + <see cref="ImageTexture"/>,
/// sin ningun paso de import previo (misma filosofia: arte externo, sin
/// Content Pipeline). Si un archivo falta o esta corrupto, se cachea
/// <c>null</c> para esa clave y el boton de la carta sigue mostrando su
/// texto -- una carta sin imagen nunca debe romper el juego.
/// </summary>
public sealed class TextureCache
{
    private readonly string _cardsDir;
    private readonly string _attributesDir;
    private readonly string _levelStarPath;
    private readonly Dictionary<string, Texture2D?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public TextureCache(string artRoot)
    {
        _cardsDir = Path.Combine(artRoot, "Cards");
        _attributesDir = Path.Combine(artRoot, "Attributes");
        _levelStarPath = Path.Combine(artRoot, "Level", "level.png");
    }

    /// <summary>Textura de arte de una carta a partir de su nombre de archivo (<c>Card.Image</c>), o null si no hay archivo (carta sin imagen, o Image vacio).</summary>
    public Texture2D? CardArt(string image) =>
        string.IsNullOrWhiteSpace(image) ? null : LoadFromFile(ArtFiles.Resolve(_cardsDir, image));

    /// <summary>Insignia del Atributo de un Monstruo, o null si no hay archivo para ese Atributo.</summary>
    public Texture2D? AttributeIcon(MonsterAttribute attribute) =>
        LoadFromFile(Path.Combine(_attributesDir, AttributeFileName(attribute)));

    /// <summary>Retrato propio de un duelista (<c>Portraits/&lt;id&gt;.png</c> o <c>.jpg</c>), o null si no hay (se dibuja uno por codigo).</summary>
    public Texture2D? Portrait(string id)
    {
        string dir = Path.Combine(Path.GetDirectoryName(_cardsDir)!, "Portraits");
        string png = Path.Combine(dir, id + ".png");
        return File.Exists(png) ? LoadFromFile(png) : LoadFromFile(Path.Combine(dir, id + ".jpg"));
    }

    /// <summary>La estrella de Nivel (se dibuja una vez por punto de Nivel), o null si falta el archivo.</summary>
    public Texture2D? LevelStar() => LoadFromFile(_levelStarPath);

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
            var image = LoadByContent(path);
            if (image != null)
                texture = ImageTexture.CreateFromImage(image);
        }

        _cache[path] = texture;
        return texture;
    }

    /// <summary>
    /// Decodifica segun el formato REAL del archivo (su firma), no segun la
    /// extension: <see cref="Image.Load"/> elige el decodificador por la
    /// extension, y un arte guardado con la extension equivocada (ej. un WebP
    /// llamado ".png") fallaria aunque el archivo este sano.
    /// </summary>
    private static Image? LoadByContent(string path)
    {
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (IOException) { return null; }

        var image = new Image();
        Error result;
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == (byte)'P' && bytes[2] == (byte)'N' && bytes[3] == (byte)'G')
            result = image.LoadPngFromBuffer(bytes);
        else if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            result = image.LoadJpgFromBuffer(bytes);
        else if (bytes.Length >= 12 && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
                 && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
            result = image.LoadWebpFromBuffer(bytes);
        else
            result = image.Load(path);
        return result == Error.Ok ? image : null;
    }
}
