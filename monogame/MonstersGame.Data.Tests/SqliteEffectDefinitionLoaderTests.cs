using Microsoft.Data.Sqlite;
using MonstersGame.Core.Effects;
using MonstersGame.Data.Sqlite;
using MonstersGame.Data.Tests.TestSupport;

namespace MonstersGame.Data.Tests;

public class SqliteEffectDefinitionLoaderTests
{
    [Fact]
    public void LoadEffectDefinitions_ReadsTheFourBackfilledLegacyEffects()
    {
        using var dir = new TempCardDirectory();
        SqliteSchema.EnsureCreated(dir.DbPath); // dispara las migraciones, incl. el backfill de efectos

        var definitions = new SqliteEffectDefinitionLoader(dir.DbPath).LoadEffectDefinitions();

        Assert.Equal(4, definitions.Count);

        var draw = Assert.Single(definitions, d => d.Id == "draw_1");
        Assert.Equal(EffectTrigger.Any, draw.Trigger);
        Assert.True(draw.IsActive);
        var drawStep = Assert.Single(draw.ActionSteps);
        Assert.Equal("draw_card", drawStep.ActionKind);
        Assert.Equal(1, drawStep.Params.GetInt("Count"));
        Assert.Null(draw.TargetSpec);

        var destroy = Assert.Single(definitions, d => d.Id == "destroy_target_monster");
        Assert.NotNull(destroy.TargetSpec);
        Assert.Equal(EffectTargetKind.MonsterZone, destroy.TargetSpec!.TargetKind);
        Assert.Null(destroy.TargetSpec.Filter);

        var revive = Assert.Single(definitions, d => d.Id == "special_summon_from_own_graveyard");
        Assert.Equal(EffectTargetKind.OwnGraveyard, revive.TargetSpec!.TargetKind);

        var negate = Assert.Single(definitions, d => d.Id == "negate_activation");
        Assert.Null(negate.TargetSpec);
        Assert.Equal("negate_activation", Assert.Single(negate.ActionSteps).ActionKind);
    }

    [Fact]
    public void LoadEffectDefinitions_ResolvesFilterOnTargetSpec_AndOrdersActionSteps()
    {
        using var dir = new TempCardDirectory();
        SqliteSchema.EnsureCreated(dir.DbPath);

        using var connection = new SqliteConnection($"Data Source={dir.DbPath}");
        connection.Open();

        int filterId;
        using (var insertFilter = connection.CreateCommand())
        {
            insertFilter.CommandText = "INSERT INTO TargetFilters (Name) VALUES ('Solo Dragon'); SELECT last_insert_rowid();";
            filterId = Convert.ToInt32((long)insertFilter.ExecuteScalar()!);
        }
        using (var insertCondition = connection.CreateCommand())
        {
            insertCondition.CommandText = "INSERT INTO TargetFilterConditions (FilterId, GroupIndex, Kind, Negate, Value) VALUES ($FilterId, 0, 'Type', 0, 'Dragon')";
            insertCondition.Parameters.AddWithValue("$FilterId", filterId);
            insertCondition.ExecuteNonQuery();
        }

        using (var insertDefinition = connection.CreateCommand())
        {
            insertDefinition.CommandText = "INSERT INTO EffectDefinitions (Id, Name, Trigger, VisualProfileKey, IsActive) VALUES ('custom_test_effect', 'Prueba', 'Activate', 'explosion', 1)";
            insertDefinition.ExecuteNonQuery();
        }
        using (var insertTargetSpec = connection.CreateCommand())
        {
            insertTargetSpec.CommandText = "INSERT INTO EffectTargetSpecs (EffectDefinitionId, TargetKind, FilterId, Required) VALUES ('custom_test_effect', 'MonsterZone', $FilterId, 1)";
            insertTargetSpec.Parameters.AddWithValue("$FilterId", filterId);
            insertTargetSpec.ExecuteNonQuery();
        }
        // Insertados fuera de orden a proposito para verificar que el loader ordena por StepOrder.
        using (var insertStep2 = connection.CreateCommand())
        {
            insertStep2.CommandText = "INSERT INTO EffectActionSteps (EffectDefinitionId, StepOrder, ActionKind, ParamsJson) VALUES ('custom_test_effect', 1, 'negate_activation', '{}')";
            insertStep2.ExecuteNonQuery();
        }
        using (var insertStep1 = connection.CreateCommand())
        {
            insertStep1.CommandText = "INSERT INTO EffectActionSteps (EffectDefinitionId, StepOrder, ActionKind, ParamsJson) VALUES ('custom_test_effect', 0, 'draw_card', $Params)";
            insertStep1.Parameters.AddWithValue("$Params", """{"Count":"2"}""");
            insertStep1.ExecuteNonQuery();
        }
        connection.Close();

        var definitions = new SqliteEffectDefinitionLoader(dir.DbPath).LoadEffectDefinitions();
        var custom = Assert.Single(definitions, d => d.Id == "custom_test_effect");

        Assert.Equal(EffectTrigger.Activate, custom.Trigger);
        Assert.Equal("explosion", custom.VisualProfileKey);
        Assert.NotNull(custom.TargetSpec!.Filter);
        Assert.Single(custom.TargetSpec.Filter!.OrGroups);

        Assert.Equal(2, custom.ActionSteps.Count);
        Assert.Equal("draw_card", custom.ActionSteps[0].ActionKind);
        Assert.Equal(2, custom.ActionSteps[0].Params.GetInt("Count"));
        Assert.Equal("negate_activation", custom.ActionSteps[1].ActionKind);
    }
}
