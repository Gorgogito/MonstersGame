using Microsoft.Xna.Framework;

namespace MonstersGame.Graphics;

/// <summary>
/// Destello de pantalla completa de proposito unico (Fase 6 del plan de
/// mejoras visuales): un color translucido dibujado sobre todo el lienzo
/// virtual, para los pocos momentos realmente "grandes" (por ahora, el fin
/// del duelo). En reposo <see cref="CurrentAlpha"/> es 0 -- no-op visual
/// garantizado, mismo principio que <see cref="ScreenShake"/>.
/// </summary>
public sealed class ScreenFlash
{
    private float _remaining;
    private float _total;

    public Color CurrentColor { get; private set; } = Color.White;
    public float CurrentAlpha { get; private set; }

    /// <summary>Dispara un destello de <paramref name="color"/> que se desvanece a lo largo de <paramref name="duration"/> segundos. Un destello ya en curso se reemplaza (son eventos raros y puntuales; no hace falta encolarlos).</summary>
    public void Trigger(Color color, float duration)
    {
        CurrentColor = color;
        _remaining = duration;
        _total = duration;
    }

    public void Update(float dt)
    {
        if (_remaining <= 0f)
        {
            CurrentAlpha = 0f;
            return;
        }

        _remaining -= dt;
        CurrentAlpha = _remaining <= 0f ? 0f : MathHelper.Clamp(_remaining / _total, 0f, 1f);
    }
}
