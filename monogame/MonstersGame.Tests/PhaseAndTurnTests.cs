using MonstersGame.Core.Battle;
using MonstersGame.Core.Entities;
using MonstersGame.Core.Rules;
using MonstersGame.Core.Services;
using MonstersGame.Tests.TestSupport;

namespace MonstersGame.Tests;

public class PhaseAndTurnTests
{
    private static DuelEngine NewEngine(DuelConfig config) =>
        new(config, new FusionService(Array.Empty<FusionRecipe>(), new CardDatabase(TestCards.All)));

    [Fact]
    public void StartingPlayer_SkipsDrawOnFirstTurn_ByDefault()
    {
        var human = new Player(PlayerSide.Human, "Human");
        var cpu = new Player(PlayerSide.Cpu, "Cpu");
        for (int i = 0; i < 6; i++) human.Deck.Add(TestCards.Level4Weak);

        var engine = NewEngine(new DuelConfig());
        engine.StartDuel(human, cpu, firstPlayerIndex: 0);

        // Mano inicial de 5 cartas robadas; la Draw Phase del primer turno del
        // jugador inicial no roba una sexta.
        Assert.Equal(5, human.Hand.Count);
        Assert.Single(human.Deck);
    }

    [Fact]
    public void StartingPlayer_DrawsOnFirstTurn_WhenRuleDisabled()
    {
        var human = new Player(PlayerSide.Human, "Human");
        var cpu = new Player(PlayerSide.Cpu, "Cpu");
        for (int i = 0; i < 6; i++) human.Deck.Add(TestCards.Level4Weak);

        var engine = NewEngine(new DuelConfig { FirstPlayerSkipsFirstDraw = false });
        engine.StartDuel(human, cpu, firstPlayerIndex: 0);

        Assert.Equal(6, human.Hand.Count);
        Assert.Empty(human.Deck);
    }

    [Fact]
    public void StartingPlayer_CannotEnterBattlePhase_OnFirstTurn_ByDefault()
    {
        var (engine, _, _) = TestDuelFactory.Create();

        engine.AdvancePhase(); // Main1 -> (Battle salteada) -> Main2

        Assert.Equal(DuelPhase.Main2, engine.Phase);
    }

    [Fact]
    public void SecondPlayer_CanEnterBattlePhase_OnTheirFirstTurn()
    {
        var (engine, _, _) = TestDuelFactory.Create(firstPlayerIndex: 0);

        engine.EndTurn(); // pasa de Human (turno 1) a Cpu (turno 2)
        engine.AdvancePhase(); // Main1 -> Battle, sin restriccion para el segundo jugador

        Assert.Equal(DuelPhase.Battle, engine.Phase);
    }

    [Fact]
    public void EndTurn_WithHandOverLimit_DoesNotEndTurnYet_AndFlagsPendingDiscard()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        for (int i = 0; i < 7; i++) human.Hand.Add(TestCards.Level4Weak);

        var result = engine.EndTurn();

        Assert.True(result.Success);
        Assert.Equal(1, engine.State.PendingDiscardCount);
        Assert.Equal(0, engine.ActiveIndex); // el turno no cambio todavia
        Assert.Equal(7, human.Hand.Count); // nada se descarto todavia
    }

    [Fact]
    public void EndTurn_WhileDiscardIsPending_Fails()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        for (int i = 0; i < 7; i++) human.Hand.Add(TestCards.Level4Weak);
        engine.EndTurn();

        var secondAttempt = engine.EndTurn();

        Assert.False(secondAttempt.Success);
    }

    [Fact]
    public void EndTurn_TogglesActivePlayerAndIncrementsTurnNumber()
    {
        var (engine, _, cpu) = TestDuelFactory.Create();

        engine.EndTurn();

        Assert.Equal(1, engine.ActiveIndex); // indice de la CPU
        Assert.Equal(2, engine.State.TurnNumber);
        Assert.Equal(cpu, engine.State.ActivePlayer);
    }
}
