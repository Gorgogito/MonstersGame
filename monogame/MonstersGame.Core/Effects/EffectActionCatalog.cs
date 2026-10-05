namespace MonstersGame.Core.Effects;

/// <summary>
/// Catalogo de acciones atomicas parametrizables, indexadas por una clave de
/// texto estable (<c>ActionKind</c>, ej. <c>"draw_card"</c>). Es el catalogo
/// de "acciones extensibles en codigo" del sistema hibrido de efectos: agregar
/// una mecanica nueva exige una clase <see cref="IEffectAction"/> nueva
/// registrada aqui, pero componer efectos a partir de las ya existentes (con
/// sus parametros) es puro dato (<see cref="EffectDefinition"/>).
///
/// Separado de <see cref="EffectRegistry"/> a proposito: ese sigue siendo el
/// catalogo legacy de EffectId completos que usan el editor y el validador de
/// cartas (<c>CardDtoValidator</c>) hoy, sin tocar. Este catalogo alimenta
/// unicamente a <see cref="EffectDefinitionResolver"/>.
/// </summary>
public static class EffectActionCatalog
{
    private static readonly Dictionary<string, Func<EffectActionParams, IEffectAction>> Factories = new()
    {
        ["draw_card"] = p => new DrawCardAction { Count = p.GetInt("Count", fallback: 1) },
        ["destroy_target_monster"] = _ => new DestroyTargetMonsterAction(),
        ["special_summon_from_own_graveyard"] = _ => new SpecialSummonFromOwnGraveyardAction(),
        ["negate_activation"] = _ => new NegateActivationAction(),
    };

    public static IEffectAction? Create(string actionKind, EffectActionParams parameters) =>
        Factories.TryGetValue(actionKind, out var factory) ? factory(parameters) : null;

    public static IReadOnlyCollection<string> RegisteredKinds => Factories.Keys;
}
