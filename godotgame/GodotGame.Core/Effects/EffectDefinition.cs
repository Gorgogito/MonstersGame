using GodotGame.Core.Requirements;

namespace GodotGame.Core.Effects;

/// <summary>Un paso de accion dentro de un <see cref="EffectDefinition"/>, ejecutado en orden.</summary>
public sealed class EffectActionStepSpec
{
    public string ActionKind { get; }
    public EffectActionParams Params { get; }

    public EffectActionStepSpec(string actionKind, EffectActionParams parameters)
    {
        ActionKind = actionKind;
        Params = parameters;
    }
}

/// <summary>Una condicion previa a ejecutar los pasos de accion.</summary>
public sealed class EffectConditionSpec
{
    public string ConditionKind { get; }
    public EffectActionParams Params { get; }

    public EffectConditionSpec(string conditionKind, EffectActionParams parameters)
    {
        ConditionKind = conditionKind;
        Params = parameters;
    }
}

/// <summary>
/// Que tipo de objetivo requiere el efecto y, opcionalmente, un
/// <see cref="TargetFilter"/> que restrinja que ocupantes de esa zona son
/// objetivo legal (ademas de la validacion propia de la accion, si la tiene:
/// ver <see cref="TargetedCompositeEffectAction"/>).
/// </summary>
public sealed class EffectTargetSpec
{
    public EffectTargetKind TargetKind { get; }
    public TargetFilter? Filter { get; }
    public bool Required { get; }

    public EffectTargetSpec(EffectTargetKind targetKind, TargetFilter? filter, bool required)
    {
        TargetKind = targetKind;
        Filter = filter;
        Required = required;
    }
}

/// <summary>
/// Un efecto de carta compuesto por datos: Trigger + Condiciones + Objetivo +
/// pasos de Accion. Es la unidad autorable desde el editor (Fase 5) sin tocar
/// codigo, siempre que solo combine acciones/condiciones ya existentes en
/// <see cref="EffectActionCatalog"/>/<see cref="EffectConditionRegistry"/>.
/// </summary>
public sealed class EffectDefinition
{
    public string Id { get; }
    public string Name { get; }
    public EffectTrigger Trigger { get; }
    public string VisualProfileKey { get; }
    public bool IsActive { get; }
    public IReadOnlyList<EffectConditionSpec> Conditions { get; }
    public EffectTargetSpec? TargetSpec { get; }
    public IReadOnlyList<EffectActionStepSpec> ActionSteps { get; }

    public EffectDefinition(
        string id,
        string name,
        EffectTrigger trigger,
        string visualProfileKey,
        bool isActive,
        IReadOnlyList<EffectConditionSpec> conditions,
        EffectTargetSpec? targetSpec,
        IReadOnlyList<EffectActionStepSpec> actionSteps)
    {
        Id = id;
        Name = name;
        Trigger = trigger;
        VisualProfileKey = visualProfileKey;
        IsActive = isActive;
        Conditions = conditions;
        TargetSpec = targetSpec;
        ActionSteps = actionSteps;
    }
}
