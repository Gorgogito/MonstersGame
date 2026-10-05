using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonstersGame.Graphics;

/// <summary>
/// Fuente de mapa de bits generada en tiempo de ejecucion. Define cada glifo
/// como un patron de 5x7 pixeles y construye un atlas de textura. Asi el juego
/// muestra texto legible sin usar el Content Pipeline ni fuentes del sistema,
/// garantizando que la solucion compile y se ejecute sin pasos extra.
///
/// Soporta A-Z, 0-9 y signos comunes. Las minusculas se dibujan en mayusculas
/// (estetica de carta) y los caracteres no soportados se omiten.
/// </summary>
public sealed class BitmapFont
{
    public const int GlyphWidth = 5;
    public const int GlyphHeight = 7;
    private const int Spacing = 1; // separacion horizontal entre glifos

    private readonly Texture2D _atlas;
    private readonly Dictionary<char, int> _columns = new();

    public BitmapFont(GraphicsDevice device)
    {
        var glyphs = BuildGlyphTable();
        var chars = glyphs.Keys.ToList();

        int atlasWidth = GlyphWidth * chars.Count;
        var data = new Color[atlasWidth * GlyphHeight];

        for (int c = 0; c < chars.Count; c++)
        {
            _columns[chars[c]] = c;
            string[] pattern = glyphs[chars[c]];
            for (int y = 0; y < GlyphHeight; y++)
            {
                string row = pattern[y];
                for (int x = 0; x < GlyphWidth; x++)
                {
                    bool on = x < row.Length && row[x] == '#';
                    int px = c * GlyphWidth + x;
                    data[y * atlasWidth + px] = on ? Color.White : Color.Transparent;
                }
            }
        }

        _atlas = new Texture2D(device, atlasWidth, GlyphHeight);
        _atlas.SetData(data);
    }

    /// <summary>Ancho en pixeles de un texto al factor de escala dado.</summary>
    public int Measure(string text, int scale)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return text.Length * (GlyphWidth + Spacing) * scale - Spacing * scale;
    }

    /// <summary>Alto en pixeles de una linea al factor de escala dado.</summary>
    public int LineHeight(int scale) => GlyphHeight * scale;

    /// <summary>Dibuja una cadena en la posicion indicada.</summary>
    public void Draw(SpriteBatch sb, string text, Vector2 position, Color color, int scale = 2)
    {
        if (string.IsNullOrEmpty(text)) return;

        float x = position.X;
        foreach (char raw in text)
        {
            char ch = NormalizeChar(raw);
            if (_columns.TryGetValue(ch, out int col))
            {
                var src = new Rectangle(col * GlyphWidth, 0, GlyphWidth, GlyphHeight);
                sb.Draw(_atlas, new Vector2(x, position.Y), src, color, 0f,
                    Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
            x += (GlyphWidth + Spacing) * scale;
        }
    }

    /// <summary>Dibuja texto centrado horizontalmente respecto a <paramref name="centerX"/>.</summary>
    public void DrawCentered(SpriteBatch sb, string text, int centerX, int y, Color color, int scale = 2)
    {
        int width = Measure(text, scale);
        Draw(sb, text, new Vector2(centerX - width / 2f, y), color, scale);
    }

    private static char NormalizeChar(char c)
    {
        if (c is >= 'a' and <= 'z') return (char)(c - 32);
        return c;
    }

    // ------------------------------------------------ Definicion de los glifos

    private static Dictionary<char, string[]> BuildGlyphTable()
    {
        return new Dictionary<char, string[]>
        {
            [' '] = new[] { ".....", ".....", ".....", ".....", ".....", ".....", "....." },
            ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
            ['C'] = new[] { ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###." },
            ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
            ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
            ['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
            ['G'] = new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###." },
            ['H'] = new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['I'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####" },
            ['J'] = new[] { "..###", "...#.", "...#.", "...#.", "#..#.", "#..#.", ".##.." },
            ['K'] = new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
            ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
            ['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
            ['N'] = new[] { "#...#", "##..#", "#.#.#", "#.#.#", "#..##", "#...#", "#...#" },
            ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
            ['Q'] = new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" },
            ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
            ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
            ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
            ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['V'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
            ['W'] = new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#" },
            ['X'] = new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" },
            ['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
            ['Z'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" },
            ['0'] = new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
            ['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
            ['3'] = new[] { "#####", "...#.", "..#..", "...#.", "....#", "#...#", ".###." },
            ['4'] = new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
            ['5'] = new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
            ['6'] = new[] { ".###.", "#....", "#....", "####.", "#...#", "#...#", ".###." },
            ['7'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." },
            ['8'] = new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
            ['9'] = new[] { ".###.", "#...#", "#...#", ".####", "....#", "....#", ".###." },
            ['.'] = new[] { ".....", ".....", ".....", ".....", ".....", "..##.", "..##." },
            [','] = new[] { ".....", ".....", ".....", ".....", "..##.", "..##.", ".#..." },
            [':'] = new[] { ".....", "..##.", "..##.", ".....", "..##.", "..##.", "....." },
            ['/'] = new[] { "....#", "....#", "...#.", "..#..", ".#...", "#....", "#...." },
            ['-'] = new[] { ".....", ".....", ".....", "#####", ".....", ".....", "....." },
            ['!'] = new[] { "..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#.." },
            ['?'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.." },
            ['('] = new[] { "...#.", "..#..", ".#...", ".#...", ".#...", "..#..", "...#." },
            [')'] = new[] { ".#...", "..#..", "...#.", "...#.", "...#.", "..#..", ".#..." },
            ['+'] = new[] { ".....", "..#..", "..#..", "#####", "..#..", "..#..", "....." },
            ['\''] = new[] { "..#..", "..#..", "..#..", ".....", ".....", ".....", "....." }
        };
    }
}
