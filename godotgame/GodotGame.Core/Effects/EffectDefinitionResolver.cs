namespace GodotGame.Core.Effects;

/// <summary>
/// Punto unico que <see cref="Battle.DuelEngine"/> consulta para resolver un
/// <c>EffectId</c> a la accion ejecutable correspondiente (unico "seam" que
/// el motor conoce: antes llamaba a <see cref="EffectRegistry"/> directo).
///
/// Arranca con los 4 efectos heredados ya definidos (<see cref="BuildDefaults"/>),
/// para que Core siga siendo autosuficiente sin depender de una base de datos
/// cargada -- exactamente igual que <see cref="EffectRegistry"/> hoy, asi que
/// ningun test o consumidor que no cargue datos externos deja de funcionar.
/// <see cref="Load"/> permite que la capa de datos (SQLite) sustituya/agregue
/// definiciones reales una vez cargadas.
/// </summary>
public static class EffectDefinitionResolver
{
    private static Dictionary<string, EffectDefinition> _definitions = BuildDefaults();

    /// <summary>
    /// Reemplaza el catalogo activo por los valores por defecto mas las
    /// definiciones dadas (una definicion con el mismo Id que un valor por
    /// defecto lo sustituye). Pensado para llamarse una vez al cargar el
    /// juego desde datos (ver <c>GameData.LoadFromDisk</c>).
    /// </summary>
    public static void Load(IEnumerable<EffectDefinition> definitions)
    {
        var merged = BuildDefaults();
        foreach (var definition in definitions)
            merged[definition.Id] = definition;
        _definitions = merged;
    }

    /// <summary>Vuelve al catalogo por defecto (util para aislar pruebas entre si).</summary>
    public static void ResetToDefaults() => _definitions = BuildDefaults();

    public static IEffectAction? Get(string effectId)
    {
        if (string.IsNullOrEmpty(effectId)) return null;
        if (!_definitions.TryGetValue(effectId, out var definition) || !definition.IsActive) return null;
        return Build(definition);
    }

    public static IReadOnlyCollection<string> RegisteredIds => _definitions.Keys;

    /// <summary>
    /// Devuelve la definicion cruda (no la accion construida) para
    /// <paramref name="effectId"/>, o null si no existe. Pensado para que la
    /// capa de presentacion lea metadatos como <see cref="EffectDefinition.VisualProfileKey"/>
    /// sin tener que reconstruir/ejecutar la accion.
    /// </summary>
    public static EffectDefinition? GetDefinition(string effectId) =>
        string.IsNullOrEmpty(effectId) ? null : _definitions.GetValueOrDefault(effectId);

    private static IEffectAction? Build(EffectDefinition definition)
    {
        var conditions = new List<IEffectCondition>();
        foreach (var spec in definition.Conditions)
        {
            var condition = EffectConditionRegistry.Create(spec.ConditionKind, spec.Params);
            if (condition != null) conditions.Add(condition);
        }

        var actionSteps = new List<IEffectAction>();
        foreach (var spec in definition.ActionSteps)
        {
            var action = EffectActionCatalog.Create(spec.ActionKind, spec.Params);
            if (action != null) actionSteps.Add(action);
        }
        if (actionSteps.Count == 0) return null;

        if (definition.TargetSpec == null)
            return new CompositeEffectAction(conditions, actionSteps);

        var delegatedTargetSource = actionSteps.OfType<ITargetedEffectAction>().FirstOrDefault();
        return delegatedTargetSource != null
            ? new TargetedCompositeEffectAction(conditions, actionSteps, delegatedTargetSource)
            : new TargetedCompositeEffectAction(conditions, actionSteps, definition.TargetSpec.TargetKind, definition.TargetSpec.Filter);
    }

    private static Dictionary<string, EffectDefinition> BuildDefaults()
    {
        var oneCount = new EffectActionParams(new Dictionary<string, string> { ["Count"] = "1" });

        var definitions = new EffectDefinition[]
        {
            new("draw_1", "Robar 1 carta", EffectTrigger.Any, "", isActive: true,
                conditions: Array.Empty<EffectConditionSpec>(),
                targetSpec: null,
                actionSteps: new[] { new EffectActionStepSpec("draw_card", oneCount) }),

            new("destroy_target_monster", "Destruir monstruo objetivo", EffectTrigger.Any, "", isActive: true,
                conditions: Array.Empty<EffectConditionSpec>(),
                targetSpec: new EffectTargetSpec(EffectTargetKind.MonsterZone, filter: null, required: true),
                actionSteps: new[] { new EffectActionStepSpec("destroy_target_monster", EffectActionParams.Empty) }),

            new("special_summon_from_own_graveyard", "Invocar de Modo Especial desde el Cementerio", EffectTrigger.Any, "", isActive: true,
                conditions: Array.Empty<EffectConditionSpec>(),
                targetSpec: new EffectTargetSpec(EffectTargetKind.OwnGraveyard, filter: null, required: true),
                actionSteps: new[] { new EffectActionStepSpec("special_summon_from_own_graveyard", EffectActionParams.Empty) }),

            new("negate_activation", "Negar activacion", EffectTrigger.Any, "", isActive: true,
                conditions: Array.Empty<EffectConditionSpec>(),
                targetSpec: null,
                actionSteps: new[] { new EffectActionStepSpec("negate_activation", EffectActionParams.Empty) }),
        };

        return definitions.ToDictionary(d => d.Id);
    }
}
