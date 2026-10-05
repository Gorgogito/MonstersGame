using Microsoft.Data.Sqlite;
using GodotGame.Core.Entities;
using GodotGame.Core.Requirements;
using GodotGame.Data.Loaders;
using GodotGame.Data.Sqlite;
using GodotGame.Tests.Data.TestSupport;

namespace GodotGame.Tests.Data;

/// <summary>
/// Verifica el escenario real que motiva la migracion: una base con datos
/// creados ANTES de que existieran las tablas de requisitos (simulado
/// recreando a mano el esquema "version 1" e insertando filas directamente,
/// sin pasar por <see cref="SqliteSchema.EnsureCreated"/>).
/// </summary>
public class SqliteMigrationsTests
{
    private static void CreateBaselineSchema(string dbPath)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE Cards (
                Id INTEGER PRIMARY KEY,
                Kind TEXT NOT NULL,
                Name TEXT NOT NULL,
                Attack INTEGER NOT NULL,
                Defense INTEGER NOT NULL,
                Level INTEGER NOT NULL,
                Type TEXT NOT NULL,
                Attribute TEXT NOT NULL,
                Image TEXT NOT NULL,
                Description TEXT NOT NULL,
                EffectId TEXT NOT NULL,
                SubType TEXT NOT NULL,
                Category TEXT NOT NULL,
                RitualMonsterId INTEGER NOT NULL,
                RequiredRitualLevel INTEGER NOT NULL
            );

            CREATE TABLE Fusions (
                MaterialA INTEGER NOT NULL,
                MaterialB INTEGER NOT NULL,
                Result INTEGER NOT NULL,
                PRIMARY KEY (MaterialA, MaterialB)
            );

            CREATE TABLE Types (
                Name TEXT PRIMARY KEY
            );
            """;
        command.ExecuteNonQuery();
    }

    [Fact]
    public void EnsureCreated_BackfillsExistingRitualCard_IntoLevelSumRequirementSet()
    {
        using var dir = new TempCardDirectory();
        CreateBaselineSchema(dir.DbPath);

        using (var connection = new SqliteConnection($"Data Source={dir.DbPath}"))
        {
            connection.Open();
            using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO Cards (Id, Kind, Name, Attack, Defense, Level, Type, Attribute, Image, Description, EffectId, SubType, Category, RitualMonsterId, RequiredRitualLevel)
                VALUES (5, 'Spell', 'Rito Preexistente', 0, 0, 0, '', 'Dark', '', '', '', 'Ritual', 'Normal', 9, 6)
                """;
            insert.ExecuteNonQuery();
        }

        // Dispara las migraciones 2 y 3 sobre datos que ya existian antes de ellas.
        SqliteSchema.EnsureCreated(dir.DbPath);

        var cards = new SqliteCardLoader(dir.DbPath).LoadCards();
        var spell = Assert.IsType<SpellCard>(Assert.Single(cards));

        Assert.Equal(6, spell.RequiredRitualLevel); // columna legacy intacta, de solo lectura
        Assert.NotNull(spell.Requirement);
        Assert.Equal(RequirementMode.LevelSum, spell.Requirement!.Mode);
        Assert.Equal(6, spell.Requirement.LevelSum!.MinLevelSum);
    }

    [Fact]
    public void EnsureCreated_DoesNotBackfillRitualWithoutRequiredLevel()
    {
        using var dir = new TempCardDirectory();
        CreateBaselineSchema(dir.DbPath);

        using (var connection = new SqliteConnection($"Data Source={dir.DbPath}"))
        {
            connection.Open();
            using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO Cards (Id, Kind, Name, Attack, Defense, Level, Type, Attribute, Image, Description, EffectId, SubType, Category, RitualMonsterId, RequiredRitualLevel)
                VALUES (6, 'Spell', 'Ritual Sin Configurar', 0, 0, 0, '', 'Dark', '', '', '', 'Ritual', 'Normal', 0, 0)
                """;
            insert.ExecuteNonQuery();
        }

        SqliteSchema.EnsureCreated(dir.DbPath);

        var spell = Assert.IsType<SpellCard>(Assert.Single(new SqliteCardLoader(dir.DbPath).LoadCards()));
        Assert.Null(spell.Requirement);
    }

    [Fact]
    public void EnsureCreated_PreservesExistingFusionRecipes_AfterTableRecreate()
    {
        using var dir = new TempCardDirectory();
        CreateBaselineSchema(dir.DbPath);

        using (var connection = new SqliteConnection($"Data Source={dir.DbPath}"))
        {
            connection.Open();
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO Fusions (MaterialA, MaterialB, Result) VALUES (10, 11, 19)";
            insert.ExecuteNonQuery();
        }

        SqliteSchema.EnsureCreated(dir.DbPath);

        var fusions = new SqliteFusionLoader(dir.DbPath).LoadFusions();

        var recipe = Assert.Single(fusions);
        Assert.Equal(10, recipe.MaterialAId);
        Assert.Equal(11, recipe.MaterialBId);
        Assert.Equal(19, recipe.ResultId);
        Assert.True(recipe.Id > 0);
        Assert.Null(recipe.Requirement);
    }

    [Fact]
    public void EnsureCreated_IsIdempotent_RunningTwiceLeavesSchemaAtLatestVersion()
    {
        using var dir = new TempCardDirectory();

        SqliteSchema.EnsureCreated(dir.DbPath);
        SqliteSchema.EnsureCreated(dir.DbPath);

        using var connection = new SqliteConnection($"Data Source={dir.DbPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        long version = (long)command.ExecuteScalar()!;

        Assert.Equal(10, version);
    }

    [Fact]
    public void EnsureCreated_BackfillsGuardianStarsOfExistingMonsters_FromTheirAttribute()
    {
        using var dir = new TempCardDirectory();
        CreateBaselineSchema(dir.DbPath);

        using (var connection = new SqliteConnection($"Data Source={dir.DbPath}"))
        {
            connection.Open();
            using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO Cards (Id, Kind, Name, Attack, Defense, Level, Type, Attribute, Image, Description, EffectId, SubType, Category, RitualMonsterId, RequiredRitualLevel)
                VALUES (7, 'Monster', 'Monstruo Preexistente', 1000, 1000, 4, 'Warrior', 'Fire', '', '', '', 'Normal', 'Normal', 0, 0)
                """;
            insert.ExecuteNonQuery();
        }

        SqliteSchema.EnsureCreated(dir.DbPath);

        var dto = Assert.Single(new SqliteCardWriter(dir.DbPath).LoadAllDtos());
        var defaults = GodotGame.Core.Rules.GuardianStars.DefaultsFor(MonsterAttribute.Fire);
        Assert.Equal(defaults.First.ToString(), dto.GuardianStar1);
        Assert.Equal(defaults.Second.ToString(), dto.GuardianStar2);
    }

    [Fact]
    public void SaveCard_RoundTripsGuardianStars()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteCardWriter(dir.DbPath);
        writer.SaveCard(new CardDto
        {
            Id = 8, Kind = "Monster", Name = "Con Estrellas", Attack = 1000, Defense = 1000, Level = 4,
            Type = "Warrior", Attribute = "Earth", GuardianStar1 = "Mercury", GuardianStar2 = "Neptune"
        });

        var monster = Assert.IsType<MonsterCard>(Assert.Single(new SqliteCardLoader(dir.DbPath).LoadCards()));
        Assert.Equal(GuardianStar.Mercury, monster.GuardianStar1);
        Assert.Equal(GuardianStar.Neptune, monster.GuardianStar2);
    }
}
