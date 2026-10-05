using System.Text.Json;
using MonstersGame.Core.Entities;

namespace MonstersGame.Data.Loaders;

/// <summary>Carga las recetas de fusion desde un archivo JSON (array).</summary>
public sealed class JsonFusionLoader : IFusionLoader
{
    private readonly string _path;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public JsonFusionLoader(string path) => _path = path;

    public IReadOnlyList<FusionRecipe> LoadFusions()
    {
        if (!File.Exists(_path)) return new List<FusionRecipe>();

        string json = File.ReadAllText(_path);
        var dtos = JsonSerializer.Deserialize<List<FusionDto>>(json, Options) ?? new List<FusionDto>();

        return dtos
            .Select(d => new FusionRecipe(d.MaterialA, d.MaterialB, d.Result))
            .ToList();
    }
}
