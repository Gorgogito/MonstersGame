using Microsoft.Xna.Framework;

namespace MonstersGame.Graphics;

/// <summary>
/// Acercamiento de camara de proposito unico (Fase B del plan de
/// adaptaciones de esfuerzo medio): un zoom breve alrededor de un punto,
/// aplicado como <c>transformMatrix</c> al dibujo principal en
/// <c>MonstersGameApp.Draw</c>, junto al de <see cref="ScreenShake"/>. En
/// reposo <see cref="GetTransform"/> devuelve <see cref="Matrix.Identity"/>
/// -- no-op visual garantizado, mismo principio que <see cref="ScreenShake"/>/
/// <see cref="ScreenFlash"/>.
/// </summary>
public sealed class CameraPunch
{
    private Vector2 _focus;
    private float _magnitude;
    private float _remaining;
    private float _total;

    /// <summary>
    /// Dispara un acercamiento del <paramref name="zoomAmount"/> (0.08 = 8%
    /// mas grande en el pico) centrado en <paramref name="focus"/> (coordenadas
    /// del lienzo virtual 1280x720), que sube y vuelve a bajar a lo largo de
    /// <paramref name="duration"/> segundos. Un zoom ya en curso mas fuerte o
    /// mas largo no se interrumpe con uno mas debil, mismo criterio que
    /// <see cref="ScreenShake.Trigger"/>.
    /// </summary>
    public void Trigger(Vector2 focus, float zoomAmount, float duration)
    {
        if (_remaining > 0f && _magnitude > zoomAmount) return;
        _focus = focus;
        _magnitude = zoomAmount;
        _remaining = duration;
        _total = duration;
    }

    public void Update(float dt)
    {
        if (_remaining <= 0f) return;
        _remaining = MathF.Max(0f, _remaining - dt);
    }

    /// <summary>Matriz de zoom alrededor del foco para este instante, o la identidad si no hay ningun acercamiento en curso.</summary>
    public Matrix GetTransform()
    {
        if (_remaining <= 0f || _total <= 0f) return Matrix.Identity;

        float progress = 1f - _remaining / _total; // 0 al disparar, 1 al terminar
        float envelope = progress < 0.5f
            ? Easing.EaseOutCubic(progress / 0.5f)
            : 1f - Easing.EaseInCubic((progress - 0.5f) / 0.5f);

        float scale = 1f + _magnitude * envelope;
        return Matrix.CreateTranslation(-_focus.X, -_focus.Y, 0f)
            * Matrix.CreateScale(scale)
            * Matrix.CreateTranslation(_focus.X, _focus.Y, 0f);
    }
}
