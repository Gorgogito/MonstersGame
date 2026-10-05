using GodotGame.Data.Loaders;
using GodotGame.Data.Sqlite;
using GodotGame.Tests.Data.TestSupport;

namespace GodotGame.Tests.Data;

public class SqliteCardWriterTests
{
    [Fact]
    public void SaveCard_InsertsANewRow()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteCardWriter(dir.DbPath);
        var dto = new CardDto { Id = 42, Kind = "Monster", Name = "Dragón Feroz", Attack = 100, Defense = 100, Level = 4 };

        writer.SaveCard(dto);

        var dtos = writer.LoadAllDtos();
        var saved = Assert.Single(dtos);
        Assert.Equal(42, saved.Id);
        Assert.Equal("Dragón Feroz", saved.Name);
    }

    [Fact]
    public void SaveCard_SameId_UpdatesInPlace_WithoutDuplicatingTheRow()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteCardWriter(dir.DbPath);
        var dto = new CardDto { Id = 1, Kind = "Monster", Name = "Guerrero", Attack = 100, Defense = 100, Level = 4 };
        writer.SaveCard(dto);

        dto.Attack = 2000; // misma carta, mismo Id
        writer.SaveCard(dto);

        var dtos = writer.LoadAllDtos();
        var saved = Assert.Single(dtos);
        Assert.Equal(2000, saved.Attack);
    }

    [Fact]
    public void SaveCard_ChangingTheName_UpdatesInPlace_NoRenameNeeded()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteCardWriter(dir.DbPath);
        var dto = new CardDto { Id = 1, Kind = "Monster", Name = "Nombre Viejo", Attack = 100, Defense = 100, Level = 4 };
        writer.SaveCard(dto);

        dto.Name = "Nombre Nuevo";
        writer.SaveCard(dto);

        var dtos = writer.LoadAllDtos();
        var saved = Assert.Single(dtos);
        Assert.Equal("Nombre Nuevo", saved.Name);
    }

    [Fact]
    public void LoadAllDtos_ReturnsRawDtos_NotMappedToDomainEntities()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteCardWriter(dir.DbPath);
        writer.SaveCard(new CardDto { Id = 1, Kind = "Monster", Name = "Guerrero", Attack = 1500, Defense = 1200, Level = 4, Type = "Warrior", Attribute = "Earth" });

        var dtos = writer.LoadAllDtos();

        var dto = Assert.Single(dtos);
        Assert.Equal("Guerrero", dto.Name);
        Assert.Equal(1500, dto.Attack);
    }

    [Fact]
    public void LoadAllDtos_NoExistingDatabase_ReturnsEmpty_WithoutCreatingOne()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteCardWriter(dir.DbPath);

        var dtos = writer.LoadAllDtos();

        Assert.Empty(dtos);
        Assert.False(File.Exists(dir.DbPath));
    }

    [Fact]
    public void DeleteCard_RemovesTheRow()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteCardWriter(dir.DbPath);
        writer.SaveCard(new CardDto { Id = 1, Kind = "Monster", Name = "Guerrero", Attack = 100, Defense = 100, Level = 4 });

        writer.DeleteCard(1);

        Assert.Empty(writer.LoadAllDtos());
    }

    [Fact]
    public void DeleteCard_NonExistentId_DoesNothing()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteCardWriter(dir.DbPath);
        writer.SaveCard(new CardDto { Id = 1, Kind = "Monster", Name = "Guerrero", Attack = 100, Defense = 100, Level = 4 });

        writer.DeleteCard(999); // no debe lanzar

        Assert.Single(writer.LoadAllDtos());
    }
}
