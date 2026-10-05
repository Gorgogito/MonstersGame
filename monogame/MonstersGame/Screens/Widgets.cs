using Microsoft.Xna.Framework;
using MonstersGame.Graphics;

namespace MonstersGame.Screens;

/// <summary>
/// Componentes de interfaz reutilizables. La deteccion de clic (<see cref="Clicked"/>)
/// se usa en Update y el dibujo (<see cref="DrawButton"/>) en Draw, respetando la
/// separacion entre logica y render de MonoGame.
/// </summary>
public static class Widgets
{
    /// <summary>Indica si el boton fue pulsado en este fotograma.</summary>
    public static bool Clicked(GameContext ctx, Rectangle rect, bool enabled = true)
        => enabled && ctx.Input.ClickedIn(rect);

    /// <summary>Dibuja un boton con resaltado segun el puntero.</summary>
    public static void DrawButton(GameContext ctx, Rectangle rect, string label, bool enabled = true)
    {
        var sb = ctx.SpriteBatch;
        bool hover = enabled && ctx.Input.IsHovering(rect);

        Color fill = !enabled
            ? new Color(40, 44, 56)
            : hover ? new Color(70, 90, 130) : new Color(48, 58, 84);
        Color border = enabled ? Theme.PanelBorder : new Color(60, 64, 76);
        Color text = enabled ? Theme.TextPrimary : Theme.TextDim;

        ctx.Primitives.Panel(sb, rect, fill, border, 2);
        ctx.Font.DrawCentered(sb, label, rect.Center.X,
            rect.Center.Y - ctx.Font.LineHeight(2) / 2, text, 2);
    }
}
