using GodotGame.Data.Sqlite;

namespace GodotGame.Editor.Data;

/// <summary>
/// Catalogo de mazos en memoria del editor de mazos. Mismo espiritu que
/// <see cref="CardRepository"/>: carga al construirse, valida antes de
/// guardar y nunca persiste un mazo invalido.
/// </summary>
public sealed class DeckRepository
{
    private readonly SqliteDeckWriter _writer;
    private List<SavedDeck> _decks = new();

    public DeckRepository(string dbPath)
    {
        _writer = new SqliteDeckWriter(dbPath);
        Reload();
    }

    public IReadOnlyList<SavedDeck> Decks => _decks;

    public void Reload() => _decks = _writer.LoadAllDecks().OrderBy(d => d.Name).ToList();

    /// <summary>
    /// Valida nombre unico y no vacio, al menos 1 carta, y como maximo 3
    /// copias de una misma carta.
    /// </summary>
    public List<string> Validate(string name, IReadOnlyList<int> cardIds, int? originalId)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
            errors.Add("El nombre del mazo no puede estar vacio.");

        var others = originalId.HasValue ? _decks.Where(d => d.Id != originalId.Value) : _decks;
        if (!string.IsNullOrWhiteSpace(name) && others.Any(d => string.Equals(d.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase)))
            errors.Add($"Ya existe otro mazo llamado \"{name}\".");

        if (cardIds.Count == 0)
            errors.Add("El mazo no puede estar vacio.");

        var tooMany = cardIds.GroupBy(id => id).Where(g => g.Count() > 3).Select(g => g.Key).ToList();
        if (tooMany.Count > 0)
            errors.Add($"No se pueden tener mas de 3 copias de la misma carta (Id {string.Join(", ", tooMany)}).");

        return errors;
    }

    /// <summary>Valida y, si no hay errores, guarda. Devuelve la lista de errores (vacia si se guardo con exito).</summary>
    public List<string> Save(int? id, string name, IReadOnlyList<int> cardIds)
    {
        var errors = Validate(name, cardIds, id);
        if (errors.Count > 0) return errors;

        _writer.SaveDeck(id, name, cardIds);
        Reload();
        return errors;
    }

    public void Delete(int id)
    {
        _writer.DeleteDeck(id);
        Reload();
    }
}
