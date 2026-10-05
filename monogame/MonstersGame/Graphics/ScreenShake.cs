using Microsoft.Xna.Framework;

namespace MonstersGame.Graphics;

/// <summary>
/// Sacudida de pantalla de proposito unico (Fase 4 del plan de mejoras
/// visuales): un desplazamiento aleatorio que decae, aplicado como
/// <c>transformMatrix</c> al dibujo principal en <c>MonstersGameApp.Draw</c>.
/// En reposo <see cref="CurrentOffset"/> es <see cref="Vector2.Zero"/>, que
/// como traslacion es la matriz identidad -- ninguna pantalla se ve afectada
/// mientras nadie dispara un shake.
/// </summary>
public sealed class ScreenShake
{
    private readonly Random _random = new();
    private float _magnitude;
    private float _remaining;
    private float _total;

    public Vector2 CurrentOffset { get; private set; }

    /// <summary>
    /// Dispara una sacudida de <paramref name="magnitude"/> pixeles maximos
    /// durante <paramref name="duration"/> segundos. Si ya hay una sacudida
    /// en curso mas fuerte, no la interrumpe con una mas debil (evita que un
    /// golpe chico "apague" el shake de uno grande que ya estaba sonando).
    /// </summary>
    public void Trigger(float magnitude, float duration)
    {
        if (_remaining > 0f && _magnitude > magnitude) return;
        _magnitude = magnitude;
        _remaining = duration;
        _total = duration;
    }

    public void Update(float dt)
    {
        if (_remaining <= 0f)
        {
            CurrentOffset = Vector2.Zero;
            return;
        }

        _remaining -= dt;
        if (_remaining <= 0f)
        {
            CurrentOffset = Vector2.Zero;
            return;
        }

        float amount = _magnitude * (_remaining / _total);
        CurrentOffset = new Vector2(
            (float)(_random.NextDouble() * 2.0 - 1.0) * amount,
            (float)(_random.NextDouble() * 2.0 - 1.0) * amount);
    }
}
