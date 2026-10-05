using Microsoft.Data.Sqlite;
using GodotGame.Core.Entities;
using GodotGame.Data.Loaders;
using GodotGame.Data.Sqlite;
using GodotGame.Tests.Data.TestSupport;

namespace GodotGame.Tests.Data;

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
        string missing = Path.Combine(Path.GetTempPath(), "GodotGame_NoExiste_" + Guid.NewGuid() + ".db");

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
    public void LoadCards_ParsesEquipFieldsIncludingTargetFilter()
    {
        using var dir = new TempCardDirectory();
        new SqliteCardWriter(dir.DbPath).SaveCard(new CardDto
        {
            Id = 8, Kind = "Spell", SubType = "Equip", Name = "Armadura de Prueba"
        });

        int filterId;
        using (var connection = new SqliteConnection($"Data Source={dir.DbPath}"))
        {
            connection.Open();
            using (var insertFilter = connection.CreateCommand())
            {
                insertFilter.CommandText = "INSERT INTO TargetFilters (Name) VALUES ('Solo Dragon'); SELECT last_insert_rowid();";
                filterId = Convert.ToInt32((long)insertFilter.ExecuteScalar()!);
            }
            using (var insertCondition = connection.CreateCommand())
            {
                insertCondition.CommandText = "INSERT INTO TargetFilterConditions (FilterId, GroupIndex, Kind, Negate, Value) VALUES ($FilterId, 0, 'Type', 0, 'Dragon')";
                insertCondition.Parameters.AddWithValue("$FilterId", filterId);
                insertCondition.ExecuteNonQuery();
            }
            using var update = connection.CreateCommand();
            update.CommandText = """
                UPDATE Cards SET EquipTargetFilterId = $FilterId, EquipAttackModifier = 500,
                    EquipDefenseModifier = -200, EquipDuration = 'ForNTurns', EquipDurationTurns = 3
                WHERE Id = 8
                """;
            update.Parameters.AddWithValue("$FilterId", filterId);
            update.ExecuteNonQuery();
        }

        var spell = Assert.IsType<SpellCard>(Assert.Single(new SqliteCardLoader(dir.DbPath).LoadCards()));

        Assert.Equal(500, spell.EquipAttackModifier);
        Assert.Equal(-200, spell.EquipDefenseModifier);
        Assert.Equal(ModifierDuration.ForNTurns, spell.EquipDuration);
        Assert.Equal(3, spell.EquipDurationTurns);
        Assert.NotNull(spell.EquipTargetFilter);
        Assert.Single(spell.EquipTargetFilter!.OrGroups);
    }

    [Fact]
    public void LoadCards_EquipWithoutTargetFilter_DefaultsToNullFilterAndNeutralModifiers()
    {
        using var dir = new TempCardDirectory();
        new SqliteCardWriter(dir.DbPath).SaveCard(new CardDto
        {
            Id = 9, Kind = "Spell", SubType = "Equip", Name = "Armadura Generica"
        });

        var spell = Assert.IsType<SpellCard>(Assert.Single(new SqliteCardLoader(dir.DbPath).LoadCards()));

        Assert.Null(spell.EquipTargetFilter);
        Assert.Equal(0, spell.EquipAttackModifier);
        Assert.Equal(0, spell.EquipDefenseModifier);
        Assert.Equal(ModifierDuration.WhileEquipped, spell.EquipDuration);
    }

    [Fact]
    public void LoadCards_ParsesFieldTypeIncludingAffectedFilter()
    {
        using var dir = new TempCardDirectory();
        new SqliteCardWriter(dir.DbPath).SaveCard(new CardDto
        {
            Id = 10, Kind = "Spell", SubType = "Field", Name = "Volcan de Prueba"
        });

        using (var connection = new SqliteConnection($"Data Source={dir.DbPath}"))
        {
            connection.Open();
            int filterId;
            using (var insertFilter = connection.CreateCommand())
            {
                insertFilter.CommandText = "INSERT INTO TargetFilters (Name) VALUES ('Solo Dragon'); SELECT last_insert_rowid();";
                filterId = Convert.ToInt32((long)insertFilter.ExecuteScalar()!);
            }
            using (var insertCondition = connection.CreateCommand())
            {
                insertCondition.CommandText = "INSERT INTO TargetFilterConditions (FilterId, GroupIndex, Kind, Negate, Value) VALUES ($FilterId, 0, 'Type', 0, 'Dragon')";
                insertCondition.Parameters.AddWithValue("$FilterId", filterId);
                insertCondition.ExecuteNonQuery();
            }
            using (var insertFieldType = connection.CreateCommand())
            {
                insertFieldType.CommandText = """
                    INSERT INTO FieldTypes (Id, Name, Description, Color, BackgroundImage, VisualEffectsKey, AffectedFilterId, StatModifierAmount, StatModifierStat)
                    VALUES ('volcanic', 'Volcanico', 'Terreno de lava', '#FF4400', 'volcanic.png', 'volcanic_fx', $FilterId, 500, 'Both')
                    """;
                insertFieldType.Parameters.AddWithValue("$FilterId", filterId);
                insertFieldType.ExecuteNonQuery();
            }
            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE Cards SET FieldTypeId = 'volcanic' WHERE Id = 10";
            update.ExecuteNonQuery();
        }

        var spell = Assert.IsType<SpellCard>(Assert.Single(new SqliteCardLoader(dir.DbPath).LoadCards()));

        Assert.NotNull(spell.FieldType);
        Assert.Equal("Volcanico", spell.FieldType!.Name);
        Assert.Equal("#FF4400", spell.FieldType.Color);
        Assert.Equal(500, spell.FieldType.StatModifierAmount);
        Assert.Equal(FieldStatKind.Both, spell.FieldType.StatModifierStat);
        Assert.NotNull(spell.FieldType.AffectedFilter);
        Assert.Single(spell.FieldType.AffectedFilter!.OrGroups);
    }

    [Fact]
    public void LoadCards_FieldWithoutFieldTypeId_LeavesFieldTypeNull()
    {
        using var dir = new TempCardDirectory();
        new SqliteCardWriter(dir.DbPath).SaveCard(new CardDto
        {
            Id = 11, Kind = "Spell", SubType = "Field", Name = "Campo Sin Configurar"
        });

        var spell = Assert.IsType<SpellCard>(Assert.Single(new SqliteCardLoader(dir.DbPath).LoadCards()));

        Assert.Null(spell.FieldType);
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
