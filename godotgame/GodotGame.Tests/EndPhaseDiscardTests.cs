using GodotGame.Tests.TestSupport;

namespace GodotGame.Tests;

public class EndPhaseDiscardTests
{
    [Fact]
    public void DiscardForEndPhase_WithoutPendingDiscard_Fails()
    {
        var (engine, _, _) = TestDuelFactory.Create();

        var result = engine.DiscardForEndPhase(new[] { 0 });

        Assert.False(result.Success);
    }

    [Fact]
    public void DiscardForEndPhase_WrongCardCount_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        for (int i = 0; i < 8; i++) human.Hand.Add(TestCards.Level4Weak); // 2 sobre el limite
        engine.EndTurn();

        var tooFew = engine.DiscardForEndPhase(new[] { 0 });

        Assert.False(tooFew.Success);
        Assert.Equal(2, engine.State.PendingDiscardCount);
    }

    [Fact]
    public void DiscardForEndPhase_InvalidHandIndex_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        for (int i = 0; i < 7; i++) human.Hand.Add(TestCards.Level4Weak);
        engine.EndTurn();

        var result = engine.DiscardForEndPhase(new[] { 99 });

        Assert.False(result.Success);
        Assert.Equal(1, engine.State.PendingDiscardCount);
    }

    [Fact]
    public void DiscardForEndPhase_DiscardsExactlyTheChosenCards_AndFinishesTheTurn()
    {
        var (engine, human, cpu) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.Level4Strong);
        human.Hand.Add(TestCards.Level4Weak);
        human.Hand.Add(TestCards.HighDefense);
        // Rellenar hasta superar el limite de 6 en 1.
        for (int i = 0; i < 4; i++) human.Hand.Add(TestCards.ZeroAttack);
        Assert.Equal(7, human.Hand.Count);

        engine.EndTurn();
        var chosen = human.Hand[1]; // descarta especificamente TestCards.Level4Weak
        var result = engine.DiscardForEndPhase(new[] { 1 });

        Assert.True(result.Success);
        Assert.Equal(0, engine.State.PendingDiscardCount);
        Assert.Equal(6, human.Hand.Count);
        Assert.DoesNotContain(chosen, human.Hand);
        Assert.Contains(chosen, human.Graveyard);
        Assert.Equal(1, engine.ActiveIndex); // el turno ya paso a la CPU
        Assert.Equal(cpu, engine.State.ActivePlayer);
    }

    [Fact]
    public void EndTurn_WithHandAtOrBelowLimit_FinishesImmediately_NoDiscardNeeded()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.Level4Strong);

        var result = engine.EndTurn();

        Assert.True(result.Success);
        Assert.Equal(0, engine.State.PendingDiscardCount);
        Assert.Equal(1, engine.ActiveIndex);
    }
}
