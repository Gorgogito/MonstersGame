using Godot;
using System.Collections.Generic;

namespace GodotGame.Game;

/// <summary>
/// Un duelista rival: quien es, con que mazo juega (por nombre, de la tabla
/// <c>Decks</c>), como se ve su retrato generado (color y peinado; ver
/// <see cref="Graphics.PortraitView"/>) y que dice en cada momento del duelo.
/// Personajes originales del juego.
/// </summary>
public sealed record OpponentProfile(
    string Id,
    string Name,
    string Title,
    string DeckName,
    Color Color,
    int HairStyle,
    int StartingLifePoints,
    string Intro,
    string[] OnAttack,
    string[] OnLoseMonster,
    string[] OnSummonStrong,
    string LowLifePoints,
    string OnWin,
    string OnLose);

/// <summary>Elenco de rivales, en el orden de la campana (el primero arranca desbloqueado).</summary>
public static class Opponents
{
    public static readonly IReadOnlyList<OpponentProfile> Roster = new[]
    {
        new OpponentProfile(
            "kael", "Kael", "Aprendiz del Torneo", "Mazo de la CPU",
            new Color(0.35f, 0.7f, 0.95f), HairStyle: 0, StartingLifePoints: 8000,
            Intro: "¡Es mi primer duelo oficial! No pienso ponértelo fácil.",
            OnAttack: new[] { "¡Ahi va!", "¡Toma esto!", "¡Ataque!" },
            OnLoseMonster: new[] { "¡No, mi monstruo!", "Uf... eso dolió." },
            OnSummonStrong: new[] { "¡Mira lo que acabo de invocar!" },
            LowLifePoints: "Todavía... ¡todavía puedo dar vuelta esto!",
            OnWin: "¡Gané! ¡No lo puedo creer!",
            OnLose: "Perdí... pero aprendí mucho. ¡Gracias por el duelo!"),

        new OpponentProfile(
            "rhea", "Rhea", "Capitana de la Legión", "Legion de Guerreros",
            new Color(0.85f, 0.35f, 0.3f), HairStyle: 1, StartingLifePoints: 8000,
            Intro: "Mi legión nunca retrocede. Formación... ¡en marcha!",
            OnAttack: new[] { "¡Carguen!", "¡Adelante, guerreros!", "¡Sin piedad!" },
            OnLoseMonster: new[] { "Un soldado caído no detiene a la legión.", "¡Reagrúpense!" },
            OnSummonStrong: new[] { "Mi campeón entra al campo de batalla." },
            LowLifePoints: "Una capitana pelea hasta el último aliento.",
            OnWin: "La disciplina siempre vence.",
            OnLose: "Peleaste con honor. Mi legión te saluda."),

        new OpponentProfile(
            "draco", "Draconis", "Domador de Dragones", "Furia de Dragones",
            new Color(0.95f, 0.6f, 0.2f), HairStyle: 2, StartingLifePoints: 8000,
            Intro: "Los dragones responden solo a quien no les teme. ¿Y tú?",
            OnAttack: new[] { "¡Fuego del cielo!", "¡Ruge, dragón!", "¡Arde!" },
            OnLoseMonster: new[] { "Ese dragón era joven todavía...", "¡Grrr!" },
            OnSummonStrong: new[] { "¡Contempla el poder de un dragón ancestral!" },
            LowLifePoints: "Un dragón herido es el más peligroso.",
            OnWin: "Cenizas. Eso es lo que queda de tu estrategia.",
            OnLose: "Has domado a mis dragones... Increíble."),

        new OpponentProfile(
            "azir", "Azir", "Sumo Sacerdote", "Ultimatum Cards",
            new Color(0.75f, 0.6f, 0.25f), HairStyle: 3, StartingLifePoints: 8000,
            Intro: "Las estrellas ya escribieron el final de este duelo.",
            OnAttack: new[] { "Así está escrito.", "El destino golpea.", "Inevitable." },
            OnLoseMonster: new[] { "Un sacrificio necesario.", "Las estrellas se movieron..." },
            OnSummonStrong: new[] { "Del otro lado del velo... ¡ven a mí!" },
            LowLifePoints: "¿Las estrellas... se equivocaron?",
            OnWin: "Tal como fue profetizado.",
            OnLose: "Has reescrito el destino. Nadie lo había hecho."),

        new OpponentProfile(
            "nyx", "Nyx", "Reina del Eclipse", "Ultimatum Cards",
            new Color(0.6f, 0.35f, 0.9f), HairStyle: 4, StartingLifePoints: 10000,
            Intro: "Llegaste lejos. Aquí termina tu camino: bienvenido al eclipse.",
            OnAttack: new[] { "Oscuridad.", "Que la sombra te cubra.", "Arrodíllate." },
            OnLoseMonster: new[] { "Interesante...", "Solo era una sombra." },
            OnSummonStrong: new[] { "Contempla mi verdadero poder." },
            LowLifePoints: "Por fin... alguien digno.",
            OnWin: "El eclipse es eterno.",
            OnLose: "La luz vuelve... Eres el nuevo campeón."),
    };

    public static OpponentProfile? Find(string id) => Roster.FirstOrDefault(o => o.Id == id);

    public static string Pick(string[] lines) => lines.Length == 0 ? "" : lines[System.Random.Shared.Next(lines.Length)];
}
