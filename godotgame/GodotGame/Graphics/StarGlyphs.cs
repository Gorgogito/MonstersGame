using Godot;
using GodotGame.Core.Entities;

namespace GodotGame.Graphics;

/// <summary>
/// Simbolo astronomico y color de cada Estrella Guardiana, para las
/// insignias del tablero, el panel de detalle, el selector y la batalla.
/// </summary>
public static class StarGlyphs
{
    /// <summary>Fuente con los simbolos astronomicos (Segoe UI Symbol en Windows; Godot cae a otra si no esta).</summary>
    public static readonly Font Font = new SystemFont
    {
        FontNames = new[] { "Segoe UI Symbol", "Segoe UI", "DejaVu Sans", "Noto Sans Symbols2", "Noto Sans Symbols", "sans-serif" },
        FontWeight = 700
    };

    public static string Symbol(GuardianStar star) => star switch
    {
        GuardianStar.Sun => "☉",
        GuardianStar.Moon => "☽",
        GuardianStar.Venus => "♀",
        GuardianStar.Mercury => "☿",
        GuardianStar.Mars => "♂",
        GuardianStar.Jupiter => "♃",
        GuardianStar.Saturn => "♄",
        GuardianStar.Uranus => "♅",
        GuardianStar.Pluto => "♇",
        GuardianStar.Neptune => "♆",
        _ => "?"
    };

    public static Color Tint(GuardianStar star) => star switch
    {
        GuardianStar.Sun => new Color(1f, 0.82f, 0.25f),
        GuardianStar.Moon => new Color(0.78f, 0.84f, 1f),
        GuardianStar.Venus => new Color(1f, 0.6f, 0.8f),
        GuardianStar.Mercury => new Color(0.45f, 0.9f, 0.85f),
        GuardianStar.Mars => new Color(1f, 0.4f, 0.32f),
        GuardianStar.Jupiter => new Color(1f, 0.65f, 0.3f),
        GuardianStar.Saturn => new Color(0.9f, 0.8f, 0.5f),
        GuardianStar.Uranus => new Color(0.5f, 0.95f, 1f),
        GuardianStar.Pluto => new Color(0.75f, 0.55f, 1f),
        GuardianStar.Neptune => new Color(0.4f, 0.6f, 1f),
        _ => Colors.White
    };

    /// <summary>Simbolo coloreado en BBCode (para RichTextLabel).</summary>
    public static string Bbcode(GuardianStar star) => $"[color=#{Tint(star).ToHtml(false)}]{Symbol(star)}[/color]";

    /// <summary>Label con el simbolo, su color y contorno oscuro.</summary>
    public static Label MakeLabel(GuardianStar star, int size)
    {
        var label = new Label
        {
            Text = Symbol(star),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Style(label, star, size);
        return label;
    }

    public static void Style(Label label, GuardianStar star, int size)
    {
        label.Text = Symbol(star);
        label.AddThemeFontOverride("font", Font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", Tint(star));
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        label.AddThemeConstantOverride("outline_size", Mathf.Max(3, size / 5));
    }
}
