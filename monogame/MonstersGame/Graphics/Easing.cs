namespace MonstersGame.Graphics;

/// <summary>
/// Funciones de easing puras (0 a 1 -&gt; 0 a 1), reutilizables por cualquier
/// animacion con envolvente por tiempo -- reemplazan las rampas lineales del
/// choque de batalla (Fase 4 del plan de mejoras visuales) y sirven para
/// cualquier otra animacion futura que necesite acelerar/desacelerar en vez
/// de moverse a velocidad constante.
/// </summary>
public static class Easing
{
    /// <summary>Arranca rapido y desacelera hacia el final -- para un movimiento que "llega con fuerza" a su destino.</summary>
    public static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - t, 3f);

    /// <summary>Arranca lento y acelera hacia el final -- para un movimiento/colapso que "se dispara" al terminar.</summary>
    public static float EaseInCubic(float t) => t * t * t;

    /// <summary>Simetrica: lenta en ambos extremos, rapida en el medio.</summary>
    public static float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2f) / 2f;
}
