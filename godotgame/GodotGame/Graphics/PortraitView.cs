using Godot;

namespace GodotGame.Graphics;

/// <summary>
/// Retrato circular de un duelista. Si hay una imagen propia en
/// <c>Data/Art/Portraits/&lt;id&gt;.png</c> se usa esa; si no, se dibuja por
/// codigo un busto estilizado (capa del color del duelista, cabeza, peinado
/// segun <see cref="HairStyle"/> y ojos que brillan con su color). Asi el
/// juego no depende de arte con derechos de terceros y cualquiera puede
/// reemplazar un retrato soltando un archivo.
/// </summary>
public partial class PortraitView : Control
{
    public Color Accent { get; set; } = Colors.White;
    /// <summary>0 corto en punta, 1 largo, 2 melena salvaje, 3 capucha, 4 lacio con flequillo, 5 jugador.</summary>
    public int HairStyle { get; set; }
    public Texture2D? Picture { get; set; }

    private static readonly Color Skin = new(0.93f, 0.8f, 0.68f);

    public static PortraitView Create(string portraitId, Color accent, int hairStyle, float size)
    {
        var view = new PortraitView
        {
            Accent = accent,
            HairStyle = hairStyle,
            CustomMinimumSize = new Vector2(size, size),
            Size = new Vector2(size, size),
            MouseFilter = MouseFilterEnum.Ignore
        };
        var cache = new TextureCache(ProjectSettings.GlobalizePath("res://Data/Art"));
        view.Picture = cache.Portrait(portraitId);
        return view;
    }

    public override void _Draw()
    {
        float r = Mathf.Min(Size.X, Size.Y) / 2f;
        var c = Size / 2f;

        // Fondo: degradado radial falso (circulos concentricos).
        for (int i = 10; i >= 1; i--)
        {
            float t = i / 10f;
            DrawCircle(c, r * t, Accent.Darkened(0.55f).Lerp(Accent.Darkened(0.85f), t));
        }

        if (Picture != null)
        {
            float side = r * 1.7f;
            DrawTextureRect(Picture, new Rect2(c - new Vector2(side, side) / 2f, new Vector2(side, side)), false);
        }
        else
        {
            DrawBust(c, r);
        }

        DrawArc(c, r - 2f, 0, Mathf.Tau, 64, Accent.Lerp(Colors.White, 0.25f), 4f, true);
        DrawArc(c, r - 7f, 0, Mathf.Tau, 64, new Color(0, 0, 0, 0.5f), 2f, true);
    }

