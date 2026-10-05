using GodotGame.Core.AI;
using GodotGame.Core.Battle;
using GodotGame.Core.Entities;
using GodotGame.Core.Requirements;
using GodotGame.Core.Services;
using GodotGame.Tests.TestSupport;

namespace GodotGame.Tests;

/// <summary>
/// Verifica que <see cref="BasicCpuAI"/> tambien considera recetas de Fusion
/// genericas de 3+ materiales (via <c>DuelEngine.FuseMany</c>), no solo pares
/// exactos -- el ultimo tramo de la conexion del motor de requisitos
/// (Fase 1) a algo realmente jugable, ahora tambien para la CPU.
/// </summary>
public class BasicCpuAIFusionTests
{
    private static (DuelEngine Engine, Player Cpu, BasicCpuAI Ai) Setup(IReadOnlyList<FusionRecipe> recipes)
    {
        var (engine, _, cpu) = TestDuelFactory.Create(recipes: recipes, firstPlayerIndex: 1);
        var ai = new BasicCpuAI(new FusionService(recipes, new CardDatabase(TestCards.All)));
        return (engine, cpu, ai);
    }

    [Fact]
    public void Step_LegacyTwoCardRecipe_StillFusesInsteadOfSummoning()
    {
        var (engine, cpu, ai) = Setup(new[] { TestCards.ValidFusionRecipe });
        cpu.Hand.Add(TestCards.FusionMaterialA);
        cpu.Hand.Add(TestCards.FusionMaterialB);

        ai.Step(engine, selfIndex: 1);

        Assert.Equal(TestCards.FusionResult, cpu.MonsterZones[0]?.Card);
        Assert.Empty(cpu.Hand);
    }

    [Fact]
    public void Step_GenericThreeMaterialRecipe_FusesAllThree()
    {
        var requirement = new RequirementSet(1, RequirementMode.MaterialSlots, new[]
        {
            new RequirementSlot(0, TargetFilter.Any, 1, 1),
            new RequirementSlot(1, TargetFilter.Any, 1, 1),
            new RequirementSlot(2, TargetFilter.Any, 1, 1)
        }, null);
        var recipe = new FusionRecipe(0, 0, TestCards.FusionResult.Id, id: 1, requirement: requirement);

        var (engine, cpu, ai) = Setup(new[] { recipe });
        cpu.Hand.Add(TestCards.Level4Strong);
        cpu.Hand.Add(TestCards.Level4Weak);
        cpu.Hand.Add(TestCards.HighDefense);

        ai.Step(engine, selfIndex: 1);

        Assert.Equal(TestCards.FusionResult, cpu.MonsterZones[0]?.Card);
        Assert.Empty(cpu.Hand);
    }

    [Fact]
    public void Step_PrefersHigherAttackResult_AcrossDifferentRecipeSizes()
    {
        // Receta de 2 materiales con resultado debil, y otra de 3 con
        // resultado fuerte: la IA debe elegir la de 3, no la primera que
        // encuentre.
        var weakResult = TestCards.Level2Fodder; // ATK 400
        var strongResult = TestCards.FusionResult; // ATK 2800

        var twoSlotRequirement = new RequirementSet(2, RequirementMode.MaterialSlots, new[]
        {
            new RequirementSlot(0, TargetFilter.Any, 1, 1),
            new RequirementSlot(1, TargetFilter.Any, 1, 1)
        }, null);
        var weakRecipe = new FusionRecipe(0, 0, weakResult.Id, id: 2, requirement: twoSlotRequirement);

        var threeSlotRequirement = new RequirementSet(3, RequirementMode.MaterialSlots, new[]
        {
            new RequirementSlot(0, TargetFilter.Any, 1, 1),
            new RequirementSlot(1, TargetFilter.Any, 1, 1),
            new RequirementSlot(2, TargetFilter.Any, 1, 1)
        }, null);
        var strongRecipe = new FusionRecipe(0, 0, strongResult.Id, id: 3, requirement: threeSlotRequirement);

        var (engine, cpu, ai) = Setup(new[] { weakRecipe, strongRecipe });
        cpu.Hand.Add(TestCards.Level4Strong);
        cpu.Hand.Add(TestCards.Level4Weak);
        cpu.Hand.Add(TestCards.HighDefense);

        ai.Step(engine, selfIndex: 1);

        Assert.Equal(strongResult, cpu.MonsterZones[0]?.Card);
    }

    [Fact]
    public void Step_FusionResultWeakerThanSimpleSummon_SummonsInsteadOfFusing()
    {
        // Regresion: si la mejor fusion posible (generica o legacy) es mas
        // debil que el mejor monstruo invocable sin sacrificio, la IA no
        // deberia fusionar -- mismo comportamiento que antes de conectar
        // TryFuseMany.
        var requirement = new RequirementSet(4, RequirementMode.MaterialSlots, new[]
        {
            new RequirementSlot(0, TargetFilter.Any, 1, 1),
            new RequirementSlot(1, TargetFilter.Any, 1, 1)
        }, null);
        var weakRecipe = new FusionRecipe(0, 0, TestCards.Level2Fodder.Id, id: 4, requirement: requirement); // ATK 400

        var (engine, cpu, ai) = Setup(new[] { weakRecipe });
        cpu.Hand.Add(TestCards.Level4Weak);   // ATK 1000, sin tributo
        cpu.Hand.Add(TestCards.HighDefense);  // ATK 300, sin tributo

        ai.Step(engine, selfIndex: 1);

        // No fusiono: la mano sigue teniendo ambas cartas menos la que se invoco.
        Assert.Single(cpu.Hand);
        Assert.NotNull(cpu.MonsterZones[0]);
        Assert.Equal(TestCards.Level4Weak, cpu.MonsterZones[0]!.Card); // invoco la de mayor ATK sin sacrificio
    }
}
