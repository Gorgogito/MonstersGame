using MonstersGame.Core.Effects;
using MonstersGame.Core.Requirements;

namespace MonstersGame.Tests;

/// <summary>
/// <see cref="EffectDefinitionResolver"/> es un catalogo estatico global
/// (mismo patron que el <see cref="EffectRegistry"/> legacy). Cada test que
/// llama <see cref="EffectDefinitionResolver.Load"/> restaura los valores por
/// defecto al terminar para no filtrar estado a otras pruebas (ver
/// xunit.runner.json: parallelizeTestCollections=false evita ademas que otra
/// clase lea el catalogo a mitad de una mutacion).
/// </summary>
public class EffectDefinitionResolverTests : IDisposable
{
    public void Dispose() => EffectDefinitionResolver.ResetToDefaults();

    [Fact]
    public void Get_LegacyIds_ResolveToTheirDefaultDefinitionsWithoutLoadingAnything()
    {
        Assert.NotNull(EffectDefinitionResolver.Get("draw_1"));
        Assert.NotNull(EffectDefinitionResolver.Get("destroy_target_monster"));
        Assert.NotNull(EffectDefinitionResolver.Get("special_summon_from_own_graveyard"));
        Assert.NotNull(EffectDefinitionResolver.Get("negate_activation"));
    }

    [Fact]
    public void Get_UnknownId_ReturnsNull()
    {
        Assert.Null(EffectDefinitionResolver.Get("no_existe"));
    }

    [Fact]
    public void GetDefinition_ReturnsRawDefinitionWithVisualProfileKey_WithoutBuildingAnAction()
    {
        // La capa de presentacion (VisualProfileCatalog.ResolveKey, MonoGame)
        // necesita leer VisualProfileKey sin ejecutar el efecto -- Get(id)
        // construye y valida la accion, GetDefinition(id) no.
        var definition = new EffectDefinition("custom_with_profile", "Con perfil visual", EffectTrigger.Activate, "summon.ritual", isActive: true,
            Array.Empty<EffectConditionSpec>(), null,
            new[] { new EffectActionStepSpec("negate_activation", EffectActionParams.Empty) });
        EffectDefinitionResolver.Load(new[] { definition });

        var resolved = EffectDefinitionResolver.GetDefinition("custom_with_profile");

        Assert.NotNull(resolved);
        Assert.Equal("summon.ritual", resolved!.VisualProfileKey);
    }

    [Fact]
    public void GetDefinition_UnknownOrEmptyId_ReturnsNull()
    {
        Assert.Null(EffectDefinitionResolver.GetDefinition("no_existe"));
        Assert.Null(EffectDefinitionResolver.GetDefinition(""));
    }

    [Fact]
    public void Get_InactiveDefinition_ReturnsNull()
    {
        var definition = new EffectDefinition("custom_inactive", "Inactivo", EffectTrigger.Activate, "", isActive: false,
            Array.Empty<EffectConditionSpec>(), null,
            new[] { new EffectActionStepSpec("draw_card", EffectActionParams.Empty) });

        EffectDefinitionResolver.Load(new[] { definition });

        Assert.Null(EffectDefinitionResolver.Get("custom_inactive"));
    }

    [Fact]
    public void Get_ComposedFromMultipleExistingActions_ExecutesThemInOrder()
    {
        // Compone "roba 2 cartas y luego niega la activacion" a partir de dos
        // acciones ya existentes, sin ninguna clase C# nueva -- exactamente
        // lo que el sistema hibrido promete.
        var definition = new EffectDefinition("custom_draw_and_negate", "Robar y negar", EffectTrigger.Activate, "", isActive: true,
            Array.Empty<EffectConditionSpec>(), null,
            new[]
            {
                new EffectActionStepSpec("draw_card", new EffectActionParams(new Dictionary<string, string> { ["Count"] = "2" })),
                new EffectActionStepSpec("negate_activation", EffectActionParams.Empty)
            });
        EffectDefinitionResolver.Load(new[] { definition });

        var human = new MonstersGame.Core.Entities.Player(MonstersGame.Core.Entities.PlayerSide.Human, "Human");
        for (int i = 0; i < 5; i++) human.Deck.Add(MonstersGame.Tests.TestSupport.TestCards.Level4Weak);
        var state = new MonstersGame.Core.Battle.DuelState(human, new MonstersGame.Core.Entities.Player(MonstersGame.Core.Entities.PlayerSide.Cpu, "Cpu"));

        bool negated = false;
        var context = new EffectContext
        {
            State = state,
            Controller = human,
            Source = TestSupport.TestCards.NormalSpell,
            NegateRespondedLink = () => negated = true
        };

        var action = EffectDefinitionResolver.Get("custom_draw_and_negate");
        Assert.NotNull(action);
        action!.Resolve(context);

        Assert.Equal(2, human.Hand.Count);
        Assert.True(negated);
    }

