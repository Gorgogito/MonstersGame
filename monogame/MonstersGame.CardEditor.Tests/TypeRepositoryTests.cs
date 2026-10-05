using MonstersGame.CardEditor;
using MonstersGame.CardEditor.Tests.TestSupport;
using MonstersGame.Data.Loaders;
using MonstersGame.Data.Sqlite;

namespace MonstersGame.CardEditor.Tests;

/// <summary>
/// Pruebas del catalogo de Tipos de Monstruo del editor (Bloque 13), contra
/// una base SQLite temporal.
/// </summary>
public class TypeRepositoryTests
{
    [Fact]
    public void Constructor_NewDatabase_StartsWithTheClassicTypes()
    {
        using var dir = new TempCardDirectory();
        var repo = new TypeRepository(dir.DbPath);

        Assert.Contains("Dragon", repo.Types);
        Assert.Contains("DivineBeast", repo.Types);
    }

    [Fact]
    public void Add_NewType_Succeeds_AndAppearsInTheList()
    {
        using var dir = new TempCardDirectory();
        var repo = new TypeRepository(dir.DbPath);

        var error = repo.Add("Bestia Divina");

        Assert.Null(error);
        Assert.Contains("Bestia Divina", repo.Types);
    }

    [Fact]
    public void Add_EmptyName_Fails()
    {
        using var dir = new TempCardDirectory();
        var repo = new TypeRepository(dir.DbPath);

        var error = repo.Add("   ");

        Assert.NotNull(error);
    }

    [Fact]
    public void Add_DuplicateName_Fails()
    {
        using var dir = new TempCardDirectory();
        var repo = new TypeRepository(dir.DbPath);

        var error = repo.Add("Dragon"); // ya viene sembrado

        Assert.NotNull(error);
    }

    [Fact]
    public void Delete_TypeNotInUse_Succeeds()
    {
        using var dir = new TempCardDirectory();
        var repo = new TypeRepository(dir.DbPath);
        repo.Add("Temporal");

        var error = repo.Delete("Temporal");

        Assert.Null(error);
        Assert.DoesNotContain("Temporal", repo.Types);
    }

    [Fact]
    public void Delete_TypeInUseByACard_Fails_AndKeepsTheType()
    {
        using var dir = new TempCardDirectory();
        new SqliteCardWriter(dir.DbPath).SaveCard(new CardDto
        {
            Id = 1, Kind = "Monster", Name = "Carta", Type = "Dragon"
        });
        var repo = new TypeRepository(dir.DbPath);

        var error = repo.Delete("Dragon");

        Assert.NotNull(error);
        Assert.Contains("Dragon", repo.Types);
    }

    [Fact]
    public void Rename_ToAFreeName_Succeeds()
    {
        using var dir = new TempCardDirectory();
        var repo = new TypeRepository(dir.DbPath);

        var error = repo.Rename("DivineBeast", "Bestia Divina");

        Assert.Null(error);
        Assert.Contains("Bestia Divina", repo.Types);
        Assert.DoesNotContain("DivineBeast", repo.Types);
    }

    [Fact]
    public void Rename_ToAnAlreadyExistingName_Fails()
    {
        using var dir = new TempCardDirectory();
        var repo = new TypeRepository(dir.DbPath);

        var error = repo.Rename("DivineBeast", "Dragon");

        Assert.NotNull(error);
        Assert.Contains("DivineBeast", repo.Types); // no se toco nada
    }
}
