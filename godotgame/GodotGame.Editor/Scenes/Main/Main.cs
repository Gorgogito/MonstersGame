using Godot;
using GodotGame.Data.Loaders;
using GodotGame.Editor.Controls;
using GodotGame.Editor.Data;
using GodotGame.Editor.Scenes.DeckEditor;
using GodotGame.Editor.Scenes.FieldTypeEditor;
using GodotGame.Editor.Scenes.TypeEditor;

namespace GodotGame.Editor.Scenes.Main;

/// <summary>
/// Ventana unica del editor: lista de cartas | previsualizacion | propiedades.
/// Adaptacion Godot de <c>MainForm</c> de MonstersGame.CardEditor: mismo
/// comportamiento (panel de propiedades dinamico segun Kind/Category/SubType,
/// Guardar deshabilitado mientras haya un error bloqueante, nunca escribe una
/// carta invalida), construido con nodos <see cref="Control"/> nativos en vez
/// de WinForms.
/// </summary>
public partial class Main : Control
{
    private CardRepository _repository = null!;
    private TypeRepository _typeRepo = null!;
    private FieldTypeRepository _fieldTypeRepo = null!;
    private string _dbPath = "";

    private LineEdit _searchBox = null!;
    private OptionButton _kindFilter = null!;
    private ItemList _cardList = null!;
    private readonly List<CardDto> _listedCards = new();

    private CardPreview _preview = null!;

    private OptionButton _kindCombo = null!;
    private LineEdit _idBox = null!;
    private LineEdit _nameBox = null!;
    private TextEdit _descriptionBox = null!;
    private LineEdit _imageBox = null!;

    private MonsterPanel _monsterPanel = null!;
    private SpellPanel _spellPanel = null!;
    private TrapPanel _trapPanel = null!;
    private EffectComposerPanel _effectComposerPanel = null!;

    private ItemList _errorsList = null!;
    private Button _saveButton = null!;

    private FileDialog _openImageDialog = null!;
    private FileDialog _openCardDialog = null!;
    private FileDialog _saveCardDialog = null!;
    private FileDialog _openPackDialog = null!;
    private FileDialog _savePackDialog = null!;
    private AcceptDialog _infoDialog = null!;
    private ConfirmationDialog _confirmDialog = null!;
    private PackNamePrompt _packNamePrompt = null!;

    private int? _editingOriginalId;
    private bool _suppressEvents;

    public override void _Ready()
    {
        string? dbPath = DatabaseLocator.Find();
        if (dbPath == null)
        {
            GD.PrintErr("No se encontro GodotGame.slnx en ningun directorio superior a " + AppContext.BaseDirectory);
            GetTree().Quit(1);
            return;
        }

        _dbPath = dbPath;
        _repository = new CardRepository(dbPath);
        _typeRepo = new TypeRepository(dbPath);
        _fieldTypeRepo = new FieldTypeRepository(dbPath);

        var effectContext = new EffectEditorContext { Types = () => _typeRepo.Types, Cards = () => _repository.Cards };
        _monsterPanel = new MonsterPanel(_typeRepo, effectContext);
        _spellPanel = new SpellPanel(_fieldTypeRepo, _typeRepo, effectContext);
        _trapPanel = new TrapPanel(effectContext);
        _effectComposerPanel = new EffectComposerPanel(_typeRepo);

        BuildUi();
        WireEvents();

        RefreshList();
        LoadIntoForm(_repository.NewBlankCard("Monster"), originalId: null);
    }

    // ------------------------------------------------------------- Layout

    private void BuildUi()
    {
        var root = new HBoxContainer { AnchorRight = 1, AnchorBottom = 1, OffsetLeft = 8, OffsetTop = 8, OffsetRight = -8, OffsetBottom = -8 };
        root.AddThemeConstantOverride("separation", 10);
        AddChild(root);

        root.AddChild(BuildListPanel());
        root.AddChild(BuildPreviewPanel());
        root.AddChild(BuildPropertiesPanel());

        BuildDialogs();
    }

