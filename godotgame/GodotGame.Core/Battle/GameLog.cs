namespace GodotGame.Core.Battle;

/// <summary>
/// Registro de eventos del duelo. Permite a la UI mostrar lo ocurrido sin que
/// el motor conozca nada de la presentacion. Mantiene una lista acotada.
/// </summary>
public sealed class GameLog
{
    private const int MaxEntries = 200;
    private readonly List<string> _entries = new();

    public IReadOnlyList<string> Entries => _entries;

    /// <summary>Evento mas reciente, util para resaltarlo en pantalla.</summary>
    public string Last => _entries.Count > 0 ? _entries[^1] : string.Empty;

    public void Add(string message)
    {
        _entries.Add(message);
        if (_entries.Count > MaxEntries)
            _entries.RemoveAt(0);
    }

    /// <summary>Devuelve las ultimas <paramref name="count"/> entradas, mas antiguas primero.</summary>
    public IEnumerable<string> Tail(int count)
    {
        int start = Math.Max(0, _entries.Count - count);
        for (int i = start; i < _entries.Count; i++)
            yield return _entries[i];
    }
}
