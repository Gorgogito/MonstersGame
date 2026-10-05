using System.Globalization;
using Microsoft.Xna.Framework;
using MonstersGame.Core.Entities;

namespace MonstersGame.Graphics;

/// <summary>Paleta de colores centralizada para una apariencia consistente.</summary>
public static class Theme
{
    public static readonly Color Background = new(18, 22, 34);
    public static readonly Color PanelFill = new(30, 36, 54);
    public static readonly Color PanelBorder = new(90, 110, 150);
    public static readonly Color Highlight = new(255, 210, 80);
    public static readonly Color TextPrimary = new(235, 238, 245);
    public static readonly Color TextDim = new(150, 160, 180);
    public static readonly Color CardBack = new(60, 40, 80);
    public static readonly Color PlayerAccent = new(80, 160, 255);
    public static readonly Color CpuAccent = new(255, 100, 100);
    public static readonly Color Danger = new(220, 70, 70);
    public static readonly Color Good = new(90, 200, 120);
    public static readonly Color SpellColor = new(50, 110, 95);
    public static readonly Color TrapColor = new(120, 55, 95);

    /// <summary>Aclara un color hacia blanco (0 = sin cambio, 1 = blanco puro). Usado para degradados.</summary>
    public static Color Lighten(Color color, float amount) => Color.Lerp(color, Color.White, amount);

    /// <summary>Oscurece un color hacia negro (0 = sin cambio, 1 = negro puro). Usado para degradados.</summary>
    public static Color Darken(Color color, float amount) => Color.Lerp(color, Color.Black, amount);

    /// <summary>
    /// Interpreta un color en formato <c>#RRGGBB</c> (el que edita <c>FieldTypeEditorForm</c>),
    /// aceptando o no el <c>#</c> inicial. Devuelve false para vacio/formato
    /// invalido en vez de lanzar -- una carta de Campo con un color mal
    /// escrito no debe romper el dibujado, solo degradar al color por defecto
    /// del llamador (ver <see cref="CardRenderer.DrawFieldZone"/>).
    /// </summary>
    public static bool TryParseHexColor(string? hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex)) return false;

        string s = hex.Trim();
        if (s.StartsWith('#')) s = s[1..];
        if (s.Length != 6) return false;

        if (!byte.TryParse(s.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r)) return false;
        if (!byte.TryParse(s.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g)) return false;
        if (!byte.TryParse(s.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b)) return false;

        color = new Color(r, g, b);
        return true;
    }

    /// <summary>Color de fondo de una carta segun su atributo de monstruo.</summary>
    public static Color AttributeColor(MonsterAttribute attribute) => attribute switch
    {
        MonsterAttribute.Dark => new Color(70, 55, 95),
        MonsterAttribute.Light => new Color(150, 140, 90),
        MonsterAttribute.Earth => new Color(95, 80, 55),
        MonsterAttribute.Fire => new Color(140, 60, 50),
        MonsterAttribute.Water => new Color(50, 90, 130),
        MonsterAttribute.Wind => new Color(60, 110, 80),
        MonsterAttribute.Divine => new Color(150, 120, 60),
        _ => new Color(70, 70, 80)
    };
}
