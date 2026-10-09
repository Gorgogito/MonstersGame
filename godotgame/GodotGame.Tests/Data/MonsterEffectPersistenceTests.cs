using GodotGame.Core.Effects.Monster;
using GodotGame.Core.Entities;
using GodotGame.Data.Loaders;
using GodotGame.Data.Sqlite;
using GodotGame.Tests.Data.TestSupport;

namespace GodotGame.Tests.Data;

/// <summary>Guardar -&gt; recargar los efectos de Monstruo (tabla MonsterEffects) y su validacion.</summary>
public class MonsterEffectPersistenceTests
{
    private static MonsterEffectDto DarkWorldDraw() => new()
    {
        Type = "Trigger",
        TriggerEvent = "DiscardedByCardEffect",
        Optional = false,
        Text = "Roba 1 carta o, si fue descartada por el adversario, roba 2.",
        Steps =
        {
            new MonsterEffectStepDto { ActionKind = "draw", Params = { ["Count"] = "1" }, Conditions = { new EffectConditionDto { Kind = "discarded_by_opponent", Negate = true } } },
            new MonsterEffectStepDto { ActionKind = "draw", Params = { ["Count"] = "2" }, Conditions = { new EffectConditionDto { Kind = "discarded_by_opponent" } } },
        }
    };

    private static CardDto Card(params MonsterEffectDto[] effects)
    {
        var dto = new CardDto
        {
            Id = 300, Kind = "Monster", Name = "Explorador de Prueba", Attack = 500, Defense = 500, Level = 2,
            Type = "Fiend", Attribute = "Dark", Category = "Effect"
        };
        dto.MonsterEffects.AddRange(effects);
        return dto;
    }

    [Fact]
    public void SaveCard_WithMonsterEffects_RoundTrips()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteCardWriter(dir.DbPath);
        var ignition = new MonsterEffectDto
        {
            Type = "Ignition", ActivationZone = "Hand", OncePerTurn = true,
            Costs = { new MonsterEffectStepDto { ActionKind = "discard_self" } },
            Steps = { new MonsterEffectStepDto { ActionKind = "add_to_hand", Params = { ["From"] = "Deck", ["CardId"] = "77" }, Optional = true } }
        };

        writer.SaveCard(Card(DarkWorldDraw(), ignition));
        var reloaded = writer.LoadAllDtos().Single(c => c.Id == 300);

        Assert.Equal(2, reloaded.MonsterEffects.Count);
        Assert.Equal("DiscardedByCardEffect", reloaded.MonsterEffects[0].TriggerEvent);
        Assert.True(reloaded.MonsterEffects[0].Steps[0].Conditions[0].Negate);
        Assert.Equal("Hand", reloaded.MonsterEffects[1].ActivationZone);
        Assert.True(reloaded.MonsterEffects[1].OncePerTurn);
        Assert.Equal("discard_self", reloaded.MonsterEffects[1].Costs[0].ActionKind);
        Assert.Equal("77", reloaded.MonsterEffects[1].Steps[0].Params["CardId"]);
        Assert.True(reloaded.MonsterEffects[1].Steps[0].Optional);
    }

    [Fact]
    public void SaveCard_ReplacesPreviousEffects()
    {
        using var dir = new TempCardDirectory();
        var writer = new SqliteCardWriter(dir.DbPath);
        writer.SaveCard(Card(DarkWorldDraw(), DarkWorldDraw()));

        writer.SaveCard(Card(DarkWorldDraw()));

        Assert.Single(writer.LoadAllDtos().Single(c => c.Id == 300).MonsterEffects);
    }

    [Fact]
    public void GameLoader_BuildsCoreMonsterEffects()
    {
        using var dir = new TempCardDirectory();
        new SqliteCardWriter(dir.DbPath).SaveCard(Card(DarkWorldDraw()));

        var card = (MonsterCard)new SqliteCardLoader(dir.DbPath).LoadCards().Single(c => c.Id == 300);

        var effect = Assert.Single(card.Effects);
        Assert.Equal(MonsterEffectType.Trigger, effect.Type);
        Assert.Equal(EffectEvent.DiscardedByCardEffect, effect.TriggerEvent);
        Assert.Equal(2, effect.Steps.Count);
        Assert.Equal(2, effect.Steps[1].Params.GetInt("Count"));
    }

    [Fact]
    public void Validator_AcceptsAWellFormedEffect()
    {
        var errors = CardDtoValidator.Validate(Card(DarkWorldDraw()), Array.Empty<CardDto>(), null);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validator_RejectsEffectsOnANormalMonster_ATriggerWithoutEvent_AndMisplacedSteps()
    {
        var bad = Card(
            new MonsterEffectDto { Type = "Trigger", TriggerEvent = "None", Steps = { new MonsterEffectStepDto { ActionKind = "draw" } } },
            new MonsterEffectDto { Type = "Continuous", Steps = { new MonsterEffectStepDto { ActionKind = "draw" } } },
            new MonsterEffectDto { Type = "Ignition", Steps = { new MonsterEffectStepDto { ActionKind = "destroy", Params = { ["UseTargets"] = "true" } } } });
        bad.Category = "Normal";

        var errors = CardDtoValidator.Validate(bad, Array.Empty<CardDto>(), null);

        Assert.Contains(errors, e => e.Contains("Monstruo Normal"));
        Assert.Contains(errors, e => e.Contains("necesita un evento"));
        Assert.Contains(errors, e => e.Contains("efecto Continuo"));
        Assert.Contains(errors, e => e.Contains("no selecciona objetivos"));
    }
}
