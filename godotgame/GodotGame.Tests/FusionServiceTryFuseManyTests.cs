using GodotGame.Core.Entities;
using GodotGame.Core.Requirements;
using GodotGame.Core.Services;

namespace GodotGame.Tests;

public class FusionServiceTryFuseManyTests
{
    private static readonly TargetFilter DragonFilter = new(1, new IReadOnlyList<FilterCondition>[]
    {
        new[] { new FilterCondition(FilterConditionKind.Type, false, "Dragon") }
    });

    private static readonly TargetFilter WarriorFilter = new(2, new IReadOnlyList<FilterCondition>[]
    {
        new[] { new FilterCondition(FilterConditionKind.Type, false, "Warrior") }
    });

    private static readonly MonsterCard DragonMaterial =
        new(101, "Dragon Generico", 1600, 1200, 4, "Dragon", MonsterAttribute.Wind);

    private static readonly MonsterCard WarriorMaterial =
        new(102, "Guerrero Generico", 1200, 800, 4, "Warrior", MonsterAttribute.Earth);

    private static readonly MonsterCard UnrelatedCard =
        new(103, "Zombie Generico", 500, 500, 2, "Zombie", MonsterAttribute.Dark);

    private static readonly MonsterCard FusionResult =
        new(104, "Fusion Generica", 2800, 2000, 7, "Dragon", MonsterAttribute.Wind, MonsterCategory.Fusion);

    private static FusionService CreateService()
    {
        var requirement = new RequirementSet(1, RequirementMode.MaterialSlots, new[]
        {
            new RequirementSlot(0, DragonFilter, 1, 1),
            new RequirementSlot(1, WarriorFilter, 1, 1)
        }, null);

        var recipe = new FusionRecipe(materialAId: 0, materialBId: 0, resultId: FusionResult.Id, id: 1, requirement: requirement);

        var database = new CardDatabase(new Card[] { DragonMaterial, WarriorMaterial, UnrelatedCard, FusionResult });
        return new FusionService(new[] { recipe }, database);
    }

    [Fact]
    public void TryFuseMany_CandidatesSatisfyAllSlots_ReturnsResultAndUsedMaterials()
    {
        var service = CreateService();

        var result = service.TryFuseMany(new[] { DragonMaterial, WarriorMaterial, UnrelatedCard }, out var used);

        Assert.Equal(FusionResult, result);
        Assert.Equal(2, used.Count);
        Assert.Contains(DragonMaterial, used);
        Assert.Contains(WarriorMaterial, used);
        Assert.DoesNotContain(UnrelatedCard, used);
    }

    [Fact]
    public void TryFuseMany_MissingRequiredType_ReturnsNull()
    {
        var service = CreateService();

        var result = service.TryFuseMany(new[] { DragonMaterial, UnrelatedCard }, out var used);

        Assert.Null(result);
        Assert.Empty(used);
    }

    [Fact]
    public void TryFuseMany_DoesNotAffectLegacyTwoCardFusion()
    {
        var legacyRecipe = new FusionRecipe(DragonMaterial.Id, WarriorMaterial.Id, FusionResult.Id);
        var database = new CardDatabase(new Card[] { DragonMaterial, WarriorMaterial, FusionResult });
        var service = new FusionService(new[] { legacyRecipe }, database);

        var legacyResult = service.TryFuse(DragonMaterial, WarriorMaterial);
        var genericResult = service.TryFuseMany(new[] { DragonMaterial, WarriorMaterial }, out var used);

        Assert.Equal(FusionResult, legacyResult);
        Assert.Null(genericResult); // la receta legacy no tiene Requirement: TryFuseMany no la ve.
        Assert.Empty(used);
    }

    [Fact]
    public void TryFuseMany_PrefersTheRecipeThatConsumesMoreCandidates_OverTheFirstSatisfiableOne()
    {
        // Bug real encontrado al conectar la IA a FuseMany: si una receta de
        // menos huecos (registrada primero) tambien queda satisfecha por un
        // subconjunto de los candidatos, no debe "tapar" a otra receta mas
        // especifica que aprovecharia TODOS los candidatos -- el llamador
        // (DuelEngine.FuseMany, BasicCpuAI) selecciono esas cartas esperando
        // consumirlas todas.
        var twoSlotRequirement = new RequirementSet(1, RequirementMode.MaterialSlots, new[]
        {
            new RequirementSlot(0, DragonFilter, 1, 1),
            new RequirementSlot(1, WarriorFilter, 1, 1)
        }, null);
        var twoSlotRecipe = new FusionRecipe(0, 0, resultId: UnrelatedCard.Id, id: 1, requirement: twoSlotRequirement);

        var threeSlotRequirement = new RequirementSet(2, RequirementMode.MaterialSlots, new[]
        {
            new RequirementSlot(0, DragonFilter, 1, 1),
            new RequirementSlot(1, WarriorFilter, 1, 1),
            new RequirementSlot(2, TargetFilter.Any, 1, 1)
        }, null);
        var threeSlotRecipe = new FusionRecipe(0, 0, resultId: FusionResult.Id, id: 2, requirement: threeSlotRequirement);

        var extraMaterial = new MonsterCard(105, "Tercer Material", 100, 100, 1, "Beast", MonsterAttribute.Earth);
        var database = new CardDatabase(new Card[] { DragonMaterial, WarriorMaterial, extraMaterial, UnrelatedCard, FusionResult });
        // twoSlotRecipe registrada ANTES que threeSlotRecipe a proposito.
        var service = new FusionService(new[] { twoSlotRecipe, threeSlotRecipe }, database);

        var result = service.TryFuseMany(new[] { DragonMaterial, WarriorMaterial, extraMaterial }, out var used);

        Assert.Equal(FusionResult, result);
        Assert.Equal(3, used.Count);
    }
}
