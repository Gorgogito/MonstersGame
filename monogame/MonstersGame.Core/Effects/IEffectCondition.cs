namespace MonstersGame.Core.Effects;

/// <summary>
/// Una condicion evaluada antes de ejecutar los pasos de accion de un
/// <see cref="EffectDefinition"/> (patron Strategy, simetrico a <see cref="IEffectAction"/>).
/// </summary>
public interface IEffectCondition
{
    bool IsMet(EffectContext context);
}
