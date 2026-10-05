using MonstersGame.Core.Entities;
using MonstersGame.Core.Requirements;
using MonstersGame.Tests.TestSupport;

namespace MonstersGame.Tests;

/// <summary>
/// Verifica que <see cref="MonstersGame.Core.Battle.DuelEngine.Fuse"/>,
/// <see cref="MonstersGame.Core.Battle.DuelEngine.FuseMany"/> y
/// <see cref="MonstersGame.Core.Battle.DuelEngine.RitualSummon"/> realmente
/// consultan el motor de requisitos generico (Fase 1) y no solo los campos
/// legacy -- el vacio que se detecto al cerrar la Fase 6.
/// </summary>
public class GenericFusionAndRitualWiringTests
{
    private static readonly TargetFilter DragonFilter = new(1, new IReadOnlyList<FilterCondition>[]
    {
        new[] { new FilterCondition(FilterConditionKind.Type, false, "Dragon") }
    });

    private static readonly TargetFilter WarriorFilter = new(2, new IReadOnlyList<FilterCondition>[]
    {
        new[] { new FilterCondition(FilterConditionKind.Type, false, "Warrior") }
    });

    [Fact]
    public void Fuse_TwoCards_FallsBackToGenericTwoSlotRecipe_WhenNoLegacyRecipeExists()
    {
        var requirement = new RequirementSet(1, RequirementMode.MaterialSlots, new[]
        {
            new RequirementSlot(0, DragonFilter, 1, 1),
            new RequirementSlot(1, WarriorFilter, 1, 1)
        }, null);
        var genericRecipe = new FusionRecipe(materialAId: 0, materialBId: 0, resultId: TestCards.FusionResult.Id, id: 1, requirement: requirement);

        var (engine, human, _) = TestDuelFactory.Create(recipes: new[] { genericRecipe });
        human.Hand.Add(TestCards.FusionMaterialA); // Type = Dragon
        human.Hand.Add(TestCards.Level4Strong);    // Type = Warrior

        var result = engine.Fuse(0, 1);

        Assert.True(result.Success);
        Assert.Equal(TestCards.FusionResult, human.MonsterZones[0]!.Card);
        Assert.Empty(human.Hand);
    }

    [Fact]
    public void Fuse_StillPrefersLegacyExactRecipe_WhenBothWouldMatch()
    {
        // Si existe una receta legacy de 2 exactas para este par, Fuse no
        // deberia romperse ni preferir una generica que tambien aplicara.
        var (engine, human, _) = TestDuelFactory.Create(recipes: new[] { TestCards.ValidFusionRecipe });
        human.Hand.Add(TestCards.FusionMaterialA);
        human.Hand.Add(TestCards.FusionMaterialB);

        var result = engine.Fuse(0, 1);

        Assert.True(result.Success);
        Assert.Equal(TestCards.FusionResult, human.MonsterZones[0]!.Card);
    }

    [Fact]
    public void FuseMany_ThreeMaterialGenericRecipe_ConsumesExactlySelectedCards()
    {
        var requirement = new RequirementSet(2, RequirementMode.MaterialSlots, new[]
        {
            new RequirementSlot(0, TargetFilter.Any, 1, 1),
            new RequirementSlot(1, TargetFilter.Any, 1, 1),
            new RequirementSlot(2, TargetFilter.Any, 1, 1)
        }, null);
        var recipe = new FusionRecipe(0, 0, TestCards.FusionResult.Id, id: 2, requirement: requirement);

        var (engine, human, _) = TestDuelFactory.Create(recipes: new[] { recipe });
        human.Hand.Add(TestCards.Level4Strong);
        human.Hand.Add(TestCards.Level4Weak);
        human.Hand.Add(TestCards.HighDefense);

        var result = engine.FuseMany(new[] { 0, 1, 2 });

        Assert.True(result.Success);
        Assert.Empty(human.Hand);
        Assert.Equal(TestCards.FusionResult, human.MonsterZones[0]!.Card);
    }

    [Fact]
    public void FuseMany_ExtraUnneededCardSelected_FailsWithoutConsumingAnything()
    {
        var requirement = new RequirementSet(3, RequirementMode.MaterialSlots, new[]
        {
            new RequirementSlot(0, TargetFilter.Any, 1, 1),
            new RequirementSlot(1, TargetFilter.Any, 1, 1)
        }, null);
        var recipe = new FusionRecipe(0, 0, TestCards.FusionResult.Id, id: 3, requirement: requirement);

        var (engine, human, _) = TestDuelFactory.Create(recipes: new[] { recipe });
        human.Hand.Add(TestCards.Level4Strong);
        human.Hand.Add(TestCards.Level4Weak);
        human.Hand.Add(TestCards.HighDefense); // de mas: la receta solo necesita 2

        var result = engine.FuseMany(new[] { 0, 1, 2 });

        Assert.False(result.Success);
        Assert.Equal(3, human.Hand.Count);
    }

