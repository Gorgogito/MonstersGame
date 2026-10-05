using Godot;
using GodotGame.Data.Sqlite;
using GodotGame.Editor.Data;

namespace GodotGame.Editor.Scenes.DeckEditor;

/// <summary>
/// Ventana secundaria para armar y guardar mazos: elegir un mazo existente
/// (o empezar uno nuevo), agregar/quitar cartas del catalogo, y guardarlo
/// para que quede disponible en la Seleccion de Mazo del juego. Adaptacion
/// Godot de <c>DeckEditorForm</c> de MonstersGame.CardEditor -- mismo
/// principio: nunca guarda un mazo invalido.
/// </summary>
public sealed partial class DeckEditorWindow : Window
{
    private readonly DeckRepository _deckRepo;
    private readonly CardRepository _cardRepo;

    private readonly ItemList _deckList = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
    private readonly List<SavedDeck> _listedDecks = new();

    private readonly LineEdit _catalogSearchBox = new() { PlaceholderText = "Buscar en el catalogo..." };
    private readonly ItemList _catalogList = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
    private readonly List<GodotGame.Data.Loaders.CardDto> _listedCatalog = new();

    private readonly LineEdit _nameBox = new();
    private readonly Label _countLabel = new();
    private readonly ItemList _contentsList = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
    private readonly ItemList _errorsList = new() { CustomMinimumSize = new Vector2(0, 70), Modulate = new Color(1f, 0.6f, 0.6f) };
    private readonly Button _saveButton = new() { Text = "Guardar Mazo", CustomMinimumSize = new Vector2(0, 32) };

    private int? _editingDeckId;
    private readonly List<int> _cardIds = new();

    public event Action? Closed;

    public DeckEditorWindow(string dbPath, CardRepository cardRepo)
    {
        _deckRepo = new DeckRepository(dbPath);
        _cardRepo = cardRepo;

        Title = "GodotGame — Editor de Mazos";
        Size = new Vector2I(1080, 720);
        Exclusive = true;

        BuildUi();

        RefreshDeckList();
        RefreshCatalogList();
        LoadBlank();

        CloseRequested += () => Closed?.Invoke();
    }

    private void BuildUi()
    {
        var root = new HBoxContainer { AnchorRight = 1, AnchorBottom = 1, OffsetLeft = 8, OffsetTop = 8, OffsetRight = -8, OffsetBottom = -8 };
        root.AddThemeConstantOverride("separation", 10);
        AddChild(root);

        root.AddChild(BuildDeckListPanel());
        root.AddChild(BuildCatalogPanel());
        root.AddChild(BuildEditorPanel());
    }

    private Control BuildDeckListPanel()
    {
        var panel = new VBoxContainer { CustomMinimumSize = new Vector2(240, 0) };
        panel.AddChild(new Label { Text = "Mazos guardados:" });
        panel.AddChild(_deckList);
        _deckList.ItemSelected += i => OnDeckListSelectionChanged((int)i);

        var newDeckButton = new Button { Text = "Nuevo Mazo" };
        var deleteDeckButton = new Button { Text = "Eliminar Mazo" };
        newDeckButton.Pressed += LoadBlank;
        deleteDeckButton.Pressed += OnDeleteDeckClicked;
        panel.AddChild(newDeckButton);
        panel.AddChild(deleteDeckButton);

        return panel;
    }

    private Control BuildCatalogPanel()
    {
        var panel = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddChild(new Label { Text = "Catalogo de cartas (doble clic para agregar):" });
        panel.AddChild(_catalogSearchBox);
        panel.AddChild(_catalogList);
        _catalogSearchBox.TextChanged += _ => RefreshCatalogList();
        _catalogList.ItemActivated += i => OnAddClicked((int)i);

        var addButton = new Button { Text = "Agregar >>" };
        addButton.Pressed += () =>
        {
            var selected = _catalogList.GetSelectedItems();
            if (selected.Length > 0) OnAddClicked(selected[0]);
        };
        panel.AddChild(addButton);

        return panel;
    }

    private Control BuildEditorPanel()
    {
        var panel = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddChild(new Label { Text = "Nombre del mazo:" });
        panel.AddChild(_nameBox);
        _nameBox.TextChanged += _ => RefreshErrors();

        panel.AddChild(_countLabel);
        panel.AddChild(_contentsList);
        _contentsList.ItemActivated += i => OnRemoveClicked((int)i);

        var removeButton = new Button { Text = "<< Quitar" };
        removeButton.Pressed += () =>
        {
            var selected = _contentsList.GetSelectedItems();
            if (selected.Length > 0) OnRemoveClicked(selected[0]);
        };
        panel.AddChild(removeButton);

        panel.AddChild(_errorsList);
        _saveButton.Pressed += OnSaveClicked;
        panel.AddChild(_saveButton);

        return panel;
    }

