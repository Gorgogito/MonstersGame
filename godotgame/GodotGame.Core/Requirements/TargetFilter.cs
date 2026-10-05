namespace GodotGame.Core.Requirements;

/// <summary>
/// Predicado reutilizable sobre <see cref="Entities.MonsterCard"/>, en forma
/// normal disyuntiva: una lista de grupos donde basta con que UNO de los
/// grupos cumpla TODAS sus condiciones (OR de grupos, AND dentro del grupo).
///
/// El mismo predicado se reutiliza sin cambios para materiales de Fusion,
/// requisitos de Ritual, objetivos permitidos de Equip y monstruos afectados
/// por una Carta de Campo (ver analisis de evolucion del sistema de cartas).
/// </summary>
public sealed class TargetFilter
{
    public int Id { get; }

    /// <summary>Grupos OR; cada grupo es una lista AND de condiciones.</summary>
    public IReadOnlyList<IReadOnlyList<FilterCondition>> OrGroups { get; }

    public TargetFilter(int id, IReadOnlyList<IReadOnlyList<FilterCondition>> orGroups)
    {
        Id = id;
        OrGroups = orGroups;
    }

    /// <summary>Filtro sin ninguna condicion: coincide con cualquier monstruo (ver <see cref="TargetFilterEvaluator"/>).</summary>
    public static readonly TargetFilter Any = new(0, Array.Empty<IReadOnlyList<FilterCondition>>());
}
