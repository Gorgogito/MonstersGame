using MonstersGame.Core.Battle;
using MonstersGame.Core.Entities;
using MonstersGame.Core.Requirements;
using MonstersGame.Tests.TestSupport;

namespace MonstersGame.Tests;

public class FieldTests
{
    private static readonly TargetFilter DragonOnly = new(1, new IReadOnlyList<FilterCondition>[]
    {
        new[] { new FilterCondition(FilterConditionKind.Type, false, "Dragon") }
    });

    private static readonly TargetFilter OwnedOnly = new(2, new IReadOnlyList<FilterCondition>[]
    {
        new[] { new FilterCondition(FilterConditionKind.ControllerSide, false, "Owner") }
    });

    private static FieldType MakeFieldType(string id, TargetFilter? filter, int amount, FieldStatKind stat,
        TargetFilter? opposedFilter = null, int opposedAmount = 0) =>
        new(id, $"Campo {id}", "", "#000000", "", "", filter, amount, stat, opposedFilter, opposedAmount);

    [Fact]
    public void ActivateField_WithoutFilter_BuffsAllMonstersOnBothSides()
    {
        var field = new SpellCard(9101, "Terreno Sagrado", SpellSubType.Field,
            fieldType: MakeFieldType("holy", filter: null, amount: 300, stat: FieldStatKind.Both));
        var (engine, human, cpu) = TestDuelFactory.Create();
        human.Hand.Add(field);
        var ownMonster = TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong);
        var rivalMonster = TestDuelFactory.PlaceOnField(cpu, 0, TestCards.Level4Weak);

        var result = engine.ActivateSpell(0);

