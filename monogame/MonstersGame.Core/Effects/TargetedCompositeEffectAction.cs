using MonstersGame.Core.Battle;
using MonstersGame.Core.Entities;
using MonstersGame.Core.Requirements;

namespace MonstersGame.Core.Effects;

/// <summary>
/// Variante de <see cref="CompositeEffectAction"/> que requiere objetivo. Hay
/// dos formas de validarlo:
///
/// - Delegada: si alguno de los pasos de accion ya es un
///   <see cref="ITargetedEffectAction"/> (caso de los 4 efectos heredados,
///   migrados 1:1), se reutiliza su validacion tal cual, sin reimplementarla
///   -- preserva reglas propias de esa accion (ej. "debe haber Zona libre"
///   en <see cref="SpecialSummonFromOwnGraveyardAction"/>) que un validador
///   generico no conoceria.
/// - Generica: si ningun paso sabe validarse a si mismo (efecto compuesto
///   puramente por datos, sin una accion "objetivo-consciente" detras), se
///   usa <see cref="GenericTargetValidator"/> con la Zona y el
///   <see cref="TargetFilter"/> de la definicion.
/// </summary>
public sealed class TargetedCompositeEffectAction : CompositeEffectAction, ITargetedEffectAction
{
    private readonly ITargetedEffectAction? _delegatedTargetSource;
    private readonly EffectTargetKind _fallbackTargetKind;
    private readonly TargetFilter? _fallbackFilter;

    public TargetedCompositeEffectAction(
        IReadOnlyList<IEffectCondition> conditions,
        IReadOnlyList<IEffectAction> actionSteps,
        ITargetedEffectAction delegatedTargetSource)
        : base(conditions, actionSteps)
    {
        _delegatedTargetSource = delegatedTargetSource;
    }

    public TargetedCompositeEffectAction(
        IReadOnlyList<IEffectCondition> conditions,
        IReadOnlyList<IEffectAction> actionSteps,
        EffectTargetKind fallbackTargetKind,
        TargetFilter? fallbackFilter)
        : base(conditions, actionSteps)
    {
        _fallbackTargetKind = fallbackTargetKind;
        _fallbackFilter = fallbackFilter;
    }

    public EffectTargetKind TargetKind => _delegatedTargetSource?.TargetKind ?? _fallbackTargetKind;

    public bool IsValidTarget(DuelState state, Player controller, EffectTarget target) =>
        _delegatedTargetSource != null
            ? _delegatedTargetSource.IsValidTarget(state, controller, target)
            : GenericTargetValidator.IsValid(state, controller, target, _fallbackTargetKind, _fallbackFilter);
}
