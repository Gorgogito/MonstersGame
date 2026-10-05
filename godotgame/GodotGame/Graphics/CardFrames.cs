using Godot;
using GodotGame.Core.Entities;
using System.Collections.Generic;

namespace GodotGame.Graphics;

/// <summary>
/// Paleta y estilos del marco de carta "clasico" (el de las cartas reales y
/// de Forbidden Memories): el color del marco dice que tipo de carta es
/// (Normal tostado, Efecto naranja, Fusion violeta, Ritual azul, Magia verde
/// azulado, Trampa magenta), con barra de nombre, arte enmarcado y franja de
/// pergamino para ATK/DEF. Lo comparten el tablero, la pantalla de batalla,
/// el panel de detalle y los efectos de salida, asi una carta se ve igual en
/// todos lados.
/// </summary>
public static class CardFrames
{
    public static readonly Color Normal = new(0.78f, 0.62f, 0.33f);
    public static readonly Color Effect = new(0.80f, 0.47f, 0.22f);
    public static readonly Color Fusion = new(0.52f, 0.35f, 0.62f);
    public static readonly Color Ritual = new(0.33f, 0.50f, 0.78f);
    public static readonly Color Spell = new(0.12f, 0.55f, 0.48f);
    public static readonly Color Trap = new(0.68f, 0.24f, 0.50f);
    public static readonly Color FaceDown = new(0.42f, 0.27f, 0.14f);
    public static readonly Color Empty = new(0.16f, 0.17f, 0.2f);

    /// <summary>Fondo de la franja inferior (ATK/DEF, tipo) y su borde.</summary>
    public static readonly Color Parchment = new(0.93f, 0.88f, 0.74f);
    public static readonly Color ParchmentBorder = new(0.45f, 0.36f, 0.22f);

    /// <summary>Texto sobre pergamino: normal, y valores subidos/bajados por un modificador (verde/rojo oscuros, legibles sobre fondo claro).</summary>
    public const string InkHex = "#2a1d0e";
    public const string InkUpHex = "#1b7a2e";
    public const string InkDownHex = "#b02a1e";

    private static readonly string[] SerifFonts = { "Palatino Linotype", "Book Antiqua", "Georgia", "Times New Roman", "serif" };

    /// <summary>Tipografia con serifa en negrita, la del nombre y los ATK/DEF de las cartas reales (fuente del sistema; Godot cae a la que haya).</summary>
    public static readonly Font SerifBold = new SystemFont { FontNames = SerifFonts, FontWeight = 700 };

    public static Color FrameColor(Card card) => card switch
    {
        MonsterCard { Category: MonsterCategory.Effect } => Effect,
        MonsterCard { Category: MonsterCategory.Fusion } => Fusion,
        MonsterCard { Category: MonsterCategory.Ritual } => Ritual,
        MonsterCard => Normal,
        _ => card.Kind == CardKind.Spell ? Spell : Trap
    };

    /// <summary>Como en las cartas reales: nombre negro en Normal/Efecto/Ritual, blanco en Fusion/Magia/Trampa.</summary>
    public static bool HasLightName(Card card) =>
        card is MonsterCard monster ? monster.Category == MonsterCategory.Fusion : true;

    /// <summary>Color de un ATK/DEF sobre pergamino segun si un modificador lo cambio del valor impreso.</summary>
    public static string StatInkHex(int effective, int printed) =>
        effective > printed ? InkUpHex : effective < printed ? InkDownHex : InkHex;

    /// <summary>"ATK/2600  DEF/2100" en BBCode, con el valor que esta en uso (segun la posicion) en negrita.</summary>
    public static string StatsBbcode(int atk, int printedAtk, int def, int printedDef, bool? defending = null)
    {
        string a = $"[color={StatInkHex(atk, printedAtk)}]ATK/{atk}[/color]";
        string d = $"[color={StatInkHex(def, printedDef)}]DEF/{def}[/color]";
        if (defending == true) d = $"[u]{d}[/u]";
        else if (defending == false) a = $"[u]{a}[/u]";
        return $"{a} {d}";
    }

    // ---------------------------------------------------------------- Estilos

    private static readonly Dictionary<(string, Color), StyleBoxFlat> Cache = new();

