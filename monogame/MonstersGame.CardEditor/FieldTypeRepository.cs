using MonstersGame.Data.Loaders;
using MonstersGame.Data.Sqlite;

namespace MonstersGame.CardEditor;

/// <summary>
/// Catalogo de tipos de Campo en memoria (Fase 4/5): mismo patron que
/// <see cref="TypeRepository"/> para el Tipo de Monstruo -- administrable
/// desde el editor sin tocar codigo, y referenciado por Id desde cualquier
/// Magia de Campo.
/// </summary>
public sealed class FieldTypeRepository
{
    private readonly SqliteFieldTypeWriter _writer;
    private List<FieldTypeDto> _fieldTypes = new();

    public FieldTypeRepository(string dbPath)
    {
        _writer = new SqliteFieldTypeWriter(dbPath);
        Reload();
    }

    public IReadOnlyList<FieldTypeDto> FieldTypes => _fieldTypes;

    public void Reload() => _fieldTypes = _writer.LoadAll();

    /// <summary>Guarda (crea o reemplaza por Id). Devuelve un mensaje de error, o null si se guardo con exito.</summary>
    public string? Save(FieldTypeDto dto)
    {
        string id = dto.Id.Trim();
        if (string.IsNullOrWhiteSpace(id)) return "El Id no puede estar vacio.";
        if (string.IsNullOrWhiteSpace(dto.Name)) return "El nombre no puede estar vacio.";

        dto.Id = id;
        _writer.Save(dto);
        Reload();
        return null;
    }

    public string? Delete(string id)
    {
        int inUse = _writer.CountCardsUsing(id);
        if (inUse > 0)
            return $"No se puede eliminar: {inUse} Magia(s) de Campo usan el tipo \"{id}\". Cambiales el tipo primero.";

        _writer.Delete(id);
        Reload();
        return null;
    }

    public FieldTypeDto NewBlank() => new() { Id = "", Name = "", Color = "#FFFFFF" };
}
