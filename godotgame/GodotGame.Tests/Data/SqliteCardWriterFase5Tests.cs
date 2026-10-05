using GodotGame.Data.Loaders;
using GodotGame.Data.Sqlite;
using GodotGame.Tests.Data.TestSupport;

namespace GodotGame.Tests.Data;

/// <summary>
/// Round-trip completo (Guardar -&gt; Recargar) de las estructuras nuevas del
/// editor (Fase 5): materiales de Fusion, filtro de Ritual, objetivo/duracion
/// de Equip, tipo de Campo, y efecto compuesto. Es la parte mas critica de
/// verificar: si el guardado o la relectura fallan en silencio, el editor
/// parece funcionar pero no persiste nada.
/// </summary>
public class SqliteCardWriterFase5Tests
{
    private static FilterDto DragonFilter() => new()
    {
        OrGroups = { new List<FilterConditionDto> { new() { Kind = "Type", Negate = false, Value = "Dragon" } } }
    };

    [Fact]
    public void SaveCard_FusionWithMaterials_RoundTripsThroughFusionsTable()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteCardWriter(dir.DbPath);

        var dto = new CardDto
        {
            Id = 100, Kind = "Monster", Name = "Fusion de Prueba", Attack = 2500, Defense = 2000, Level = 7,
            Type = "Dragon", Attribute = "Wind", Category = "Fusion",
            FusionMaterials =
            {
                new FusionSlotDto { Filter = DragonFilter(), MinCount = 1, MaxCount = 1 },
                new FusionSlotDto { Filter = new FilterDto(), MinCount = 1, MaxCount = 2 }
            }
        };

        writer.SaveCard(dto);
        var reloaded = writer.LoadAllDtos().Single(c => c.Id == 100);

