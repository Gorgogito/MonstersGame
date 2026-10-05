using Godot;
using GodotGame.Core.AI;
using GodotGame.Core.Battle;
using GodotGame.Core.Entities;
using GodotGame.Data;
using GodotGame.Data.Loaders;
using GodotGame.Game;

namespace GodotGame;

/// <summary>
/// Autoload (singleton global de Godot, ver project.godot) que reemplaza al
/// <c>GameContext</c> inyectado de MonoGame: los servicios que de verdad
/// necesitan compartirse entre escenas (el catalogo de datos, el generador
/// aleatorio) viven aca. El resto de lo que tenia GameContext (Primitives,
/// Font, Input, Audio...) lo resuelve Godot nativamente por escena -- no hace
/// falta un service locator para eso en este motor.
///
/// Tambien sirve de "buzon" para pasar el <see cref="DuelEngine"/>/<see cref="IDuelAI"/>
/// recien construidos de Seleccion de Mazo a Duelo: Godot no permite pasar
/// argumentos de constructor al cambiar de escena (<c>ChangeSceneToFile</c>
/// siempre instancia el <c>.tscn</c> desde cero), asi que la escena de origen
/// deja el estado aca antes de cambiar, y la escena destino lo recoge en su
/// propio <c>_Ready()</c>.
/// </summary>
public partial class GameRoot : Node
{
    public GameData Data { get; private set; } = null!;
    public System.Random Random { get; } = new();

    public DuelEngine? PendingEngine;
    public IDuelAI? PendingAi;
    public PlayerSide? PendingWinner;

    /// <summary>Campana (escalera de rivales con desbloqueo) o duelo libre (cualquier rival).</summary>
    public enum PlayMode { Campaign, Free }
    public PlayMode Mode { get; set; } = PlayMode.Free;

    /// <summary>Rival elegido en la seleccion de rival; lo usan Seleccion de Mazo, la presentacion VS, el Duelo y el Resultado.</summary>
    public OpponentProfile? PendingOpponent;

    /// <summary>Mazo con el que juega el jugador en el duelo en curso (para sembrar su coleccion).</summary>
    public DeckDefinition? PendingPlayerDeck;

    /// <summary>Estadisticas del ultimo duelo, para la calificacion en Resultado.</summary>
    public DuelStats? LastDuelStats;

    /// <summary>Progreso persistente (campana, coleccion).</summary>
    public SaveData Save { get; private set; } = new();

    public override void _Ready()
    {
        string dbPath = ProjectSettings.GlobalizePath("res://Data/monstersgame.db");
        Data = GameData.LoadFromDisk(dbPath);
        Save = SaveData.Load();
    }

    /// <summary>Mazo de la base por nombre (sin distinguir mayusculas), o el primero si no existe.</summary>
    public DeckDefinition? DeckByName(string name) =>
        Data.Decks.FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? Data.Decks.FirstOrDefault();
}