    /// <summary>Cuerpo de la carta: el color del marco a pleno, con borde oscuro y esquinas apenas redondeadas.</summary>
    public static StyleBoxFlat Body(Color frame, int border = 2, int corner = 5) => Cached($"body{border}{corner}", frame, () => new StyleBoxFlat
    {
        BgColor = frame,
        BorderColor = frame.Darkened(0.55f),
        BorderWidthLeft = border,
        BorderWidthRight = border,
        BorderWidthTop = border,
        BorderWidthBottom = border,
        CornerRadiusTopLeft = corner,
        CornerRadiusTopRight = corner,
        CornerRadiusBottomLeft = corner,
        CornerRadiusBottomRight = corner
    });

    /// <summary>Como <see cref="Body"/>, con un aura (sombra de color) alrededor: monstruo potenciado o debilitado por un terreno/Equipo.</summary>
    public static StyleBoxFlat GlowBody(Color frame, Color glow) => Cached($"glow{glow.ToHtml()}", frame, () =>
    {
        var style = (StyleBoxFlat)Body(frame).Duplicate();
        style.ShadowColor = glow;
        style.ShadowSize = 7;
        return style;
    });

    /// <summary>Zona vacia: un hueco oscuro con borde tenue (no es una carta).</summary>
    public static StyleBoxFlat EmptySlot() => Cached("empty", Empty, () => new StyleBoxFlat
    {
        BgColor = Empty.Lerp(Colors.Black, 0.55f),
        BorderColor = Empty.Lerp(Colors.White, 0.15f),
        BorderWidthLeft = 3,
        BorderWidthRight = 3,
        BorderWidthTop = 3,
        BorderWidthBottom = 3,
        CornerRadiusTopLeft = 8,
        CornerRadiusTopRight = 8,
        CornerRadiusBottomLeft = 8,
        CornerRadiusBottomRight = 8
    });

    /// <summary>Barra del nombre: un tono mas claro que el marco, con un filo oscuro (el "relieve" de la carta real).</summary>
    public static StyleBoxFlat NameBar(Color frame) => Cached("name", frame, () => new StyleBoxFlat
    {
        BgColor = frame.Lightened(0.22f),
        BorderColor = frame.Darkened(0.5f),
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 2,
        CornerRadiusTopRight = 2,
        CornerRadiusBottomLeft = 2,
        CornerRadiusBottomRight = 2,
        ContentMarginLeft = 3,
        ContentMarginRight = 2
    });

    /// <summary>Marco del arte: borde oscuro fino sobre fondo negro.</summary>
    public static StyleBoxFlat ArtFrame(Color frame) => Cached("art", frame, () => new StyleBoxFlat
    {
        BgColor = Colors.Black,
        BorderColor = frame.Darkened(0.65f),
        BorderWidthLeft = 2,
        BorderWidthRight = 2,
        BorderWidthTop = 2,
        BorderWidthBottom = 2,
        ContentMarginLeft = 2,
        ContentMarginRight = 2,
        ContentMarginTop = 2,
        ContentMarginBottom = 2
    });

    /// <summary>Franja de pergamino inferior.</summary>
    public static StyleBoxFlat ParchmentBox() => Cached("parchment", Parchment, () => new StyleBoxFlat
    {
        BgColor = Parchment,
        BorderColor = ParchmentBorder,
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 2,
        CornerRadiusTopRight = 2,
        CornerRadiusBottomLeft = 2,
        CornerRadiusBottomRight = 2,
        ContentMarginLeft = 2,
        ContentMarginRight = 2
    });

    /// <summary>Aplica la tipografia y el color de nombre de <paramref name="card"/> a un Label (blanco con contorno, o negro sin contorno).</summary>
    public static void StyleName(Label label, Card card)
    {
        label.AddThemeFontOverride("font", SerifBold);
        bool light = HasLightName(card);
        label.AddThemeColorOverride("font_color", light ? Colors.White : new Color(0.08f, 0.06f, 0.04f));
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        label.AddThemeConstantOverride("outline_size", light ? 3 : 0);
    }

    private static StyleBoxFlat Cached(string kind, Color color, System.Func<StyleBoxFlat> create)
    {
        if (!Cache.TryGetValue((kind, color), out var style))
        {
            style = create();
            Cache[(kind, color)] = style;
        }
        return style;
    }
}
