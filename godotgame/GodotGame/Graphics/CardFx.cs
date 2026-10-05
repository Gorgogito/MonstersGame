using Godot;
using System;

namespace GodotGame.Graphics;

/// <summary>
/// Efectos de "salida" de una carta, compartidos entre el tablero y la
/// pantalla de batalla: <see cref="Shatter"/> (romperse en fragmentos, para
/// una destruccion) y <see cref="Dissolve"/> (desvanecerse hacia arriba, para
/// una carta que se va al Cementerio sin ser destruida: tributo, Magia usada).
///
/// Ambos trabajan sobre una copia: crean un contenedor propio con el mismo
/// centro, tamano y rotacion que la carta original (asi un monstruo en
/// Defensa, girado 90°, se rompe girado), lo agregan a <c>parent</c> y se
/// autodestruyen al terminar. La carta original no se toca.
/// </summary>
public static class CardFx
{
    /// <summary>Aspecto visual de la carta que se va: arte (o null), donde se dibuja ese arte dentro de la carta (estirado a ese rect), y color de marco.</summary>
    public readonly record struct CardLook(Texture2D? Art, Rect2 ArtRect, Color Frame, float Border);

    /// <summary>
    /// La carta se rompe en una grilla de fragmentos que salen despedidos,
    /// giran y se desvanecen. Cada fragmento recorta su porcion del arte con
    /// un <see cref="AtlasTexture"/>, y el resto es el color del marco.
    /// <paramref name="spread"/> escala la distancia de vuelo (la carta grande
    /// de la batalla vuela mas lejos que una de tablero).
    /// </summary>
    public static void Shatter(Node parent, Vector2 center, Vector2 size, float rotation, CardLook look,
        int cols, int rows, float spread, int zIndex = 10)
    {
        var container = NewContainer(parent, center, size, rotation, zIndex);
        var pieceSize = new Vector2(size.X / cols, size.Y / rows);
        var bg = look.Frame;
        var edge = look.Frame.Darkened(0.55f);
        var innerRect = new Rect2(look.Border, look.Border, size.X - 2 * look.Border, size.Y - 2 * look.Border);
        var texScale = look.Art != null ? look.Art.GetSize() / look.ArtRect.Size : Vector2.One;
        var cardCenter = size / 2f;
        var rng = Random.Shared;

        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            var local = new Rect2(new Vector2(c * pieceSize.X, r * pieceSize.Y), pieceSize);
            var piece = new Control
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Position = local.Position,
                Size = pieceSize,
                PivotOffset = pieceSize / 2f,
                ClipContents = true
            };
            piece.AddChild(new ColorRect { Color = edge, Size = pieceSize, MouseFilter = Control.MouseFilterEnum.Ignore });

            var inner = local.Intersection(innerRect);
            if (inner.HasArea())
                piece.AddChild(new ColorRect { Color = bg, Position = inner.Position - local.Position, Size = inner.Size, MouseFilter = Control.MouseFilterEnum.Ignore });

            var artPart = local.Intersection(look.ArtRect);
            if (look.Art != null && artPart.HasArea())
            {
                piece.AddChild(new TextureRect
                {
                    Texture = new AtlasTexture
                    {
                        Atlas = look.Art,
                        Region = new Rect2((artPart.Position - look.ArtRect.Position) * texScale, artPart.Size * texScale)
                    },
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.Scale,
                    Position = artPart.Position - local.Position,
                    Size = artPart.Size
                });
            }
            container.AddChild(piece);

            var dir = (local.GetCenter() - cardCenter).Normalized();
            if (dir == Vector2.Zero) dir = Vector2.Up;
            dir = dir.Rotated((float)(rng.NextDouble() - 0.5) * 0.8f);
            float distance = spread * (0.6f + (float)rng.NextDouble());
            // La "gravedad" es hacia abajo de la pantalla, no del contenedor (que puede estar girado).
            var gravity = new Vector2(0, spread * 0.4f).Rotated(-rotation);
            var target = piece.Position + dir * distance + gravity;

