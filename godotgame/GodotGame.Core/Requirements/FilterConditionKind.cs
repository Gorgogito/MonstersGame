namespace GodotGame.Core.Requirements;

/// <summary>Que aspecto de un <see cref="Entities.MonsterCard"/> evalua una <see cref="FilterCondition"/>.</summary>
public enum FilterConditionKind
{
    /// <summary>Sin restriccion: cualquier monstruo cumple. Se usa como condicion explicita dentro de un grupo, no como filtro vacio.</summary>
    Any,

    /// <summary>Coincide solo con la carta cuyo Id es exactamente <see cref="FilterCondition.Value"/>.</summary>
    SpecificCard,

    /// <summary>Coincide si <see cref="Entities.MonsterCard.Type"/> es igual (sin distinguir mayusculas) a <see cref="FilterCondition.Value"/>.</summary>
    Type,

    /// <summary>Coincide si <see cref="Entities.MonsterCard.Category"/> es igual al valor de enum nombrado en <see cref="FilterCondition.Value"/>.</summary>
    Category,

    /// <summary>Coincide si <see cref="Entities.MonsterCard.Attribute"/> es igual al valor de enum nombrado en <see cref="FilterCondition.Value"/>.</summary>
    Attribute,

    /// <summary>Coincide segun de quien es el monstruo candidato relativo al dueno del filtro: "Owner" u "Opponent".</summary>
    ControllerSide
}
