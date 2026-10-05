using System.Collections.Generic;

namespace GodotGame.Game;

/// <summary>
/// Estadisticas del jugador en un duelo, las mismas que puntua Forbidden
/// Memories al terminar. Las acumula la pantalla de Duelo mirando eventos y
/// ataques (no el motor: son puramente para la calificacion).
/// </summary>
public sealed class DuelStats
{
    public int Turns { get; set; }
    /// <summary>Ataques tuyos que destruyeron un monstruo o hicieron dano.</summary>
    public int EffectiveAttacks { get; set; }
    /// <summary>Ataques de la CPU que resististe (tu monstruo sobrevivio o destruyo al atacante).</summary>
    public int DefenseWins { get; set; }
    /// <summary>Cartas que colocaste boca abajo (monstruos y Magias/Trampas).</summary>
    public int FaceDownPlays { get; set; }
    public int Fusions { get; set; }
    public int EquipMagic { get; set; }
    /// <summary>Magias que no son de Equipo.</summary>
    public int PureMagic { get; set; }
    public int TrapsActivated { get; set; }
    /// <summary>Cartas que jugaste desde la mano (incluye materiales de Fusion).</summary>
    public int CardsUsed { get; set; }
    public int RemainingLifePoints { get; set; }
}

/// <summary>
/// Calificacion de fin de duelo al estilo Forbidden Memories: se parte de 52
/// puntos, cada estadistica suma o resta segun tramos, y el total da un rango
/// POW (ganaste con fuerza) o TEC (ganaste con tecnica), de S a D.
/// </summary>
public static class DuelRank
{
    public const int BasePoints = 52;

    public sealed record Line(string Label, string Value, int Points);

    public static IReadOnlyList<Line> Breakdown(DuelStats s) => new[]
    {
        new Line("Turnos", s.Turns.ToString(), s.Turns switch { <= 4 => 12, <= 8 => 8, <= 28 => 0, <= 32 => -8, _ => -12 }),
        new Line("Ataques efectivos", s.EffectiveAttacks.ToString(), s.EffectiveAttacks switch { <= 1 => 4, <= 3 => 2, <= 9 => 0, <= 19 => -2, _ => -4 }),
        new Line("Defensas exitosas", s.DefenseWins.ToString(), s.DefenseWins switch { <= 1 => 0, <= 5 => -10, <= 9 => -20, <= 14 => -30, _ => -40 }),
        new Line("Jugadas boca abajo", s.FaceDownPlays.ToString(), s.FaceDownPlays switch { 0 => 0, <= 10 => -2, <= 20 => -4, <= 30 => -6, _ => -8 }),
        new Line("Fusiones", s.Fusions.ToString(), s.Fusions switch { 0 => 4, <= 4 => 0, <= 9 => -4, <= 14 => -8, _ => -12 }),
        new Line("Magias de Equipo", s.EquipMagic.ToString(), s.EquipMagic switch { 0 => 4, <= 4 => 0, <= 9 => -4, <= 14 => -8, _ => -12 }),
        new Line("Magias", s.PureMagic.ToString(), s.PureMagic switch { 0 => 2, <= 3 => -4, <= 6 => -8, <= 9 => -12, _ => -16 }),
        new Line("Trampas", s.TrapsActivated.ToString(), s.TrapsActivated switch { 0 => 2, <= 2 => -8, <= 4 => -16, <= 6 => -24, _ => -32 }),
        new Line("Cartas usadas", s.CardsUsed.ToString(), s.CardsUsed switch { <= 8 => 15, <= 12 => 12, <= 32 => 0, <= 36 => -5, _ => -7 }),
        new Line("LP restantes", s.RemainingLifePoints.ToString(), s.RemainingLifePoints switch { >= 8000 => 6, >= 7000 => 4, >= 1000 => 0, >= 100 => -5, _ => -7 }),
    };

    public static int Points(DuelStats stats) => BasePoints + Breakdown(stats).Sum(l => l.Points);

    /// <summary>Rango por puntos: 90+ S-POW ... 50-59 D-POW, 40-49 D-TEC ... 0-9 S-TEC.</summary>
    public static string RankFor(int points) => points switch
    {
        >= 90 => "S-POW",
        >= 80 => "A-POW",
        >= 70 => "B-POW",
        >= 60 => "C-POW",
        >= 50 => "D-POW",
        >= 40 => "D-TEC",
        >= 30 => "C-TEC",
        >= 20 => "B-TEC",
        >= 10 => "A-TEC",
        _ => "S-TEC"
    };

    /// <summary>Que tan "alto" es el rango para la recompensa (S de cualquier estilo = 4, D = 0).</summary>
    public static int Tier(string rank) => rank[0] switch { 'S' => 4, 'A' => 3, 'B' => 2, 'C' => 1, _ => 0 };
}
