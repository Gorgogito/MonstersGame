using GodotGame.Data.Loaders;
using GodotGame.Data.Sqlite;
using GodotGame.Tests.Data.TestSupport;

namespace GodotGame.Tests.Data;

public class SqliteTypeWriterTests
{
    [Fact]
    public void LoadAll_NewDatabase_IsSeededWithTheClassicTypes_IncludingDivineBeast()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteTypeWriter(dir.DbPath);

        var types = writer.LoadAll();

        Assert.Contains("Dragon", types);
        Assert.Contains("Warrior", types);
        Assert.Contains("DivineBeast", types);
    }

    [Fact]
    public void LoadAll_MissingDatabase_CreatesItAndSeedsTheClassicTypes()
    {
        // A diferencia de Cards/Decks, Types siempre se siembra al leer —
        // incluso antes de que exista una sola carta, hace falta poder
        // elegir un Tipo para crear la primera.
        using var dir = new TempCardDirectory();

        var types = new SqliteTypeWriter(dir.DbPath).LoadAll();

        Assert.True(File.Exists(dir.DbPath));
        Assert.Contains("Dragon", types);
    }

    [Fact]
    public void Add_NewType_AppearsInLoadAll()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteTypeWriter(dir.DbPath);

        writer.Add("Bestia Divina");

        Assert.Contains("Bestia Divina", writer.LoadAll());
    }

    [Fact]
    public void Add_DuplicateName_DoesNotDuplicateTheRow()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteTypeWriter(dir.DbPath);
        writer.Add("Bestia Divina");

        writer.Add("Bestia Divina");

        Assert.Single(writer.LoadAll(), t => t == "Bestia Divina");
    }

    [Fact]
    public void Delete_RemovesTheType()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteTypeWriter(dir.DbPath);
        writer.Add("Temporal");

        writer.Delete("Temporal");

        Assert.DoesNotContain("Temporal", writer.LoadAll());
    }

    [Fact]
    public void CountCardsUsing_NoCardsWithThatType_ReturnsZero()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteTypeWriter(dir.DbPath);
        writer.Add("SinUso");

        Assert.Equal(0, writer.CountCardsUsing("SinUso"));
    }

    [Fact]
    public void CountCardsUsing_CountsCardsWithThatExactType()
    {
        using var dir = new TempCardDirectory();
        var cardWriter = new SqliteCardWriter(dir.DbPath);
        cardWriter.SaveCard(new CardDto { Id = 1, Kind = "Monster", Name = "Uno", Type = "Bestia Divina" });
        cardWriter.SaveCard(new CardDto { Id = 2, Kind = "Monster", Name = "Dos", Type = "Bestia Divina" });
        cardWriter.SaveCard(new CardDto { Id = 3, Kind = "Monster", Name = "Tres", Type = "Dragon" });

        var typeWriter = new SqliteTypeWriter(dir.DbPath);

        Assert.Equal(2, typeWriter.CountCardsUsing("Bestia Divina"));
    }

    [Fact]
    public void Rename_UpdatesTheTypeRow_AndEveryCardUsingIt()
    {
        using var dir = new TempCardDirectory();
        var cardWriter = new SqliteCardWriter(dir.DbPath);
        cardWriter.SaveCard(new CardDto { Id = 1, Kind = "Monster", Name = "Carta", Type = "DivineBeast" });

        var typeWriter = new SqliteTypeWriter(dir.DbPath);
        typeWriter.Rename("DivineBeast", "Bestia Divina");

        Assert.Contains("Bestia Divina", typeWriter.LoadAll());
        Assert.DoesNotContain("DivineBeast", typeWriter.LoadAll());
        var updatedCard = Assert.Single(cardWriter.LoadAllDtos());
        Assert.Equal("Bestia Divina", updatedCard.Type);
    }
}
