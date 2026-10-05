using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonstersGame.Core.Entities;

namespace MonstersGame.Graphics;

/// <summary>Iconos disponibles: uno por Atributo de Monstruo, mas Magia y Trampa.</summary>
public enum IconKind
{
    AttributeDark,
    AttributeLight,
    AttributeEarth,
    AttributeFire,
    AttributeWater,
    AttributeWind,
    AttributeDivine,
    Spell,
    Trap
}

/// <summary>
/// Iconos de 7x7 pixeles generados en tiempo de ejecucion, con la misma
/// tecnica que <see cref="BitmapFont"/> (patron de texto -> atlas de
/// textura): sin archivos de arte externos. Da a cada carta una insignia
/// reconocible ademas del color de fondo por Atributo/Tipo.
/// </summary>
public sealed class IconAtlas
{
    public const int Size = 7;

    private readonly Texture2D _atlas;
    private readonly Dictionary<IconKind, int> _columns = new();

    public IconAtlas(GraphicsDevice device)
    {
        var patterns = BuildPatterns();
        var kinds = patterns.Keys.ToList();

        int atlasWidth = Size * kinds.Count;
        var data = new Color[atlasWidth * Size];

        for (int k = 0; k < kinds.Count; k++)
        {
            _columns[kinds[k]] = k;
            string[] pattern = patterns[kinds[k]];
            for (int y = 0; y < Size; y++)
            {
                string row = pattern[y];
                for (int x = 0; x < Size; x++)
                {
                    bool on = x < row.Length && row[x] == '#';
                    int px = k * Size + x;
                    data[y * atlasWidth + px] = on ? Color.White : Color.Transparent;
                }
            }
        }

        _atlas = new Texture2D(device, atlasWidth, Size);
        _atlas.SetData(data);
    }

    /// <summary>Icono correspondiente al atributo de un monstruo.</summary>
    public static IconKind ForAttribute(MonsterAttribute attribute) => attribute switch
    {
        MonsterAttribute.Dark => IconKind.AttributeDark,
        MonsterAttribute.Light => IconKind.AttributeLight,
        MonsterAttribute.Earth => IconKind.AttributeEarth,
        MonsterAttribute.Fire => IconKind.AttributeFire,
        MonsterAttribute.Water => IconKind.AttributeWater,
        MonsterAttribute.Wind => IconKind.AttributeWind,
        MonsterAttribute.Divine => IconKind.AttributeDivine,
        _ => IconKind.AttributeDark
    };

    /// <summary>Dibuja el icono escalado en la posicion indicada (esquina superior izquierda).</summary>
    public void Draw(SpriteBatch sb, IconKind kind, Vector2 position, Color color, int scale = 2)
    {
        if (!_columns.TryGetValue(kind, out int col)) return;
        var src = new Rectangle(col * Size, 0, Size, Size);
        sb.Draw(_atlas, position, src, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    public int PixelSize(int scale) => Size * scale;

    private static Dictionary<IconKind, string[]> BuildPatterns() => new()
    {
        [IconKind.AttributeFire] = new[]
        {
            "...#...",
            "...#...",
            "..###..",
            "..###..",
            ".#####.",
            ".#####.",
            "#######"
        },
        [IconKind.AttributeWater] = new[]
        {
            "...#...",
            "..###..",
            "..###..",
            ".#####.",
            ".#####.",
            ".#####.",
            "..###.."
        },
        [IconKind.AttributeEarth] = new[]
        {
            "...#...",
            "..###..",
            ".#####.",
            "#######",
            ".#####.",
            "..###..",
            "...#..."
        },
        [IconKind.AttributeWind] = new[]
        {
            "..###..",
            ".#...#.",
            "#.....#",
            "#.....#",
            "#.....#",
            ".#...#.",
            "..###.."
        },
        [IconKind.AttributeLight] = new[]
        {
            "...#...",
            ".#.#.#.",
            "..###..",
            "#######",
            "..###..",
            ".#.#.#.",
            "...#..."
        },
        [IconKind.AttributeDark] = new[]
        {
            "..###..",
            ".##....",
            "##.....",
            "##.....",
            "##.....",
            ".##....",
            "..###.."
        },
        [IconKind.AttributeDivine] = new[]
        {
            "...#...",
            "...#...",
            ".#####.",
            ".#####.",
            ".#####.",
            "...#...",
            "...#..."
        },
        [IconKind.Spell] = new[]
        {
            "...#...",
            "..#.#..",
            ".#...#.",
            "#..#..#",
            ".#...#.",
            "..#.#..",
            "...#..."
        },
        [IconKind.Trap] = new[]
        {
            ".#####.",
            ".#...#.",
            ".#.#.#.",
            ".#.#.#.",
            ".#.#.#.",
            ".#...#.",
            ".#####."
        }
    };
}
