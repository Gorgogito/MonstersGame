using GodotGame.Core.Entities;

namespace GodotGame.Core.Effects.Monster;

/// <summary>
/// Nombres y explicaciones de los subtipos de Magia/Trampa, y que tipos de
/// efecto por datos admite cada clase de carta. Lo comparten el editor (para
/// explicar cada subtipo y ofrecer solo los tipos de efecto que tienen
/// sentido) y el validador de cartas.
/// </summary>
public static class SpellTrapCatalog
{
    public static readonly IReadOnlyList<EnumLabel<SpellSubType>> SpellSubTypes = new EnumLabel<SpellSubType>[]
    {
        new(SpellSubType.Normal, "Normal",
            "Se activa desde la mano (o Colocada) en tu Main Phase, se resuelve y va al Cementerio. Velocidad de Hechizo 1."),
        new(SpellSubType.Continuous, "Continua",
            "Se queda boca arriba en el Campo después de activarse y aplica su efecto de forma constante (efectos Continuos) mientras siga ahí. También puede tener efectos de Encendido o Disparados desde el Campo."),
        new(SpellSubType.Equip, "De Equipo",
            "Se coloca sobre un monstruo concreto al activarse (eliges el objetivo) para modificar su ATK/DEF u otorgarle reglas (efectos Continuos \"al monstruo equipado\"). Si ese monstruo deja el Campo, va al Cementerio."),
        new(SpellSubType.Field, "De Campo",
            "Se activa en la Zona del Campo (reemplaza a la anterior) y modifica todo el tablero para ambos jugadores mientras siga ahí (efectos Continuos con \"De quién = De cualquier jugador\")."),
        new(SpellSubType.QuickPlay, "De Juego Rápido",
            "Velocidad de Hechizo 2: desde la mano solo en tu turno (incluso en la Battle Phase o respondiendo en una Cadena). Si la Colocas, desde el turno siguiente puedes usarla también en el turno rival (al responder a una Cadena o cuando te declaran un ataque)."),
        new(SpellSubType.Ritual, "De Ritual",
            "Sirve para Invocar por Ritual a su Monstruo de Ritual (botón INVOCAR RITUAL) Sacrificando monstruos cuyos Niveles sumen lo indicado. Va al Cementerio después."),
    };

    public static readonly IReadOnlyList<EnumLabel<TrapSubType>> TrapSubTypes = new EnumLabel<TrapSubType>[]
    {
        new(TrapSubType.Normal, "Normal",
            "Se Coloca y, desde el turno siguiente, se activa (Velocidad 2) en cualquier turno: respondiendo a una Cadena o cuando te declaran un ataque. Se resuelve y va al Cementerio."),
        new(TrapSubType.Continuous, "Continua",
            "Como la Normal, pero se queda boca arriba en el Campo y aplica sus efectos Continuos mientras siga ahí."),
        new(TrapSubType.Counter, "De Contraefecto",
            "Velocidad de Hechizo 3: solo puede responderle otra de Contraefecto. Suele negar activaciones."),
    };

    public static string SpellSubTypeLabel(SpellSubType subType) => SpellSubTypes.First(s => s.Value == subType).Label;
    public static string TrapSubTypeLabel(TrapSubType subType) => TrapSubTypes.First(s => s.Value == subType).Label;

    /// <summary>Tipos de efecto por datos que tienen sentido para cada clase de carta.</summary>
    public static IReadOnlyList<MonsterEffectType> AllowedTypes(CardKind kind) => kind switch
    {
        CardKind.Monster => new[]
        {
            MonsterEffectType.Continuous, MonsterEffectType.Ignition, MonsterEffectType.Trigger,
            MonsterEffectType.Quick, MonsterEffectType.Flip, MonsterEffectType.Unclassified
        },
        _ => new[]
        {
            MonsterEffectType.Activation, MonsterEffectType.Continuous, MonsterEffectType.Ignition,
            MonsterEffectType.Trigger, MonsterEffectType.Quick
        }
    };
}
