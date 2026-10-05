using Godot;
using GodotGame.Graphics;
using System;

namespace GodotGame;

/// <summary>
/// Base de las escenas a pantalla completa que interrumpen el duelo (batalla,
/// fusion, ritual): oscurece el tablero, bloquea el input, tiene un "escenario"
/// que puede temblar, un destello blanco y un titulo, y se cierra sola al
/// terminar su animacion -- o antes, con un clic o Espacio/Enter/Esc.
///
/// Mientras haya una abierta, <see cref="Duel"/> congela el tablero: el
/// resultado que el motor ya aplico se ve recien al cerrarse.
/// </summary>
public abstract partial class DuelOverlay : Control
{
    /// <summary>Se dispara una sola vez, cuando termina (o se saltea) la animacion.</summary>
    public event Action? Finished;

    protected static readonly Color BandLineColor = new(0.86f, 0.7f, 0.32f);

    protected TextureCache Textures = null!;
    protected AudioManager Audio = null!;

    protected ColorRect Dim = null!;
    protected ColorRect FlashRect = null!;
    /// <summary>Contenedor de todo lo que tiembla con <see cref="Shake"/> (cartas, fragmentos, particulas).</summary>
    protected Control Stage = null!;
    protected Label Caption = null!;
    /// <summary>Secuencia principal; se corta si se saltea.</summary>
    protected Tween? Timeline;
    protected Vector2 ViewportSize;
    protected Vector2 Center;

    private bool _finishing;

    protected void SetupBase(TextureCache textures, AudioManager audio)
    {
        Textures = textures;
        Audio = audio;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 50;
        // Un boton con foco reaccionaria a Espacio/Enter por debajo del overlay.
        GetViewport().GuiReleaseFocus();

        ViewportSize = GetViewportRect().Size;
        Center = ViewportSize / 2f;

        Dim = new ColorRect { Color = new Color(0, 0, 0, 0), MouseFilter = MouseFilterEnum.Ignore };
        Dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(Dim);

        Stage = new Control { MouseFilter = MouseFilterEnum.Ignore, Size = ViewportSize };
        AddChild(Stage);

        FlashRect = new ColorRect { Color = new Color(1, 1, 1, 0), MouseFilter = MouseFilterEnum.Ignore };
        FlashRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(FlashRect);

        // Fuera de Stage (no tiembla) y despues de el: queda por encima de fragmentos y particulas.
        Caption = MakeLabel("", 30, Colors.White, 9);
        Caption.Position = new Vector2(0, Center.Y - 292);
        Caption.Size = new Vector2(ViewportSize.X, 44);
        Caption.Modulate = new Color(1, 1, 1, 0);
        AddChild(Caption);

        CreateTween().TweenProperty(Dim, "color:a", 0.72f, 0.2);

        Build();
    }

    /// <summary>Arma la escena y arranca <see cref="Timeline"/>; la secuencia debe terminar llamando a <see cref="Finish"/>.</summary>
    protected abstract void Build();

    // ------------------------------------------------------------- Utilidades

    /// <summary>Franja horizontal oscura con filos dorados que se abre desde el centro, detras de las cartas.</summary>
    protected Control AddBand(float height = 470f)
    {
        var band = new Control
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Position = new Vector2(0, Center.Y - height / 2f),
            Size = new Vector2(ViewportSize.X, height),
            PivotOffset = new Vector2(ViewportSize.X / 2f, height / 2f),
            Scale = new Vector2(1, 0)
        };

        var fill = new ColorRect { Color = new Color(0.04f, 0.06f, 0.14f, 0.88f), MouseFilter = MouseFilterEnum.Ignore };
        fill.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        band.AddChild(fill);

        foreach (float y in new[] { 0f, height - 3f })
        {
            band.AddChild(new ColorRect
            {
                Color = BandLineColor,
                MouseFilter = MouseFilterEnum.Ignore,
                Position = new Vector2(0, y),
                Size = new Vector2(ViewportSize.X, 3)
            });
        }
        Stage.AddChild(band);
        CreateTween().TweenProperty(band, "scale:y", 1f, 0.22).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        return band;
    }

    protected static Label MakeLabel(string text, int size, Color color, int outline)
    {
        var label = new Label
        {
            Text = text,
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", outline);
        return label;
    }

    protected void ShowCaption(string text, Color color, double delay)
    {
        Caption.Text = text;
        Caption.AddThemeColorOverride("font_color", color);
        Caption.PivotOffset = Caption.Size / 2f;
        Caption.Scale = new Vector2(1.4f, 1.4f);
        Caption.Modulate = new Color(1, 1, 1, 0);
        var tween = CreateTween();
        tween.TweenInterval(delay);
        tween.TweenProperty(Caption, "modulate:a", 1f, 0.1);
        tween.Parallel().TweenProperty(Caption, "scale", Vector2.One, 0.25)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    protected void Flash(float alpha, double duration)
    {
        FlashRect.Color = new Color(1, 1, 1, alpha);
        CreateTween().TweenProperty(FlashRect, "color:a", 0f, duration)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    protected void Shake(float magnitude, float duration)
    {
        var rng = Random.Shared;
        var tween = CreateTween();
        const int steps = 7;
        for (int i = 0; i < steps; i++)
        {
            float decay = 1f - i / (float)steps;
            var offset = new Vector2((float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1)) * magnitude * decay;
            tween.TweenProperty(Stage, "position", offset, duration / steps);
        }
        tween.TweenProperty(Stage, "position", Vector2.Zero, duration / steps);
    }

    /// <summary>Volteo de una carta grande: se cierra de canto, cambia de cara y se vuelve a abrir.</summary>
    protected void Flip(BigCardFactory.View card)
    {
        Audio.PlaySfx("set");
        var tween = CreateTween();
        tween.TweenProperty(card.Root, "scale:x", 0f, 0.12).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(() =>
        {
            card.Back.Visible = false;
            card.Front.Visible = true;
        }));
        tween.TweenProperty(card.Root, "scale:x", 1f, 0.12).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    // ------------------------------------------------------- Cierre / salto

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true })
        {
            AcceptEvent();
            Finish();
        }
    }

    public override void _Input(InputEvent @event)
    {
        // Mientras el overlay esta en pantalla, ninguna tecla llega al tablero;
        // las de confirmar/cancelar ademas lo saltean.
        if (@event is not InputEventKey { Pressed: true }) return;
        GetViewport().SetInputAsHandled();
        if (@event.IsActionPressed("ui_accept") || @event.IsActionPressed("ui_cancel") || @event.IsActionPressed("ui_select"))
            Finish();
    }

    /// <summary>Cierra el overlay (desvaneciendolo) y avisa a <see cref="Finished"/>. Idempotente.</summary>
    protected void Finish(double fadeDuration = 0.22)
    {
        if (_finishing) return;
        _finishing = true;
        Timeline?.Kill();

        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 0f, fadeDuration);
        tween.TweenCallback(Callable.From(() =>
        {
            Finished?.Invoke();
            QueueFree();
        }));
    }
}