    [Fact]
    public void FuseMany_LessThanTwoCards_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.Level4Strong);

        var result = engine.FuseMany(new[] { 0 });

        Assert.False(result.Success);
    }

    [Fact]
    public void RitualSummon_WithRequirementFilter_ExplicitTributeNotMatchingFilter_Fails()
    {
        var requirement = new RequirementSet(4, RequirementMode.LevelSum, Array.Empty<RequirementSlot>(),
            new LevelSumRequirement(DragonFilter, minLevelSum: 6));
        var ritualSpell = new SpellCard(9301, "Ritual Filtrado", SpellSubType.Ritual,
            ritualMonsterId: TestCards.RitualMonster.Id, requiredRitualLevel: 6, requirement: requirement);

        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(ritualSpell);
        human.Hand.Add(TestCards.RitualMonster);
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level6OneTribute); // Nivel 6 pero Tipo Beast, no Dragon

        var result = engine.RitualSummon(0, 1, handTributeIndices: Array.Empty<int>(), fieldTributeZones: new[] { 0 });

        Assert.False(result.Success);
        Assert.NotNull(human.MonsterZones[0]); // nada se sacrifica si la validacion falla
    }

    [Fact]
    public void RitualSummon_WithRequirementFilter_ExplicitMatchingTribute_Succeeds()
    {
        var requirement = new RequirementSet(5, RequirementMode.LevelSum, Array.Empty<RequirementSlot>(),
            new LevelSumRequirement(DragonFilter, minLevelSum: 4));
        var ritualSpell = new SpellCard(9302, "Ritual Filtrado", SpellSubType.Ritual,
            ritualMonsterId: TestCards.RitualMonster.Id, requiredRitualLevel: 4, requirement: requirement);

        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(ritualSpell);
        human.Hand.Add(TestCards.RitualMonster);
        TestDuelFactory.PlaceOnField(human, 0, TestCards.FusionMaterialA); // Dragon, Nivel 4

        var result = engine.RitualSummon(0, 1, handTributeIndices: Array.Empty<int>(), fieldTributeZones: new[] { 0 });

        Assert.True(result.Success);
        Assert.Equal(TestCards.RitualMonster, human.MonsterZones[0]!.Card);
    }

    [Fact]
    public void RitualSummon_WithRequirementFilter_AutoSelectSkipsNonMatchingMonsters()
    {
        var requirement = new RequirementSet(6, RequirementMode.LevelSum, Array.Empty<RequirementSlot>(),
            new LevelSumRequirement(DragonFilter, minLevelSum: 6));
        var ritualSpell = new SpellCard(9303, "Ritual Filtrado", SpellSubType.Ritual,
            ritualMonsterId: TestCards.RitualMonster.Id, requiredRitualLevel: 6, requirement: requirement);

        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(ritualSpell);
        human.Hand.Add(TestCards.RitualMonster);
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level6OneTribute); // Nivel 6, no es Dragon: auto-select debe ignorarlo
        TestDuelFactory.PlaceOnField(human, 1, TestCards.FusionMaterialA);  // Dragon, Nivel 4: no alcanza solo

        var result = engine.RitualSummon(0, 1); // sin seleccion explicita

        Assert.False(result.Success); // 4 < 6: el monstruo que no es Dragon nunca se considero
        Assert.NotNull(human.MonsterZones[0]);
        Assert.NotNull(human.MonsterZones[1]);
    }

    [Fact]
    public void RitualSummon_WithoutRequirement_StillUsesLegacyFieldsUnchanged()
    {
        // Cobertura de regresion explicita: una carta sin Requirement (todas
        // las fixtures existentes) debe comportarse exactamente igual que
        // antes de conectar el Ritual al motor generico.
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level6OneTribute);
        human.Hand.Add(TestCards.RitualSummonSpell);
        human.Hand.Add(TestCards.RitualMonster);

        var result = engine.RitualSummon(0, 1, handTributeIndices: Array.Empty<int>(), fieldTributeZones: new[] { 0 });

        Assert.True(result.Success);
    }
}
