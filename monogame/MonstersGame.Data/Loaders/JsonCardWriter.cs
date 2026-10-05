using System.Text;
using System.Text.Json;

namespace MonstersGame.Data.Loaders;

/// <summary>
/// Guarda y elimina cartas individuales en la misma carpeta que lee
/// <see cref="JsonCardLoader"/> (un archivo por carta, nombrado
/// <c>&lt;id&gt;__&lt;slug&gt;.json</c>). Es el lado de escritura que usa el
/// editor de cartas; el juego solo lee.
/// </summary>
public sealed class JsonCardWriter
{
    private readonly string _directory;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public JsonCardWriter(string directory) => _directory = directory;

    /// <summary>
    /// Lee todas las cartas de la carpeta como DTOs "en crudo" (sin mapear a
    /// las entidades de dominio): es la forma que necesita el editor para
    /// mostrar y modificar cada campo. Para jugar, el juego usa
    /// <see cref="JsonCardLoader"/> en su lugar.
    /// </summary>
    public List<CardDto> LoadAllDtos()
    {
        var result = new List<CardDto>();
        if (!Directory.Exists(_directory)) return result;

        foreach (var file in Directory.GetFiles(_directory, "*.json").OrderBy(f => f))
        {
            string json = File.ReadAllText(file);
            var dto = JsonSerializer.Deserialize<CardDto>(json, Options);
            if (dto != null) result.Add(dto);
        }
        return result;
    }

    /// <summary>
    /// Guarda <paramref name="dto"/> en su propio archivo. Si la carta ya
    /// existia con un nombre distinto (el slug cambio), el archivo anterior
    /// se elimina para no dejar copias huerfanas del mismo Id.
    /// </summary>
    public void SaveCard(CardDto dto)
    {
        Directory.CreateDirectory(_directory);

        var previous = FindExistingFile(dto.Id);
        string targetPath = Path.Combine(_directory, FileName(dto));

        string json = JsonSerializer.Serialize(dto, Options);
        File.WriteAllText(targetPath, json);

        if (previous != null && !string.Equals(previous, targetPath, StringComparison.OrdinalIgnoreCase))
            File.Delete(previous);
    }

    /// <summary>Elimina el archivo de la carta con el Id indicado, si existe.</summary>
    public void DeleteCard(int id)
    {
        var existing = FindExistingFile(id);
        if (existing != null) File.Delete(existing);
    }

    /// <summary>Busca el archivo actual de una carta por Id (sin importar como este el slug ahora).</summary>
    private string? FindExistingFile(int id)
    {
        if (!Directory.Exists(_directory)) return null;
        string prefix = id + "__";
        return Directory.GetFiles(_directory, "*.json")
            .FirstOrDefault(f => Path.GetFileName(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string FileName(CardDto dto) => $"{dto.Id}__{Slugify(dto.Name)}.json";

    /// <summary>Convierte un nombre de carta en un slug apto para nombre de archivo.</summary>
    public static string Slugify(string name)
    {
        var sb = new StringBuilder();
        bool lastWasUnderscore = false;
        foreach (char c in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                lastWasUnderscore = false;
            }
            else if (!lastWasUnderscore && sb.Length > 0)
            {
                sb.Append('_');
                lastWasUnderscore = true;
            }
        }
        while (sb.Length > 0 && sb[^1] == '_')
            sb.Length--;
        return sb.Length > 0 ? sb.ToString() : "carta";
    }
}
