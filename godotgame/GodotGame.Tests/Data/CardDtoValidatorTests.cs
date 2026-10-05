using GodotGame.Data.Loaders;

namespace GodotGame.Tests.Data;

public class CardDtoValidatorTests
{
    private static CardDto ValidMonster(int id = 1, string name = "Guerrero de Prueba") => new()
    {
        Id = id,
        Kind = "Monster",
        Name = name,
        Attack = 1500,
        Defense = 1200,
        Level = 4,
        Type = "Warrior",
        Attribute = "Earth",
        Category = "Normal"
    };

    [Fact]
    public void Validate_ValidMonster_HasNoErrors()
    {
        var errors = CardDtoValidator.Validate(ValidMonster(), Array.Empty<CardDto>(), originalId: null);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MissingName_Fails()
    {
        var dto = ValidMonster();
        dto.Name = "   ";

        var errors = CardDtoValidator.Validate(dto, Array.Empty<CardDto>(), originalId: null);

        Assert.Contains(errors, e => e.Contains("nombre"));
    }

    [Fact]
    public void Validate_NonPositiveId_Fails()
    {
        var dto = ValidMonster(id: 0);

        var errors = CardDtoValidator.Validate(dto, Array.Empty<CardDto>(), originalId: null);

        Assert.Contains(errors, e => e.Contains("Id"));
    }

    [Fact]
    public void Validate_DuplicateId_FailsAgainstAnotherCard()
    {
        var existing = ValidMonster(id: 1, name: "Otra Carta");
        var dto = ValidMonster(id: 1, name: "Carta Nueva");

        var errors = CardDtoValidator.Validate(dto, new[] { existing }, originalId: null);

        Assert.Contains(errors, e => e.Contains("Id"));
    }

    [Fact]
    public void Validate_EditingTheSameCard_DoesNotFlagItsOwnIdOrName()
    {
        var dto = ValidMonster(id: 1, name: "Guerrero de Prueba");
        dto.Attack = 9999; // se edito, mismo Id/nombre que antes

        var errors = CardDtoValidator.Validate(dto, new[] { ValidMonster(id: 1) }, originalId: 1);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_DuplicateName_Fails()
    {
        var existing = ValidMonster(id: 1, name: "Nombre Repetido");
        var dto = ValidMonster(id: 2, name: "Nombre Repetido");

        var errors = CardDtoValidator.Validate(dto, new[] { existing }, originalId: null);

        Assert.Contains(errors, e => e.Contains("mismo nombre") || e.Contains("misma carta"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Validate_LevelOutOfRange_Fails(int level)
    {
        var dto = ValidMonster();
        dto.Level = level;

        var errors = CardDtoValidator.Validate(dto, Array.Empty<CardDto>(), originalId: null);

        Assert.Contains(errors, e => e.Contains("Nivel"));
    }

    [Fact]
    public void Validate_EmptyMonsterType_Fails()
    {
        // El Tipo dejo de ser un enum fijo (ver TypeRepository/tabla Types):
        // cualquier texto no vacio es valido aqui, solo se rechaza vacio.
        var dto = ValidMonster();
        dto.Type = "";

        var errors = CardDtoValidator.Validate(dto, Array.Empty<CardDto>(), originalId: null);

        Assert.Contains(errors, e => e.Contains("Tipo"));
    }

    [Fact]
    public void Validate_AnyNonEmptyMonsterType_Succeeds()
    {
        var dto = ValidMonster();
        dto.Type = "Bestia Divina"; // texto libre: no necesita existir en ningun enum

        var errors = CardDtoValidator.Validate(dto, Array.Empty<CardDto>(), originalId: null);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_UnregisteredEffectId_Fails()
    {
        var dto = ValidMonster();
        dto.EffectId = "efecto_que_no_existe";

        var errors = CardDtoValidator.Validate(dto, Array.Empty<CardDto>(), originalId: null);

        Assert.Contains(errors, e => e.Contains("EffectId"));
    }

    [Fact]
    public void Validate_RegisteredEffectId_Passes()
    {
        var dto = ValidMonster();
        dto.EffectId = "draw_1";

        var errors = CardDtoValidator.Validate(dto, Array.Empty<CardDto>(), originalId: null);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_UnknownKind_Fails()
    {
        var dto = ValidMonster();
        dto.Kind = "NoExiste";

        var errors = CardDtoValidator.Validate(dto, Array.Empty<CardDto>(), originalId: null);

        Assert.Contains(errors, e => e.Contains("Kind"));
    }

    [Fact]
    public void Validate_RitualSpell_WithoutLinkedRitualMonster_Fails()
    {
        var dto = new CardDto { Id = 10, Kind = "Spell", Name = "Rito de Prueba", SubType = "Ritual", RitualMonsterId = 99, RequiredRitualLevel = 6 };

        var errors = CardDtoValidator.Validate(dto, Array.Empty<CardDto>(), originalId: null);

        Assert.Contains(errors, e => e.Contains("RitualMonsterId"));
    }

    [Fact]
    public void Validate_RitualSpell_LinkedToNonRitualMonster_Fails()
    {
        var monster = ValidMonster(id: 5, name: "Monstruo Normal"); // Category = "Normal"
        var dto = new CardDto { Id = 10, Kind = "Spell", Name = "Rito de Prueba", SubType = "Ritual", RitualMonsterId = 5, RequiredRitualLevel = 6 };

        var errors = CardDtoValidator.Validate(dto, new[] { monster }, originalId: null);

        Assert.Contains(errors, e => e.Contains("Categoria"));
    }

    [Fact]
    public void Validate_RitualSpell_ValidLink_Passes()
    {
        var monster = ValidMonster(id: 5, name: "Monstruo Ritual");
        monster.Category = "Ritual";
        var dto = new CardDto { Id = 10, Kind = "Spell", Name = "Rito de Prueba", SubType = "Ritual", RitualMonsterId = 5, RequiredRitualLevel = 6 };

        var errors = CardDtoValidator.Validate(dto, new[] { monster }, originalId: null);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_RitualSpell_ZeroRequiredLevel_Fails()
    {
        var monster = ValidMonster(id: 5, name: "Monstruo Ritual");
        monster.Category = "Ritual";
        var dto = new CardDto { Id = 10, Kind = "Spell", Name = "Rito de Prueba", SubType = "Ritual", RitualMonsterId = 5, RequiredRitualLevel = 0 };

        var errors = CardDtoValidator.Validate(dto, new[] { monster }, originalId: null);

        Assert.Contains(errors, e => e.Contains("Nivel"));
    }
}
