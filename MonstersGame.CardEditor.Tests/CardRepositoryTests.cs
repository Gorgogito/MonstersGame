using MonstersGame.CardEditor;
using MonstersGame.CardEditor.Tests.TestSupport;
using MonstersGame.Data.Sqlite;

namespace MonstersGame.CardEditor.Tests;

/// <summary>
/// Pruebas del catalogo en memoria del editor, contra una base SQLite
/// temporal (Bloque 9). Cubren la logica real (validar-antes-de-guardar,
/// alta/edicion/baja, duplicar) sin depender de WinForms. Las aserciones de
/// persistencia usan una instancia nueva de <see cref="SqliteCardWriter"/> en
/// vez de confiar en el estado en memoria del repositorio bajo prueba.
/// </summary>
public class CardRepositoryTests
{
    [Fact]
    public void Constructor_WithNoExistingDatabase_StartsWithNoCards()
    {
        using var dir = new TempCardDirectory();
        var repo = new CardRepository(dir.DbPath);

        Assert.Empty(repo.Cards);
        Assert.Equal(1, repo.NextId());
        Assert.False(File.Exists(dir.DbPath)); // leer no crea la base
    }

    [Fact]
    public void NextId_ReturnsOneMoreThanTheHighestExistingId()
    {
        using var dir = new TempCardDirectory();
        var repo = new CardRepository(dir.DbPath);
        repo.Save(TestDtos.Monster(1, "Uno"), originalId: null);
        repo.Save(TestDtos.Monster(7, "Siete"), originalId: null);

        Assert.Equal(8, repo.NextId());
    }

    [Fact]
    public void Save_NewValidCard_WritesToDatabaseAndAddsToCatalog()
    {
        using var dir = new TempCardDirectory();
        var repo = new CardRepository(dir.DbPath);

        var errors = repo.Save(TestDtos.Monster(1, "Dragon Novato"), originalId: null);

        Assert.Empty(errors);
        Assert.Single(repo.Cards);
        Assert.Single(new SqliteCardWriter(dir.DbPath).LoadAllDtos());
    }

    [Fact]
    public void Save_InvalidCard_ReturnsErrors_AndWritesNothing()
    {
        using var dir = new TempCardDirectory();
        var repo = new CardRepository(dir.DbPath);

        var errors = repo.Save(TestDtos.Monster(0, ""), originalId: null);

        Assert.NotEmpty(errors);
        Assert.Empty(repo.Cards);
        Assert.False(File.Exists(dir.DbPath));
    }

    [Fact]
    public void Save_DuplicateId_AgainstAnotherCard_Fails()
    {
        using var dir = new TempCardDirectory();
        var repo = new CardRepository(dir.DbPath);
        repo.Save(TestDtos.Monster(1, "Primero"), originalId: null);

        var errors = repo.Save(TestDtos.Monster(1, "Otro Nombre"), originalId: null);

        Assert.Contains(errors, e => e.Contains("Id"));
        Assert.Single(repo.Cards); // no se agrego el segundo
    }

    [Fact]
    public void Save_EditingExistingCard_UpdatesInPlace_WithoutDuplicating()
    {
        using var dir = new TempCardDirectory();
        var repo = new CardRepository(dir.DbPath);
        repo.Save(TestDtos.Monster(1, "Nombre Original"), originalId: null);

        var edited = TestDtos.Monster(1, "Nombre Editado", attack: 2000);
        var errors = repo.Save(edited, originalId: 1);

        Assert.Empty(errors);
        Assert.Single(repo.Cards);
        Assert.Equal("Nombre Editado", repo.Cards[0].Name);
        Assert.Equal(2000, repo.Cards[0].Attack);

        var persisted = new SqliteCardWriter(dir.DbPath).LoadAllDtos();
        Assert.Single(persisted);
        Assert.Equal("Nombre Editado", persisted[0].Name);
    }

    [Fact]
    public void Delete_RemovesFromDatabaseAndCatalog()
    {
        using var dir = new TempCardDirectory();
        var repo = new CardRepository(dir.DbPath);
        repo.Save(TestDtos.Monster(1, "Para Borrar"), originalId: null);

        repo.Delete(1);

        Assert.Empty(repo.Cards);
        Assert.Empty(new SqliteCardWriter(dir.DbPath).LoadAllDtos());
    }

    [Fact]
    public void Duplicate_AssignsNewIdAndCopySuffix_WithoutTouchingTheDatabase()
    {
        using var dir = new TempCardDirectory();
        var repo = new CardRepository(dir.DbPath);
        repo.Save(TestDtos.Monster(1, "Original", attack: 1800), originalId: null);

        var copy = repo.Duplicate(repo.Cards[0]);

        Assert.Equal(2, copy.Id);
        Assert.Equal("Original (copia)", copy.Name);
        Assert.Equal(1800, copy.Attack);
        Assert.Single(repo.Cards); // no se guarda automaticamente
        Assert.Single(new SqliteCardWriter(dir.DbPath).LoadAllDtos());
    }

    [Fact]
    public void NewBlankCard_UsesNextIdAndSafeDefaults()
    {
        using var dir = new TempCardDirectory();
        var repo = new CardRepository(dir.DbPath);
        repo.Save(TestDtos.Monster(3, "Existente"), originalId: null);

        var blank = repo.NewBlankCard("Spell");

        Assert.Equal(4, blank.Id);
        Assert.Equal("Spell", blank.Kind);
        Assert.Equal("", blank.Name);
    }

    [Fact]
    public void Reload_DiscardsInMemoryChanges_NotYetSaved()
    {
        using var dir = new TempCardDirectory();
        var repo = new CardRepository(dir.DbPath);
        repo.Save(TestDtos.Monster(1, "Guardada"), originalId: null);
        var unsaved = repo.Duplicate(repo.Cards[0]); // vive solo en memoria hasta que se guarda

        repo.Reload();

        Assert.Single(repo.Cards);
        Assert.DoesNotContain(repo.Cards, c => c.Id == unsaved.Id);
    }

    [Fact]
    public void Validate_DoesNotWriteToTheDatabase()
    {
        using var dir = new TempCardDirectory();
        var repo = new CardRepository(dir.DbPath);

        var errors = repo.Validate(TestDtos.Monster(1, "Solo Validar"), originalId: null);

        Assert.Empty(errors);
        Assert.False(File.Exists(dir.DbPath));
        Assert.Empty(repo.Cards);
    }
}
