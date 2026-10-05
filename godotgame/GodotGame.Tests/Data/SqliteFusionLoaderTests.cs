using Microsoft.Data.Sqlite;
using GodotGame.Data.Loaders;
using GodotGame.Data.Sqlite;
using GodotGame.Tests.Data.TestSupport;

namespace GodotGame.Tests.Data;

public class SqliteFusionLoaderTests
{
    private static void InsertFusion(string dbPath, int a, int b, int result)
    {
        SqliteSchema.EnsureCreated(dbPath);
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Fusions (MaterialA, MaterialB, Result) VALUES ($A, $B, $R)";
        command.Parameters.AddWithValue("$A", a);
        command.Parameters.AddWithValue("$B", b);
        command.Parameters.AddWithValue("$R", result);
        command.ExecuteNonQuery();
    }

    [Fact]
    public void LoadFusions_ReadsEveryRow()
    {
        using var dir = new TempCardDirectory();
        InsertFusion(dir.DbPath, 10, 11, 19);
        InsertFusion(dir.DbPath, 5, 6, 20);

        var fusions = new SqliteFusionLoader(dir.DbPath).LoadFusions();

        Assert.Equal(2, fusions.Count);
        Assert.Contains(fusions, f => f.MaterialAId == 10 && f.MaterialBId == 11 && f.ResultId == 19);
    }

    [Fact]
    public void LoadFusions_MissingDatabase_ReturnsEmpty()
    {
        string missing = Path.Combine(Path.GetTempPath(), "GodotGame_NoExiste_" + Guid.NewGuid() + ".db");

        var fusions = new SqliteFusionLoader(missing).LoadFusions();

        Assert.Empty(fusions);
    }
}
