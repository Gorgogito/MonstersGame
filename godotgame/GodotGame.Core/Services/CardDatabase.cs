using GodotGame.Core.Entities;

namespace GodotGame.Core.Services;

/// <summary>
/// Catalogo en memoria de todas las cartas disponibles, indexado por Id.
/// Se construye a partir de los datos cargados por los loaders, de modo que el
/// motor nunca codifica cartas directamente.
/// </summary>
public sealed class CardDatabase
{
    private readonly Dictionary<int, Card> _cards;

    public CardDatabase(IEnumerable<Card> cards)
    {
        _cards = new Dictionary<int, Card>();
        foreach (var card in cards)
            _cards[card.Id] = card;
    }

    public int Count => _cards.Count;

    public IEnumerable<Card> AllCards => _cards.Values;

    /// <summary>Obtiene una carta por Id o null si no existe.</summary>
    public Card? Get(int id) => _cards.TryGetValue(id, out var card) ? card : null;

    /// <summary>Obtiene una carta de monstruo por Id o null si no existe / no es monstruo.</summary>
    public MonsterCard? GetMonster(int id) => Get(id) as MonsterCard;

    /// <summary>Crea una copia jugable (nueva instancia logica) de la carta indicada.</summary>
    public Card? CreateCardCopy(int id)
    {
        // Las cartas son inmutables, por lo que se puede reutilizar la misma
        // referencia en el Deck. El estado mutable vive en CardInstance.
        return Get(id);
    }
}
