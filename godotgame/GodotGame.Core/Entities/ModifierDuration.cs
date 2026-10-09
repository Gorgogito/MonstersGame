namespace GodotGame.Core.Entities;

/// <summary>
/// Cuanto dura un <see cref="ActiveStatModifier"/>.
///
/// Deliberadamente NO incluye "hasta que ocurra una condicion": esa duracion
/// necesita el motor de condiciones de efectos (<c>Effects.EffectConditionRegistry</c>,
/// hoy vacio) mas un punto del motor que las evalue en cada cambio de estado,
/// ninguno de los cuales existe todavia. Agregarla ahora seria un enum sin
/// implementacion real detras. Se puede sumar como un valor mas de este enum
/// sin romper nada existente cuando ese mecanismo exista.
/// </summary>
public enum ModifierDuration
{
    /// <summary>Dura mientras la Magia de Equipo que lo origino siga en el Campo, equipada a este Monstruo.</summary>
    WhileEquipped,

    /// <summary>Se elimina en la Fase Final del turno en que se aplico.</summary>
    UntilEndOfTurn,

    /// <summary>Se elimina despues de que transcurran <see cref="ActiveStatModifier.RemainingTurns"/> Fases Finales del controlador que lo aplico.</summary>
    ForNTurns,

    /// <summary>Dura mientras el Monstruo siga boca arriba en el Campo (ej. "ese objetivo gana 500 ATK").</summary>
    Permanent
}
