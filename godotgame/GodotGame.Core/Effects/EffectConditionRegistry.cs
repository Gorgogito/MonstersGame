namespace GodotGame.Core.Effects;

/// <summary>
/// Catalogo de condiciones atomicas parametrizables, indexadas por
/// <c>ConditionKind</c> (mismo patron que <see cref="EffectActionCatalog"/>).
/// Ninguno de los 4 efectos heredados usa condiciones, asi que el catalogo
/// empieza vacio: el mecanismo queda listo (Trigger/Target/Action ya son
/// datos) para cuando una carta futura la necesite, sin inventar una
/// condicion de ejemplo que nadie pidio todavia.
/// </summary>
public static class EffectConditionRegistry
{
    private static readonly Dictionary<string, Func<EffectActionParams, IEffectCondition>> Factories = new();

    public static IEffectCondition? Create(string conditionKind, EffectActionParams parameters) =>
        Factories.TryGetValue(conditionKind, out var factory) ? factory(parameters) : null;

    public static IReadOnlyCollection<string> RegisteredKinds => Factories.Keys;
}
