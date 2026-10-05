using Godot;

namespace GodotGame;

/// <summary>
/// Muestra el tablero inclinado en perspectiva, como el campo de Forbidden
/// Memories. El tablero (las franjas de Zonas de ambos jugadores) se dibuja
/// plano dentro de un <see cref="SubViewport"/> hijo, y este control lo
/// muestra deformado en un trapecio (mas angosto y comprimido arriba, en el
/// lado de la CPU) con una homografia en un shader.
///
/// Como la imagen esta deformada, el input no puede llegar solo al
/// SubViewport: cada evento de mouse se lleva al espacio del tablero plano con
/// la homografia inversa y se le empuja con <see cref="Viewport.PushInput"/>.
/// <see cref="ProjectToScreen"/> hace el camino contrario, para los efectos
/// que viajan entre el tablero y el resto de la pantalla.
/// </summary>
public partial class PerspectiveFieldView : TextureRect
{
    /// <summary>Cuanto se angosta el borde de arriba, de cada lado (fraccion del ancho).</summary>
    [Export] public float TopInset { get; set; } = 0.11f;

    /// <summary>Alto con el que se dibuja el tablero plano; el control lo muestra mas bajo (la inclinacion lo comprime).</summary>
    [Export] public float FlatHeight { get; set; } = 600f;

    private const string ShaderCode = """
        shader_type canvas_item;

        // Homografia inversa: de coordenadas normalizadas de este control
        // (UV) a coordenadas normalizadas del tablero plano (SubViewport).
        uniform vec3 row0;
        uniform vec3 row1;
        uniform vec3 row2;
        uniform float far_shade = 0.78;

        void fragment() {
            vec3 p = vec3(UV, 1.0);
            float w = dot(row2, p);
            vec2 src = vec2(dot(row0, p), dot(row1, p)) / w;
            if (src.x < 0.0 || src.x > 1.0 || src.y < 0.0 || src.y > 1.0) {
                COLOR = vec4(0.0);
            } else {
                vec4 c = texture(TEXTURE, src);
                // El fondo (arriba) un poco mas oscuro: da sensacion de profundidad.
                c.rgb *= mix(far_shade, 1.0, src.y);
                COLOR = c;
            }
        }
        """;

    private SubViewport _viewport = null!;
    private Control _content = null!;
    private ShaderMaterial _material = null!;

    /// <summary>Homografia directa (tablero plano normalizado -> este control normalizado), fila por fila.</summary>
    private Vector3 _h0, _h1, _h2;
    /// <summary>Homografia inversa (este control -> tablero plano).</summary>
    private Vector3 _i0, _i1, _i2;

    /// <summary>Capa dentro del SubViewport, en el origen, para efectos que deben verse en perspectiva junto con el tablero.</summary>
    public Control EffectsLayer { get; private set; } = null!;

    public override void _Ready()
    {
        _viewport = GetNode<SubViewport>("FieldViewport");
        _content = GetNode<Control>("FieldViewport/FieldRoot");
        _viewport.TransparentBg = true;
        _viewport.HandleInputLocally = true;
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;

        EffectsLayer = new Control { MouseFilter = MouseFilterEnum.Ignore, ZIndex = 20 };
        _viewport.AddChild(EffectsLayer);

        Texture = _viewport.GetTexture();
        ExpandMode = ExpandModeEnum.IgnoreSize;
        StretchMode = StretchModeEnum.Scale;
        TextureFilter = TextureFilterEnum.Linear;
        MouseFilter = MouseFilterEnum.Stop;

        _material = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
        Material = _material;

        Resized += SyncViewportSize;
        SyncViewportSize();
        ComputeHomography();
    }

    private void SyncViewportSize()
    {
        if (_viewport == null || Size.X <= 0) return;
        var flat = new Vector2I(Mathf.RoundToInt(Size.X), Mathf.RoundToInt(FlatHeight));
        _viewport.Size = flat;
        _content.Size = flat;
    }

