using GodotGame.Core.Effects;
using GodotGame.Core.Entities;
using GodotGame.Core.Services;
using GodotGame.Data.Loaders;
using GodotGame.Data.Sqlite;

namespace GodotGame.Data;

/// <summary>
/// Punto unico de acceso a los datos del juego. Orquesta los loaders, construye
/// la base de cartas, el servicio de fusiones y la lista de mazos. La logica de
/// juego depende de estas abstracciones, no de los archivos concretos.
///
/// Desde el Bloque 9, la fuente de datos por defecto es una base SQLite unica
/// (<c>Data/monstersgame.db</c>), pensada para un catalogo que va a seguir
/// creciendo. Los loaders JSON (<see cref="JsonCardLoader"/>, etc.) se
/// conservan intactos como backend alternativo — el diseno por interfaces
/// (<see cref="ICardLoader"/>, etc.) hace que cualquiera de los dos funcione
/// sin tocar el resto del motor.
/// </summary>
public sealed class GameData
{
    public CardDatabase Cards { get; }
    public FusionService Fusions { get; }
    public IReadOnlyList<DeckDefinition> Decks { get; }

    private GameData(CardDatabase cards, FusionService fusions, IReadOnlyList<DeckDefinition> decks)
    {
        Cards = cards;
        Fusions = fusions;
        Decks = decks;
    }

    /// <summary>
    /// Carga todos los datos desde <c>Data/monstersgame.db</c>. Sin
    /// <paramref name="dbPath"/>, asume que esta junto al ejecutable
    /// (comportamiento original, valido para un backend de escritorio que
    /// copia el archivo al directorio de salida). Godot pasa su propia ruta
    /// resuelta (<c>ProjectSettings.GlobalizePath("res://Data/monstersgame.db")</c>)
    /// porque su directorio de salida en tiempo de edicion no coincide con la
    /// raiz del proyecto -- ver <c>GameRoot</c> en el proyecto Godot.
    /// </summary>
    public static GameData LoadFromDisk(string? dbPath = null)
    {
        dbPath ??= Path.Combine(AppContext.BaseDirectory, "Data", "monstersgame.db");

        var cardLoader = new SqliteCardLoader(dbPath);
        var deckLoader = new SqliteDeckLoader(dbPath);
        var fusionLoader = new SqliteFusionLoader(dbPath);
        var effectDefinitionLoader = new SqliteEffectDefinitionLoader(dbPath);

        var database = new CardDatabase(cardLoader.LoadCards());
        var fusions = new FusionService(fusionLoader.LoadFusions(), database);
        var decks = deckLoader.LoadDecks();
        EffectDefinitionResolver.Load(effectDefinitionLoader.LoadEffectDefinitions());

        return new GameData(database, fusions, decks);
    }

    /// <summary>
    /// Construye la lista de cartas de un mazo resolviendo los Ids contra la
    /// base de datos. Ignora Ids inexistentes (datos defensivos).
    /// </summary>
    public List<Card> BuildDeck(DeckDefinition definition)
    {
        var cards = new List<Card>(definition.Count);
        foreach (int id in definition.CardIds)
        {
            var card = Cards.Get(id);
            if (card != null) cards.Add(card);
        }
        return cards;
    }

    /// <summary>Baraja una lista de cartas en sitio (algoritmo Fisher-Yates).</summary>
    public static void Shuffle(IList<Card> cards, Random random)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }
}
