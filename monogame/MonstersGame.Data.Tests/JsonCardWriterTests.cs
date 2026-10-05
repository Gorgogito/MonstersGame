using MonstersGame.Data.Loaders;
using MonstersGame.Data.Tests.TestSupport;

namespace MonstersGame.Data.Tests;

public class JsonCardWriterTests
{
    [Fact]
    public void SaveCard_WritesANewFile_NamedByIdAndSlug()
    {
        using var dir = new TempCardDirectory();
        var writer = new JsonCardWriter(dir.Path);
        var dto = new CardDto { Id = 42, Kind = "Monster", Name = "Dragón Feroz", Attack = 100, Defense = 100, Level = 4 };

        writer.SaveCard(dto);

        var files = Directory.GetFiles(dir.Path, "*.json");
        var file = Assert.Single(files);
        Assert.Equal("42__dragón_feroz.json", Path.GetFileName(file));
    }

    [Fact]
    public void SaveCard_OverwritingTheSameCard_DoesNotDuplicateTheFile()
    {
        using var dir = new TempCardDirectory();
        var writer = new JsonCardWriter(dir.Path);
        var dto = new CardDto { Id = 1, Kind = "Monster", Name = "Guerrero", Attack = 100, Defense = 100, Level = 4 };
        writer.SaveCard(dto);

        dto.Attack = 2000; // misma carta, mismo Id y nombre
        writer.SaveCard(dto);

        var files = Directory.GetFiles(dir.Path, "*.json");
        Assert.Single(files);
        var reloaded = new JsonCardLoader(dir.Path).LoadCards();
        var monster = Assert.IsType<MonstersGame.Core.Entities.MonsterCard>(Assert.Single(reloaded));
        Assert.Equal(2000, monster.Attack);
    }

    [Fact]
    public void SaveCard_WhenNameChanges_RenamesTheFile_AndRemovesTheOldOne()
    {
        using var dir = new TempCardDirectory();
        var writer = new JsonCardWriter(dir.Path);
        var dto = new CardDto { Id = 1, Kind = "Monster", Name = "Nombre Viejo", Attack = 100, Defense = 100, Level = 4 };
        writer.SaveCard(dto);

        dto.Name = "Nombre Nuevo";
        writer.SaveCard(dto);

        var files = Directory.GetFiles(dir.Path, "*.json").Select(Path.GetFileName).ToList();
        var file = Assert.Single(files);
        Assert.Equal("1__nombre_nuevo.json", file);
    }

    [Fact]
    public void LoadAllDtos_ReturnsRawDtos_NotMappedToDomainEntities()
    {
        using var dir = new TempCardDirectory();
        var writer = new JsonCardWriter(dir.Path);
        writer.SaveCard(new CardDto { Id = 1, Kind = "Monster", Name = "Guerrero", Attack = 1500, Defense = 1200, Level = 4, Type = "Warrior", Attribute = "Earth" });

        var dtos = writer.LoadAllDtos();

        var dto = Assert.Single(dtos);
        Assert.Equal("Guerrero", dto.Name);
        Assert.Equal(1500, dto.Attack);
    }

    [Fact]
    public void DeleteCard_RemovesTheFile()
    {
        using var dir = new TempCardDirectory();
        var writer = new JsonCardWriter(dir.Path);
        writer.SaveCard(new CardDto { Id = 1, Kind = "Monster", Name = "Guerrero", Attack = 100, Defense = 100, Level = 4 });

        writer.DeleteCard(1);

        Assert.Empty(Directory.GetFiles(dir.Path, "*.json"));
    }

    [Fact]
    public void DeleteCard_NonExistentId_DoesNothing()
    {
        using var dir = new TempCardDirectory();
        var writer = new JsonCardWriter(dir.Path);

        writer.DeleteCard(999); // no debe lanzar

        Assert.Empty(Directory.GetFiles(dir.Path, "*.json"));
    }

    [Theory]
    [InlineData("Dragón Blanco de Ojos Azules", "dragón_blanco_de_ojos_azules")]
    [InlineData("  espacios   extra  ", "espacios_extra")]
    [InlineData("!!!", "carta")]
    [InlineData("", "carta")]
    public void Slugify_ProducesFilesystemSafeNames(string name, string expected)
    {
        Assert.Equal(expected, JsonCardWriter.Slugify(name));
    }
}
