using MonstersGame.Core.Entities;
using MonstersGame.Core.Requirements;

namespace MonstersGame.Tests;

public class RequirementEvaluatorTests
{
    private static readonly TargetFilter DragonFilter = new(1, new IReadOnlyList<FilterCondition>[]
    {
        new[] { new FilterCondition(FilterConditionKind.Type, false, "Dragon") }
    });

    private static readonly TargetFilter WarriorFilter = new(2, new IReadOnlyList<FilterCondition>[]
    {
        new[] { new FilterCondition(FilterConditionKind.Type, false, "Warrior") }
    });

    private static MonsterCard Make(int id, string type, int level = 4) =>
        new(id, $"Carta {id}", 1000, 1000, level, type, MonsterAttribute.Fire);

    public class MaterialSlots
    {
        [Fact]
        public void TryMatch_EnoughCandidatesPerSlot_Succeeds()
        {
            var set = new RequirementSet(1, RequirementMode.MaterialSlots, new[]
            {
                new RequirementSlot(0, DragonFilter, 1, 1),
                new RequirementSlot(1, WarriorFilter, 1, 1)
            }, null);

            var candidates = new[] { Make(1, "Dragon"), Make(2, "Warrior"), Make(3, "Zombie") };

            bool matched = new MaterialSlotRequirementEvaluator().TryMatch(set, candidates, out var assignment);

            Assert.True(matched);
            Assert.NotNull(assignment);
            Assert.Equal(2, assignment!.UsedMaterials.Count);
            Assert.Contains(assignment.UsedMaterials, c => c.Id == 1);
            Assert.Contains(assignment.UsedMaterials, c => c.Id == 2);
        }

        [Fact]
        public void TryMatch_MissingCandidateForASlot_Fails()
        {
            var set = new RequirementSet(1, RequirementMode.MaterialSlots, new[]
            {
                new RequirementSlot(0, DragonFilter, 1, 1),
                new RequirementSlot(1, WarriorFilter, 1, 1)
            }, null);

            var candidates = new[] { Make(1, "Dragon"), Make(3, "Zombie") };

            bool matched = new MaterialSlotRequirementEvaluator().TryMatch(set, candidates, out var assignment);

            Assert.False(matched);
            Assert.Null(assignment);
        }

        [Fact]
        public void TryMatch_SameCardNeverAssignedToTwoSlots()
        {
            // Un solo Dragon disponible, dos huecos que ambos lo aceptarian:
            // no debe "duplicarse" para satisfacer los dos huecos.
            var set = new RequirementSet(1, RequirementMode.MaterialSlots, new[]
            {
                new RequirementSlot(0, DragonFilter, 1, 1),
                new RequirementSlot(1, DragonFilter, 1, 1)
            }, null);

            var candidates = new[] { Make(1, "Dragon") };

            bool matched = new MaterialSlotRequirementEvaluator().TryMatch(set, candidates, out var assignment);

            Assert.False(matched);
        }
    }

    public class LevelSum
    {
        [Fact]
        public void TryMatch_SumReachesThreshold_Succeeds()
        {
            var set = new RequirementSet(1, RequirementMode.LevelSum, Array.Empty<RequirementSlot>(),
                new LevelSumRequirement(TargetFilter.Any, minLevelSum: 8));

            var candidates = new[] { Make(1, "Dragon", level: 5), Make(2, "Warrior", level: 4) };

            bool matched = new LevelSumRequirementEvaluator().TryMatch(set, candidates, out var assignment);

            Assert.True(matched);
            Assert.NotNull(assignment);
            // Greedy por Nivel descendente: con el Nivel 5 solo ya no alcanza (5 &lt; 8),
            // asi que debe tomar tambien el de Nivel 4.
            Assert.Equal(9, assignment!.UsedMaterials.Sum(c => c.Level));
        }

        [Fact]
        public void TryMatch_FiltersByType_IgnoresIneligibleCandidates()
        {
            var set = new RequirementSet(1, RequirementMode.LevelSum, Array.Empty<RequirementSlot>(),
                new LevelSumRequirement(DragonFilter, minLevelSum: 4));

            var candidates = new[] { Make(1, "Warrior", level: 10) };

            bool matched = new LevelSumRequirementEvaluator().TryMatch(set, candidates, out var assignment);

            Assert.False(matched);
        }

        [Fact]
        public void TryMatch_NotEnoughLevel_Fails()
        {
            var set = new RequirementSet(1, RequirementMode.LevelSum, Array.Empty<RequirementSlot>(),
                new LevelSumRequirement(TargetFilter.Any, minLevelSum: 12));

            var candidates = new[] { Make(1, "Dragon", level: 5), Make(2, "Warrior", level: 4) };

            bool matched = new LevelSumRequirementEvaluator().TryMatch(set, candidates, out var assignment);

            Assert.False(matched);
        }
    }
}
