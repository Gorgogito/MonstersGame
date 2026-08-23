using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonstersGame.Graphics;

/// <summary>
/// Utilidades de dibujo de formas simples usando una textura de 1x1 pixel.
/// Evita depender del Content Pipeline para placeholders graficos.
/// </summary>
public sealed class Primitives
{
    private readonly Texture2D _pixel;

    public Primitives(GraphicsDevice device)
    {
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    /// <summary>Textura blanca de 1x1 reutilizable.</summary>
    public Texture2D Pixel => _pixel;

    /// <summary>Dibuja un rectangulo relleno.</summary>
    public void FillRect(SpriteBatch sb, Rectangle rect, Color color)
        => sb.Draw(_pixel, rect, color);

    /// <summary>Dibuja un rectangulo relleno con coordenadas sueltas.</summary>
    public void FillRect(SpriteBatch sb, int x, int y, int w, int h, Color color)
        => sb.Draw(_pixel, new Rectangle(x, y, w, h), color);

    /// <summary>Dibuja un rectangulo relleno translucido, usado para destellos/flashes cortos.</summary>
    public void FillRectAlpha(SpriteBatch sb, Rectangle rect, Color color, float alpha01)
    {
        byte a = (byte)Math.Clamp(alpha01 * 255f, 0, 255);
        if (a == 0) return;
        FillRect(sb, rect, new Color(color.R, color.G, color.B, a));
    }

    /// <summary>Dibuja el borde de un rectangulo con el grosor indicado.</summary>
    public void DrawBorder(SpriteBatch sb, Rectangle rect, int thickness, Color color)
    {
        FillRect(sb, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);                          // arriba
        FillRect(sb, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);          // abajo
        FillRect(sb, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);                          // izquierda
        FillRect(sb, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);          // derecha
    }

    /// <summary>Dibuja un panel: relleno + borde.</summary>
    public void Panel(SpriteBatch sb, Rectangle rect, Color fill, Color border, int thickness = 2)
    {
        FillRect(sb, rect, fill);
        DrawBorder(sb, rect, thickness, border);
    }

    /// <summary>
    /// Rellena un rectangulo con un degradado vertical (de <paramref name="top"/>
    /// a <paramref name="bottom"/>), dibujado como franjas horizontales de 2px.
    /// Sigue sin depender de texturas externas: son rectangulos de 1x1 de
    /// siempre, solo que con un color distinto por franja.
    /// </summary>
    public void FillGradientVertical(SpriteBatch sb, Rectangle rect, Color top, Color bottom)
    {
        const int stripHeight = 2;
        int strips = Math.Max(1, rect.Height / stripHeight);
        for (int i = 0; i < strips; i++)
        {
            float t = strips <= 1 ? 0f : i / (float)(strips - 1);
            var color = Color.Lerp(top, bottom, t);
            int y = rect.Y + i * stripHeight;
            int h = (i == strips - 1) ? rect.Bottom - y : stripHeight;
            FillRect(sb, new Rectangle(rect.X, y, rect.Width, h), color);
        }
    }

    /// <summary>Dibuja un panel con relleno degradado (de <paramref name="top"/> a <paramref name="bottom"/>) y borde solido.</summary>
    public void GradientPanel(SpriteBatch sb, Rectangle rect, Color top, Color bottom, Color border, int thickness = 2)
    {
        FillGradientVertical(sb, rect, top, bottom);
        DrawBorder(sb, rect, thickness, border);
    }
}
