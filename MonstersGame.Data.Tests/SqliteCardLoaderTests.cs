using MonstersGame.Core.Entities;
using MonstersGame.Data.Loaders;
using MonstersGame.Data.Sqlite;
using MonstersGame.Data.Tests.TestSupport;

namespace MonstersGame.Data.Tests;

public class SqliteCardLoaderTests
{
    [Fact]
    public void LoadCards_ReadsEveryRowFromTheCardsTable()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteCardWriter(dir.DbPath);
        writer.SaveCard(new CardDto { Id = 1, Kind = "Monster", Name = "Guerrero", Attack = 1500, Defense = 1200, Level = 4, Type = "Warrior", Attribute = "Earth" });
        writer.SaveCard(new CardDto { Id = 2, Kind = "Spell", SubType = "Normal", Name = "Trueno" });

        var cards = new SqliteCardLoader(dir.DbPath).LoadCards();

        Assert.Equal(2, cards.Count);
        var monster = Assert.IsType<MonsterCard>(cards.Single(c => c.Id == 1));
        Assert.Equal("Guerrero", monster.Name);
        Assert.Equal(1500, monster.Attack);
        var spell = Assert.IsType<SpellCard>(cards.Single(c => c.Id == 2));
        Assert.Equal(SpellSubType.Normal, spell.SubType);
    }

    [Fact]
    public void LoadCards_MissingDatabase_ReturnsEmpty()
    {
        string missing = Path.Combine(Path.GetTempPath(), "MonstersGame_NoExiste_" + Guid.NewGuid() + ".db");

        var cards = new SqliteCardLoader(missing).LoadCards();

        Assert.Empty(cards);
    }

    [Fact]
    public void LoadCards_ParsesRitualSpellFields()
    {
        using var dir = new TempCardDirectory();
        new SqliteCardWriter(dir.DbPath).SaveCard(new CardDto
        {
            Id = 5, Kind = "Spell", SubType = "Ritual", Name = "Rito", RitualMonsterId = 9, RequiredRitualLevel = 6
        });

        var cards = new SqliteCardLoader(dir.DbPath).LoadCards();

        var spell = Assert.IsType<SpellCard>(Assert.Single(cards));
        Assert.Equal(9, spell.RitualMonsterId);
        Assert.Equal(6, spell.RequiredRitualLevel);
    }

    [Fact]
    public void LoadCards_ParsesMonsterCategoryAndEffectId()
    {
        using var dir = new TempCardDirectory();
        new SqliteCardWriter(dir.DbPath).SaveCard(new CardDto
        {
            Id = 7, Kind = "Monster", Name = "Centinela", Attack = 800, Defense = 600, Level = 3,
            Type = "Reptile", Attribute = "Water", Category = "Effect", EffectId = "draw_1"
        });

        var cards = new SqliteCardLoader(dir.DbPath).LoadCards();

        var monster = Assert.IsType<MonsterCard>(Assert.Single(cards));
        Assert.Equal(MonsterCategory.Effect, monster.Category);
        Assert.Equal("draw_1", monster.EffectId);
    }

    [Fact]
    public void LoadCards_OrdersById()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteCardWriter(dir.DbPath);
        writer.SaveCard(new CardDto { Id = 3, Kind = "Monster", Name = "C" });
        writer.SaveCard(new CardDto { Id = 1, Kind = "Monster", Name = "A" });
        writer.SaveCard(new CardDto { Id = 2, Kind = "Monster", Name = "B" });

        var cards = new SqliteCardLoader(dir.DbPath).LoadCards();

        Assert.Equal(new[] { 1, 2, 3 }, cards.Select(c => c.Id));
    }
}
