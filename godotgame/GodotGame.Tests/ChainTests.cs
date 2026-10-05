using GodotGame.Core.AI;
using GodotGame.Core.Entities;
using GodotGame.Core.Rules;
using GodotGame.Core.Services;
using GodotGame.Tests.TestSupport;

namespace GodotGame.Tests;

public class ChainTests
{
    [Fact]
    public void ActivateSpell_OpensAChain_AndGivesPriorityToTheOpponent()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.NormalSpell);

        engine.ActivateSpell(0);

        Assert.Single(engine.State.Chain);
        Assert.Equal(PlayerSide.Cpu, engine.State.ChainPendingResponder);
        Assert.Equal(0, engine.State.ChainConsecutivePasses);
    }

    [Fact]
    public void ChainSpeed_CannotRespondToASpellWithAnotherSpeed1Card()
    {
        var (engine, human, cpu) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.NormalSpell); // Velocidad 1
        TestDuelFactory.PlaceSpellTrap(cpu, 0, TestCards.ContinuousSpell, faceUp: false, setThisTurn: false); // tambien Velocidad 1
        engine.ActivateSpell(0);

        var response = engine.ActivateSetCard(0); // le toca a la CPU (jugador que responde)

        Assert.False(response.Success);
        Assert.Single(engine.State.Chain);
    }

    [Fact]
    public void ChainSpeed_CanRespondToASpellWithATrap()
    {
        var (engine, human, cpu) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.NormalSpell); // Velocidad 1
        TestDuelFactory.PlaceSpellTrap(cpu, 0, TestCards.NormalTrap, faceUp: false, setThisTurn: false); // Velocidad 2
        engine.ActivateSpell(0);

        var response = engine.ActivateSetCard(0);

        Assert.True(response.Success);
        Assert.Equal(2, engine.State.Chain.Count);
        Assert.Equal(PlayerSide.Human, engine.State.ChainPendingResponder);
    }

    [Fact]
    public void ChainSpeed_CannotRespondToACounterTrapWithANormalTrap()
    {
        var (engine, human, cpu) = TestDuelFactory.Create();
        TestDuelFactory.PlaceSpellTrap(human, 0, TestCards.CounterTrap, faceUp: false, setThisTurn: false); // Velocidad 3
        TestDuelFactory.PlaceSpellTrap(cpu, 0, TestCards.NormalTrap, faceUp: false, setThisTurn: false);    // Velocidad 2
        engine.ActivateSetCard(0); // Human activa su Contraefecto

        var response = engine.ActivateSetCard(0); // le toca a la CPU

        Assert.False(response.Success);
    }

    [Fact]
    public void ChainSpeed_CanRespondToACounterTrapWithAnotherCounterTrap()
    {
        var (engine, human, cpu) = TestDuelFactory.Create();
        TestDuelFactory.PlaceSpellTrap(human, 0, TestCards.CounterTrap, faceUp: false, setThisTurn: false);
        TestDuelFactory.PlaceSpellTrap(cpu, 0, TestCards.CounterTrap, faceUp: false, setThisTurn: false);
        engine.ActivateSetCard(0);

        var response = engine.ActivateSetCard(0);

        Assert.True(response.Success);
        Assert.Equal(2, engine.State.Chain.Count);
    }

    [Fact]
    public void Chain_ResolvesInReverseOrder_LastActivatedFirst()
    {
        var (engine, human, cpu) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.NormalSpell);
        TestDuelFactory.PlaceSpellTrap(cpu, 0, TestCards.NormalTrap, faceUp: false, setThisTurn: false);

        engine.ActivateSpell(0);          // Eslabon 1: Magia de Human
        engine.ActivateSetCard(0);        // Eslabon 2: Trampa de Cpu (responde)
        TestDuelFactory.CloseChain(engine);

        var entries = engine.Log.Entries.ToList();
        int trapResolvedAt = entries.FindIndex(e => e.Contains(TestCards.NormalTrap.Name) && e.Contains("Cementerio"));
        int spellResolvedAt = entries.FindIndex(e => e.Contains(TestCards.NormalSpell.Name) && e.Contains("Cementerio"));

        Assert.True(trapResolvedAt >= 0);
        Assert.True(spellResolvedAt >= 0);
        Assert.True(trapResolvedAt < spellResolvedAt, "El ultimo eslabon activado (la Trampa) debe resolverse antes que el primero (la Magia).");
    }

    [Fact]
    public void SelfChaining_SamePlayerCanAddAnotherLink_WhenOpponentDeclines()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.NormalSpell);
        human.Hand.Add(TestCards.QuickPlaySpell); // Velocidad 2: puede encadenarse a si misma

        engine.ActivateSpell(0); // Eslabon 1
        engine.PassPriority();   // el rival (Cpu) declina responder

        Assert.Equal(PlayerSide.Human, engine.State.ChainPendingResponder);

        var secondLink = engine.ActivateSpell(0); // Human encadena su propia segunda carta

        Assert.True(secondLink.Success);
        Assert.Equal(2, engine.State.Chain.Count);
    }

    [Fact]
    public void PassPriority_WithNoOpenChain_Fails()
    {
        var (engine, _, _) = TestDuelFactory.Create();

        var result = engine.PassPriority();

        Assert.False(result.Success);
    }

    [Theory]
    [InlineData(true)]  // NormalSummon
    [InlineData(false)] // EndTurn
    public void Chain_BlocksOtherMainPhaseActions_WhileOpen(bool tryNormalSummon)
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.NormalSpell);
        human.Hand.Add(TestCards.Level4Strong);
        engine.ActivateSpell(0);

        var result = tryNormalSummon
            ? engine.NormalSummon(0, BattlePosition.Attack)
            : engine.EndTurn();

        Assert.False(result.Success);
    }

    [Fact]
    public void Chain_BlocksAdvancePhase_WhileOpen()
    {
        var (engine, human, _) = TestDuelFactory.Create();
        human.Hand.Add(TestCards.NormalSpell);
        engine.ActivateSpell(0);

        var result = engine.AdvancePhase();

        Assert.False(result.Success);
    }

    [Fact]
    public void Chain_BlocksDeclareAttack_WhileOpen()
    {
        var config = new DuelConfig { FirstPlayerSkipsFirstBattle = false };
        var (engine, human, _) = TestDuelFactory.Create(config);
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong);
        human.Hand.Add(TestCards.QuickPlaySpell); // Velocidad 2: activable en Battle Phase
        engine.AdvancePhase(); // Main1 -> Battle
        var activation = engine.ActivateSpell(0);
        Assert.True(activation.Success); // valida la premisa del test

        var result = engine.DeclareAttack(0, -1);

        Assert.False(result.Success);
    }

    [Fact]
    public void Ai_PassesPriority_WhenGivenTheChanceToRespond()
    {
        var (engine, human, _) = TestDuelFactory.Create(firstPlayerIndex: 0);
        human.Hand.Add(TestCards.NormalSpell);
        engine.ActivateSpell(0); // le toca a la CPU responder

        var ai = new BasicCpuAI(new FusionService(Array.Empty<FusionRecipe>(), new CardDatabase(TestCards.All)));
        bool acted = ai.Step(engine, selfIndex: 1);

        Assert.True(acted);
        Assert.Equal(1, engine.State.ChainConsecutivePasses);
        Assert.Equal(PlayerSide.Human, engine.State.ChainPendingResponder);
    }
}