        Assert.True(result.Success);
        Assert.Equal(TestCards.Level4Strong.Attack + 300, EffectiveStats.EffectiveAttack(ownMonster, engine.State, human));
        Assert.Equal(TestCards.Level4Strong.Defense + 300, EffectiveStats.EffectiveDefense(ownMonster, engine.State, human));
        Assert.Equal(TestCards.Level4Weak.Attack + 300, EffectiveStats.EffectiveAttack(rivalMonster, engine.State, cpu));
    }

    [Fact]
    public void ActivateField_WithOwnerOnlyFilter_DoesNotAffectOpponentMonsters()
    {
        var field = new SpellCard(9102, "Fortaleza Propia", SpellSubType.Field,
            fieldType: MakeFieldType("fortress", OwnedOnly, amount: 400, stat: FieldStatKind.Attack));
        var (engine, human, cpu) = TestDuelFactory.Create();
        human.Hand.Add(field);
        var ownMonster = TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong);
        var rivalMonster = TestDuelFactory.PlaceOnField(cpu, 0, TestCards.Level4Weak);

        engine.ActivateSpell(0);

        Assert.Equal(TestCards.Level4Strong.Attack + 400, EffectiveStats.EffectiveAttack(ownMonster, engine.State, human));
        Assert.Equal(TestCards.Level4Weak.Attack, EffectiveStats.EffectiveAttack(rivalMonster, engine.State, cpu));
    }

    [Fact]
    public void ActivateField_WithTypeFilter_OnlyAffectsMatchingType()
    {
        var field = new SpellCard(9103, "Nido de Dragones", SpellSubType.Field,
            fieldType: MakeFieldType("dragon_nest", DragonOnly, amount: 500, stat: FieldStatKind.Attack));
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(field);
        var dragon = TestDuelFactory.PlaceOnField(human, 0, TestCards.FusionMaterialA); // Type = Dragon
        var warrior = TestDuelFactory.PlaceOnField(human, 1, TestCards.Level4Strong); // Type = Warrior

        engine.ActivateSpell(0);

        Assert.Equal(TestCards.FusionMaterialA.Attack + 500, EffectiveStats.EffectiveAttack(dragon, engine.State, human));
        Assert.Equal(TestCards.Level4Strong.Attack, EffectiveStats.EffectiveAttack(warrior, engine.State, human));
    }

    [Fact]
    public void ActivateField_MonsterEnteringAfterActivation_AlsoReceivesTheModifier()
    {
        var field = new SpellCard(9104, "Terreno Sagrado", SpellSubType.Field,
            fieldType: MakeFieldType("holy2", filter: null, amount: 200, stat: FieldStatKind.Defense));
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(field);
        engine.ActivateSpell(0);

        // El monstruo se coloca DESPUES de que la Carta de Campo ya esta activa.
        var lateArrival = TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong);

        Assert.Equal(TestCards.Level4Strong.Defense + 200, EffectiveStats.EffectiveDefense(lateArrival, engine.State, human));
    }

    [Fact]
    public void ActivateField_ReplacingWithAnotherFieldSpell_RemovesThePreviousModifier()
    {
        var oldField = new SpellCard(9105, "Campo Viejo", SpellSubType.Field,
            fieldType: MakeFieldType("old", filter: null, amount: 500, stat: FieldStatKind.Attack));
        var newField = new SpellCard(9106, "Campo Nuevo", SpellSubType.Field,
            fieldType: MakeFieldType("new", filter: null, amount: 100, stat: FieldStatKind.Attack));
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(oldField);
        human.Hand.Add(newField);
        var monster = TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong);

        engine.ActivateSpell(0); // activa oldField
        Assert.Equal(TestCards.Level4Strong.Attack + 500, EffectiveStats.EffectiveAttack(monster, engine.State, human));

        engine.ActivateSpell(0); // el indice 0 ahora es newField; reemplaza a oldField en la Zona del Campo

        Assert.Equal(TestCards.Level4Strong.Attack + 100, EffectiveStats.EffectiveAttack(monster, engine.State, human));
        Assert.Contains(oldField, human.Graveyard);
    }

    [Fact]
    public void ActivateField_WithOpposedFilter_BoostsMatchingAttribute_AndPenalizesTheOpposedOne()
    {
        // Terreno elemental "de verdad" (adaptacion de esfuerzo medio): un
        // solo FieldType da +500 ATK a Fuego (filtro principal) y -500 ATK a
        // Agua (filtro opuesto, independiente) en el mismo Campo.
        var fireOnly = new TargetFilter(3, new IReadOnlyList<FilterCondition>[]
        {
            new[] { new FilterCondition(FilterConditionKind.Attribute, false, "Fire") }
        });
        var waterOnly = new TargetFilter(4, new IReadOnlyList<FilterCondition>[]
        {
            new[] { new FilterCondition(FilterConditionKind.Attribute, false, "Water") }
        });
        var field = new SpellCard(9108, "Terreno de Fuego de Prueba", SpellSubType.Field,
            fieldType: MakeFieldType("terrain_fire_test", fireOnly, amount: 500, stat: FieldStatKind.Attack,
                opposedFilter: waterOnly, opposedAmount: -500));
        var (engine, human, cpu) = TestDuelFactory.Create();
        human.Hand.Add(field);
        var fireMonster = TestDuelFactory.PlaceOnField(human, 0, TestCards.Level6OneTribute); // Atributo Fuego
        var waterMonster = TestDuelFactory.PlaceOnField(cpu, 0, TestCards.FlipEffectMonster); // Atributo Agua

        engine.ActivateSpell(0);

        Assert.Equal(TestCards.Level6OneTribute.Attack + 500, EffectiveStats.EffectiveAttack(fireMonster, engine.State, human));
        Assert.Equal(TestCards.FlipEffectMonster.Attack - 500, EffectiveStats.EffectiveAttack(waterMonster, engine.State, cpu));
    }

    [Fact]
    public void ActivateField_WithoutOpposedFilter_OnlyAppliesTheMainModifier()
    {
        // Sin OpposedFilter (null), el comportamiento es identico al de
        // antes de esta adaptacion -- ningun FieldType existente cambia.
        var fireOnly = new TargetFilter(5, new IReadOnlyList<FilterCondition>[]
        {
            new[] { new FilterCondition(FilterConditionKind.Attribute, false, "Fire") }
        });
        var field = new SpellCard(9109, "Terreno Sin Penalizacion", SpellSubType.Field,
            fieldType: MakeFieldType("terrain_no_malus", fireOnly, amount: 500, stat: FieldStatKind.Attack));
        var (engine, human, cpu) = TestDuelFactory.Create();
        human.Hand.Add(field);
        var waterMonster = TestDuelFactory.PlaceOnField(cpu, 0, TestCards.FlipEffectMonster);

        engine.ActivateSpell(0);

        Assert.Equal(TestCards.FlipEffectMonster.Attack, EffectiveStats.EffectiveAttack(waterMonster, engine.State, cpu));
    }

    [Fact]
    public void ActivateField_WithoutFieldType_HasNoModifier()
    {
        var field = new SpellCard(9107, "Campo Sin Configurar", SpellSubType.Field);
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(field);
        var monster = TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong);

        engine.ActivateSpell(0);

        Assert.Equal(TestCards.Level4Strong.Attack, EffectiveStats.EffectiveAttack(monster, engine.State, human));
    }
}
