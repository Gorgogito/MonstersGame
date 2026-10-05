namespace MonstersGame.Core.Requirements;

/// <summary>Forma de un <see cref="RequirementSet"/>: huecos con multiplicidad fija, o un umbral agregado.</summary>
public enum RequirementMode
{
    /// <summary>N huecos, cada uno con su propio <see cref="TargetFilter"/> y cantidad min/max. Usado por Fusion.</summary>
    MaterialSlots,

    /// <summary>Un unico filtro; la suma de Nivel de los candidatos elegidos debe alcanzar un minimo. Usado por Ritual.</summary>
    LevelSum
}