    private void DrawBust(Vector2 c, float r)
    {
        var cloak = Accent.Darkened(0.25f);
        var hair = HairStyle switch
        {
            0 => new Color(0.75f, 0.55f, 0.25f),
            1 => new Color(0.55f, 0.18f, 0.12f),
            2 => new Color(0.9f, 0.4f, 0.12f),
            3 => new Color(0.85f, 0.7f, 0.3f),
            4 => new Color(0.2f, 0.12f, 0.3f),
            _ => new Color(0.3f, 0.2f, 0.12f)
        };
        var head = c + new Vector2(0, -r * 0.12f);
        float headR = r * 0.3f;

        // Pelo largo detras de la cabeza (estilos 1 y 4).
        if (HairStyle is 1 or 4)
            DrawColoredPolygon(new[]
            {
                head + new Vector2(-headR * 1.15f, -headR * 0.3f), head + new Vector2(headR * 1.15f, -headR * 0.3f),
                head + new Vector2(headR * 1.3f, headR * 2.2f), head + new Vector2(-headR * 1.3f, headR * 2.2f)
            }, hair.Darkened(0.15f));

        // Hombros / capa.
        var shoulders = new Vector2[20];
        for (int i = 0; i < shoulders.Length; i++)
        {
            float a = Mathf.Pi + i / (float)(shoulders.Length - 1) * Mathf.Pi;
            shoulders[i] = c + new Vector2(Mathf.Cos(a) * r * 0.78f, r * 0.95f + Mathf.Sin(a) * r * 0.55f);
        }
        DrawColoredPolygon(shoulders, cloak);
        DrawColoredPolygon(new[] { c + new Vector2(-r * 0.2f, r * 0.42f), c + new Vector2(r * 0.2f, r * 0.42f), c + new Vector2(0, r * 0.72f) },
            Accent.Lerp(Colors.White, 0.35f));

        DrawRect(new Rect2(head + new Vector2(-headR * 0.35f, headR * 0.6f), new Vector2(headR * 0.7f, headR * 0.8f)), Skin.Darkened(0.12f));
        DrawCircle(head, headR, Skin);

        // Peinado.
        switch (HairStyle)
        {
            case 0: case 5:
                for (int i = -2; i <= 2; i++)
                    DrawColoredPolygon(new[]
                    {
                        head + new Vector2(i * headR * 0.42f - headR * 0.3f, -headR * 0.45f),
                        head + new Vector2(i * headR * 0.42f + headR * 0.3f, -headR * 0.45f),
                        head + new Vector2(i * headR * 0.55f, -headR * (HairStyle == 5 ? 1.75f : 1.45f))
                    }, hair);
                DrawCircle(head + new Vector2(0, -headR * 0.55f), headR * 0.75f, hair);
                break;
            case 1: case 4:
            {
                // Casquete: solo la mitad superior (no tapa la cara), mas mechones a los costados.
                var cap = new Vector2[18];
                for (int i = 0; i < cap.Length; i++)
                {
                    float a = Mathf.Pi + i / (float)(cap.Length - 1) * Mathf.Pi;
                    cap[i] = head + new Vector2(Mathf.Cos(a) * headR * 1.08f, -headR * 0.15f + Mathf.Sin(a) * headR * 1.05f);
                }
                DrawColoredPolygon(cap, hair);
                foreach (float side in new[] { -1f, 1f })
                    DrawColoredPolygon(new[]
                    {
                        head + new Vector2(side * headR * 0.82f, -headR * 0.2f), head + new Vector2(side * headR * 1.08f, -headR * 0.2f),
                        head + new Vector2(side * headR * 1.2f, headR * 1.4f), head + new Vector2(side * headR * 0.85f, headR * 1.2f)
                    }, hair);
                if (HairStyle == 4) // flequillo recto
                    DrawRect(new Rect2(head + new Vector2(-headR * 0.85f, -headR * 0.45f), new Vector2(headR * 1.7f, headR * 0.28f)), hair);
                break;
            }
            case 2:
                for (int i = 0; i < 9; i++)
                {
                    float a = Mathf.Pi + i / 8f * Mathf.Pi;
                    var dir = Vector2.FromAngle(a);
                    DrawColoredPolygon(new[]
                    {
                        head + dir.Rotated(-0.35f) * headR * 0.8f, head + dir.Rotated(0.35f) * headR * 0.8f, head + dir * headR * 1.7f
                    }, hair);
                }
                DrawCircle(head + new Vector2(0, -headR * 0.5f), headR * 0.8f, hair);
                break;
            case 3:
                DrawColoredPolygon(new[]
                {
                    head + new Vector2(-headR * 1.25f, headR * 0.9f), head + new Vector2(-headR * 1.15f, -headR * 0.6f),
                    head + new Vector2(0, -headR * 1.6f), head + new Vector2(headR * 1.15f, -headR * 0.6f),
                    head + new Vector2(headR * 1.25f, headR * 0.9f), head + new Vector2(headR * 0.85f, headR * 0.9f),
                    head + new Vector2(headR * 0.85f, -headR * 0.2f), head + new Vector2(-headR * 0.85f, -headR * 0.2f),
                    head + new Vector2(-headR * 0.85f, headR * 0.9f)
                }, hair);
                DrawCircle(head + new Vector2(0, -headR * 0.95f), headR * 0.16f, Accent.Lerp(Colors.White, 0.4f));
                break;
        }

        // Ojos que brillan con el color del duelista.
        var glow = Accent.Lerp(Colors.White, 0.45f);
        foreach (float side in new[] { -1f, 1f })
        {
            var eye = head + new Vector2(side * headR * 0.38f, headR * 0.05f);
            DrawCircle(eye, headR * 0.17f, new Color(1, 1, 1));
            DrawCircle(eye, headR * 0.11f, glow);
            DrawCircle(eye, headR * 0.05f, Colors.Black);
            DrawLine(eye + new Vector2(-headR * 0.2f, -headR * 0.22f), eye + new Vector2(headR * 0.2f, -headR * (side < 0 ? 0.28f : 0.16f)), hair.Darkened(0.3f), 2f, true);
        }
        DrawArc(head + new Vector2(0, headR * 0.45f), headR * 0.25f, 0.3f, Mathf.Pi - 0.3f, 12, new Color(0.55f, 0.25f, 0.2f), 2f, true);
    }
}
