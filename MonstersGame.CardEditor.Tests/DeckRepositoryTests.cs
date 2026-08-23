using MonstersGame.CardEditor;
using MonstersGame.CardEditor.Tests.TestSupport;

namespace MonstersGame.CardEditor.Tests;

/// <summary>
/// Pruebas del catalogo de mazos del editor de mazos (Bloque 12), contra una
/// base SQLite temporal — misma logica de validar-antes-de-guardar que
/// <see cref="CardRepositoryTests"/>, pero para mazos en vez de cartas.
/// </summary>
public class DeckRepositoryTests
{
    [Fact]
    public void Constructor_WithNoExistingDatabase_StartsWithNoDecks()
    {
        using var dir = new TempCardDirectory();
        var repo = new DeckRepository(dir.DbPath);

        Assert.Empty(repo.Decks);
    }

    [Fact]
    public void Save_NewValidDeck_PersistsAndAddsToCatalog()
    {
        using var dir = new TempCardDirectory();
        var repo = new DeckRepository(dir.DbPath);

        var errors = repo.Save(null, "Mazo Nuevo", new[] { 1, 1, 2 });

        Assert.Empty(errors);
        var deck = Assert.Single(repo.Decks);
        Assert.Equal("Mazo Nuevo", deck.Name);
        Assert.Equal(new[] { 1, 1, 2 }, deck.CardIds);
    }

    [Fact]
    public void Save_EmptyName_Fails_AndDoesNotPersist()
    {
        using var dir = new TempCardDirectory();
        var repo = new DeckRepository(dir.DbPath);

        var errors = repo.Save(null, "", new[] { 1 });

        Assert.Contains(errors, e => e.Contains("nombre"));
        Assert.Empty(repo.Decks);
    }

    [Fact]
    public void Save_EmptyDeck_Fails()
    {
        using var dir = new TempCardDirectory();
        var repo = new DeckRepository(dir.DbPath);

        var errors = repo.Save(null, "Vacio", Array.Empty<int>());

        Assert.Contains(errors, e => e.Contains("vacio"));
        Assert.Empty(repo.Decks);
    }

    [Fact]
    public void Save_MoreThanThreeCopiesOfTheSameCard_Fails()
    {
        using var dir = new TempCardDirectory();
        var repo = new DeckRepository(dir.DbPath);

        var errors = repo.Save(null, "Demasiadas Copias", new[] { 1, 1, 1, 1 });

        Assert.Contains(errors, e => e.Contains("3 copias"));
        Assert.Empty(repo.Decks);
    }

    [Fact]
    public void Save_ExactlyThreeCopies_Succeeds()
    {
        using var dir = new TempCardDirectory();
        var repo = new DeckRepository(dir.DbPath);

        var errors = repo.Save(null, "Justo Tres", new[] { 1, 1, 1 });

        Assert.Empty(errors);
        Assert.Single(repo.Decks);
    }

    [Fact]
    public void Save_DuplicateName_AgainstAnotherDeck_Fails()
    {
        using var dir = new TempCardDirectory();
        var repo = new DeckRepository(dir.DbPath);
        repo.Save(null, "Primero", new[] { 1 });

        var errors = repo.Save(null, "Primero", new[] { 2 });

        Assert.Contains(errors, e => e.Contains("Primero"));
        Assert.Single(repo.Decks); // no se agrego el segundo
    }

    [Fact]
    public void Save_EditingExistingDeck_KeepingItsOwnName_Succeeds()
    {
        using var dir = new TempCardDirectory();
        var repo = new DeckRepository(dir.DbPath);
        repo.Save(null, "Mi Mazo", new[] { 1 });
        int id = repo.Decks[0].Id;

        var errors = repo.Save(id, "Mi Mazo", new[] { 1, 2 });

        Assert.Empty(errors);
        Assert.Single(repo.Decks);
        Assert.Equal(new[] { 1, 2 }, repo.Decks[0].CardIds);
    }

    [Fact]
    public void Delete_RemovesFromCatalog()
    {
        using var dir = new TempCardDirectory();
        var repo = new DeckRepository(dir.DbPath);
        repo.Save(null, "Para Borrar", new[] { 1 });
        int id = repo.Decks[0].Id;

        repo.Delete(id);

        Assert.Empty(repo.Decks);
    }

    [Fact]
    public void Reload_DiscardsNothingItWasNeverAskedToDiscard_JustRereadsFromDisk()
    {
        using var dir = new TempCardDirectory();
        var repo = new DeckRepository(dir.DbPath);
        repo.Save(null, "Persistente", new[] { 1 });

        repo.Reload();

        Assert.Single(repo.Decks);
        Assert.Equal("Persistente", repo.Decks[0].Name);
    }
}
