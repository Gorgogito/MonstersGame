using Godot;
using GodotGame.Core.Entities;
using GodotGame.Graphics;
using System.Collections.Generic;

namespace GodotGame;

/// <summary>
/// Secuencia a pantalla completa de una Invocacion especial (estilo Forbidden
/// Memories):
/// <list type="bullet">
/// <item><b>Fusion</b>: los materiales entran en grande, giran en espiral
/// hacia el centro sobre un circulo magico, se funden en un destello y el
/// monstruo resultante aparece con su nombre.</item>
/// <item><b>Ritual</b>: se abre un gran circulo magico con una columna de
/// luz y el monstruo de Ritual emerge desde abajo.</item>
/// </list>
/// En ambos casos la carta termina volando, encogida, hasta su Zona del
/// tablero. Puramente visual: el motor ya hizo la Invocacion.
/// </summary>
public partial class SummonOverlay : DuelOverlay
{
    public enum SummonKind { Fusion, Ritual }

    private static readonly Vector2 CardSize = BigCardFactory.CardSize;
    private const float MaterialScale = 0.62f;
    private static readonly Color FusionColor = new(0.78f, 0.55f, 1f);
    private static readonly Color RitualColor = new(0.5f, 0.75f, 1f);

    private SummonKind _kind;
    private IReadOnlyList<MonsterCard> _materials = new List<MonsterCard>();
    private MonsterCard _result = null!;
    private bool _isHuman;
    private Vector2 _targetCenter;
    private Vector2 _targetSize;

    private Control _band = null!;
    private MagicCircle? _circle;

    /// <param name="targetCenter">Centro (en coordenadas de pantalla) de la Zona donde quedo el monstruo.</param>
    /// <param name="targetSize">Tamano de esa Zona, para que la carta llegue del tamano justo.</param>
    public void Setup(SummonKind kind, IReadOnlyList<MonsterCard> materials, MonsterCard result, bool isHuman,
        Vector2 targetCenter, Vector2 targetSize, TextureCache textures, AudioManager audio)
    {
        _kind = kind;
        _materials = materials;
        _result = result;
        _isHuman = isHuman;
        _targetCenter = targetCenter;
        _targetSize = targetSize;
        SetupBase(textures, audio);
    }

    protected override void Build()
    {
        _band = AddBand();
        if (_kind == SummonKind.Fusion)
            PlayFusion();
        else
            PlayRitual();
    }

    // ------------------------------------------------------------ Secuencias

    private void PlayFusion()
    {
        ShowCaption("FUSION", FusionColor.Lerp(Colors.White, 0.3f), delay: 0.15);

        // Materiales en fila, entrando desde el lado de su duenio (jugador abajo, CPU arriba).
        var views = new List<BigCardFactory.View>();
        var rowCenters = new List<Vector2>();
        float spacing = CardSize.X * MaterialScale + 34f;
        float rowY = Center.Y - 10f;
        float startY = _isHuman ? ViewportSize.Y + CardSize.Y : -CardSize.Y;
        for (int i = 0; i < _materials.Count; i++)
        {
            var view = BigCardFactory.Build(Textures, _materials[i], faceDown: false);
            view.Root.Scale = new Vector2(MaterialScale, MaterialScale);
            Stage.AddChild(view.Root);
            var rowCenter = new Vector2(Center.X + (i - (_materials.Count - 1) / 2f) * spacing, rowY);
            SetCenter(view.Root, new Vector2(rowCenter.X, startY));
            views.Add(view);
            rowCenters.Add(rowCenter);

            var enter = CreateTween();
            enter.TweenInterval(0.08 + i * 0.08);
            enter.TweenProperty(view.Root, "position", rowCenter - CardSize / 2f, 0.35)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }

        Timeline = CreateTween();
        Timeline.TweenInterval(0.85);

        // Espiral: cada material gira alrededor del centro mientras el radio se cierra.
        Timeline.TweenCallback(Callable.From(() =>
        {
            Audio.PlaySfx("fusion");
            _circle = AddCircle(160f, FusionColor, startScale: 0f);
            var grow = CreateTween().SetParallel();
            grow.TweenProperty(_circle, "scale", Vector2.One, 1.0).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            grow.TweenProperty(_circle, "rotation", 4f, 1.0);
        }));
        Timeline.TweenMethod(Callable.From<float>(u =>
        {
            for (int i = 0; i < views.Count; i++)
            {
                var offset = rowCenters[i] - Center;
                float radius = offset.Length() * (1f - u);
                float angle = offset.Angle() + u * Mathf.Tau * 1.25f;
                SetCenter(views[i].Root, Center + Vector2.FromAngle(angle) * radius);
                float scale = MaterialScale * (1f - 0.65f * u);
                views[i].Root.Scale = new Vector2(scale, scale);
                views[i].Root.Rotation = u * Mathf.Tau * 2f;
            }
        }), 0f, 1f, 1.0).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);

