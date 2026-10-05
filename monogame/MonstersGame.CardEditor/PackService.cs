using System.IO.Compression;
using System.Text.Json;
using MonstersGame.Data.Loaders;

namespace MonstersGame.CardEditor;

/// <summary>
/// Exporta/importa cartas individuales y paquetes ("MonsterCardPack": un
/// manifiesto <c>pack.json</c> + un archivo JSON por carta, empaquetados como
/// .zip). Reutiliza el mismo formato de carta que ya usa el catalogo en
/// disco: "exportar una carta" es, literalmente, escribir el mismo JSON en
/// otro lugar (seccion 10 del analisis de adaptacion).
/// </summary>
public static class PackService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static void ExportCard(CardDto dto, string destinationPath) =>
        File.WriteAllText(destinationPath, JsonSerializer.Serialize(dto, Options));

    public static CardDto ImportCard(string sourcePath)
    {
        var dto = JsonSerializer.Deserialize<CardDto>(File.ReadAllText(sourcePath), Options);
        if (dto == null) throw new InvalidDataException("El archivo no contiene una carta valida.");
        return dto;
    }

    public static void ExportPack(IEnumerable<CardDto> cards, string packName, string author, string destinationZipPath)
    {
        var list = cards.ToList();
        if (File.Exists(destinationZipPath)) File.Delete(destinationZipPath);

        using var stream = new FileStream(destinationZipPath, FileMode.Create);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);

        var manifest = new PackManifest
        {
            Name = packName,
            Author = author,
            Version = "1.0",
            CardIds = list.Select(c => c.Id).ToList()
        };
        WriteEntry(zip, "pack.json", JsonSerializer.Serialize(manifest, Options));

        foreach (var card in list)
            WriteEntry(zip, $"{card.Id}__{JsonCardWriter.Slugify(card.Name)}.json", JsonSerializer.Serialize(card, Options));
    }

    public static List<CardDto> ImportPack(string zipPath)
    {
        using var stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        var cards = new List<CardDto>();
        foreach (var entry in zip.Entries)
        {
            if (!entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            if (entry.FullName.Equals("pack.json", StringComparison.OrdinalIgnoreCase)) continue;

            using var reader = new StreamReader(entry.Open());
            var dto = JsonSerializer.Deserialize<CardDto>(reader.ReadToEnd(), Options);
            if (dto != null) cards.Add(dto);
        }
        return cards;
    }

    private static void WriteEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }
}

internal sealed class PackManifest
{
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public string Version { get; set; } = "1.0";
    public List<int> CardIds { get; set; } = new();
}
