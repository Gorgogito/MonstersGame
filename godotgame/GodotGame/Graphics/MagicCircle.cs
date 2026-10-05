using Godot;

namespace GodotGame.Graphics;

/// <summary>
/// Circulo de invocacion dibujado por codigo (sin arte): doble anillo, un
/// hexagrama y seis "runas" en los vertices. Se centra en su propia
/// posicion; quien lo usa lo escala, lo gira y lo desvanece con Tween.
/// </summary>
public partial class MagicCircle : Node2D
{
    public float Radius { get; set; } = 60f;
    public Color CircleColor { get; set; } = Colors.White;
    public float LineWidth { get; set; } = 3f;

    public override void _Draw()
    {
        float inner = Radius * 0.82f;
        DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 72, CircleColor, LineWidth, true);
        DrawArc(Vector2.Zero, inner, 0, Mathf.Tau, 72, CircleColor, LineWidth * 0.6f, true);

        // Hexagrama: dos triangulos opuestos inscritos en el anillo interior.
        for (int t = 0; t < 2; t++)
        {
            var points = new Vector2[4];
            for (int i = 0; i < 3; i++)
                points[i] = Vector2.FromAngle(-Mathf.Pi / 2f + t * Mathf.Pi + i * Mathf.Tau / 3f) * inner;
            points[3] = points[0];
            DrawPolyline(points, CircleColor, LineWidth * 0.6f, true);
        }

        for (int i = 0; i < 6; i++)
            DrawCircle(Vector2.FromAngle(i * Mathf.Tau / 6f - Mathf.Pi / 2f) * (Radius * 0.91f), LineWidth * 0.9f, CircleColor);
    }
}
