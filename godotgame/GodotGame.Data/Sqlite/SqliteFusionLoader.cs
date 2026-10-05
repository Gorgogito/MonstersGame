using GodotGame.Core.Entities;
using GodotGame.Core.Requirements;
using GodotGame.Data.Sqlite;

namespace GodotGame.Data.Loaders;

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
        var requirementLoader = new SqliteRequirementLoader(connection);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, MaterialA, MaterialB, Result, RequirementSetId FROM Fusions";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            int id = reader.GetInt32(0);
            int materialA = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            int materialB = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
            int result = reader.GetInt32(3);
            RequirementSet? requirement = reader.IsDBNull(4) ? null : requirementLoader.LoadRequirementSet(reader.GetInt32(4));

            fusions.Add(new FusionRecipe(materialA, materialB, result, id, requirement));
        }

        return fusions;
    }
}
