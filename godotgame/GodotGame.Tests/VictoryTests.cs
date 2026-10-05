using GodotGame.Core.Entities;
using GodotGame.Core.Rules;
using GodotGame.Tests.TestSupport;

namespace GodotGame.Tests;

public class VictoryTests
{
    [Fact]
    public void DirectAttack_ReducingLpToZero_EndsTheDuelForTheDefender()
    {
        var config = new DuelConfig { FirstPlayerSkipsFirstBattle = false };
        var (engine, human, cpu) = TestDuelFactory.Create(config);
        var lethal = new MonsterCard(3001, "Finalizador", attack: 9000, defense: 0, level: 4, "Warrior", MonsterAttribute.Dark);
        TestDuelFactory.PlaceOnField(human, 0, lethal);
        engine.AdvancePhase(); // Main1 -> Battle

        var result = engine.DeclareAttack(0, -1);

        Assert.True(result.Success);
        Assert.True(engine.IsOver);
        Assert.Equal(0, cpu.LifePoints); // se satura en 0, nunca negativo
        Assert.Equal(PlayerSide.Human, engine.State.Winner);
    }

    [Fact]
    public void BothPlayersAtZeroLp_SimultaneousDefeat_IsADraw()
    {
        var config = new DuelConfig { FirstPlayerSkipsFirstBattle = false };
        var (engine, human, cpu) = TestDuelFactory.Create(config);
        TestDuelFactory.PlaceOnField(human, 0, TestCards.Level4Strong);
        human.LifePoints = 0;
        cpu.LifePoints = 0;
        engine.AdvancePhase(); // Main1 -> Battle

        var result = engine.DeclareAttack(0, -1); // el rival no tiene monstruos

        Assert.True(result.Success);
        Assert.True(engine.IsOver);
        Assert.Null(engine.State.Winner);
    }

    [Fact]
    public void PlayerUnableToDraw_LosesTheDuel_DeckOut()
    {
        var (engine, human, _) = TestDuelFactory.CreateWithEmptyDecks(firstPlayerIndex: 0);
        // El humano (jugador inicial) no roba en su turno 1; al pasar a la CPU
        // (turno 2, no es su primer turno como jugador inicial) si su mazo esta
        // vacio, pierde por no poder robar.
        engine.EndTurn();

        Assert.True(engine.IsOver);
        Assert.Equal(PlayerSide.Human, engine.State.Winner);
    }
}
