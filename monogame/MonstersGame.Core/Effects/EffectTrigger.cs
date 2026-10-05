namespace MonstersGame.Core.Effects;

/// <summary>
/// En que momento del motor se dispara un <see cref="EffectDefinition"/>. Es
/// un enum en codigo (no texto libre) porque cada valor esta atado a un hook
/// concreto de <see cref="Battle.DuelEngine"/>: agregar un Trigger nuevo exige
/// un hook nuevo en el motor; asociar una definicion existente a un Trigger
/// existente es puro dato.
/// </summary>
public enum EffectTrigger
{
    /// <summary>
    /// Sin hook propio: el motor invoca esta definicion desde el contexto que
    /// ya sabe cuando corresponde (Volteo para un Monstruo de Efecto,
    /// resolucion de Cadena para Magia/Trampa). Es el valor de los 4 efectos
    /// heredados, que hoy funcionan igual en cualquiera de esos contextos.
    /// </summary>
    Any,

    /// <summary>Al resolverse en la Cadena (Magia/Trampa).</summary>
    Activate,

    /// <summary>Al voltearse boca arriba un Monstruo de Efecto.</summary>
    Flip,

    /// <summary>Al equiparse una Magia de Equipo (hook futuro, Fase 3).</summary>
    OnEquip,

    /// <summary>Al activarse una Carta de Campo (hook futuro, Fase 4).</summary>
    OnFieldEnter,

    /// <summary>Al salir del Campo una Carta de Campo (hook futuro, Fase 4).</summary>
    OnFieldExit
}
