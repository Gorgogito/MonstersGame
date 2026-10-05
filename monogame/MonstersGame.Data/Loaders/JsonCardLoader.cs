using System.Text.Json;
using MonstersGame.Core.Entities;

namespace MonstersGame.Data.Loaders;

/// <summary>
/// Carga cartas desde una carpeta con un archivo JSON por carta (cada archivo
/// describe una sola carta, no un array). Un archivo por carta hace que el
/// editor pueda Guardar/Eliminar una carta tocando un unico archivo, y que los
/// diffs de control de versiones sean por carta, no por catalogo entero.
/// </summary>
public sealed class JsonCardLoader : ICardLoader
{
    private readonly string _directory;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public JsonCardLoader(string directory) => _directory = directory;

    public IReadOnlyList<Card> LoadCards()
    {
        var cards = new List<Card>();
        if (!Directory.Exists(_directory))
            return cards;

        foreach (var file in Directory.GetFiles(_directory, "*.json").OrderBy(f => f))
        {
            string json = File.ReadAllText(file);
            var dto = JsonSerializer.Deserialize<CardDto>(json, Options);
            if (dto == null) continue;
            cards.Add(CardDtoMapper.ToCard(dto));
        }
        return cards;
    }
}