            var tween = piece.CreateTween().SetParallel();
            tween.TweenProperty(piece, "position", target, 0.75).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(piece, "rotation", (float)(rng.NextDouble() * 2 - 1) * 5f, 0.75);
            tween.TweenProperty(piece, "scale", new Vector2(0.5f, 0.5f), 0.75);
            tween.TweenProperty(piece, "modulate:a", 0f, 0.45).SetDelay(0.3);
        }

        Burst(container, cardCenter, look.Frame.Lerp(Colors.White, 0.4f), amount: Math.Clamp((int)(spread / 5f), 16, 50), speed: spread);
        FreeAfter(container, 0.85);
    }

    /// <summary>
    /// La carta brilla, se estira hacia arriba y se desvanece, dejando una
    /// estela de chispas que suben: una salida "limpia", sin romperse.
    /// </summary>
    public static void Dissolve(Node parent, Vector2 center, Vector2 size, float rotation, CardLook look, int zIndex = 10)
    {
        var container = NewContainer(parent, center, size, rotation, zIndex);

        var card = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore, Size = size, PivotOffset = new Vector2(size.X / 2f, size.Y) };
        card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = look.Frame,
            BorderColor = look.Frame.Darkened(0.55f),
            BorderWidthLeft = (int)look.Border,
            BorderWidthRight = (int)look.Border,
            BorderWidthTop = (int)look.Border,
            BorderWidthBottom = (int)look.Border,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8
        });
        if (look.Art != null)
        {
            card.AddChild(new TextureRect
            {
                Texture = look.Art,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                Position = look.ArtRect.Position,
                Size = look.ArtRect.Size
            });
        }
        container.AddChild(card);

        var tween = card.CreateTween();
        tween.TweenProperty(card, "modulate", new Color(2.2f, 2.2f, 2.2f, 1f), 0.14);
        tween.TweenProperty(card, "scale", new Vector2(0.7f, 1.35f), 0.4).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(card, "modulate:a", 0f, 0.4);
        // "Hacia arriba" de la pantalla, aunque el contenedor este girado (monstruo en Defensa).
        tween.Parallel().TweenProperty(card, "position", new Vector2(0, -size.Y * 0.35f).Rotated(-rotation), 0.4)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

        var sparks = new CpuParticles2D
        {
            Position = size / 2f,
            Emitting = false,
            OneShot = true,
            Amount = 24,
            Lifetime = 0.8,
            Explosiveness = 0.6f,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = size / 2f,
            Direction = Vector2.Up,
            Spread = 25f,
            InitialVelocityMin = 40f,
            InitialVelocityMax = 110f,
            Gravity = new Vector2(0, -60f).Rotated(-rotation),
            ScaleAmountMin = 2f,
            ScaleAmountMax = 4f,
            Color = look.Frame.Lerp(Colors.White, 0.6f)
        };
        container.AddChild(sparks);
        sparks.Emitting = true;

        FreeAfter(container, 0.9);
    }

    /// <summary>
    /// Invocacion en una Zona: un circulo magico se abre girando bajo la
    /// carta, sube una columna de luz y chispas, y todo se desvanece.
    /// <paramref name="center"/>/<paramref name="size"/> son los de la Zona,
    /// en coordenadas de <paramref name="parent"/>.
    /// </summary>
    public static void SummonFlare(Node parent, Vector2 center, Vector2 size, Color color, int zIndex = 9)
    {
        var circle = new MagicCircle
        {
            Radius = size.X * 0.62f,
            CircleColor = new Color(color.R, color.G, color.B, 0.9f),
            LineWidth = 2.5f,
            Position = center,
            Scale = new Vector2(0.2f, 0.2f),
            ZIndex = zIndex
        };
        parent.AddChild(circle);
        var circleTween = circle.CreateTween().SetParallel();
        circleTween.TweenProperty(circle, "scale", Vector2.One, 0.35).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        circleTween.TweenProperty(circle, "rotation", 1.6f, 0.9);
        circleTween.TweenProperty(circle, "modulate:a", 0f, 0.45).SetDelay(0.45);
        circleTween.Chain().TweenCallback(Callable.From(circle.QueueFree));

        // Columna de luz: degradado vertical (transparente arriba), que crece desde la Zona.
        const float pillarHeight = 190f;
        float pillarWidth = size.X * 0.7f;
        var pillar = new TextureRect
        {
            Texture = new GradientTexture2D
            {
                Gradient = new Gradient
                {
                    Colors = new[] { new Color(color.R, color.G, color.B, 0f), new Color(color.R, color.G, color.B, 0.7f) },
                    Offsets = new[] { 0f, 1f }
                },
                FillFrom = new Vector2(0, 0),
                FillTo = new Vector2(0, 1),
                Width = 8,
                Height = 64
            },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = center - new Vector2(pillarWidth / 2f, pillarHeight),
            Size = new Vector2(pillarWidth, pillarHeight),
            PivotOffset = new Vector2(pillarWidth / 2f, pillarHeight),
            Scale = new Vector2(1, 0),
            ZIndex = zIndex
        };
        parent.AddChild(pillar);
        var pillarTween = pillar.CreateTween();
        pillarTween.TweenProperty(pillar, "scale:y", 1f, 0.18).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        pillarTween.TweenProperty(pillar, "modulate:a", 0f, 0.45);
        pillarTween.TweenCallback(Callable.From(pillar.QueueFree));

        var sparks = new CpuParticles2D
        {
            Position = center,
            Emitting = false,
            OneShot = true,
            Amount = 26,
            Lifetime = 0.8,
            Explosiveness = 0.7f,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(size.X * 0.35f, 6f),
            Direction = Vector2.Up,
            Spread = 15f,
            InitialVelocityMin = 70f,
            InitialVelocityMax = 160f,
            Gravity = new Vector2(0, 40f),
            ScaleAmountMin = 2f,
            ScaleAmountMax = 4f,
            Color = color.Lerp(Colors.White, 0.5f),
            ZIndex = zIndex
        };
        parent.AddChild(sparks);
        sparks.Emitting = true;
        sparks.Finished += sparks.QueueFree;
    }

    /// <summary>Explosion de particulas de un solo disparo en <paramref name="position"/> (coordenadas de <paramref name="parent"/>).</summary>
    public static void Burst(Node parent, Vector2 position, Color color, int amount, float speed)
    {
        var particles = new CpuParticles2D
        {
            Position = position,
            Emitting = false,
            OneShot = true,
            Amount = amount,
            Lifetime = 0.7,
            Explosiveness = 1f,
            Direction = Vector2.Up,
            Spread = 180f,
            InitialVelocityMin = speed * 0.35f,
            InitialVelocityMax = speed,
            Gravity = new Vector2(0, 320f),
            ScaleAmountMin = 3f,
            ScaleAmountMax = 6f,
            Color = color
        };
        parent.AddChild(particles);
        particles.Emitting = true;
        particles.Finished += particles.QueueFree;
    }

    private static Control NewContainer(Node parent, Vector2 center, Vector2 size, float rotation, int zIndex)
    {
        var container = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Size = size,
            PivotOffset = size / 2f,
            Position = center - size / 2f,
            Rotation = rotation,
            ZIndex = zIndex
        };
        parent.AddChild(container);
        return container;
    }

    private static void FreeAfter(Node node, double seconds)
    {
        var tween = node.CreateTween();
        tween.TweenInterval(seconds);
        tween.TweenCallback(Callable.From(node.QueueFree));
    }
}
