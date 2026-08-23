using MonstersGame.Data.Loaders;
using MonstersGame.Data.Sqlite;

namespace MonstersGame.CardEditor;

/// <summary>
/// Ventana secundaria para armar y guardar mazos: elegir un mazo existente
/// (o empezar uno nuevo), agregar/quitar cartas del catalogo, y guardarlo
/// para que quede disponible en la Seleccion de Mazo del juego. Mismo
/// principio que <see cref="MainForm"/>: nunca guarda un mazo invalido.
/// </summary>
public sealed class DeckEditorForm : Form
{
    private readonly DeckRepository _deckRepo;
    private readonly CardRepository _cardRepo;

    private readonly ListBox _deckList = new();
    private readonly Button _newDeckButton = new() { Text = "Nuevo Mazo" };
    private readonly Button _deleteDeckButton = new() { Text = "Eliminar Mazo" };

    private readonly TextBox _catalogSearchBox = new() { PlaceholderText = "Buscar en el catalogo..." };
    private readonly ListBox _catalogList = new();
    private readonly Button _addButton = new() { Text = "Agregar >>" };

    private readonly TextBox _nameBox = new();
    private readonly Label _countLabel = new() { AutoSize = true };
    private readonly ListBox _contentsList = new();
    private readonly Button _removeButton = new() { Text = "<< Quitar" };
    private readonly ListBox _errorsList = new() { Height = 70, BackColor = Color.FromArgb(40, 20, 20), ForeColor = Color.FromArgb(255, 150, 150) };
    private readonly Button _saveButton = new() { Text = "Guardar Mazo" };

    private int? _editingDeckId;
    private readonly List<int> _cardIds = new();

    public DeckEditorForm(string dbPath, CardRepository cardRepo)
    {
        _deckRepo = new DeckRepository(dbPath);
        _cardRepo = cardRepo;

        Text = "MonstersGame — Editor de Mazos";
        Width = 1080;
        Height = 720;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 600);

        BuildLayout();
        WireEvents();