    /// <summary>
    /// Homografia del cuadrado unidad al trapecio (Heckbert, "square to
    /// quad"): (0,0)->(t,0), (1,0)->(1-t,0), (1,1)->(1,1), (0,1)->(0,1).
    /// </summary>
    private void ComputeHomography()
    {
        float t = TopInset;
        float x0 = t, y0 = 0, x1 = 1 - t, y1 = 0, x2 = 1, y2 = 1, x3 = 0, y3 = 1;
        float dx1 = x1 - x2, dx2 = x3 - x2, dx3 = x0 - x1 + x2 - x3;
        float dy1 = y1 - y2, dy2 = y3 - y2, dy3 = y0 - y1 + y2 - y3;
        float den = dx1 * dy2 - dx2 * dy1;
        float g = (dx3 * dy2 - dx2 * dy3) / den;
        float h = (dx1 * dy3 - dx3 * dy1) / den;
        float a = x1 - x0 + g * x1, b = x3 - x0 + h * x3, c = x0;
        float d = y1 - y0 + g * y1, e = y3 - y0 + h * y3, f = y0;

        _h0 = new Vector3(a, b, c);
        _h1 = new Vector3(d, e, f);
        _h2 = new Vector3(g, h, 1);

        // Inversa por adjunta (la escala no importa: es una homografia).
        _i0 = new Vector3(e - f * h, c * h - b, b * f - c * e);
        _i1 = new Vector3(f * g - d, a - c * g, c * d - a * f);
        _i2 = new Vector3(d * h - e * g, b * g - a * h, a * e - b * d);

        _material.SetShaderParameter("row0", _i0);
        _material.SetShaderParameter("row1", _i1);
        _material.SetShaderParameter("row2", _i2);
    }

    private static Vector2 Apply(Vector3 r0, Vector3 r1, Vector3 r2, Vector2 p)
    {
        var v = new Vector3(p.X, p.Y, 1);
        float w = r2.Dot(v);
        return new Vector2(r0.Dot(v), r1.Dot(v)) / w;
    }

    private Vector2 FlatSize => new(_viewport.Size.X, _viewport.Size.Y);

    /// <summary>Punto del tablero plano (coordenadas del SubViewport) -> punto en pantalla (coordenadas globales del lienzo principal).</summary>
    public Vector2 ProjectToScreen(Vector2 flatPoint)
    {
        var normalized = Apply(_h0, _h1, _h2, flatPoint / FlatSize);
        return GlobalPosition + normalized * Size;
    }

    /// <summary>Escala aproximada con la que se ve el tablero alrededor de <paramref name="flatPoint"/> (menor arriba, en el fondo).</summary>
    public float ScaleAt(Vector2 flatPoint)
    {
        const float step = 20f;
        return ProjectToScreen(flatPoint + new Vector2(step, 0)).DistanceTo(ProjectToScreen(flatPoint)) / step;
    }

    /// <summary>Punto de este control (coordenadas locales) -> punto del tablero plano, o null si cae fuera del trapecio.</summary>
    private Vector2? ToFlat(Vector2 localPoint)
    {
        if (Size.X <= 0 || Size.Y <= 0) return null;
        var src = Apply(_i0, _i1, _i2, localPoint / Size);
        if (src.X < 0 || src.X > 1 || src.Y < 0 || src.Y > 1) return null;
        return src * FlatSize;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouse mouse) return;
        var flat = ToFlat(mouse.Position);
        // Fuera del trapecio: se manda "lejos" para que los botones pierdan el hover.
        var forwarded = (InputEventMouse)mouse.Duplicate();
        forwarded.Position = flat ?? new Vector2(-10000, -10000);
        forwarded.GlobalPosition = forwarded.Position;
        _viewport.PushInput(forwarded, true);
        if (flat != null) AcceptEvent();
    }

    public override void _Notification(int what)
    {
        // Al salir el mouse del control, los botones del tablero tambien pierden el hover.
        if (what == NotificationMouseExit && _viewport != null && _viewport.IsInsideTree())
            _viewport.PushInput(new InputEventMouseMotion { Position = new Vector2(-10000, -10000) }, true);
    }
}
