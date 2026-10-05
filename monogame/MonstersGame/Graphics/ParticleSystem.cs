using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonstersGame.Graphics;

/// <summary>
/// Una particula individual: rectangulo de 1x1 reciclado (mismo pixel que usa
/// <see cref="Primitives"/>), sin textura propia -- consistente con el resto
/// del proyecto (<see cref="BitmapFont"/>, <see cref="IconAtlas"/>, <see cref="Theme"/>),
/// que genera toda su presentacion por codigo en vez de depender de assets.
/// </summary>
internal struct Particle
{
    public Vector2 Position;
    public Vector2 Velocity;
    public Vector2 Gravity;
    public Color Color;
    public float Size;
    public float LifeRemaining;
    public float LifeTotal;
}

/// <summary>
/// Parametros declarativos de una emision (una "rafaga"): cuantas particulas,
/// en que rango de velocidad/angulo salen, su color, tamano y vida, y una
/// gravedad opcional. Reutilizable como preset sin necesitar una clase C#
/// nueva por efecto -- la Fase 3 construye <c>ParticleBurstKind</c> encima de
/// esto, mapeando cada valor del enum a un <see cref="ParticleSpec"/>
/// concreto.
/// </summary>
public sealed record ParticleSpec(
    int Count,
    Color Color,
    float MinSpeed,
    float MaxSpeed,
    float MinAngleDegrees,
    float MaxAngleDegrees,
    float MinSize,
    float MaxSize,
    float MinLifeSeconds,
    float MaxLifeSeconds,
    Vector2 Gravity = default);

/// <summary>
/// Sistema de particulas de proposito general: cimiento reutilizable (Fase 1)
/// sin ninguna emision todavia conectada a eventos de juego -- eso llega en
/// la Fase 3, cuando <c>DuelScreen</c> empiece a llamar <see cref="Emit"/> (o
/// el <c>EmitBurst</c> que se agregue encima) al procesar disparos nuevos de
/// <c>EventAnimationController</c>. Vive en <see cref="GameContext"/> como un
/// servicio mas, igual que <see cref="Primitives"/>/<see cref="TextureCache"/>.
/// </summary>
public sealed class ParticleSystem
{
    private readonly List<Particle> _particles = new();
    private readonly Random _random = new();

    /// <summary>Cuantas particulas hay vivas ahora mismo (para diagnostico/tests).</summary>
    public int ActiveCount => _particles.Count;

    /// <summary>
    /// Crea <see cref="ParticleSpec.Count"/> particulas en <paramref name="origin"/>,
    /// cada una con velocidad/tamano/vida elegidos al azar dentro de los
    /// rangos del spec. El angulo se interpreta en grados, 0 = +X, sentido
    /// horario (coordenadas de pantalla, Y crece hacia abajo).
    /// </summary>
    public void Emit(Vector2 origin, ParticleSpec spec)
    {
        for (int i = 0; i < spec.Count; i++)
        {
            float angle = MathHelper.ToRadians(Lerp(spec.MinAngleDegrees, spec.MaxAngleDegrees, (float)_random.NextDouble()));
            float speed = Lerp(spec.MinSpeed, spec.MaxSpeed, (float)_random.NextDouble());
            var velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
            float life = Lerp(spec.MinLifeSeconds, spec.MaxLifeSeconds, (float)_random.NextDouble());

            _particles.Add(new Particle
            {
                Position = origin,
                Velocity = velocity,
                Gravity = spec.Gravity,
                Color = spec.Color,
                Size = Lerp(spec.MinSize, spec.MaxSize, (float)_random.NextDouble()),
                LifeRemaining = life,
                LifeTotal = life
            });
        }
    }

    /// <summary>
    /// Emite la rafaga preconfigurada de <paramref name="kind"/> (Fase 3 del
    /// plan de mejoras visuales) en <paramref name="origin"/>, teñida con
    /// <paramref name="tint"/> (normalmente el mismo <c>FlashColor</c> del
    /// <see cref="VisualProfile"/> que disparo el evento). <see cref="ParticleBurstKind.None"/>
    /// no emite nada -- es el caso de los perfiles que todavia no tienen
    /// rafaga propia (ej. cambio de Campo, modificador de Equipo).
    /// </summary>
    public void EmitBurst(Vector2 origin, ParticleBurstKind kind, Color tint)
    {
        var spec = SpecFor(kind, tint);
        if (spec != null) Emit(origin, spec);
    }

