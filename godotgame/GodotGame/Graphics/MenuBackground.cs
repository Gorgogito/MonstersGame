using Godot;
using System;
using System.Collections.Generic;

namespace GodotGame.Graphics;

/// <summary>
/// Fondo animado de los menus: degradado oscuro, un gran circulo magico que
/// gira despacio, reversos de carta que suben flotando y girando, y motas de
/// luz. Va como primer hijo de la escena, detras de todo.
/// </summary>
public partial class MenuBackground : Control
{
    private sealed class FloatingCard
    {
        public TextureRect Node = null!;
        public float Speed;
        public float Spin;
        public float Sway;
        public float Phase;
    }

    private readonly List<FloatingCard> _cards = new();
    private MagicCircle _circle = null!;
    private readonly Random _rng = new();

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        var size = GetViewportRect().Size;

        AddChild(new TextureRect
        {
            Texture = new GradientTexture2D
            {
                Gradient = new Gradient
                {
                    Colors = new[] { new Color(0.03f, 0.04f, 0.1f), new Color(0.12f, 0.06f, 0.2f), new Color(0.02f, 0.02f, 0.05f) },
                    Offsets = new[] { 0f, 0.55f, 1f }
                },
                FillFrom = new Vector2(0, 0),
                FillTo = new Vector2(0, 1),
                Width = 8,
                Height = 128
            },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            Size = size
        });

        _circle = new MagicCircle
        {
            Radius = Mathf.Min(size.X, size.Y) * 0.42f,
            CircleColor = new Color(0.85f, 0.7f, 0.35f, 0.14f),
            LineWidth = 3f,
            Position = size / 2f
        };
        AddChild(_circle);

        var back = new TextureCache(ProjectSettings.GlobalizePath("res://Data/Art")).CardArt("CardBack.jpg");
        for (int i = 0; i < 16; i++)
        {
            float scale = 0.5f + (float)_rng.NextDouble() * 0.6f;
            var node = new TextureRect
            {
                Texture = back,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = MouseFilterEnum.Ignore,
                Size = new Vector2(62, 90) * scale,
                PivotOffset = new Vector2(31, 45) * scale,
                Position = new Vector2((float)_rng.NextDouble() * size.X, (float)_rng.NextDouble() * size.Y),
                Modulate = new Color(1, 1, 1, 0.12f + 0.25f * scale - 0.12f),
                RotationDegrees = (float)_rng.NextDouble() * 360f
            };
            AddChild(node);
            _cards.Add(new FloatingCard
            {
                Node = node,
                Speed = 12f + 28f * scale,
                Spin = ((float)_rng.NextDouble() - 0.5f) * 40f,
                Sway = 10f + (float)_rng.NextDouble() * 25f,
                Phase = (float)_rng.NextDouble() * Mathf.Tau
            });
        }

        AddChild(new CpuParticles2D
        {
            Position = size / 2f,
            Amount = 50,
            Lifetime = 6,
            Preprocess = 6,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = size / 2f,
            Direction = Vector2.Up,
            Spread = 30f,
            InitialVelocityMin = 6f,
            InitialVelocityMax = 18f,
            Gravity = Vector2.Zero,
            ScaleAmountMin = 1.5f,
            ScaleAmountMax = 3f,
            Color = new Color(1f, 0.85f, 0.5f, 0.5f)
        });
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        var size = GetViewportRect().Size;
        _circle.Rotation += dt * 0.05f;
        foreach (var card in _cards)
        {
            card.Phase += dt;
            var p = card.Node.Position;
            p.Y -= card.Speed * dt;
            p.X += Mathf.Sin(card.Phase * 0.6f) * card.Sway * dt;
            if (p.Y < -120f) p = new Vector2((float)_rng.NextDouble() * size.X, size.Y + 40f);
            card.Node.Position = p;
            card.Node.RotationDegrees += card.Spin * dt;
        }
    }
}
