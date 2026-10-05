using GodotGame.Core.Entities;

namespace GodotGame.Core.Rules;

/// <summary>
/// Estrellas Guardianas (mecanica de Forbidden Memories): cada Monstruo tiene
/// dos, y al Invocarlo su controlador elige cual usa. En una batalla entre
/// dos Monstruos, si la estrella de uno "vence" a la del otro, el ganador
/// suma <see cref="Bonus"/> a su ATK/DEF solo durante esa batalla.
///
/// Dos ciclos independientes (una estrella de un ciclo nunca vence ni pierde
/// contra una del otro):
/// <list type="bullet">
/// <item>Sol &gt; Luna &gt; Venus &gt; Mercurio &gt; Sol</item>
/// <item>Marte &gt; Jupiter &gt; Saturno &gt; Urano &gt; Pluton &gt; Neptuno &gt; Marte</item>
/// </list>
/// </summary>
public static class GuardianStars
{
    public const int Bonus = 500;

    private static readonly GuardianStar[] InnerCycle =
        { GuardianStar.Sun, GuardianStar.Moon, GuardianStar.Venus, GuardianStar.Mercury };

    private static readonly GuardianStar[] OuterCycle =
        { GuardianStar.Mars, GuardianStar.Jupiter, GuardianStar.Saturn, GuardianStar.Uranus, GuardianStar.Pluto, GuardianStar.Neptune };

    /// <summary>Verdadero si <paramref name="star"/> tiene ventaja sobre <paramref name="other"/> (es la anterior en su mismo ciclo).</summary>
    public static bool Beats(GuardianStar star, GuardianStar other) =>
        BeatsIn(InnerCycle, star, other) || BeatsIn(OuterCycle, star, other);

    private static bool BeatsIn(GuardianStar[] cycle, GuardianStar star, GuardianStar other)
    {
        int i = Array.IndexOf(cycle, star);
        int j = Array.IndexOf(cycle, other);
        return i >= 0 && j >= 0 && (i + 1) % cycle.Length == j;
    }

    /// <summary>
    /// Par de estrellas por defecto segun el Atributo, para cartas que no
    /// tienen las suyas cargadas (ej. catalogos anteriores a esta mecanica).
    /// </summary>
    public static (GuardianStar First, GuardianStar Second) DefaultsFor(MonsterAttribute attribute) => attribute switch
    {
        MonsterAttribute.Light => (GuardianStar.Sun, GuardianStar.Venus),
        MonsterAttribute.Dark => (GuardianStar.Moon, GuardianStar.Pluto),
        MonsterAttribute.Fire => (GuardianStar.Mars, GuardianStar.Sun),
        MonsterAttribute.Water => (GuardianStar.Neptune, GuardianStar.Mercury),
        MonsterAttribute.Wind => (GuardianStar.Jupiter, GuardianStar.Venus),
        MonsterAttribute.Earth => (GuardianStar.Saturn, GuardianStar.Uranus),
        MonsterAttribute.Divine => (GuardianStar.Sun, GuardianStar.Jupiter),
        _ => (GuardianStar.Sun, GuardianStar.Moon)
    };

    /// <summary>
    /// Bonos de estrella para una batalla entre dos Monstruos: +<see cref="Bonus"/>
    /// para el que tenga ventaja, 0 para el otro (como mucho uno de los dos la
    /// tiene, porque cada ciclo tiene al menos 4 estrellas).
    /// </summary>
    public static (int AttackerBonus, int DefenderBonus) BattleBonuses(CardInstance attacker, CardInstance defender)
    {
        if (Beats(attacker.GuardianStar, defender.GuardianStar)) return (Bonus, 0);
        if (Beats(defender.GuardianStar, attacker.GuardianStar)) return (0, Bonus);
        return (0, 0);
    }

    /// <summary>
    /// La estrella de <paramref name="card"/> que mas conviene contra los
    /// Monstruos rivales boca arriba: la que vence a mas de ellos menos las
    /// que pierden contra ellos. Empate: la primera. La usa la IA.
    /// </summary>
    public static GuardianStar BestAgainst(MonsterCard card, IEnumerable<CardInstance> opponents)
    {
        var visible = opponents.Where(o => o.IsFaceUp).Select(o => o.GuardianStar).ToList();
        int Score(GuardianStar star) => visible.Count(o => Beats(star, o)) - visible.Count(o => Beats(o, star));
        return Score(card.GuardianStar2) > Score(card.GuardianStar1) ? card.GuardianStar2 : card.GuardianStar1;
    }

    /// <summary>Nombre en castellano para mostrar.</summary>
    public static string DisplayName(GuardianStar star) => star switch
    {
        GuardianStar.Sun => "Sol",
        GuardianStar.Moon => "Luna",
        GuardianStar.Venus => "Venus",
        GuardianStar.Mercury => "Mercurio",
        GuardianStar.Mars => "Marte",
        GuardianStar.Jupiter => "Jupiter",
        GuardianStar.Saturn => "Saturno",
        GuardianStar.Uranus => "Urano",
        GuardianStar.Pluto => "Pluton",
        GuardianStar.Neptune => "Neptuno",
        _ => star.ToString()
    };

    /// <summary>La estrella a la que <paramref name="star"/> vence (la siguiente de su ciclo).</summary>
    public static GuardianStar BeatsWhich(GuardianStar star)
    {
        var cycle = Array.IndexOf(InnerCycle, star) >= 0 ? InnerCycle : OuterCycle;
        return cycle[(Array.IndexOf(cycle, star) + 1) % cycle.Length];
    }
}
