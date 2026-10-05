namespace MonstersGame.Core.Effects;

/// <summary>
/// Ejecuta un <see cref="EffectDefinition"/> sin objetivo: evalua sus
/// condiciones (si todas se cumplen) y luego resuelve sus pasos de accion en
/// orden. Ver <see cref="TargetedCompositeEffectAction"/> para la variante con
/// objetivo.
/// </summary>
public class CompositeEffectAction : IEffectAction
{
    private readonly IReadOnlyList<IEffectCondition> _conditions;
    private readonly IReadOnlyList<IEffectAction> _actionSteps;

    public CompositeEffectAction(IReadOnlyList<IEffectCondition> conditions, IReadOnlyList<IEffectAction> actionSteps)
    {
        _conditions = conditions;
        _actionSteps = actionSteps;
    }

    public void Resolve(EffectContext context)
    {
        foreach (var condition in _conditions)
            if (!condition.IsMet(context)) return;

        foreach (var step in _actionSteps)
            step.Resolve(context);
    }
}
