using MonstersGame.Data.Loaders;
using MonstersGame.Data.Sqlite;
using MonstersGame.Data.Tests.TestSupport;

namespace MonstersGame.Data.Tests;

public class JsonToSqliteMigratorTests
{
    [Fact]
    public void Migrate_MovesCardsDecksAndFusions_IntoTheDatabase()
    {
        using var dir = new TempCardDirectory();
        string cardsDir = Path.Combine(dir.Path, "Cards");
        string decksDir = Path.Combine(dir.Path, "Decks");
        string fusionsDir = Path.Combine(dir.Path, "Fusions");
        Directory.CreateDirectory(cardsDir);
        Directory.CreateDirectory(decksDir);
        Directory.CreateDirectory(fusionsDir);

        File.WriteAllText(Path.Combine(cardsDir, "1__guerrero.json"),
            """{ "id": 1, "kind": "Monster", "name": "Guerrero", "attack": 1500, "defense": 1200, "level": 4, "type": "Warrior", "attribute": "Earth" }""");
        File.WriteAllText(Path.Combine(cardsDir, "2__mago.json"),
            """{ "id": 2, "kind": "Monster", "name": "Mago", "attack": 1200, "defense": 1000, "level": 4, "type": "Spellcaster", "attribute": "Dark" }""");
        File.WriteAllText(Path.Combine(decksDir, "deck_prueba.json"),
            """{ "name": "Mazo de Prueba", "cards": [1, 1, 2] }""");
        File.WriteAllText(Path.Combine(fusionsDir, "fusions.json"),
            """[{ "materialA": 1, "materialB": 2, "result": 1 }]""");

        string dbPath = Path.Combine(dir.Path, "migrated.db");
        JsonToSqliteMigrator.Migrate(dir.Path, dbPath);

        var cards = new SqliteCardLoader(dbPath).LoadCards();
        Assert.Equal(2, cards.Count);

        var decks = new SqliteDeckLoader(dbPath).LoadDecks();
        var deck = Assert.Single(decks);
        Assert.Equal("Mazo de Prueba", deck.Name);
        Assert.Equal(new[] { 1, 1, 2 }, deck.CardIds);

        var fusions = new SqliteFusionLoader(dbPath).LoadFusions();
        var fusion = Assert.Single(fusions);
        Assert.Equal(1, fusion.MaterialAId);
        Assert.Equal(2, fusion.MaterialBId);
        Assert.Equal(1, fusion.ResultId);
    }

    [Fact]
    public void Migrate_MissingDecksAndFusions_StillMigratesCards()
    {
        using var dir = new TempCardDirectory();
        string cardsDir = Path.Combine(dir.Path, "Cards");
        Directory.CreateDirectory(cardsDir);
        File.WriteAllText(Path.Combine(cardsDir, "1__solo.json"),
            """{ "id": 1, "kind": "Monster", "name": "Solitario", "attack": 100, "defense": 100, "level": 1 }""");

        string dbPath = Path.Combine(dir.Path, "migrated.db");
        JsonToSqliteMigrator.Migrate(dir.Path, dbPath);

        Assert.Single(new SqliteCardLoader(dbPath).LoadCards());
        Assert.Empty(new SqliteDeckLoader(dbPath).LoadDecks());
        Assert.Empty(new SqliteFusionLoader(dbPath).LoadFusions());
    }

    [Fact]
    public void Migrate_OverwritesAnExistingDatabaseAtTheSamePath()
    {
        using var dir = new TempCardDirectory();
        string cardsDir = Path.Combine(dir.Path, "Cards");
        Directory.CreateDirectory(cardsDir);
        File.WriteAllText(Path.Combine(cardsDir, "1__uno.json"),
            """{ "id": 1, "kind": "Monster", "name": "Uno", "attack": 100, "defense": 100, "level": 1 }""");

        string dbPath = Path.Combine(dir.Path, "migrated.db");
        JsonToSqliteMigrator.Migrate(dir.Path, dbPath);
        JsonToSqliteMigrator.Migrate(dir.Path, dbPath); // segunda corrida, no debe duplicar

        Assert.Single(new SqliteCardLoader(dbPath).LoadCards());
    }
}
