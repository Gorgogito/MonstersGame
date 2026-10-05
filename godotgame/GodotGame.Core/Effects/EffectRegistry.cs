namespace GodotGame.Core.Effects;

/// <summary>
/// Vinculo entre el <c>EffectId</c> de una carta y su implementacion en
/// codigo. Es la "via de escape" descrita en el analisis de adaptacion
/// (seccion 7): mientras no exista un formato declarativo en JSON para
/// componer efectos (ligado al futuro editor de cartas, Bloque 6), el
/// catalogo real de efectos se registra aqui.
/// </summary>
public static class EffectRegistry
{
    private static readonly Dictionary<string, IEffectAction> Effects = new()
    {
        ["draw_1"] = new DrawCardAction { Count = 1 },
        ["destroy_target_monster"] = new DestroyTargetMonsterAction(),
        ["special_summon_from_own_graveyard"] = new SpecialSummonFromOwnGraveyardAction(),
        ["negate_activation"] = new NegateActivationAction(),
    };

    /// <summary>Devuelve la accion registrada para <paramref name="effectId"/>, o null si no hay ninguna (carta sin efecto, o efecto todavia no implementado).</summary>
    public static IEffectAction? Get(string effectId) =>
        string.IsNullOrEmpty(effectId) ? null : Effects.GetValueOrDefault(effectId);

    /// <summary>Todos los EffectId validos y con implementacion real (fuente de verdad para validacion y para el editor de cartas).</summary>
    public static IReadOnlyCollection<string> RegisteredIds => Effects.Keys;
}