        Assert.Equal(2, reloaded.FusionMaterials.Count);
        Assert.Single(reloaded.FusionMaterials[0].Filter.OrGroups);
        Assert.Equal("Dragon", reloaded.FusionMaterials[0].Filter.OrGroups[0][0].Value);
        Assert.True(reloaded.FusionMaterials[1].Filter.IsEmpty);
        Assert.Equal(1, reloaded.FusionMaterials[1].MinCount);
        Assert.Equal(2, reloaded.FusionMaterials[1].MaxCount);
    }

    [Fact]
    public void SaveCard_FusionWithoutMaterials_SavesNoFusionsRow()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteCardWriter(dir.DbPath);
        writer.SaveCard(new CardDto { Id = 101, Kind = "Monster", Name = "Sin Receta", Attack = 100, Defense = 100, Level = 4, Type = "Dragon", Attribute = "Wind", Category = "Fusion" });

        var reloaded = writer.LoadAllDtos().Single(c => c.Id == 101);

        Assert.Empty(reloaded.FusionMaterials);
    }

    [Fact]
    public void SaveCard_ResavingFusion_ReplacesMaterialsInsteadOfAccumulating()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteCardWriter(dir.DbPath);
        var dto = new CardDto
        {
            Id = 102, Kind = "Monster", Name = "Fusion Editable", Attack = 100, Defense = 100, Level = 4, Type = "Dragon", Attribute = "Wind", Category = "Fusion",
            FusionMaterials = { new FusionSlotDto { Filter = DragonFilter() }, new FusionSlotDto { Filter = DragonFilter() } }
        };
        writer.SaveCard(dto);

        dto.FusionMaterials = new List<FusionSlotDto> { new() { Filter = new FilterDto() } };
        writer.SaveCard(dto);

        var reloaded = writer.LoadAllDtos().Single(c => c.Id == 102);
        Assert.Single(reloaded.FusionMaterials);
    }

    [Fact]
    public void SaveCard_RitualWithFilter_RoundTrips()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteCardWriter(dir.DbPath);
        var dto = new CardDto
        {
            Id = 200, Kind = "Spell", Name = "Ritual de Prueba", SubType = "Ritual",
            RitualMonsterId = 1, RequiredRitualLevel = 8, RitualFilter = DragonFilter()
        };

        writer.SaveCard(dto);
        var reloaded = writer.LoadAllDtos().Single(c => c.Id == 200);

        Assert.Equal(8, reloaded.RequiredRitualLevel);
        Assert.NotNull(reloaded.RitualFilter);
        Assert.Equal("Dragon", reloaded.RitualFilter!.OrGroups[0][0].Value);
    }

    [Fact]
    public void SaveCard_EquipWithFilterAndModifiers_RoundTrips()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteCardWriter(dir.DbPath);
        var dto = new CardDto
        {
            Id = 300, Kind = "Spell", Name = "Equip de Prueba", SubType = "Equip",
            EquipTargetFilter = DragonFilter(), EquipAttackModifier = 500, EquipDefenseModifier = -200,
            EquipDuration = "ForNTurns", EquipDurationTurns = 3
        };

        writer.SaveCard(dto);
        var reloaded = writer.LoadAllDtos().Single(c => c.Id == 300);

        Assert.NotNull(reloaded.EquipTargetFilter);
        Assert.Equal("Dragon", reloaded.EquipTargetFilter!.OrGroups[0][0].Value);
        Assert.Equal(500, reloaded.EquipAttackModifier);
        Assert.Equal(-200, reloaded.EquipDefenseModifier);
        Assert.Equal("ForNTurns", reloaded.EquipDuration);
        Assert.Equal(3, reloaded.EquipDurationTurns);
    }

    [Fact]
    public void SaveCard_FieldWithFieldTypeId_RoundTrips()
    {
        using var dir = new TempCardDirectory();
        new SqliteFieldTypeWriter(dir.DbPath).Save(new FieldTypeDto { Id = "volcanic", Name = "Volcanico" });

        var writer = new SqliteCardWriter(dir.DbPath);
        writer.SaveCard(new CardDto { Id = 400, Kind = "Spell", Name = "Campo de Prueba", SubType = "Field", FieldTypeId = "volcanic" });

        var reloaded = writer.LoadAllDtos().Single(c => c.Id == 400);
        Assert.Equal("volcanic", reloaded.FieldTypeId);
    }

    [Fact]
    public void SaveCard_ComposedEffect_RoundTripsTriggerTargetAndSteps()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteCardWriter(dir.DbPath);
        var dto = new CardDto
        {
            Id = 500, Kind = "Spell", Name = "Efecto Compuesto", SubType = "Normal",
            ComposeCustomEffect = true,
            EffectTrigger = "Activate",
            EffectRequiresTarget = true,
            EffectTargetKind = "MonsterZone",
            EffectTargetFilter = DragonFilter(),
            EffectActionSteps =
            {
                new EffectActionStepDto { ActionKind = "draw_card", ParamsText = "Count=2" },
                new EffectActionStepDto { ActionKind = "destroy_target_monster", ParamsText = "" }
            }
        };

        writer.SaveCard(dto);
        var reloaded = writer.LoadAllDtos().Single(c => c.Id == 500);

        Assert.Equal("card_500_effect", reloaded.EffectId);
        Assert.True(reloaded.ComposeCustomEffect);
        Assert.Equal("Activate", reloaded.EffectTrigger);
        Assert.True(reloaded.EffectRequiresTarget);
        Assert.Equal("MonsterZone", reloaded.EffectTargetKind);
        Assert.Equal("Dragon", reloaded.EffectTargetFilter!.OrGroups[0][0].Value);
        Assert.Equal(2, reloaded.EffectActionSteps.Count);
        Assert.Equal("draw_card", reloaded.EffectActionSteps[0].ActionKind);
        Assert.Equal("Count=2", reloaded.EffectActionSteps[0].ParamsText);
        Assert.Equal("destroy_target_monster", reloaded.EffectActionSteps[1].ActionKind);
    }

    [Fact]
    public void SaveCard_ResavingComposedEffect_ReplacesStepsInsteadOfDuplicating()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteCardWriter(dir.DbPath);
        var dto = new CardDto
        {
            Id = 501, Kind = "Spell", Name = "Efecto Reeditado", SubType = "Normal",
            ComposeCustomEffect = true, EffectTrigger = "Any",
            EffectActionSteps = { new EffectActionStepDto { ActionKind = "draw_card", ParamsText = "Count=1" } }
        };
        writer.SaveCard(dto);

        dto.EffectActionSteps = new List<EffectActionStepDto> { new() { ActionKind = "negate_activation" } };
        writer.SaveCard(dto);

        var reloaded = writer.LoadAllDtos().Single(c => c.Id == 501);
        Assert.Single(reloaded.EffectActionSteps);
        Assert.Equal("negate_activation", reloaded.EffectActionSteps[0].ActionKind);
    }

    [Fact]
    public void SaveCard_ComposedEffect_RoundTripsVisualProfileKey()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteCardWriter(dir.DbPath);
        var dto = new CardDto
        {
            Id = 502, Kind = "Spell", Name = "Efecto Con Perfil Visual", SubType = "Normal",
            ComposeCustomEffect = true, EffectTrigger = "Activate",
            EffectVisualProfileKey = "summon.ritual",
            EffectActionSteps = { new EffectActionStepDto { ActionKind = "draw_card", ParamsText = "Count=1" } }
        };

        writer.SaveCard(dto);
        var reloaded = writer.LoadAllDtos().Single(c => c.Id == 502);

        Assert.Equal("summon.ritual", reloaded.EffectVisualProfileKey);
    }

    [Fact]
    public void SaveCard_ComposedEffectWithoutVisualProfileKey_RoundTripsAsEmpty()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteCardWriter(dir.DbPath);
        writer.SaveCard(new CardDto
        {
            Id = 503, Kind = "Spell", Name = "Sin Perfil Visual", SubType = "Normal",
            ComposeCustomEffect = true, EffectTrigger = "Any",
            EffectActionSteps = { new EffectActionStepDto { ActionKind = "draw_card", ParamsText = "Count=1" } }
        });

        var reloaded = writer.LoadAllDtos().Single(c => c.Id == 503);

        Assert.Equal("", reloaded.EffectVisualProfileKey);
    }

    [Fact]
    public void SaveCard_LegacyEffectId_IsNotTreatedAsComposed()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteCardWriter(dir.DbPath);
        writer.SaveCard(new CardDto { Id = 600, Kind = "Spell", Name = "Efecto Heredado", SubType = "Normal", EffectId = "draw_1" });

        var reloaded = writer.LoadAllDtos().Single(c => c.Id == 600);

        Assert.False(reloaded.ComposeCustomEffect);
        Assert.Equal("draw_1", reloaded.EffectId);
        Assert.Empty(reloaded.EffectActionSteps);
    }
}
