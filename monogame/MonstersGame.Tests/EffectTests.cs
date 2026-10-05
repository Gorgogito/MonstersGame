using MonstersGame.Core.Effects;
using MonstersGame.Core.Entities;
using MonstersGame.Core.Rules;
using MonstersGame.Tests.TestSupport;

namespace MonstersGame.Tests;

public class EffectTests
{
    // ------------------------------------------------------------- Categoria de Monstruo

    [Fact]
    public void NormalSummon_FusionCategoryMonster_IsRejected()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.FusionResult);

        var result = engine.NormalSummon(0, BattlePosition.Attack);

        Assert.False(result.Success);
    }

    [Fact]
    public void SetMonster_RitualCategoryMonster_IsRejected()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.RitualMonster);

        var result = engine.SetMonster(0);

        Assert.False(result.Success);
    }

    // ------------------------------------------------------------- DrawCardAction (sin objetivo)

    [Fact]
    public void DrawCardAction_DrawsOneCard_OnceTheChainResolves()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.DrawEffectSpell);
        int deckBefore = human.Deck.Count;

        var result = engine.ActivateSpell(0);
        Assert.True(result.Success);
        Assert.Equal(deckBefore, human.Deck.Count); // todavia no se resolvio

        TestDuelFactory.CloseChain(engine);

        Assert.Equal(deckBefore - 1, human.Deck.Count);
        Assert.Single(human.Hand);
    }

    // ------------------------------------------------------------- Validacion de objetivo

    [Fact]
    public void ActivateSetCard_TargetedEffect_WithoutTarget_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceSpellTrap(human, 0, TestCards.DestroyEffectTrap, faceUp: false, setThisTurn: false);

        var result = engine.ActivateSetCard(0);

        Assert.False(result.Success);
        Assert.Empty(engine.State.Chain);
    }

    [Fact]
    public void ActivateSetCard_TargetedEffect_WithInvalidTarget_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceSpellTrap(human, 0, TestCards.DestroyEffectTrap, faceUp: false, setThisTurn: false);

        var result = engine.ActivateSetCard(0, new EffectTarget { Side = PlayerSide.Cpu, ZoneIndex = 0 }); // zona vacia

        Assert.False(result.Success);
    }

    // ------------------------------------------------------------- DestroyTargetMonsterAction

    [Fact]
    public void DestroyTargetMonsterAction_DestroysTheChosenMonster_OnceTheChainResolves()
    {
        var (engine, human, cpu) = TestDuelFactory.Create();
        TestDuelFactory.PlaceSpellTrap(human, 0, TestCards.DestroyEffectTrap, faceUp: false, setThisTurn: false);
        TestDuelFactory.PlaceOnField(cpu, 2, TestCards.Level4Strong);

        var result = engine.ActivateSetCard(0, new EffectTarget { Side = PlayerSide.Cpu, ZoneIndex = 2 });
        Assert.True(result.Success);
        Assert.NotNull(cpu.MonsterZones[2]); // todavia no se resolvio

        TestDuelFactory.CloseChain(engine);

        Assert.Null(cpu.MonsterZones[2]);
        Assert.Contains(TestCards.Level4Strong, cpu.Graveyard);
    }

    // ------------------------------------------------------------- SpecialSummonFromOwnGraveyardAction

    [Fact]
    public void SpecialSummonFromOwnGraveyardAction_RejectsTheOpponentsGraveyard()
    {
        var (engine, human, cpu) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.ReviveEffectSpell);
        cpu.Graveyard.Add(TestCards.Level4Strong);

        var result = engine.ActivateSpell(0, new EffectTarget { Side = PlayerSide.Cpu, ZoneIndex = 0 });

        Assert.False(result.Success);
    }

    [Fact]
    public void SpecialSummonFromOwnGraveyardAction_SummonsTheChosenMonster_OnceTheChainResolves()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.ReviveEffectSpell);
        human.Graveyard.Add(TestCards.Level4Strong);

        var result = engine.ActivateSpell(0, new EffectTarget { Side = PlayerSide.Human, ZoneIndex = 0 });
        Assert.True(result.Success);

        TestDuelFactory.CloseChain(engine);

        Assert.DoesNotContain(TestCards.Level4Strong, human.Graveyard);
        var summoned = human.MonsterZones.SingleOrDefault(z => z != null && z.Card == TestCards.Level4Strong);
        Assert.NotNull(summoned);
        Assert.Equal(BattlePosition.Attack, summoned!.Position);
        Assert.True(summoned.SummonedThisTurn);
    }

    // ------------------------------------------------------------- NegateActivationAction

    [Fact]
    public void NegateActivationAction_PreventsTheRespondedEffect_ButBothCardsStillResolveToGraveyard()
    {
        var (engine, human, cpu) = TestDuelFactory.Create();
        TestDuelFactory.PlaceSpellTrap(human, 0, TestCards.DestroyEffectTrap, faceUp: false, setThisTurn: false);
        TestDuelFactory.PlaceSpellTrap(cpu, 0, TestCards.NegateEffectTrap, faceUp: false, setThisTurn: false);
        TestDuelFactory.PlaceOnField(cpu, 2, TestCards.Level4Strong);

        engine.ActivateSetCard(0, new EffectTarget { Side = PlayerSide.Cpu, ZoneIndex = 2 }); // Eslabon 1: Human
        var negation = engine.ActivateSetCard(0); // Eslabon 2: Cpu responde con su Contraefecto
        Assert.True(negation.Success);

        TestDuelFactory.CloseChain(engine);

        Assert.NotNull(cpu.MonsterZones[2]); // el efecto se nego: no se destruyo
        Assert.Contains(TestCards.DestroyEffectTrap, human.Graveyard);
        Assert.Contains(TestCards.NegateEffectTrap, cpu.Graveyard);
    }

    // ------------------------------------------------------------- Efectos de Volteo

    [Fact]
    public void FlipSummon_TriggersFlipEffect_Immediately_WithoutOpeningAChain()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceOnField(human, 0, TestCards.FlipEffectMonster, BattlePosition.DefenseFaceDown);
        int deckBefore = human.Deck.Count;

        var result = engine.FlipSummon(0);

        Assert.True(result.Success);
        Assert.Equal(deckBefore - 1, human.Deck.Count);
        Assert.Empty(engine.State.Chain);
    }

    [Fact]
    public void DeclareAttack_FlippingAFaceDownEffectMonster_TriggersItsFlipEffect_EvenIfDestroyed()
    {
        var config = new DuelConfig { FirstPlayerSkipsFirstBattle = false };
        var (engine, human, cpu) = TestDuelFactory.Create(config);
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong); // ATK 1500
        TestDuelFactory.PlaceOnField(cpu, 0, TestCards.FlipEffectMonster, BattlePosition.DefenseFaceDown); // DEF 600
        int deckBefore = cpu.Deck.Count;
        engine.AdvancePhase(); // Main1 -> Battle

        var result = engine.DeclareAttack(0, 0);

        Assert.True(result.Success);
        Assert.Null(cpu.MonsterZones[0]); // el 1500 ATK destruye la DEF 600
        Assert.Equal(deckBefore - 1, cpu.Deck.Count); // pero el efecto de Volteo igual se resolvio
    }
}