        // Fusion: destello, onda expansiva y aparece el resultado.
        Timeline.TweenCallback(Callable.From(() =>
        {
            foreach (var view in views) view.Root.Visible = false;
            Merge(FusionColor);
            RevealResult(fromBelow: false);
        }));
        Timeline.TweenInterval(1.35);
        Timeline.TweenCallback(Callable.From(FlyToZone));
    }

    private void PlayRitual()
    {
        ShowCaption("INVOCACION RITUAL", RitualColor.Lerp(Colors.White, 0.3f), delay: 0.15);
        Audio.PlaySfx("ritual");

        _circle = AddCircle(180f, RitualColor, startScale: 0f);
        _circle.Position = new Vector2(Center.X, Center.Y + 40f);
        var open = CreateTween().SetParallel();
        open.TweenProperty(_circle, "scale", new Vector2(1f, 0.45f), 0.55).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        open.TweenProperty(_circle, "rotation", 3f, 2.6);

        AddPillar(RitualColor, width: 260f, height: 470f, bottomY: Center.Y + 60f, delay: 0.35);

        Timeline = CreateTween();
        Timeline.TweenInterval(0.55);
        Timeline.TweenCallback(Callable.From(() => RevealResult(fromBelow: true)));
        Timeline.TweenInterval(0.6);
        Timeline.TweenCallback(Callable.From(() => Merge(RitualColor)));
        Timeline.TweenInterval(1.2);
        Timeline.TweenCallback(Callable.From(FlyToZone));
    }

    // --------------------------------------------------------------- Piezas

    private BigCardFactory.View? _resultView;

    /// <summary>El monstruo invocado aparece en el centro: de golpe con un rebote (Fusion) o emergiendo desde abajo (Ritual).</summary>
    private void RevealResult(bool fromBelow)
    {
        var view = BigCardFactory.Build(Textures, _result, faceDown: false);
        _resultView = view;
        Stage.AddChild(view.Root);

        if (fromBelow)
        {
            SetCenter(view.Root, Center + new Vector2(0, 170f));
            view.Root.Scale = new Vector2(0.6f, 0.6f);
            view.Root.Modulate = new Color(1, 1, 1, 0);
            var rise = CreateTween().SetParallel();
            rise.TweenProperty(view.Root, "position", Center - CardSize / 2f, 0.6).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            rise.TweenProperty(view.Root, "scale", Vector2.One, 0.6).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            rise.TweenProperty(view.Root, "modulate:a", 1f, 0.3);
        }
        else
        {
            SetCenter(view.Root, Center);
            view.Root.Scale = Vector2.Zero;
            var pop = CreateTween();
            pop.TweenProperty(view.Root, "scale", new Vector2(1.15f, 1.15f), 0.22).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            pop.TweenProperty(view.Root, "scale", Vector2.One, 0.15);
        }

        var name = _result.Name.ToUpperInvariant();
        var gold = new Color(1f, 0.88f, 0.5f);
        var show = CreateTween();
        show.TweenInterval(fromBelow ? 0.6 : 0.1);
        show.TweenCallback(Callable.From(() => ShowCaption($"¡{name}!", gold, delay: 0)));
    }

    /// <summary>Momento de la union: destello, temblor, particulas y una onda (circulo que se expande y se desvanece).</summary>
    private void Merge(Color color)
    {
        Audio.PlaySfx("impact");
        Flash(0.9f, 0.5);
        Shake(10f, 0.35f);
        CardFx.Burst(Stage, Center, color.Lerp(Colors.White, 0.4f), amount: 70, speed: 460f);

        var wave = AddCircle(120f, color.Lerp(Colors.White, 0.5f), startScale: 0.3f);
        var waveTween = CreateTween().SetParallel();
        waveTween.TweenProperty(wave, "scale", new Vector2(2.6f, 2.6f), 0.55).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        waveTween.TweenProperty(wave, "modulate:a", 0f, 0.55);
        waveTween.Chain().TweenCallback(Callable.From(wave.QueueFree));
    }

    /// <summary>La carta se encoge y vuela a su Zona mientras la escena se desvanece; al llegar, cierra el overlay.</summary>
    private void FlyToZone()
    {
        if (_resultView == null) { Finish(); return; }
        float scale = _targetSize.X / CardSize.X;

        var fly = CreateTween().SetParallel();
        fly.TweenProperty(_resultView.Root, "position", _targetCenter - CardSize / 2f, 0.38)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        fly.TweenProperty(_resultView.Root, "scale", new Vector2(scale, scale), 0.38)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        fly.TweenProperty(Dim, "color:a", 0f, 0.3);
        fly.TweenProperty(_band, "scale:y", 0f, 0.25);
        fly.TweenProperty(Caption, "modulate:a", 0f, 0.2);
        if (_circle != null) fly.TweenProperty(_circle, "modulate:a", 0f, 0.25);
        fly.Chain().TweenCallback(Callable.From(() => Finish(0.08)));
    }

    private MagicCircle AddCircle(float radius, Color color, float startScale)
    {
        var circle = new MagicCircle
        {
            Radius = radius,
            CircleColor = color,
            LineWidth = 4f,
            Position = Center,
            Scale = new Vector2(startScale, startScale)
        };
        Stage.AddChild(circle);
        return circle;
    }

    private void AddPillar(Color color, float width, float height, float bottomY, double delay)
    {
        var pillar = new TextureRect
        {
            Texture = new GradientTexture2D
            {
                Gradient = new Gradient
                {
                    Colors = new[] { new Color(color.R, color.G, color.B, 0f), new Color(color.R, color.G, color.B, 0.55f) },
                    Offsets = new[] { 0f, 1f }
                },
                FillFrom = new Vector2(0, 0),
                FillTo = new Vector2(0, 1),
                Width = 8,
                Height = 64
            },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            Position = new Vector2(Center.X - width / 2f, bottomY - height),
            Size = new Vector2(width, height),
            PivotOffset = new Vector2(width / 2f, height),
            Scale = new Vector2(1, 0)
        };
        Stage.AddChild(pillar);
        var tween = CreateTween();
        tween.TweenInterval(delay);
        tween.TweenProperty(pillar, "scale:y", 1f, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenInterval(0.9);
        tween.TweenProperty(pillar, "modulate:a", 0f, 0.5);
    }

    private static void SetCenter(Control root, Vector2 center) => root.Position = center - CardSize / 2f;
}
