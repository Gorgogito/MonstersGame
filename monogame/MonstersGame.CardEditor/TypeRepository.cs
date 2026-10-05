using MonstersGame.Data.Sqlite;

namespace MonstersGame.CardEditor;

/// <summary>
/// Catalogo de Tipos de Monstruo en memoria (Bloque 13): a diferencia de
/// Categoria/SubType/Atributo, el Tipo no tiene ninguna sinergia de reglas
/// propia, asi que se administra como texto libre desde el editor en vez de
/// un enum del codigo — agregar un Tipo nuevo (p. ej. "Bestia Divina") ya no
/// exige tocar codigo ni recompilar.
/// </summary>
public sealed class TypeRepository
{
    private readonly SqliteTypeWriter _writer;
    private List<string> _types = new();

    public TypeRepository(string dbPath)
    {
        _writer = new SqliteTypeWriter(dbPath);
        Reload();
    }

    public IReadOnlyList<string> Types => _types;

    public void Reload() => _types = _writer.LoadAll();

    /// <summary>Agrega un Tipo. Devuelve un mensaje de error, o null si se agrego con exito.</summary>
    public string? Add(string name)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return "El nombre del Tipo no puede estar vacio.";
        if (_types.Any(t => string.Equals(t, name, StringComparison.OrdinalIgnoreCase)))
            return $"Ya existe el Tipo \"{name}\".";

        _writer.Add(name);
        Reload();
        return null;
    }

    /// <summary>Elimina un Tipo, salvo que alguna carta del catalogo lo este usando (evita dejar cartas con un Tipo huerfano).</summary>
    public string? Delete(string name)
    {
        int inUse = _writer.CountCardsUsing(name);
        if (inUse > 0)
            return $"No se puede eliminar: {inUse} carta(s) usan el Tipo \"{name}\". Cambiales el Tipo primero, o renombralo en vez de borrarlo.";

        _writer.Delete(name);
        Reload();
        return null;
    }

    /// <summary>Renombra un Tipo y actualiza toda carta que lo tuviera asignado.</summary>
    public string? Rename(string oldName, string newName)
    {
        newName = newName.Trim();
        if (string.IsNullOrWhiteSpace(newName)) return "El nuevo nombre no puede estar vacio.";
        if (!string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase)
            && _types.Any(t => string.Equals(t, newName, StringComparison.OrdinalIgnoreCase)))
            return $"Ya existe el Tipo \"{newName}\".";

        _writer.Rename(oldName, newName);
        Reload();
        return null;
    }
}
