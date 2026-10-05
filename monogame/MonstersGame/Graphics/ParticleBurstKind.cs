namespace MonstersGame.Graphics;

/// <summary>
/// Forma de rafaga de particulas asociada a un <see cref="VisualProfile"/>
/// (Fase 3 del plan de mejoras visuales): nombra el LENGUAJE visual general
/// del evento (destello para invocacion normal, haz/pilar para especial o
/// Ritual, fragmentacion para destruccion en batalla, disolucion para
/// destruccion por efecto/costo) sin copiar ningun asset concreto -- cada
/// valor se mapea a un <see cref="ParticleSpec"/> de codigo en
/// <see cref="ParticleSystem.EmitBurst"/>.
/// </summary>
public enum ParticleBurstKind
{
    None,
    SummonSparkle,
    SummonBeam,
    DestroyShatter,
    DestroyDissolve,
    FusionSwirl,
    RitualPillar,
    SpellActivate,
    Impact
}