        RefreshDeckList();
        RefreshCatalogList();
        LoadBlank();
    }

    // ------------------------------------------------------------- Layout

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(8) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        Controls.Add(root);

        root.Controls.Add(BuildDeckListPanel(), 0, 0);
        root.Controls.Add(BuildCatalogPanel(), 1, 0);
        root.Controls.Add(BuildEditorPanel(), 2, 0);
    }

    private Control BuildDeckListPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        var stack = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        stack.Controls.Add(new Label { Text = "Mazos guardados:", AutoSize = true, Margin = new Padding(0, 0, 0, 4) }, 0, 0);
        _deckList.Dock = DockStyle.Fill;
        stack.Controls.Add(_deckList, 0, 1);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 4, 0, 0) };
        _newDeckButton.Width = 220;
        _deleteDeckButton.Width = 220;
        buttons.Controls.Add(_newDeckButton);
        buttons.Controls.Add(_deleteDeckButton);
        stack.Controls.Add(buttons, 0, 2);

        panel.Controls.Add(stack);
        return panel;
    }

    private Control BuildCatalogPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        var stack = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        stack.Controls.Add(new Label { Text = "Catalogo de cartas (doble clic para agregar):", AutoSize = true, Margin = new Padding(0, 0, 0, 4) }, 0, 0);
        _catalogSearchBox.Dock = DockStyle.Fill;
        stack.Controls.Add(_catalogSearchBox, 0, 1);
        _catalogList.Dock = DockStyle.Fill;
        stack.Controls.Add(_catalogList, 0, 2);
        _addButton.Dock = DockStyle.Top;
        _addButton.Margin = new Padding(0, 4, 0, 0);
        stack.Controls.Add(_addButton, 0, 3);

        panel.Controls.Add(stack);
        return panel;
    }

    private Control BuildEditorPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        var stack = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7 };
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        stack.Controls.Add(new Label { Text = "Nombre del mazo:", AutoSize = true, Margin = new Padding(0, 0, 0, 4) }, 0, 0);
        _nameBox.Dock = DockStyle.Fill;
        stack.Controls.Add(_nameBox, 0, 1);

        _countLabel.Margin = new Padding(0, 8, 0, 4);
        stack.Controls.Add(_countLabel, 0, 2);

        _contentsList.Dock = DockStyle.Fill;
        stack.Controls.Add(_contentsList, 0, 3);

        _removeButton.Dock = DockStyle.Top;
        _removeButton.Margin = new Padding(0, 4, 0, 0);
        stack.Controls.Add(_removeButton, 0, 4);

        _errorsList.Dock = DockStyle.Top;
        _errorsList.Margin = new Padding(0, 8, 0, 0);
        stack.Controls.Add(_errorsList, 0, 5);

        _saveButton.Dock = DockStyle.Top;
        _saveButton.Height = 32;
        _saveButton.Margin = new Padding(0, 8, 0, 0);
        stack.Controls.Add(_saveButton, 0, 6);

        panel.Controls.Add(stack);
        return panel;
    }

    // -------------------------------------------------------------- Eventos

    private void WireEvents()
    {
        _deckList.SelectedIndexChanged += (_, _) => OnDeckListSelectionChanged();
        _newDeckButton.Click += (_, _) => LoadBlank();
        _deleteDeckButton.Click += (_, _) => OnDeleteDeckClicked();

        _catalogSearchBox.TextChanged += (_, _) => RefreshCatalogList();
        _addButton.Click += (_, _) => OnAddClicked();
        _catalogList.DoubleClick += (_, _) => OnAddClicked();

        _removeButton.Click += (_, _) => OnRemoveClicked();
        _contentsList.DoubleClick += (_, _) => OnRemoveClicked();

        _nameBox.TextChanged += (_, _) => RefreshErrors();
        _saveButton.Click += (_, _) => OnSaveClicked();
    }

    private void OnDeckListSelectionChanged()
    {
        if (_deckList.SelectedItem is not DeckListItem item) return;

        _editingDeckId = item.Deck.Id;
        _nameBox.Text = item.Deck.Name;
        _cardIds.Clear();
        _cardIds.AddRange(item.Deck.CardIds);
        RefreshContentsList();
    }

    private void LoadBlank()
    {
        _editingDeckId = null;
        _deckList.ClearSelected();
        _nameBox.Text = "";
        _cardIds.Clear();
        RefreshContentsList();
    }

    private void OnDeleteDeckClicked()
    {
        if (_editingDeckId is not int id)
        {
            MessageBox.Show(this, "Selecciona un mazo guardado de la lista para eliminarlo.", "MonstersGame — Editor de Mazos");
            return;
        }

        var confirm = MessageBox.Show(this,
            $"¿Eliminar definitivamente el mazo \"{_nameBox.Text}\"? Esta accion no se puede deshacer.",
            "Confirmar eliminacion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes) return;

        _deckRepo.Delete(id);
        RefreshDeckList();
        LoadBlank();
    }

    private void OnAddClicked()
    {
        if (_catalogList.SelectedItem is not CatalogItem item) return;

        int copies = _cardIds.Count(id => id == item.Dto.Id);
        if (copies >= 3)
        {
            MessageBox.Show(this, "Ya hay 3 copias de esta carta en el mazo (el maximo permitido).", "MonstersGame — Editor de Mazos");
            return;
        }

        _cardIds.Add(item.Dto.Id);
        RefreshContentsList();
    }

    private void OnRemoveClicked()
    {
        if (_contentsList.SelectedItem is not ContentItem item) return;

        _cardIds.Remove(item.CardId); // todas las copias son iguales: alcanza con sacar la primera que aparezca
        RefreshContentsList();
    }

    private void OnSaveClicked()
    {
        var errors = _deckRepo.Save(_editingDeckId, _nameBox.Text, _cardIds);
        _errorsList.Items.Clear();
        foreach (var e in errors) _errorsList.Items.Add(e);
        _saveButton.Enabled = errors.Count == 0;
        if (errors.Count > 0) return;

        int? savedId = _editingDeckId;
        RefreshDeckList();
        if (savedId.HasValue) SelectDeckInList(savedId.Value);
        else
        {
            // Mazo nuevo: la lista ya se releyo de la base, hay que ubicar
            // cual de los mazos releidos es el que se acaba de crear.
            var created = _deckRepo.Decks.FirstOrDefault(d => string.Equals(d.Name.Trim(), _nameBox.Text.Trim(), StringComparison.OrdinalIgnoreCase));
            if (created != null) { _editingDeckId = created.Id; SelectDeckInList(created.Id); }
        }

        MessageBox.Show(this, $"Mazo \"{_nameBox.Text}\" guardado.", "MonstersGame — Editor de Mazos", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SelectDeckInList(int id)
    {
        for (int i = 0; i < _deckList.Items.Count; i++)
            if (_deckList.Items[i] is DeckListItem item && item.Deck.Id == id)
            {
                _deckList.SelectedIndex = i;
                return;
            }
    }

    // --------------------------------------------------------- Refrescar UI

    private void RefreshDeckList()
    {
        _deckList.BeginUpdate();
        _deckList.Items.Clear();
        foreach (var deck in _deckRepo.Decks)
            _deckList.Items.Add(new DeckListItem(deck));
        _deckList.EndUpdate();
    }

    private void RefreshCatalogList()
    {
        string search = _catalogSearchBox.Text.Trim();
        var filtered = _cardRepo.Cards
            .Where(c => search.Length == 0 || c.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Id);

        _catalogList.BeginUpdate();
        _catalogList.Items.Clear();
        foreach (var dto in filtered)
            _catalogList.Items.Add(new CatalogItem(dto));
        _catalogList.EndUpdate();
    }

    private void RefreshContentsList()
    {
        var groups = _cardIds.GroupBy(id => id).ToList();

        _contentsList.BeginUpdate();
        _contentsList.Items.Clear();
        foreach (var group in groups)
        {
            var dto = _cardRepo.Cards.FirstOrDefault(c => c.Id == group.Key);
            string name = dto?.Name ?? $"(Id {group.Key}: no esta en el catalogo)";
            int count = group.Count();
            string label = count > 1 ? $"[{group.Key}] {name} x{count}" : $"[{group.Key}] {name}";
            _contentsList.Items.Add(new ContentItem(group.Key, label));
        }
        _contentsList.EndUpdate();

        _countLabel.Text = $"{_cardIds.Count} carta(s) en el mazo, {groups.Count} distinta(s).";
        RefreshErrors();
    }

    private void RefreshErrors()
    {
        var errors = _deckRepo.Validate(_nameBox.Text, _cardIds, _editingDeckId);
        _errorsList.Items.Clear();
        foreach (var e in errors) _errorsList.Items.Add(e);
        _saveButton.Enabled = errors.Count == 0;
    }

    // ------------------------------------------------------------------ Items

    private sealed class DeckListItem
    {
        public SavedDeck Deck { get; }
        public DeckListItem(SavedDeck deck) => Deck = deck;
        public override string ToString() => $"{Deck.Name} ({Deck.CardIds.Count} cartas)";
    }

    private sealed class CatalogItem
    {
        public CardDto Dto { get; }
        public CatalogItem(CardDto dto) => Dto = dto;
        public override string ToString() => $"[{Dto.Id}] {Dto.Name} ({Dto.Kind})";
    }

    private sealed class ContentItem
    {
        public int CardId { get; }
        private readonly string _label;
        public ContentItem(int cardId, string label) { CardId = cardId; _label = label; }
        public override string ToString() => _label;
    }
}
