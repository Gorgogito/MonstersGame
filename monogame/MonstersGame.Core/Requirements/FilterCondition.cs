namespace MonstersGame.Core.Requirements;

/// <summary>
/// Una condicion atomica dentro de un grupo AND de un <see cref="TargetFilter"/>.
/// </summary>
/// <param name="Kind">Que se compara.</param>
/// <param name="Negate">Si es verdadero, invierte el resultado de la comparacion.</param>
/// <param name="Value">Valor contra el que se compara (Id, nombre de Tipo, o nombre de enum segun <paramref name="Kind"/>).</param>
public readonly record struct FilterCondition(FilterConditionKind Kind, bool Negate, string Value);
