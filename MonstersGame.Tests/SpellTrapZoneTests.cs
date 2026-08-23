using MonstersGame.Core.Rules;
using MonstersGame.Tests.TestSupport;

namespace MonstersGame.Tests;

public class SpellTrapZoneTests
{
    // ------------------------------------------------------------- SetSpellOrTrap

    [Fact]
    public void SetSpellOrTrap_PlacesSpellFaceDown()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.NormalSpell);

        var result = engine.SetSpellOrTrap(0);

        Assert.True(result.Success);
        Assert.Empty(human.Hand);
        var zone = human.SpellTrapZones[0]!;
        Assert.False(zone.FaceUp);
        Assert.True(zone.SetThisTurn);
        Assert.Equal(TestCards.NormalSpell, zone.Card);
    }

    [Fact]
    public void SetSpellOrTrap_PlacesTrapFaceDown()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.NormalTrap);

        var result = engine.SetSpellOrTrap(0);

        Assert.True(result.Success);
        Assert.Equal(TestCards.NormalTrap, human.SpellTrapZones[0]!.Card);
    }

    [Fact]
    public void SetSpellOrTrap_FieldSpell_IsRejected()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.FieldSpellA);

        var result = engine.SetSpellOrTrap(0);

        Assert.False(result.Success);
        Assert.Single(human.Hand);
    }

    [Fact]
    public void SetSpellOrTrap_RitualSpell_IsRejected()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.RitualSummonSpell);

        var result = engine.SetSpellOrTrap(0);

        Assert.False(result.Success);
        Assert.Single(human.Hand);
    }

    [Fact]
    public void SetSpellOrTrap_MonsterCard_IsRejected()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.Level4Strong);

        var result = engine.SetSpellOrTrap(0);

        Assert.False(result.Success);
    }

    [Fact]
    public void SetSpellOrTrap_NoFreeZone_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        for (int i = 0; i < 5; i++)
            TestDuelFactory.PlaceSpellTrap(human, i, TestCards.NormalTrap, faceUp: false);
        human.Hand.Add(TestCards.NormalSpell);

        var result = engine.SetSpellOrTrap(0);

        Assert.False(result.Success);
    }

    [Fact]
    public void SetSpellOrTrap_IsIndependentFromNormalSummon()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.Level4Strong);
        human.Hand.Add(TestCards.NormalSpell);
        engine.NormalSummon(0, MonstersGame.Core.Entities.BattlePosition.Attack);

        var result = engine.SetSpellOrTrap(0); // el unico indice restante en mano

        Assert.True(result.Success);
    }

    // ------------------------------------------------------------- ActivateSpell (mano)

    [Fact]
    public void ActivateSpell_Normal_OpensAChain_AndResolvesToGraveyardOnceItCloses()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.NormalSpell);

        var result = engine.ActivateSpell(0);

        Assert.True(result.Success);
        Assert.Empty(human.Hand);
        // Activar abre una Cadena: la carta ya esta boca arriba en el campo,
        // pero todavia no se resolvio (nadie la mando al Cementerio).
        Assert.Single(engine.State.Chain);
        Assert.Equal(TestCards.NormalSpell, human.SpellTrapZones.Single(z => z != null)!.Card);
        Assert.Empty(human.Graveyard);

        TestDuelFactory.CloseChain(engine);

        Assert.Empty(engine.State.Chain);
        Assert.Contains(TestCards.NormalSpell, human.Graveyard);
        Assert.DoesNotContain(human.SpellTrapZones, z => z != null);
    }

    [Fact]
    public void ActivateSpell_Continuous_StaysFaceUpOnField_EvenAfterTheChainResolves()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.ContinuousSpell);

        var result = engine.ActivateSpell(0);
        TestDuelFactory.CloseChain(engine);

        Assert.True(result.Success);
        var zone = human.SpellTrapZones.Single(z => z != null)!;
        Assert.True(zone.FaceUp);
        Assert.Equal(TestCards.ContinuousSpell, zone.Card);
        Assert.Empty(human.Graveyard);
    }

    [Fact]
    public void ActivateSpell_Equip_StaysFaceUpOnField_EvenAfterTheChainResolves()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.EquipSpell);

        var result = engine.ActivateSpell(0);
        TestDuelFactory.CloseChain(engine);

        Assert.True(result.Success);
        Assert.True(human.SpellTrapZones.Single(z => z != null)!.FaceUp);
    }

    [Fact]
    public void ActivateSpell_Field_GoesToFieldZone_NotSpellTrapZones()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.FieldSpellA);

        var result = engine.ActivateSpell(0);

        Assert.True(result.Success);
        Assert.NotNull(human.FieldZone);
        Assert.Equal(TestCards.FieldSpellA, human.FieldZone!.Card);
        Assert.True(human.FieldZone.FaceUp);
        Assert.DoesNotContain(human.SpellTrapZones, z => z != null);
    }

    [Fact]
    public void ActivateSpell_Field_ReplacesPreviousFieldSpell_SendingItToGraveyard()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.FieldSpellA);
        engine.ActivateSpell(0);
        human.Hand.Add(TestCards.FieldSpellB);

        var result = engine.ActivateSpell(0);

        Assert.True(result.Success);
        Assert.Equal(TestCards.FieldSpellB, human.FieldZone!.Card);
        Assert.Contains(TestCards.FieldSpellA, human.Graveyard);
    }

    [Fact]
    public void ActivateSpell_QuickPlay_WorksDuringBattlePhase()
    {
        var config = new DuelConfig { FirstPlayerSkipsFirstBattle = false };
        var (engine, human, _) = TestDuelFactory.Create(config);
        human.Hand.Add(TestCards.QuickPlaySpell);
        engine.AdvancePhase(); // Main1 -> Battle

        var result = engine.ActivateSpell(0);

        Assert.True(result.Success);
    }

    [Fact]
    public void ActivateSpell_Normal_DuringBattlePhase_Fails()
    {
        var config = new DuelConfig { FirstPlayerSkipsFirstBattle = false };
        var (engine, human, _) = TestDuelFactory.Create(config);
        human.Hand.Add(TestCards.NormalSpell);
        engine.AdvancePhase(); // Main1 -> Battle

        var result = engine.ActivateSpell(0);

        Assert.False(result.Success);
        Assert.Single(human.Hand);
    }

    [Fact]
    public void ActivateSpell_Ritual_IsNotYetSupported()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.RitualSpell);

        var result = engine.ActivateSpell(0);

        Assert.False(result.Success);
        Assert.Single(human.Hand);
    }

    [Fact]
    public void ActivateSpell_ATrapCard_IsRejected()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.NormalTrap);

        var result = engine.ActivateSpell(0);

        Assert.False(result.Success);
    }

    [Fact]
    public void ActivateSpell_NoFreeZone_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        for (int i = 0; i < 5; i++)
            TestDuelFactory.PlaceSpellTrap(human, i, TestCards.ContinuousTrap, faceUp: true);
        human.Hand.Add(TestCards.NormalSpell);

        var result = engine.ActivateSpell(0);

        Assert.False(result.Success);
    }

    // ------------------------------------------------------------- ActivateSetCard

    [Fact]
    public void ActivateSetCard_Spell_CanActivateTheSameTurnItWasSet()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceSpellTrap(human, 0, TestCards.NormalSpell, faceUp: false, setThisTurn: true);

        var result = engine.ActivateSetCard(0);
        TestDuelFactory.CloseChain(engine);

        Assert.True(result.Success);
        Assert.Contains(TestCards.NormalSpell, human.Graveyard);
    }

    [Fact]
    public void ActivateSetCard_ContinuousSpell_FlipsFaceUpInPlace()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceSpellTrap(human, 2, TestCards.ContinuousSpell, faceUp: false, setThisTurn: true);

        var result = engine.ActivateSetCard(2);
        TestDuelFactory.CloseChain(engine);

        Assert.True(result.Success);
        Assert.True(human.SpellTrapZones[2]!.FaceUp);
        Assert.Equal(TestCards.ContinuousSpell, human.SpellTrapZones[2]!.Card);
    }

    [Fact]
    public void ActivateSetCard_Trap_TheSameTurnItWasSet_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceSpellTrap(human, 0, TestCards.NormalTrap, faceUp: false, setThisTurn: true);

        var result = engine.ActivateSetCard(0);

        Assert.False(result.Success);
        Assert.False(human.SpellTrapZones[0]!.FaceUp);
    }

    [Fact]
    public void ActivateSetCard_Trap_OnALaterTurn_Succeeds()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceSpellTrap(human, 0, TestCards.NormalTrap, faceUp: false, setThisTurn: false);

        var result = engine.ActivateSetCard(0);
        TestDuelFactory.CloseChain(engine);

        Assert.True(result.Success);
        Assert.Contains(TestCards.NormalTrap, human.Graveyard);
    }

    [Fact]
    public void ActivateSetCard_ContinuousTrap_FlipsFaceUpInPlace()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceSpellTrap(human, 1, TestCards.ContinuousTrap, faceUp: false, setThisTurn: false);

        var result = engine.ActivateSetCard(1);
        TestDuelFactory.CloseChain(engine);

        Assert.True(result.Success);
        Assert.True(human.SpellTrapZones[1]!.FaceUp);
    }

    [Fact]
    public void ActivateSetCard_CounterTrap_WorksDuringBattlePhase_OnALaterTurn()
    {
        var config = new DuelConfig { FirstPlayerSkipsFirstBattle = false };
        var (engine, human, _) = TestDuelFactory.Create(config);
        TestDuelFactory.PlaceSpellTrap(human, 0, TestCards.CounterTrap, faceUp: false, setThisTurn: false);
        engine.AdvancePhase(); // Main1 -> Battle

        var result = engine.ActivateSetCard(0);

        Assert.True(result.Success);
    }

    [Fact]
    public void ActivateSetCard_AlreadyFaceUp_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceSpellTrap(human, 0, TestCards.ContinuousSpell, faceUp: true);

        var result = engine.ActivateSetCard(0);

        Assert.False(result.Success);
    }

    [Fact]
    public void ActivateSetCard_EmptyZone_Fails()
    {
        var (engine, _, _) = TestDuelFactory.Create();

        var result = engine.ActivateSetCard(0);

        Assert.False(result.Success);
    }

    [Fact]
    public void ActivateSetCard_RitualSpell_IsNotYetSupported()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        TestDuelFactory.PlaceSpellTrap(human, 0, TestCards.RitualSpell, faceUp: false, setThisTurn: false);

        var result = engine.ActivateSetCard(0);

        Assert.False(result.Success);
    }
}
