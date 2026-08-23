using MonstersGame.Core.Entities;
using MonstersGame.Data.Sqlite;

namespace MonstersGame.Data.Loaders;

/// <summary>Carga las recetas de fusion desde la tabla <c>Fusions</c> de una base SQLite.</summary>
public sealed class SqliteFusionLoader : IFusionLoader
{
    private readonly string _dbPath;

    public SqliteFusionLoader(string dbPath) => _dbPath = dbPath;

    public IReadOnlyList<FusionRecipe> LoadFusions()
    {
        var fusions = new List<FusionRecipe>();
        if (!File.Exists(_dbPath)) return fusions;

        SqliteSchema.EnsureCreated(_dbPath);
        using var connection = SqliteSchema.OpenConnection(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT MaterialA, MaterialB, Result FROM Fusions";

        using var reader = command.ExecuteReader();
        while (reader.Read())
            fusions.Add(new FusionRecipe(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2)));

        return fusions;
    }
}
