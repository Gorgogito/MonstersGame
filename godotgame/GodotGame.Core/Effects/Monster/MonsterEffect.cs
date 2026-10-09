namespace GodotGame.Core.Effects.Monster;

/// <summary>
/// Una condicion evaluada contra la activacion en curso (ej. "si fue
/// descartada de tu mano por efecto de una carta del adversario"). Ver
/// <see cref="MonsterEffectCatalog.Conditions"/> para las claves disponibles.
/// </summary>
public sealed record StepCondition(string Kind, bool Negate, EffectActionParams Params);

/// <summary>
/// Un paso de un efecto de Monstruo (costo o accion), ejecutado en orden.
/// <see cref="Optional"/> = el texto dice "puedes": quien controla el efecto
/// decide al resolverse si lo aplica. <see cref="Conditions"/> = "y despues,
/// si ..." (todas deben cumplirse para ejecutar el paso).
/// </summary>
public sealed class EffectStep
{
    public string ActionKind { get; }
    public EffectActionParams Params { get; }
    public bool Optional { get; }
    public IReadOnlyList<StepCondition> Conditions { get; }

    public EffectStep(string actionKind, EffectActionParams parameters, bool optional = false, IReadOnlyList<StepCondition>? conditions = null)
    {
        ActionKind = actionKind;
        Params = parameters;
        Optional = optional;
        Conditions = conditions ?? Array.Empty<StepCondition>();
    }
}

/// <summary>
/// Un efecto de una carta de Monstruo, compuesto por datos (autorable desde
/// el editor de cartas): tipo, cuando/desde donde se activa, condiciones,
/// objetivos ("selecciona"), costos y pasos de accion. Un Monstruo puede
/// tener varios (ver <see cref="Entities.MonsterCard.Effects"/>).
/// </summary>
public sealed class MonsterEffect
{
    public MonsterEffectType Type { get; }

    /// <summary>Solo Trigger: el evento que lo dispara. Un efecto de Volteo usa siempre <see cref="EffectEvent.Flipped"/>.</summary>
    public EffectEvent TriggerEvent { get; }

    /// <summary>"Puedes ...": quien lo controla decide si lo activa. Un efecto de Volteo o un Disparado obligatorio se activa siempre.</summary>
    public bool Optional { get; }

    /// <summary>Encendido/Rapido/No clasificado: donde tiene que estar esta carta para activarlo.</summary>
    public EffectZone ActivationZone { get; }

    /// <summary>"Solo puedes usar este efecto de X una vez por turno" (se cuenta por nombre de carta, como en el reglamento).</summary>
    public bool OncePerTurn { get; }

    /// <summary>Texto del efecto tal como aparece en la carta (opcional, solo informativo).</summary>
    public string Text { get; }

    /// <summary>Deben cumplirse todas para poder activar el efecto.</summary>
    public IReadOnlyList<StepCondition> ActivationConditions { get; }

    /// <summary>"Selecciona ...": objetivos elegidos al activar (parametros de <see cref="CardQuery"/>). Null = sin objetivo.</summary>
    public EffectActionParams? Target { get; }

    /// <summary>Costos pagados al activar (ej. descartar esta carta, mostrarla, devolver un monstruo a la mano).</summary>
    public IReadOnlyList<EffectStep> Costs { get; }

    /// <summary>Lo que hace el efecto al resolverse (o, si es Continuo, lo que aplica de forma pasiva).</summary>
    public IReadOnlyList<EffectStep> Steps { get; }

    public MonsterEffect(
        MonsterEffectType type,
        IReadOnlyList<EffectStep> steps,
        EffectEvent triggerEvent = EffectEvent.None,
        bool optional = false,
        EffectZone activationZone = EffectZone.Field,
        bool oncePerTurn = false,
        string text = "",
        IReadOnlyList<StepCondition>? activationConditions = null,
        EffectActionParams? target = null,
        IReadOnlyList<EffectStep>? costs = null)
    {
        Type = type;
        Steps = steps;
        TriggerEvent = type == MonsterEffectType.Flip ? EffectEvent.Flipped : triggerEvent;
        Optional = type is MonsterEffectType.Ignition or MonsterEffectType.Quick or MonsterEffectType.Unclassified || (type == MonsterEffectType.Trigger && optional);
        ActivationZone = activationZone;
        OncePerTurn = oncePerTurn;
        Text = text ?? "";
        ActivationConditions = activationConditions ?? Array.Empty<StepCondition>();
        Target = target;
        Costs = costs ?? Array.Empty<EffectStep>();
    }

    /// <summary>Velocidad de Hechizo con la que entra en la Cadena.</summary>
    public int SpellSpeed => Type == MonsterEffectType.Quick ? 2 : 1;

    /// <summary>Verdadero si se activa por un evento (Disparado o Volteo) en vez de manualmente.</summary>
    public bool IsTriggered => Type is MonsterEffectType.Trigger or MonsterEffectType.Flip;

    /// <summary>Verdadero si el jugador lo activa a mano (Encendido, Rapido o No clasificado).</summary>
    public bool IsManual => Type is MonsterEffectType.Ignition or MonsterEffectType.Quick or MonsterEffectType.Unclassified;
}