    private Control BuildListPanel()
    {
        var panel = new VBoxContainer { CustomMinimumSize = new Vector2(260, 0) };
        panel.AddThemeConstantOverride("separation", 4);

        panel.AddChild(new Label { Text = "Catalogo:\n" + _dbPath, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, 0.6f) });

        _kindFilter = new OptionButton();
        foreach (string k in new[] { "Todas", "Monster", "Spell", "Trap" }) _kindFilter.AddItem(k);
        _kindFilter.Selected = 0;
        panel.AddChild(_kindFilter);

        _searchBox = new LineEdit { PlaceholderText = "Buscar por nombre..." };
        panel.AddChild(_searchBox);

        _cardList = new ItemList { SelectMode = ItemList.SelectModeEnum.Multi, SizeFlagsVertical = SizeFlags.ExpandFill };
        panel.AddChild(_cardList);

        _saveButton = new Button { Text = "Guardar", CustomMinimumSize = new Vector2(0, 32) }; // placeholder, movido a propiedades

        var listButtons = new VBoxContainer();
        foreach (var (text, handler) in new (string, Action)[]
        {
            ("Nueva", () => LoadIntoForm(_repository.NewBlankCard("Monster"), null)),
            ("Duplicar", OnDuplicateClicked),
            ("Eliminar", OnDeleteClicked),
            ("Recargar del disco", OnReloadClicked)
        })
        {
            var b = new Button { Text = text };
            b.Pressed += handler;
            listButtons.AddChild(b);
        }
        panel.AddChild(listButtons);

        var packButtons = new VBoxContainer();
        foreach (var (text, handler) in new (string, Action)[]
        {
            ("Exportar carta...", OnExportCardClicked),
            ("Importar carta...", OnImportCardClicked),
            ("Exportar paquete...", OnExportPackClicked),
            ("Importar paquete...", OnImportPackClicked)
        })
        {
            var b = new Button { Text = text };
            b.Pressed += handler;
            packButtons.AddChild(b);
        }
        panel.AddChild(packButtons);

        var deckEditorButton = new Button { Text = "Editor de Mazos..." };
        deckEditorButton.Pressed += OnDeckEditorClicked;
        panel.AddChild(deckEditorButton);

