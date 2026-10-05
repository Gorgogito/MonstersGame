namespace GodotGame.Data.Loaders;

/// <summary>
/// Definicion de un mazo cargada desde datos externos: un nombre y la lista de
/// Ids de carta que lo componen (admite repeticiones, hasta 3 por reglamento).
/// </summary>
public sealed class DeckDefinition
{
    public string Name { get; }
    public IReadOnlyList<int> CardIds { get; }

    public DeckDefinition(string name, IReadOnlyList<int> cardIds)
    {
        Name = name;
        CardIds = cardIds;
    }

    public int Count => CardIds.Count;
}
