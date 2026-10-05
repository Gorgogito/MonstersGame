using MonstersGame.Core.Effects;

namespace MonstersGame.CardEditor;

/// <summary>Un EffectId elegible en el combo, con su nombre legible.</summary>
public readonly record struct EffectOption(string Id, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// Nombres legibles para los EffectId de <see cref="EffectRegistry"/>, para que
/// el editor los muestre en un combo en vez de exigir que el usuario escriba el
/// codigo de memoria. La lista de ids validos siempre viene de
/// <see cref="EffectRegistry.RegisteredIds"/> (la fuente de verdad, la misma
/// que usa <c>CardDtoValidator</c>); este catalogo solo le pone nombre a cada
/// uno y cae de vuelta al propio id si alguno todavia no tiene nombre asignado.
/// </summary>
public static class EffectCatalog
{
    /// <summary>Opcion para "sin efecto" (EffectId vacio), siempre disponible.</summary>
    public static readonly EffectOption None = new(string.Empty, "(Sin efecto)");

    /// <summary>
    /// Sentinela que activa el panel "Componer efecto" (Fase 5) en vez de
    /// referenciar uno de los efectos ya registrados en codigo. Nunca es un
    /// EffectId real guardado en <c>Cards.EffectId</c>: al guardar,
    /// <c>SqliteCardWriter</c> lo sustituye por <c>card_{Id}_effect</c>.
    /// </summary>
    public static readonly EffectOption ComposeNew = new("__compose__", "★ Componer efecto nuevo para esta carta...");

    private static readonly Dictionary<string, string> FriendlyNames = new()
    {
        ["draw_1"] = "Robar 1 carta",
        ["destroy_target_monster"] = "Destruir 1 monstruo objetivo",
        ["special_summon_from_own_graveyard"] = "Invocar de Modo Especial desde tu Cementerio",
        ["negate_activation"] = "Negar la activacion (Contraefecto)",
    };

    public static IReadOnlyList<EffectOption> Options { get; } = BuildOptions();

    private static IReadOnlyList<EffectOption> BuildOptions()
    {
        var options = new List<EffectOption> { None };
        foreach (string id in EffectRegistry.RegisteredIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            string label = FriendlyNames.TryGetValue(id, out string? friendly) ? $"{friendly} ({id})" : id;
            options.Add(new EffectOption(id, label));
        }
        options.Add(ComposeNew);
        return options;
    }
}
