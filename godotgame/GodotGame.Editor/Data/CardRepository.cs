using GodotGame.Data.Loaders;
using GodotGame.Data.Sqlite;

namespace GodotGame.Editor.Data;

/// <summary>
/// Catalogo de cartas en memoria del editor. Carga desde la base SQLite al
/// construirse y expone crear/duplicar/validar/guardar/eliminar -- sin
/// reimplementar ninguna regla: valida y persiste llamando directo a
/// <see cref="CardDtoValidator"/>/<see cref="SqliteCardWriter"/> de
/// GodotGame.Data (identico al de MonstersGame.CardEditor).
/// </summary>
public sealed class CardRepository
{
    private readonly SqliteCardWriter _writer;
    private readonly List<CardDto> _cards = new();

    public CardRepository(string dbPath)
    {
        _writer = new SqliteCardWriter(dbPath);
        Reload();
    }

    public IReadOnlyList<CardDto> Cards => _cards;

    /// <summary>Descarta cualquier cambio no guardado y vuelve a leer el disco.</summary>
    public void Reload()
    {
        _cards.Clear();
        _cards.AddRange(_writer.LoadAllDtos().OrderBy(c => c.Id));
    }

    /// <summary>Siguiente Id libre, para prellenar una carta nueva.</summary>
    public int NextId() => _cards.Count == 0 ? 1 : _cards.Max(c => c.Id) + 1;

    /// <summary>Valida sin guardar (para retroalimentacion en vivo mientras se edita).</summary>
    public List<string> Validate(CardDto dto, int? originalId) =>
        CardDtoValidator.Validate(dto, _cards, originalId);

    /// <summary>
    /// Valida y, si no hay errores, guarda en disco y actualiza el catalogo
    /// en memoria. Devuelve la lista de errores (vacia si se guardo con
    /// exito) -- nunca guarda una carta invalida.
    /// </summary>
    public List<string> Save(CardDto dto, int? originalId)
    {
        var errors = Validate(dto, originalId);
        if (errors.Count > 0) return errors;

        _writer.SaveCard(dto);

        int idx = originalId.HasValue ? _cards.FindIndex(c => c.Id == originalId.Value) : -1;
        if (idx >= 0) _cards[idx] = dto;
        else _cards.Add(dto);
        _cards.Sort((a, b) => a.Id.CompareTo(b.Id));

        return errors;
    }

    public void Delete(int id)
    {
        _writer.DeleteCard(id);
        _cards.RemoveAll(c => c.Id == id);
    }

    /// <summary>Crea una copia en memoria (sin guardar todavia) con un Id nuevo y "(copia)" en el nombre.</summary>
    public CardDto Duplicate(CardDto source)
    {
        return new CardDto
        {
            Id = NextId(),
            Kind = source.Kind,
            Name = source.Name + " (copia)",
            Attack = source.Attack,
            Defense = source.Defense,
            Level = source.Level,
            Type = source.Type,
            Attribute = source.Attribute,
            Image = source.Image,
            Description = source.Description,
            EffectId = source.EffectId,
            SubType = source.SubType,
            Category = source.Category,
            GuardianStar1 = source.GuardianStar1,
            GuardianStar2 = source.GuardianStar2,
            RitualMonsterId = source.RitualMonsterId,
            RequiredRitualLevel = source.RequiredRitualLevel
        };
    }

    public CardDto NewBlankCard(string kind) => new()
    {
        Id = NextId(),
        Kind = kind,
        Name = "",
        Type = "Unknown",
        Attribute = "Dark",
        Category = "Normal",
        SubType = "Normal"
    };
}