    private static ParticleSpec? SpecFor(ParticleBurstKind kind, Color tint) => kind switch
    {
        ParticleBurstKind.None =>
            null,
        // Destello radial parejo: invocacion normal/volteo.
        ParticleBurstKind.SummonSparkle =>
            new ParticleSpec(Count: 14, Color: tint, MinSpeed: 40, MaxSpeed: 90, MinAngleDegrees: 0, MaxAngleDegrees: 360, MinSize: 2, MaxSize: 4, MinLifeSeconds: 0.3f, MaxLifeSeconds: 0.5f),
        // Haz estrecho hacia arriba: invocacion especial.
        ParticleBurstKind.SummonBeam =>
            new ParticleSpec(Count: 10, Color: tint, MinSpeed: 70, MaxSpeed: 130, MinAngleDegrees: -110, MaxAngleDegrees: -70, MinSize: 2, MaxSize: 5, MinLifeSeconds: 0.35f, MaxLifeSeconds: 0.55f),
        // Fragmentos que salen disparados en todas direcciones y caen: destruccion en batalla.
        ParticleBurstKind.DestroyShatter =>
            new ParticleSpec(Count: 18, Color: tint, MinSpeed: 60, MaxSpeed: 160, MinAngleDegrees: 0, MaxAngleDegrees: 360, MinSize: 2, MaxSize: 4, MinLifeSeconds: 0.25f, MaxLifeSeconds: 0.45f, Gravity: new Vector2(0, 220)),
        // Motas lentas que flotan hacia arriba: destruccion por efecto/costo.
        ParticleBurstKind.DestroyDissolve =>
            new ParticleSpec(Count: 10, Color: tint, MinSpeed: 15, MaxSpeed: 40, MinAngleDegrees: -110, MaxAngleDegrees: -70, MinSize: 2, MaxSize: 4, MinLifeSeconds: 0.5f, MaxLifeSeconds: 0.8f, Gravity: new Vector2(0, -30)),
        // Remolino amplio y mas denso: Fusion.
        ParticleBurstKind.FusionSwirl =>
            new ParticleSpec(Count: 24, Color: tint, MinSpeed: 30, MaxSpeed: 100, MinAngleDegrees: 0, MaxAngleDegrees: 360, MinSize: 2, MaxSize: 5, MinLifeSeconds: 0.4f, MaxLifeSeconds: 0.65f),
        // Pilar ancho hacia arriba: Ritual.
        ParticleBurstKind.RitualPillar =>
            new ParticleSpec(Count: 16, Color: tint, MinSpeed: 60, MaxSpeed: 140, MinAngleDegrees: -115, MaxAngleDegrees: -65, MinSize: 2, MaxSize: 5, MinLifeSeconds: 0.4f, MaxLifeSeconds: 0.65f),
        // Chispazo breve y contenido: activacion de Magia/Trampa.
        ParticleBurstKind.SpellActivate =>
            new ParticleSpec(Count: 8, Color: tint, MinSpeed: 25, MaxSpeed: 55, MinAngleDegrees: 0, MaxAngleDegrees: 360, MinSize: 2, MaxSize: 3, MinLifeSeconds: 0.25f, MaxLifeSeconds: 0.4f),
        // Estallido corto y rapido en el punto de choque (Fase 4, todavia sin disparar).
        ParticleBurstKind.Impact =>
            new ParticleSpec(Count: 16, Color: tint, MinSpeed: 90, MaxSpeed: 180, MinAngleDegrees: 0, MaxAngleDegrees: 360, MinSize: 2, MaxSize: 3, MinLifeSeconds: 0.15f, MaxLifeSeconds: 0.3f),
        _ => null
    };

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>Avanza posicion/velocidad/vida de cada particula y descarta las que ya murieron.</summary>
    public void Update(float dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.LifeRemaining -= dt;
            if (p.LifeRemaining <= 0f)
            {
                _particles.RemoveAt(i);
                continue;
            }

            p.Velocity += p.Gravity * dt;
            p.Position += p.Velocity * dt;
            _particles[i] = p;
        }
    }

    /// <summary>
    /// Dibuja todas las particulas vivas con blend aditivo (para que se vean
    /// como destellos de luz al superponerse, no como rectangulos opacos),
    /// reabriendo el <see cref="SpriteBatch"/> con el mismo patron
    /// End/Begin/.../End/Begin que ya usa <see cref="CardRenderer"/> para
    /// rotar/escalar cartas -- no hace falta un mecanismo nuevo. Si no hay
    /// particulas vivas, no toca el batch en absoluto (evita un End/Begin
    /// de mas en el caso comun de esta fase, en la que todavia nadie emite).
    /// </summary>
    public void Draw(SpriteBatch sb, Primitives primitives)
    {
        if (_particles.Count == 0) return;

        sb.End();
        sb.Begin(blendState: BlendState.Additive, samplerState: SamplerState.PointClamp);
        foreach (var p in _particles)
        {
            float lifeRatio = p.LifeTotal > 0f ? MathHelper.Clamp(p.LifeRemaining / p.LifeTotal, 0f, 1f) : 0f;
            byte alpha = (byte)(lifeRatio * 255f);
            var color = new Color(p.Color.R, p.Color.G, p.Color.B, alpha);
            int half = (int)(p.Size / 2f);
            var rect = new Rectangle((int)p.Position.X - half, (int)p.Position.Y - half, (int)p.Size, (int)p.Size);
            primitives.FillRect(sb, rect, color);
        }
        sb.End();
        sb.Begin(samplerState: SamplerState.PointClamp);
    }
}