    [Fact]
    public void Get_TargetedDefinitionWithoutDelegatedAction_UsesGenericFilterValidation()
    {
        // Objetivo puramente por datos (Zona + TargetFilter), sin ninguna
        // accion ITargetedEffectAction detras: prueba GenericTargetValidator.
        var dragonOnly = new TargetFilter(1, new IReadOnlyList<FilterCondition>[]
        {
            new[] { new FilterCondition(FilterConditionKind.Type, false, "Dragon") }
        });

        // negate_activation no tiene target propio: se usa aqui solo como
        // "accion cualquiera" para poder declarar un EffectTargetSpec sin
        // arrastrar la logica de Destroy/SpecialSummon.
        var definition = new EffectDefinition("custom_targeted", "Objetivo generico", EffectTrigger.Activate, "", isActive: true,
            Array.Empty<EffectConditionSpec>(),
            new EffectTargetSpec(EffectTargetKind.MonsterZone, dragonOnly, required: true),
            new[] { new EffectActionStepSpec("negate_activation", EffectActionParams.Empty) });
        EffectDefinitionResolver.Load(new[] { definition });

        var human = new MonstersGame.Core.Entities.Player(MonstersGame.Core.Entities.PlayerSide.Human, "Human");
        var cpu = new MonstersGame.Core.Entities.Player(MonstersGame.Core.Entities.PlayerSide.Cpu, "Cpu");
        var state = new MonstersGame.Core.Battle.DuelState(human, cpu);
        human.MonsterZones[0] = new MonstersGame.Core.Entities.CardInstance(TestSupport.TestCards.FusionMaterialA /* Dragon */, MonstersGame.Core.Entities.BattlePosition.Attack);
        human.MonsterZones[1] = new MonstersGame.Core.Entities.CardInstance(TestSupport.TestCards.Level4Strong /* Warrior */, MonstersGame.Core.Entities.BattlePosition.Attack);

        var action = EffectDefinitionResolver.Get("custom_targeted");
        var targeted = Assert.IsAssignableFrom<ITargetedEffectAction>(action);

        Assert.Equal(EffectTargetKind.MonsterZone, targeted.TargetKind);
        Assert.True(targeted.IsValidTarget(state, human, new EffectTarget { Side = MonstersGame.Core.Entities.PlayerSide.Human, ZoneIndex = 0 }));
        Assert.False(targeted.IsValidTarget(state, human, new EffectTarget { Side = MonstersGame.Core.Entities.PlayerSide.Human, ZoneIndex = 1 }));
    }

    [Fact]
    public void Load_OverridingALegacyId_ReplacesItsBehavior()
    {
        var overridden = new EffectDefinition("draw_1", "Robar 3 cartas (sobrescrito)", EffectTrigger.Any, "", isActive: true,
            Array.Empty<EffectConditionSpec>(), null,
            new[] { new EffectActionStepSpec("draw_card", new EffectActionParams(new Dictionary<string, string> { ["Count"] = "3" })) });
        EffectDefinitionResolver.Load(new[] { overridden });

        var human = new MonstersGame.Core.Entities.Player(MonstersGame.Core.Entities.PlayerSide.Human, "Human");
        for (int i = 0; i < 5; i++) human.Deck.Add(TestSupport.TestCards.Level4Weak);
        var state = new MonstersGame.Core.Battle.DuelState(human, new MonstersGame.Core.Entities.Player(MonstersGame.Core.Entities.PlayerSide.Cpu, "Cpu"));

        var action = EffectDefinitionResolver.Get("draw_1");
        action!.Resolve(new EffectContext { State = state, Controller = human, Source = TestSupport.TestCards.NormalSpell });

        Assert.Equal(3, human.Hand.Count);

        // Otros ids no tocados por Load siguen usando el valor por defecto.
        Assert.NotNull(EffectDefinitionResolver.Get("destroy_target_monster"));
    }
}
