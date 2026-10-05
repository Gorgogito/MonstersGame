using System.Text.Json;

namespace GodotGame.Data.Loaders;

/// <summary>
/// Carga todos los mazos definidos como archivos <c>*.json</c> dentro de una
/// carpeta. Cada archivo describe un mazo (nombre + lista de Ids).
/// </summary>
public sealed class JsonDeckLoader : IDeckLoader
{
    private readonly string _directory;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public JsonDeckLoader(string directory) => _directory = directory;

    public IReadOnlyList<DeckDefinition> LoadDecks()
    {
        var decks = new List<DeckDefinition>();
        if (!Directory.Exists(_directory))
            return decks;

        // Orden alfabetico para una presentacion estable en la UI.
        foreach (var file in Directory.GetFiles(_directory, "*.json").OrderBy(f => f))
        {
            string json = File.ReadAllText(file);
            var dto = JsonSerializer.Deserialize<DeckDto>(json, Options);
            if (dto == null) continue;
            decks.Add(new DeckDefinition(dto.Name, dto.Cards));
        }
        return decks;
    }
}
