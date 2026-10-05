using MonstersGame.CardEditor;
using MonstersGame.CardEditor.Tests.TestSupport;
using MonstersGame.Data.Loaders;
using MonstersGame.Data.Sqlite;

namespace MonstersGame.CardEditor.Tests;

/// <summary>Pruebas del catalogo de tipos de Campo del editor (Fase 4/5), contra una base SQLite temporal.</summary>
public class FieldTypeRepositoryTests
{
    [Fact]
    public void Constructor_NewDatabase_StartsWithTheSixSeededElementalTerrains()
    {
        // Migration009_SeedElementalFieldTypes (adaptacion de esfuerzo medio,
        // terreno elemental) precarga 6 FieldTypes de ejemplo -- una base
        // nueva ya no arranca vacia, a diferencia de antes de esa migracion.
        using var dir = new TempCardDirectory();
        var repo = new FieldTypeRepository(dir.DbPath);

        Assert.Equal(6, repo.FieldTypes.Count);
        Assert.Contains(repo.FieldTypes, f => f.Id == "terrain_fire");
        Assert.Contains(repo.FieldTypes, f => f.Id == "terrain_water");
    }

    [Fact]
    public void Save_NewFieldType_Succeeds_AndAppearsInTheList()
    {
        using var dir = new TempCardDirectory();
        var repo = new FieldTypeRepository(dir.DbPath);

        var error = repo.Save(new FieldTypeDto { Id = "volcanic", Name = "Volcanico", StatModifierAmount = 500, StatModifierStat = "Both" });

        Assert.Null(error);
        Assert.Contains(repo.FieldTypes, f => f.Id == "volcanic" && f.Name == "Volcanico" && f.StatModifierAmount == 500);
    }

    [Fact]
    public void Save_EmptyId_Fails()
    {
        using var dir = new TempCardDirectory();
        var repo = new FieldTypeRepository(dir.DbPath);

        var error = repo.Save(new FieldTypeDto { Id = "  ", Name = "Sin Id" });

        Assert.NotNull(error);
    }

    [Fact]
    public void Save_EmptyName_Fails()
    {
        using var dir = new TempCardDirectory();
        var repo = new FieldTypeRepository(dir.DbPath);

        var error = repo.Save(new FieldTypeDto { Id = "x", Name = "" });

        Assert.NotNull(error);
    }

    [Fact]
    public void Save_SameIdTwice_ReplacesInsteadOfDuplicating()
    {
        using var dir = new TempCardDirectory();
        var repo = new FieldTypeRepository(dir.DbPath);
        repo.Save(new FieldTypeDto { Id = "volcanic", Name = "Volcanico", StatModifierAmount = 100 });

        repo.Save(new FieldTypeDto { Id = "volcanic", Name = "Volcanico Mejorado", StatModifierAmount = 500 });

        var match = Assert.Single(repo.FieldTypes, f => f.Id == "volcanic");
        Assert.Equal("Volcanico Mejorado", match.Name);
        Assert.Equal(500, match.StatModifierAmount);
    }

    [Fact]
    public void Delete_FieldTypeNotInUse_Succeeds()
    {
        using var dir = new TempCardDirectory();
        var repo = new FieldTypeRepository(dir.DbPath);
        repo.Save(new FieldTypeDto { Id = "temp", Name = "Temporal" });

        var error = repo.Delete("temp");

        Assert.Null(error);
        Assert.DoesNotContain(repo.FieldTypes, f => f.Id == "temp");
    }

    [Fact]
    public void Save_WithOpposedFilterAndAmount_RoundTrips()
    {
        // Terreno elemental (adaptacion de esfuerzo medio): el modificador
        // opuesto es independiente del principal -- confirma que se guarda y
        // relee, no solo el modificador de siempre.
        using var dir = new TempCardDirectory();
        var repo = new FieldTypeRepository(dir.DbPath);
        var opposedFilter = new FilterDto
        {
            OrGroups = { new List<FilterConditionDto> { new() { Kind = "Attribute", Negate = false, Value = "Water" } } }
        };

        var error = repo.Save(new FieldTypeDto
        {
            Id = "custom_fire",
            Name = "Fuego Personalizado",
            StatModifierAmount = 500,
            StatModifierStat = "Attack",
            OpposedFilter = opposedFilter,
            OpposedStatModifierAmount = -400
        });

        Assert.Null(error);
        var reloaded = Assert.Single(repo.FieldTypes, f => f.Id == "custom_fire");
        Assert.Equal(-400, reloaded.OpposedStatModifierAmount);
        Assert.NotNull(reloaded.OpposedFilter);
        Assert.Equal("Water", reloaded.OpposedFilter!.OrGroups[0][0].Value);
    }

    [Fact]
    public void Save_WithoutOpposedFilter_RoundTripsAsNull()
    {
        using var dir = new TempCardDirectory();
        var repo = new FieldTypeRepository(dir.DbPath);

        repo.Save(new FieldTypeDto { Id = "no_terrain", Name = "Sin Terreno" });

        var reloaded = Assert.Single(repo.FieldTypes, f => f.Id == "no_terrain");
        Assert.Null(reloaded.OpposedFilter);
        Assert.Equal(0, reloaded.OpposedStatModifierAmount);
    }

    [Fact]
    public void Delete_FieldTypeInUseByACard_Fails_AndKeepsIt()
    {
        using var dir = new TempCardDirectory();
        new SqliteFieldTypeWriter(dir.DbPath).Save(new FieldTypeDto { Id = "volcanic", Name = "Volcanico" });
        new SqliteCardWriter(dir.DbPath).SaveCard(new CardDto { Id = 1, Kind = "Spell", Name = "Campo", SubType = "Field", FieldTypeId = "volcanic" });
        var repo = new FieldTypeRepository(dir.DbPath);

        var error = repo.Delete("volcanic");

        Assert.NotNull(error);
        Assert.Contains(repo.FieldTypes, f => f.Id == "volcanic");
    }
}
