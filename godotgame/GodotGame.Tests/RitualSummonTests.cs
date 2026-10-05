using GodotGame.Core.Entities;
using GodotGame.Tests.TestSupport;

namespace GodotGame.Tests;

public class RitualSummonTests
{
    [Fact]
    public void RitualSummon_WithHandTributesSummingTheRequiredLevel_Succeeds()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.RitualSummonSpell);
        human.Hand.Add(TestCards.RitualMonster);
        human.Hand.Add(TestCards.Level4Strong); // Nivel 4
        human.Hand.Add(TestCards.Level2Fodder);  // Nivel 2 (suma 6, el minimo exigido)

        var result = engine.RitualSummon(0, 1, handTributeIndices: new[] { 2, 3 });

        Assert.True(result.Success);
        Assert.Empty(human.Hand);
        Assert.Contains(TestCards.Level4Strong, human.Graveyard);
        Assert.Contains(TestCards.Level2Fodder, human.Graveyard);
        Assert.Contains(TestCards.RitualSummonSpell, human.Graveyard);
        var summoned = human.MonsterZones.SingleOrDefault(z => z != null && z.Card == TestCards.RitualMonster);
        Assert.NotNull(summoned);
        Assert.Equal(BattlePosition.Attack, summoned!.Position);
    }

    [Fact]
    public void RitualSummon_WithFieldTributes_Succeeds()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.RitualSummonSpell);
        human.Hand.Add(TestCards.RitualMonster);
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong); // Nivel 4
        TestDuelFactory.PlaceOnField(human, 1, TestCards.Level2Fodder);  // Nivel 2

        var result = engine.RitualSummon(0, 1, fieldTributeZones: new[] { 0, 1 });

        Assert.True(result.Success);
        Assert.Contains(TestCards.Level4Strong, human.Graveyard);
        Assert.Contains(TestCards.Level2Fodder, human.Graveyard);
        // El Monstruo de Ritual ocupa una de las Zonas liberadas por el Sacrificio.
        Assert.Contains(human.MonsterZones, z => z != null && z.Card == TestCards.RitualMonster);
    }

    [Fact]
    public void RitualSummon_MixingHandAndFieldTributes_Succeeds()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.RitualSummonSpell);
        human.Hand.Add(TestCards.RitualMonster);
        human.Hand.Add(TestCards.Level2Fodder); // Nivel 2, de la mano
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong); // Nivel 4, del Campo

        var result = engine.RitualSummon(0, 1, handTributeIndices: new[] { 2 }, fieldTributeZones: new[] { 0 });

        Assert.True(result.Success);
    }

    [Fact]
    public void RitualSummon_InsufficientLevel_Fails_AndConsumesNothing()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.RitualSummonSpell);
        human.Hand.Add(TestCards.RitualMonster);
        human.Hand.Add(TestCards.Level2Fodder); // Nivel 2, menor al minimo de 6

        var result = engine.RitualSummon(0, 1, handTributeIndices: new[] { 2 });

        Assert.False(result.Success);
        Assert.Equal(3, human.Hand.Count);
        Assert.Empty(human.Graveyard);
    }

    [Fact]
    public void RitualSummon_MoreThanEnoughLevel_StillSucceeds()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.RitualSummonSpell);
        human.Hand.Add(TestCards.RitualMonster);
        human.Hand.Add(TestCards.Level4Strong);
        human.Hand.Add(TestCards.Level6OneTribute); // 4 + 6 = 10, mas que el minimo de 6

        var result = engine.RitualSummon(0, 1, handTributeIndices: new[] { 2, 3 });

        Assert.True(result.Success);
    }

    [Fact]
    public void RitualSummon_WrongMonsterForTheSpell_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.RitualSummonSpell); // vinculada a RitualMonster (id 5001)
        human.Hand.Add(TestCards.Level4Strong);      // no es un Monstruo de Ritual

        var result = engine.RitualSummon(0, 1);

        Assert.False(result.Success);
    }

    [Fact]
    public void RitualSummon_NonRitualSpell_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.NormalSpell);
        human.Hand.Add(TestCards.RitualMonster);

        var result = engine.RitualSummon(0, 1);

        Assert.False(result.Success);
    }

    [Fact]
    public void RitualSummon_CannotSacrificeTheRitualCardsThemselves()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.RitualSummonSpell);
        human.Hand.Add(TestCards.RitualMonster);

        var result = engine.RitualSummon(0, 1, handTributeIndices: new[] { 1 }); // se intenta sacrificar al propio Monstruo de Ritual

        Assert.False(result.Success);
    }

    [Fact]
    public void RitualSummon_WithoutExplicitTributes_AutoSelectsEnoughToMeetTheRequiredLevel()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.RitualSummonSpell);
        human.Hand.Add(TestCards.RitualMonster);
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong); // Nivel 4, en el Campo
        human.Hand.Add(TestCards.Level2Fodder); // Nivel 2, en la mano

        var result = engine.RitualSummon(0, 1);

        Assert.True(result.Success);
        Assert.Contains(TestCards.Level4Strong, human.Graveyard);
        Assert.Contains(TestCards.Level2Fodder, human.Graveyard);
        Assert.Contains(human.MonsterZones, z => z != null && z.Card == TestCards.RitualMonster);
    }

    [Fact]
    public void RitualSummon_AutoSelect_PrefersFieldMonsters_OverHandWhenEnough()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.RitualSummonSpell);
        human.Hand.Add(TestCards.RitualMonster);
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level6OneTribute); // Nivel 6: alcanza solo
        human.Hand.Add(TestCards.Level4Strong); // no deberia hacer falta

        var result = engine.RitualSummon(0, 1);

        Assert.True(result.Success);
        Assert.Contains(TestCards.Level6OneTribute, human.Graveyard);
        Assert.DoesNotContain(TestCards.Level4Strong, human.Graveyard);
        Assert.Contains(TestCards.Level4Strong, human.Hand);
    }

    [Fact]
    public void RitualSummon_DoesNotConsumeTheNormalSummon()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.RitualSummonSpell);
        human.Hand.Add(TestCards.RitualMonster);
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong);
        TestDuelFactory.PlaceOnField(human, 1, TestCards.Level2Fodder);
        human.Hand.Add(TestCards.Level4Weak); // para la Invocacion Normal posterior

        engine.RitualSummon(0, 1, fieldTributeZones: new[] { 0, 1 });
        var normalSummon = engine.NormalSummon(0, BattlePosition.Attack);

        Assert.True(normalSummon.Success);
    }

    [Fact]
    public void RitualSummon_DoesNotOpenAChain()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.RitualSummonSpell);
        human.Hand.Add(TestCards.RitualMonster);
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong);
        TestDuelFactory.PlaceOnField(human, 1, TestCards.Level2Fodder);

        engine.RitualSummon(0, 1, fieldTributeZones: new[] { 0, 1 });

        Assert.Empty(engine.State.Chain);
    }

    [Fact]
    public void RitualSummon_DefenseFaceDown_IsRejected()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.RitualSummonSpell);
        human.Hand.Add(TestCards.RitualMonster);
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong);
        TestDuelFactory.PlaceOnField(human, 1, TestCards.Level2Fodder);

        var result = engine.RitualSummon(0, 1, fieldTributeZones: new[] { 0, 1 }, position: BattlePosition.DefenseFaceDown);

        Assert.False(result.Success);
    }

    [Fact]
    public void RitualSummon_NoFreeMonsterZone_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.RitualSummonSpell);
        human.Hand.Add(TestCards.RitualMonster);
        human.Hand.Add(TestCards.Level4Strong);
        human.Hand.Add(TestCards.Level2Fodder); // Sacrificios validos (suman 6), pero de la mano: no liberan ninguna Zona
        for (int i = 0; i < 5; i++)
            TestDuelFactory.PlaceOnField(human, i, TestCards.Level4Weak);

        var result = engine.RitualSummon(0, 1, handTributeIndices: new[] { 2, 3 });

        Assert.False(result.Success);
        Assert.Equal(4, human.Hand.Count); // nada se consumio
    }
}
