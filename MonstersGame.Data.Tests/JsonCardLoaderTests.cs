using MonstersGame.Core.Entities;
using MonstersGame.Data.Loaders;
using MonstersGame.Data.Tests.TestSupport;

namespace MonstersGame.Data.Tests;

public class JsonCardLoaderTests
{
    [Fact]
    public void LoadCards_ReadsOneFilePerCard_FromDirectory()
    {
        using var dir = new TempCardDirectory();
        File.WriteAllText(Path.Combine(dir.Path, "1__guerrero.json"),
            """{ "id": 1, "kind": "Monster", "name": "Guerrero", "attack": 1500, "defense": 1200, "level": 4, "type": "Warrior", "attribute": "Earth" }""");
        File.WriteAllText(Path.Combine(dir.Path, "2__trueno.json"),
            """{ "id": 2, "kind": "Spell", "subType": "Normal", "name": "Trueno" }""");

        var cards = new JsonCardLoader(dir.Path).LoadCards();

        Assert.Equal(2, cards.Count);
        var monster = Assert.IsType<MonsterCard>(cards.Single(c => c.Id == 1));
        Assert.Equal("Guerrero", monster.Name);
        Assert.Equal(1500, monster.Attack);
        var spell = Assert.IsType<SpellCard>(cards.Single(c => c.Id == 2));
        Assert.Equal(SpellSubType.Normal, spell.SubType);
    }

    [Fact]
    public void LoadCards_EmptyDirectory_ReturnsEmpty()
    {
        using var dir = new TempCardDirectory();

        var cards = new JsonCardLoader(dir.Path).LoadCards();

        Assert.Empty(cards);
    }

    [Fact]
    public void LoadCards_MissingDirectory_ReturnsEmpty()
    {
        string missing = Path.Combine(Path.GetTempPath(), "MonstersGame_NoExiste_" + Guid.NewGuid());

        var cards = new JsonCardLoader(missing).LoadCards();

        Assert.Empty(cards);
    }

    [Fact]
    public void LoadCards_ParsesRitualSpellFields()
    {
        using var dir = new TempCardDirectory();
        File.WriteAllText(Path.Combine(dir.Path, "5__rito.json"),
            """{ "id": 5, "kind": "Spell", "subType": "Ritual", "name": "Rito", "ritualMonsterId": 9, "requiredRitualLevel": 6 }""");

        var cards = new JsonCardLoader(dir.Path).LoadCards();

        var spell = Assert.IsType<SpellCard>(Assert.Single(cards));
        Assert.Equal(9, spell.RitualMonsterId);
        Assert.Equal(6, spell.RequiredRitualLevel);
    }

    [Fact]
    public void LoadCards_ParsesMonsterCategoryAndEffectId()
    {
        using var dir = new TempCardDirectory();
        File.WriteAllText(Path.Combine(dir.Path, "7__centinela.json"),
            """{ "id": 7, "kind": "Monster", "name": "Centinela", "attack": 800, "defense": 600, "level": 3, "type": "Reptile", "attribute": "Water", "category": "Effect", "effectId": "draw_1" }""");

        var cards = new JsonCardLoader(dir.Path).LoadCards();

        var monster = Assert.IsType<MonsterCard>(Assert.Single(cards));
        Assert.Equal(MonsterCategory.Effect, monster.Category);
        Assert.Equal("draw_1", monster.EffectId);
    }
}
