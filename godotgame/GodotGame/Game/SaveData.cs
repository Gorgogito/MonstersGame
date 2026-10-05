using Godot;
using System.Collections.Generic;
using System.Text.Json;

namespace GodotGame.Game;

/// <summary>
/// Progreso persistente del jugador, guardado como JSON en <c>user://save.json</c>:
/// rivales vencidos en la campana, cartas obtenidas (Id -> copias) y
/// estadisticas generales. Si el archivo falta o esta corrupto, se empieza
/// de cero: perder el progreso nunca debe romper el juego.
/// </summary>
public sealed class SaveData
{
    private const string SavePath = "user://save.json";

    public List<string> BeatenOpponents { get; set; } = new();
    public Dictionary<int, int> Collection { get; set; } = new();
    public int Wins { get; set; }
    public int Losses { get; set; }

    public bool HasBeaten(string opponentId) => BeatenOpponents.Contains(opponentId);

    /// <summary>En la campana, un rival esta disponible si es el primero o si el anterior ya fue vencido.</summary>
    public bool IsUnlocked(OpponentProfile opponent)
    {
        int index = Opponents.Roster.ToList().IndexOf(opponent);
        return index <= 0 || HasBeaten(Opponents.Roster[index - 1].Id);
    }

    public void AddCard(int cardId, int copies = 1) =>
        Collection[cardId] = Collection.GetValueOrDefault(cardId) + copies;

    public static SaveData Load()
    {
        try
        {
            string path = ProjectSettings.GlobalizePath(SavePath);
            if (File.Exists(path))
                return JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path)) ?? new SaveData();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            GD.PushWarning($"No se pudo leer el progreso guardado ({e.Message}); se empieza de cero.");
        }
        return new SaveData();
    }

    public void Save()
    {
        try
        {
            string path = ProjectSettings.GlobalizePath(SavePath);
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"No se pudo guardar el progreso: {e.Message}");
        }
    }
}
