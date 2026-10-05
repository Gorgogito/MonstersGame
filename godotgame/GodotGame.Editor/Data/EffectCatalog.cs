using GodotGame.Core.Effects;

namespace GodotGame.Editor.Data;

/// <summary>Un EffectId elegible en el combo, con su nombre legible.</summary>
public readonly record struct EffectOption(string Id, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// Nombres legibles para los EffectId de <see cref="EffectRegistry"/>, para
/// que el editor los muestre en un combo en vez de exigir que el usuario
/// escriba el codigo de memoria. La lista de ids validos siempre viene de
/// <see cref="EffectRegistry.RegisteredIds"/> (la misma fuente que usa
/// <c>CardDtoValidator</c>); este catalogo solo le pone nombre a cada uno.
/// </summary>
public static class EffectCatalog
{
    /// <summary>Opcion para "sin efecto" (EffectId vacio), siempre disponible.</summary>
    public static readonly EffectOption None = new(string.Empty, "(Sin efecto)");

    /// <summary>
    /// Sentinela que activa el compositor de efectos en vez de referenciar
    /// uno de los efectos ya registrados en codigo. Nunca es un EffectId real
    /// guardado en <c>Cards.EffectId</c>: al guardar, <c>SqliteCardWriter</c>
    /// lo sustituye por <c>card_{Id}_effect</c>.
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