        return panel;
    }

    private Control BuildPreviewPanel()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(340, 0) };
        _preview = new CardPreview { ArtRoot = Path.Combine(Path.GetDirectoryName(_dbPath)!, "Art"), SizeFlagsVertical = SizeFlags.ExpandFill };
        panel.AddChild(_preview);
        return panel;
    }

    private Control BuildPropertiesPanel()
    {
        var scroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        var stack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        stack.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(stack);

        var layout = EditorLayout.TwoColumnLayout();
        _kindCombo = new OptionButton();
        foreach (string k in new[] { "Monster", "Spell", "Trap" }) _kindCombo.AddItem(k);
        EditorLayout.AddRow(layout, "Tipo de carta", _kindCombo);

        _idBox = new LineEdit();
        EditorLayout.AddRow(layout, "Id", _idBox);

        _nameBox = new LineEdit();
        EditorLayout.AddRow(layout, "Nombre", _nameBox);

        EditorLayout.AddRow(layout, "Imagen", BuildImageRow());

        _descriptionBox = new TextEdit { CustomMinimumSize = new Vector2(0, 70) };
        EditorLayout.AddRow(layout, "Descripcion", _descriptionBox);
        stack.AddChild(layout);

        stack.AddChild(_monsterPanel);
        stack.AddChild(_spellPanel);
        stack.AddChild(_trapPanel);
        stack.AddChild(_effectComposerPanel);

        stack.AddChild(new Label { Text = "Validacion:" });
        _errorsList = new ItemList { CustomMinimumSize = new Vector2(0, 90), Modulate = new Color(1f, 0.7f, 0.7f) };
        stack.AddChild(_errorsList);

        _saveButton = new Button { Text = "Guardar", CustomMinimumSize = new Vector2(150, 32) };
        stack.AddChild(_saveButton);

        return scroll;
    }

    private Control BuildImageRow()
    {
        var row = new HBoxContainer();
        _imageBox = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(_imageBox);
        var browseButton = new Button { Text = "Examinar..." };
        browseButton.Pressed += () => _openImageDialog.Popup();
        row.AddChild(browseButton);
        return row;
    }

    private void BuildDialogs()
    {
        _openImageDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            Filters = new[] { "*.png,*.jpg,*.jpeg,*.gif,*.bmp ; Imagenes" },
            Size = new Vector2I(700, 500)
        };
        _openImageDialog.FileSelected += path => _imageBox.Text = Path.GetFileName(path);
        AddChild(_openImageDialog);

        _openCardDialog = new FileDialog { FileMode = FileDialog.FileModeEnum.OpenFile, Access = FileDialog.AccessEnum.Filesystem, Filters = new[] { "*.json ; Carta GodotGame" }, Size = new Vector2I(700, 500) };
        _openCardDialog.FileSelected += OnImportCardFileSelected;
        AddChild(_openCardDialog);

        _saveCardDialog = new FileDialog { FileMode = FileDialog.FileModeEnum.SaveFile, Access = FileDialog.AccessEnum.Filesystem, Filters = new[] { "*.json ; Carta GodotGame" }, Size = new Vector2I(700, 500) };
        _saveCardDialog.FileSelected += OnExportCardFileSelected;
        AddChild(_saveCardDialog);

        _openPackDialog = new FileDialog { FileMode = FileDialog.FileModeEnum.OpenFile, Access = FileDialog.AccessEnum.Filesystem, Filters = new[] { "*.zip ; Paquete GodotGame" }, Size = new Vector2I(700, 500) };
        _openPackDialog.FileSelected += OnImportPackFileSelected;
        AddChild(_openPackDialog);

        _savePackDialog = new FileDialog { FileMode = FileDialog.FileModeEnum.SaveFile, Access = FileDialog.AccessEnum.Filesystem, Filters = new[] { "*.zip ; Paquete GodotGame" }, Size = new Vector2I(700, 500) };
        _savePackDialog.FileSelected += OnExportPackFileSelected;
        AddChild(_savePackDialog);

        _infoDialog = new AcceptDialog { Title = "GodotGame — Editor de Cartas" };
        AddChild(_infoDialog);

        _confirmDialog = new ConfirmationDialog { Title = "Confirmar" };
        AddChild(_confirmDialog);

        _packNamePrompt = new PackNamePrompt();
        AddChild(_packNamePrompt);
    }

    private void ShowInfo(string message)
    {
        _infoDialog.DialogText = message;
        _infoDialog.PopupCentered();
    }

    private void ShowConfirm(string message, Action onConfirm)
    {
        _confirmDialog.DialogText = message;
        foreach (var c in _confirmDialog.GetSignalConnectionList("confirmed")) _confirmDialog.Disconnect("confirmed", c["callable"].AsCallable());
        _confirmDialog.Confirmed += onConfirm;
        _confirmDialog.PopupCentered();
    }

    // -------------------------------------------------------------- Eventos

    private void WireEvents()
    {
        _searchBox.TextChanged += _ => RefreshList();
        _kindFilter.ItemSelected += _ => RefreshList();
        _cardList.MultiSelected += (_, _) => OnListSelectionChanged();

        _kindCombo.ItemSelected += _ => { UpdateActivePanel(); OnFormFieldChanged(); };
        _idBox.TextChanged += _ => OnFormFieldChanged();
        _nameBox.TextChanged += _ => OnFormFieldChanged();
        _imageBox.TextChanged += _ => OnFormFieldChanged();
        _descriptionBox.TextChanged += () => OnFormFieldChanged();

        _monsterPanel.Changed += () => { UpdateActivePanel(); OnFormFieldChanged(); };
        _monsterPanel.TypeEditorRequested += OnTypeEditorClicked;
        _spellPanel.Changed += () => { UpdateActivePanel(); OnFormFieldChanged(); };
        _spellPanel.FieldTypeEditorRequested += OnFieldTypeEditorClicked;
        _trapPanel.Changed += () => { UpdateActivePanel(); OnFormFieldChanged(); };
        _effectComposerPanel.Changed += OnFormFieldChanged;

        _saveButton.Pressed += OnSaveClicked;
    }

    // --------------------------------------------------------- Lista/filtros

    private void RefreshList()
    {
        string search = _searchBox.Text.Trim();
        string kindFilter = _kindFilter.Selected >= 0 ? _kindFilter.GetItemText(_kindFilter.Selected) : "Todas";

        var filtered = _repository.Cards
            .Where(c => kindFilter == "Todas" || c.Kind.Equals(kindFilter, StringComparison.OrdinalIgnoreCase))
            .Where(c => search.Length == 0 || c.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Id)
            .ToList();

        _cardList.Clear();
        _listedCards.Clear();
        foreach (var c in filtered)
        {
            _cardList.AddItem($"[{c.Id}] {c.Name} ({c.Kind})");
            _listedCards.Add(c);
        }
    }

    private void OnListSelectionChanged()
    {
        var selected = _cardList.GetSelectedItems();
        if (selected.Length == 1)
            LoadIntoForm(CardDtoCloning.Clone(_listedCards[selected[0]]), originalId: _listedCards[selected[0]].Id);
    }

    private void SelectInList(int id)
    {
        for (int i = 0; i < _listedCards.Count; i++)
            if (_listedCards[i].Id == id) { _cardList.Select(i); return; }
    }

    // ------------------------------------------------------------ Formulario

    private void LoadIntoForm(CardDto dto, int? originalId)
    {
        _suppressEvents = true;
        _editingOriginalId = originalId;

        SelectComboText(_kindCombo, dto.Kind);
        _idBox.Text = dto.Id.ToString();
        _idBox.Editable = originalId == null; // Id inmutable una vez guardada.
        _nameBox.Text = dto.Name;
        _imageBox.Text = dto.Image;
        _descriptionBox.Text = dto.Description;

        _monsterPanel.LoadFrom(dto);
        _spellPanel.LoadFrom(dto);
        _trapPanel.LoadFrom(dto);
        _effectComposerPanel.LoadFrom(dto);

        _suppressEvents = false;

        UpdateActivePanel();
        OnFormFieldChanged();
    }

    private void UpdateActivePanel()
    {
        string kind = SelectedItemText(_kindCombo, "Monster");
        _monsterPanel.Visible = kind == "Monster";
        _spellPanel.Visible = kind == "Spell";
        _trapPanel.Visible = kind == "Trap";

        string effectId = kind switch
        {
            "Spell" => _spellPanel.SelectedEffectId,
            "Trap" => _trapPanel.SelectedEffectId,
            _ => _monsterPanel.SelectedEffectId
        };
        bool isRitual = kind == "Spell" && _spellPanel.IsRitual;
        _effectComposerPanel.Visible = !isRitual && effectId == EffectCatalog.ComposeNew.Id;
    }

    private CardDto BuildDtoFromForm()
    {
        string kind = SelectedItemText(_kindCombo, "Monster");
        int.TryParse(_idBox.Text, out int id);

        var dto = new CardDto
        {
            Id = id,
            Kind = kind,
            Name = _nameBox.Text,
            Image = _imageBox.Text,
            Description = _descriptionBox.Text
        };

        bool isRitual = false;
        switch (kind)
        {
            case "Spell":
                _spellPanel.ApplyTo(dto);
                isRitual = _spellPanel.IsRitual;
                break;
            case "Trap":
                _trapPanel.ApplyTo(dto);
                break;
            default:
                _monsterPanel.ApplyTo(dto);
                break;
        }

        bool composing = !isRitual && dto.EffectId == EffectCatalog.ComposeNew.Id;
        dto.ComposeCustomEffect = composing;
        if (composing)
            _effectComposerPanel.ApplyTo(dto);

        return dto;
    }

    private void OnFormFieldChanged()
    {
        if (_suppressEvents) return;

        UpdateActivePanel();

        var dto = BuildDtoFromForm();
        _preview.SetCard(dto);

        var errors = _repository.Validate(dto, _editingOriginalId);
        _errorsList.Clear();
        foreach (var e in errors) _errorsList.AddItem(e);
        _saveButton.Disabled = errors.Count > 0;
    }

    // -------------------------------------------------------------- Acciones

    private void OnSaveClicked()
    {
        var dto = BuildDtoFromForm();
        var errors = _repository.Save(dto, _editingOriginalId);
        if (errors.Count > 0)
        {
            _errorsList.Clear();
            foreach (var e in errors) _errorsList.AddItem(e);
            _saveButton.Disabled = true;
            return;
        }

        _editingOriginalId = dto.Id;
        _idBox.Editable = false;
        RefreshList();
        SelectInList(dto.Id);
        ShowInfo($"\"{dto.Name}\" guardada.");
    }

    private void OnDuplicateClicked()
    {
        var selected = _cardList.GetSelectedItems();
        if (selected.Length == 0) { ShowInfo("Selecciona una carta de la lista para duplicarla."); return; }
        LoadIntoForm(_repository.Duplicate(_listedCards[selected[0]]), originalId: null);
    }

    private void OnDeleteClicked()
    {
        var selected = _cardList.GetSelectedItems();
        if (selected.Length == 0) { ShowInfo("Selecciona 1 o mas cartas de la lista para eliminarlas."); return; }

        var cards = selected.Select(i => _listedCards[i]).ToList();
        string names = string.Join(", ", cards.Select(c => c.Name));
        ShowConfirm($"¿Eliminar definitivamente {cards.Count} carta(s)?\n\n{names}\n\nEsto borra el archivo del disco y no se puede deshacer.", () =>
        {
            foreach (var c in cards) _repository.Delete(c.Id);
            RefreshList();
            LoadIntoForm(_repository.NewBlankCard("Monster"), originalId: null);
        });
    }

    private void OnReloadClicked()
    {
        ShowConfirm("Recargar del disco descarta cualquier cambio sin guardar en la carta actual. ¿Continuar?", () =>
        {
            _repository.Reload();
            RefreshList();
            LoadIntoForm(_repository.NewBlankCard("Monster"), originalId: null);
        });
    }

    private void OnTypeEditorClicked()
    {
        var window = new TypeEditorWindow(_dbPath);
        AddChild(window);
        window.Closed += () =>
        {
            _typeRepo.Reload();
            _monsterPanel.RefreshTypeCombo();
            _spellPanel.RefreshTypes();
            _effectComposerPanel.RefreshTypes();
            window.QueueFree();
        };
        window.PopupCentered();
    }

    private void OnFieldTypeEditorClicked()
    {
        var window = new FieldTypeEditorWindow(_dbPath);
        AddChild(window);
        window.Closed += () =>
        {
            _fieldTypeRepo.Reload();
            _spellPanel.RefreshFieldTypeCombo();
            window.QueueFree();
        };
        window.PopupCentered();
    }

    private void OnDeckEditorClicked()
    {
        var window = new DeckEditorWindow(_dbPath, _repository);
        AddChild(window);
        window.Closed += window.QueueFree;
        window.PopupCentered();
    }

    private void OnExportCardClicked()
    {
        var selected = _cardList.GetSelectedItems();
        if (selected.Length == 0) { ShowInfo("Selecciona una carta guardada de la lista para exportarla."); return; }

        var card = _listedCards[selected[0]];
        _saveCardDialog.CurrentFile = $"{card.Id}__{JsonCardWriter.Slugify(card.Name)}.json";
        _saveCardDialog.SetMeta("cardId", card.Id);
        _saveCardDialog.Popup();
    }

    private void OnExportCardFileSelected(string path)
    {
        int cardId = (int)_saveCardDialog.GetMeta("cardId");
        var card = _repository.Cards.FirstOrDefault(c => c.Id == cardId);
        if (card == null) return;
        PackService.ExportCard(card, path);
        ShowInfo("Carta exportada.");
    }

    private void OnImportCardClicked() => _openCardDialog.Popup();

    private void OnImportCardFileSelected(string path)
    {
        CardDto imported;
        try { imported = PackService.ImportCard(path); }
        catch (Exception ex) { ShowInfo($"No se pudo leer el archivo:\n{ex.Message}"); return; }

        if (_repository.Cards.Any(c => c.Id == imported.Id))
            imported.Id = _repository.NextId();

        // Se carga en el formulario para revision, sin guardar todavia: el
        // usuario confirma con Guardar (nunca se sobrescribe en silencio).
        LoadIntoForm(imported, originalId: null);
        ShowInfo("Carta importada al formulario. Revisala y pulsa Guardar para agregarla al catalogo.");
    }

    private void OnExportPackClicked()
    {
        var selected = _cardList.GetSelectedItems();
        if (selected.Length == 0) { ShowInfo("Selecciona 1 o mas cartas de la lista (Ctrl+clic) para armar el paquete."); return; }

        _packNamePrompt.RequestName("Mi Paquete", packName =>
        {
            if (packName == null) return;
            _savePackDialog.CurrentFile = JsonCardWriter.Slugify(packName) + ".zip";
            _savePackDialog.SetMeta("packName", packName);
            _savePackDialog.Popup();
        });
    }

    private void OnExportPackFileSelected(string path)
    {
        string packName = _savePackDialog.GetMeta("packName").AsString();
        var selected = _cardList.GetSelectedItems().Select(i => _listedCards[i]).ToList();
        PackService.ExportPack(selected, packName, System.Environment.UserName, path);
        ShowInfo($"Paquete exportado con {selected.Count} carta(s).");
    }

    private void OnImportPackClicked() => _openPackDialog.Popup();

    private void OnImportPackFileSelected(string path)
    {
        List<CardDto> imported;
        try { imported = PackService.ImportPack(path); }
        catch (Exception ex) { ShowInfo($"No se pudo leer el paquete:\n{ex.Message}"); return; }

        int saved = 0;
        var skipped = new List<string>();
        foreach (var dto in imported)
        {
            if (_repository.Cards.Any(c => c.Id == dto.Id))
            {
                skipped.Add($"{dto.Name} (Id {dto.Id} ya existe)");
                continue;
            }
            var errors = _repository.Save(dto, originalId: null);
            if (errors.Count > 0)
            {
                skipped.Add($"{dto.Name}: {string.Join("; ", errors)}");
                continue;
            }
            saved++;
        }

        RefreshList();
        string summary = $"Importadas {saved} de {imported.Count} carta(s).";
        if (skipped.Count > 0)
            summary += "\n\nOmitidas (nunca se sobrescribe sin confirmar):\n" + string.Join("\n", skipped);
        ShowInfo(summary);
    }

    private static string SelectedItemText(OptionButton combo, string fallback) =>
        combo.Selected >= 0 && combo.ItemCount > 0 ? combo.GetItemText(combo.Selected) : fallback;

    private static void SelectComboText(OptionButton combo, string text)
    {
        for (int i = 0; i < combo.ItemCount; i++)
            if (combo.GetItemText(i) == text) { combo.Selected = i; return; }
        combo.Selected = combo.ItemCount > 0 ? 0 : -1;
    }
}
