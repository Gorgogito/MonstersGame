using Godot;
using GodotGame.Core.Entities;

namespace GodotGame.Graphics;

/// <summary>
/// Fondo animado de un terreno (Magia de Campo), estilo Forbidden Memories:
/// un degradado del color del <see cref="FieldType"/> y particulas propias
/// del elemento. El elemento se deduce del Id/Nombre del terreno (fuego,
/// agua/mar, viento, tierra, luz, oscuridad); cualquier otro usa motas del
/// color del terreno.
/// </summary>
public static class TerrainFx
{
    private enum Element { Fire, Water, Wind, Earth, Light, Dark, Generic }

    /// <summary>Capa del tamano <paramref name="size"/>, recortada a sus bordes, para poner detras de las Zonas.</summary>
    public static Control BuildLayer(FieldType fieldType, Vector2 size)
    {
        var color = new Color(fieldType.Color);
        var layer = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipContents = true,
            Size = size
        };

        layer.AddChild(new TextureRect
        {
            Texture = new GradientTexture2D
            {
                Gradient = new Gradient
                {
                    Colors = new[] { new Color(color.R, color.G, color.B, 0.5f), new Color(color.R * 0.4f, color.G * 0.4f, color.B * 0.4f, 0.15f) },
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
            Size = size
        });

        var particles = BuildParticles(ElementOf(fieldType), color, size);
        layer.AddChild(particles);
        return layer;
    }

    private static Element ElementOf(FieldType fieldType)
    {
        string key = (fieldType.Id + " " + fieldType.Name).ToLowerInvariant();
        if (key.Contains("fire") || key.Contains("fuego") || key.Contains("volcan")) return Element.Fire;
        if (key.Contains("water") || key.Contains("agua") || key.Contains("umi") || key.Contains("mar")) return Element.Water;
        if (key.Contains("wind") || key.Contains("viento")) return Element.Wind;
        if (key.Contains("earth") || key.Contains("tierra") || key.Contains("montan")) return Element.Earth;
        if (key.Contains("light") || key.Contains("luz")) return Element.Light;
        if (key.Contains("dark") || key.Contains("oscur")) return Element.Dark;
        return Element.Generic;
    }

    private static CpuParticles2D BuildParticles(Element element, Color fieldColor, Vector2 size)
    {
        var p = new CpuParticles2D
        {
            Position = size / 2f,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = size / 2f,
            Preprocess = 3.0, // ya "lleno" al aparecer, sin esperar a que nazcan
            ColorRamp = FadeInOut()
        };

        switch (element)
        {
            case Element.Fire: // brasas que suben
                p.Amount = 45; p.Lifetime = 2.4;
                p.Direction = Vector2.Up; p.Spread = 20f;
                p.InitialVelocityMin = 20f; p.InitialVelocityMax = 55f;
                p.Gravity = new Vector2(0, -25f);
                p.ScaleAmountMin = 2f; p.ScaleAmountMax = 4.5f;
                p.Color = new Color(1f, 0.6f, 0.2f);
                break;
            case Element.Water: // burbujas lentas
                p.Amount = 30; p.Lifetime = 3.5;
                p.Direction = Vector2.Up; p.Spread = 10f;
                p.InitialVelocityMin = 12f; p.InitialVelocityMax = 30f;
                p.Gravity = Vector2.Zero;
                p.ScaleAmountMin = 3f; p.ScaleAmountMax = 7f;
                p.Color = new Color(0.65f, 0.9f, 1f, 0.6f);
                break;
            case Element.Wind: // rafagas horizontales
                p.Amount = 40; p.Lifetime = 1.6;
                p.Direction = Vector2.Right; p.Spread = 4f;
                p.InitialVelocityMin = 140f; p.InitialVelocityMax = 230f;
                p.Gravity = Vector2.Zero;
                p.ScaleAmountMin = 1.5f; p.ScaleAmountMax = 3f;
                p.Color = new Color(0.85f, 1f, 0.9f, 0.7f);
                break;
            case Element.Earth: // polvo que cae despacio
                p.Amount = 35; p.Lifetime = 3.0;
                p.Direction = Vector2.Down; p.Spread = 25f;
                p.InitialVelocityMin = 8f; p.InitialVelocityMax = 20f;
                p.Gravity = new Vector2(0, 15f);
                p.ScaleAmountMin = 2f; p.ScaleAmountMax = 3.5f;
                p.Color = new Color(0.8f, 0.65f, 0.4f);
                break;
            case Element.Light: // destellos quietos que titilan
                p.Amount = 35; p.Lifetime = 1.8;
                p.Direction = Vector2.Up; p.Spread = 180f;
                p.InitialVelocityMin = 3f; p.InitialVelocityMax = 12f;
                p.Gravity = Vector2.Zero;
                p.ScaleAmountMin = 2f; p.ScaleAmountMax = 5f;
                p.Color = new Color(1f, 0.95f, 0.6f);
                break;
            case Element.Dark: // bruma violeta que baja
                p.Amount = 30; p.Lifetime = 3.2;
                p.Direction = Vector2.Down; p.Spread = 40f;
                p.InitialVelocityMin = 6f; p.InitialVelocityMax = 18f;
                p.Gravity = Vector2.Zero;
                p.ScaleAmountMin = 4f; p.ScaleAmountMax = 8f;
                p.Color = new Color(0.6f, 0.4f, 0.85f, 0.5f);
                break;
            default:
                p.Amount = 30; p.Lifetime = 2.5;
                p.Direction = Vector2.Up; p.Spread = 30f;
                p.InitialVelocityMin = 10f; p.InitialVelocityMax = 25f;
                p.Gravity = Vector2.Zero;
                p.ScaleAmountMin = 2f; p.ScaleAmountMax = 4f;
                p.Color = fieldColor.Lerp(Colors.White, 0.4f);
                break;
        }
        return p;
    }

    /// <summary>Transparencia que entra y sale a lo largo de la vida de cada particula (sin "pops").</summary>
    private static Gradient FadeInOut() => new()
    {
        Colors = new[] { new Color(1, 1, 1, 0), new Color(1, 1, 1, 1), new Color(1, 1, 1, 1), new Color(1, 1, 1, 0) },
        Offsets = new[] { 0f, 0.2f, 0.75f, 1f }
    };
}
