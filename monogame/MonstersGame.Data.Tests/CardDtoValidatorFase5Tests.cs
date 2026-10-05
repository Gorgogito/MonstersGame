using MonstersGame.Data.Loaders;

namespace MonstersGame.Data.Tests;

/// <summary>Validaciones nuevas de la Fase 5: Fusion sin materiales, filtros mal formados, efecto compuesto incompleto, Equip incoherente.</summary>
public class CardDtoValidatorFase5Tests
{
    private static CardDto ValidFusionMonster() => new()
    {
        Id = 1, Kind = "Monster", Name = "Fusion de Prueba", Attack = 2500, Defense = 2000, Level = 7,
        Type = "Dragon", Attribute = "Wind", Category = "Fusion",
        FusionMaterials = { new FusionSlotDto { Filter = new FilterDto(), MinCount = 1, MaxCount = 1 } }
    };

    [Fact]
    public void Validate_FusionWithoutMaterials_Fails()
    {
        var dto = ValidFusionMonster();
        dto.FusionMaterials.Clear();

        var errors = CardDtoValidator.Validate(dto, Array.Empty<CardDto>(), originalId: null);

        Assert.Contains(errors, e => e.Contains("material de Fusion"));
    }

    [Fact]
    public void Validate_FusionWithMaterials_Succeeds()
    {
        var errors = CardDtoValidator.Validate(ValidFusionMonster(), Array.Empty<CardDto>(), originalId: null);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_FusionSlotWithMaxLessThanMin_Fails()
    {
        var dto = ValidFusionMonster();
        dto.FusionMaterials[0].MinCount = 3;
        dto.FusionMaterials[0].MaxCount = 1;

        var errors = CardDtoValidator.Validate(dto, Array.Empty<CardDto>(), originalId: null);

        Assert.Contains(errors, e => e.Contains("maxima menor que la minima"));
    }

    [Fact]
    public void Validate_FilterWithEmptyOrGroup_Fails()
    {
        var dto = ValidFusionMonster();
        dto.FusionMaterials[0].Filter.OrGroups.Add(new List<FilterConditionDto>()); // grupo vacio

        var errors = CardDtoValidator.Validate(dto, Array.Empty<CardDto>(), originalId: null);

        Assert.Contains(errors, e => e.Contains("grupo vacio"));
    }

    [Fact]
    public void Validate_FilterWithInvalidAttributeValue_Fails()
    {
        var dto = ValidFusionMonster();
        dto.FusionMaterials[0].Filter.OrGroups.Add(new List<FilterConditionDto>
        {
            new() { Kind = "Attribute", Value = "NoExiste" }
        });

        var errors = CardDtoValidator.Validate(dto, Array.Empty<CardDto>(), originalId: null);

        Assert.Contains(errors, e => e.Contains("Atributo") && e.Contains("NoExiste"));
    }

    [Fact]
    public void Validate_FilterWithNonNumericSpecificCard_Fails()
    {
        var dto = ValidFusionMonster();
        dto.FusionMaterials[0].Filter.OrGroups.Add(new List<FilterConditionDto>
        {
            new() { Kind = "SpecificCard", Value = "no-es-un-numero" }
        });

        var errors = CardDtoValidator.Validate(dto, Array.Empty<CardDto>(), originalId: null);

        Assert.Contains(errors, e => e.Contains("Id de carta"));
    }

    [Fact]
    public void Validate_ComposedEffectWithoutActionSteps_Fails()
    {
        var dto = new CardDto { Id = 2, Kind = "Spell", Name = "Efecto Vacio", SubType = "Normal", ComposeCustomEffect = true };

        var errors = CardDtoValidator.Validate(dto, Array.Empty<CardDto>(), originalId: null);

        Assert.Contains(errors, e => e.Contains("al menos un paso de accion"));
    }

    [Fact]
    public void Validate_ComposedEffectWithUnregisteredActionKind_Fails()
    {
        var dto = new CardDto
        {
            Id = 2, Kind = "Spell", Name = "Efecto Invalido", SubType = "Normal", ComposeCustomEffect = true,
            EffectActionSteps = { new EffectActionStepDto { ActionKind = "accion_que_no_existe" } }
        };

        var errors = CardDtoValidator.Validate(dto, Array.Empty<CardDto>(), originalId: null);

        Assert.Contains(errors, e => e.Contains("accion_que_no_existe"));
    }

    [Fact]
    public void Validate_ComposedEffectWithValidStep_Succeeds()
    {
        var dto = new CardDto
        {
            Id = 2, Kind = "Spell", Name = "Efecto Valido", SubType = "Normal", ComposeCustomEffect = true,
            EffectTrigger = "Activate",
            EffectActionSteps = { new EffectActionStepDto { ActionKind = "draw_card", ParamsText = "Count=1" } }
        };

        var errors = CardDtoValidator.Validate(dto, Array.Empty<CardDto>(), originalId: null);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_EquipForNTurnsWithoutTurnCount_Fails()
    {
        var dto = new CardDto { Id = 3, Kind = "Spell", Name = "Equip de Prueba", SubType = "Equip", EquipDuration = "ForNTurns", EquipDurationTurns = 0 };

        var errors = CardDtoValidator.Validate(dto, Array.Empty<CardDto>(), originalId: null);

        Assert.Contains(errors, e => e.Contains("al menos 1 turno"));
    }

    [Fact]
    public void Validate_EquipWithUnrecognizedDuration_Fails()
    {
        var dto = new CardDto { Id = 3, Kind = "Spell", Name = "Equip de Prueba", SubType = "Equip", EquipDuration = "Eterna" };

        var errors = CardDtoValidator.Validate(dto, Array.Empty<CardDto>(), originalId: null);

        Assert.Contains(errors, e => e.Contains("Duracion"));
    }

    [Fact]
    public void Validate_FieldWithoutFieldTypeId_Succeeds()
    {
        var dto = new CardDto { Id = 4, Kind = "Spell", Name = "Campo Sin Configurar", SubType = "Field" };

        var errors = CardDtoValidator.Validate(dto, Array.Empty<CardDto>(), originalId: null);

        Assert.Empty(errors);
    }
}
