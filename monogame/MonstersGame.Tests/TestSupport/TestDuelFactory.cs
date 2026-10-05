using MonstersGame.Core.Battle;
using MonstersGame.Core.Entities;
using MonstersGame.Core.Rules;
using MonstersGame.Core.Services;

namespace MonstersGame.Tests.TestSupport;

/// <summary>
/// Construye un <see cref="DuelEngine"/> ya iniciado y listo para que cada test
/// puebre manualmente la mano y las Zonas de Monstruo que necesite, sin
/// depender del robo aleatorio ni de <c>Data/Cards/cards.json</c>.
/// </summary>
internal static class TestDuelFactory
{
    /// <summary>
    /// Inicia un duelo con mazos vacios (el robo inicial simplemente no hace
    /// nada) y limpia cualquier mano resultante, dejando el duelo en Main
    /// Phase 1 del jugador indicado como activo.
    /// </summary>
    public static (DuelEngine Engine, Player Human, Player Cpu) Create(
        DuelConfig? config = null,
        IEnumerable<FusionRecipe>? recipes = null,
        int firstPlayerIndex = 0)
    {
        var human = new Player(PlayerSide.Human, "Human");
        var cpu = new Player(PlayerSide.Cpu, "Cpu");

        // Se abastece cada mazo con cartas de relleno para que las pruebas
        // que avanzan varios turnos (EndTurn) no disparen un deck-out
        // accidental: solo las pruebas de la seccion de victoria vacian el
        // mazo a proposito para probar esa regla.
        for (int i = 0; i < 40; i++)
        {
            human.Deck.Add(TestCards.Level4Weak);
            cpu.Deck.Add(TestCards.Level4Weak);
        }

        var database = new CardDatabase(TestCards.All);
        var fusion = new FusionService(recipes ?? Array.Empty<FusionRecipe>(), database);
        var engine = new DuelEngine(config ?? new DuelConfig(), fusion);

        engine.StartDuel(human, cpu, firstPlayerIndex);

        // La mano inicial (5 cartas robadas del mazo de relleno) se descarta
        // de la vista: cada test define su propia mano/zonas desde cero.
        human.Hand.Clear();
        cpu.Hand.Clear();

        return (engine, human, cpu);
    }

    /// <summary>
    /// Igual que <see cref="Create"/> pero con los mazos vacios desde el
    /// inicio, para probar deliberadamente la regla de deck-out.
    /// </summary>
    public static (DuelEngine Engine, Player Human, Player Cpu) CreateWithEmptyDecks(
        DuelConfig? config = null,
        IEnumerable<FusionRecipe>? recipes = null,
        int firstPlayerIndex = 0)
    {
        var human = new Player(PlayerSide.Human, "Human");
        var cpu = new Player(PlayerSide.Cpu, "Cpu");

        var database = new CardDatabase(TestCards.All);
        var fusion = new FusionService(recipes ?? Array.Empty<FusionRecipe>(), database);
        var engine = new DuelEngine(config ?? new DuelConfig(), fusion);

        engine.StartDuel(human, cpu, firstPlayerIndex);
        human.Hand.Clear();
        cpu.Hand.Clear();

        return (engine, human, cpu);
    }

    /// <summary>Coloca un monstruo boca arriba en Ataque directamente en una Zona de Monstruos, sin pasar por Invocacion.</summary>
    public static CardInstance PlaceOnField(Player player, int zone, MonsterCard card, BattlePosition position = BattlePosition.Attack)
    {
        var instance = new CardInstance(card, position);
        player.MonsterZones[zone] = instance;
        return instance;
    }

    /// <summary>Coloca una Carta Magica/Trampa directamente en una Zona de Magia/Trampa, sin pasar por SetSpellOrTrap.</summary>
    public static SpellTrapInstance PlaceSpellTrap(Player player, int zone, Card card, bool faceUp, bool setThisTurn = false)
    {
        var instance = new SpellTrapInstance(card, faceUp) { SetThisTurn = setThisTurn };
        player.SpellTrapZones[zone] = instance;
        return instance;
    }

    /// <summary>
    /// Simula que nadie mas quiere encadenar: pasa la Prioridad hasta que la
    /// Cadena abierta se resuelve por completo (dos "paso" seguidos).
    /// </summary>
    public static void CloseChain(DuelEngine engine)
    {
        while (engine.State.Chain.Count > 0)
        {
            var result = engine.PassPriority();
            if (!result.Success) throw new InvalidOperationException(result.Message);
        }
    }
}