    private void OnDeckListSelectionChanged(int index)
    {
        var deck = _listedDecks[index];
        _editingDeckId = deck.Id;
        _nameBox.Text = deck.Name;
        _cardIds.Clear();
        _cardIds.AddRange(deck.CardIds);
        RefreshContentsList();
    }

    private void LoadBlank()
    {
        _editingDeckId = null;
        _deckList.DeselectAll();
        _nameBox.Text = "";
        _cardIds.Clear();
        RefreshContentsList();
    }

    private void OnDeleteDeckClicked()
    {
        if (_editingDeckId is not int id) return;
        _deckRepo.Delete(id);
        RefreshDeckList();
        LoadBlank();
    }

    private void OnAddClicked(int catalogIndex)
    {
        var dto = _listedCatalog[catalogIndex];
        int copies = _cardIds.Count(id => id == dto.Id);
        if (copies >= 3) return; // ya hay 3 copias (el maximo permitido)

        _cardIds.Add(dto.Id);
        RefreshContentsList();
    }

    private void OnRemoveClicked(int contentsIndex)
    {
        var groups = _cardIds.GroupBy(id => id).ToList();
        if (contentsIndex >= groups.Count) return;
        _cardIds.Remove(groups[contentsIndex].Key); // todas las copias son iguales: alcanza con sacar la primera
        RefreshContentsList();
    }

    private void OnSaveClicked()
    {
        var errors = _deckRepo.Save(_editingDeckId, _nameBox.Text, _cardIds);
        _errorsList.Clear();
        foreach (var e in errors) _errorsList.AddItem(e);
        _saveButton.Disabled = errors.Count > 0;
        if (errors.Count > 0) return;

        int? savedId = _editingDeckId;
        RefreshDeckList();
        if (savedId.HasValue) SelectDeckInList(savedId.Value);
        else
        {
            var created = _deckRepo.Decks.FirstOrDefault(d => string.Equals(d.Name.Trim(), _nameBox.Text.Trim(), StringComparison.OrdinalIgnoreCase));
            if (created != null) { _editingDeckId = created.Id; SelectDeckInList(created.Id); }
        }
    }

    private void SelectDeckInList(int id)
    {
        for (int i = 0; i < _listedDecks.Count; i++)
            if (_listedDecks[i].Id == id) { _deckList.Select(i); return; }
    }

    private void RefreshDeckList()
    {
        _deckList.Clear();
        _listedDecks.Clear();
        foreach (var deck in _deckRepo.Decks)
        {
            _deckList.AddItem($"{deck.Name} ({deck.CardIds.Count} cartas)");
            _listedDecks.Add(deck);
        }
    }

    private void RefreshCatalogList()
    {
        string search = _catalogSearchBox.Text.Trim();
        var filtered = _cardRepo.Cards
            .Where(c => search.Length == 0 || c.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Id)
            .ToList();

        _catalogList.Clear();
        _listedCatalog.Clear();
        foreach (var dto in filtered)
        {
            _catalogList.AddItem($"[{dto.Id}] {dto.Name} ({dto.Kind})");
            _listedCatalog.Add(dto);
        }
    }

    private void RefreshContentsList()
    {
        var groups = _cardIds.GroupBy(id => id).ToList();

        _contentsList.Clear();
        foreach (var group in groups)
        {
            var dto = _cardRepo.Cards.FirstOrDefault(c => c.Id == group.Key);
            string name = dto?.Name ?? $"(Id {group.Key}: no esta en el catalogo)";
            int count = group.Count();
            string label = count > 1 ? $"[{group.Key}] {name} x{count}" : $"[{group.Key}] {name}";
            _contentsList.AddItem(label);
        }

        _countLabel.Text = $"{_cardIds.Count} carta(s) en el mazo, {groups.Count} distinta(s).";
        RefreshErrors();
    }

    private void RefreshErrors()
    {
        var errors = _deckRepo.Validate(_nameBox.Text, _cardIds, _editingDeckId);
        _errorsList.Clear();
        foreach (var e in errors) _errorsList.AddItem(e);
        _saveButton.Disabled = errors.Count > 0;
    }
}
