using MonstersGame.Data.Loaders;

namespace MonstersGame.CardEditor;

/// <summary>
/// Ventana unica del editor: lista de cartas | previsualizacion | propiedades.
/// El panel de propiedades es dinamico segun Kind/Category/SubType (Fase 5):
/// Fusion muestra sus huecos de material, Ritual su filtro de Sacrificio,
/// Equip sus objetivos/modificadores/duracion, Field su tipo de Campo, y
/// cualquier carta con efecto puede componer uno nuevo a partir de acciones
/// ya existentes sin tocar codigo. El boton Guardar se deshabilita mientras
/// haya un error bloqueante — el editor nunca escribe una carta invalida.
///
/// Orquestador delgado (Fase 5, paso de refactor puro): las propiedades
/// especificas de cada Kind viven en <see cref="MonsterPanel"/>/<see cref="SpellPanel"/>/
/// <see cref="TrapPanel"/>, y el compositor de efectos compartido en
/// <see cref="EffectComposerPanel"/>. Este formulario solo decide cual esta
/// visible y junta sus <c>LoadFrom</c>/<c>ApplyTo</c> con los campos comunes
/// (Id/Nombre/Imagen/Descripcion).
/// </summary>
public sealed class MainForm : Form
{
    private readonly CardRepository _repository;
    private readonly TypeRepository _typeRepo;
    private readonly FieldTypeRepository _fieldTypeRepo;
    private readonly string _dbPath;

    // Lista y filtros.
    private readonly TextBox _searchBox = new() { PlaceholderText = "Buscar por nombre..." };
    private readonly ComboBox _kindFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ListBox _cardList = new() { SelectionMode = SelectionMode.MultiExtended };

    // Previsualizacion.
    private readonly CardPreviewControl _preview = new();

    // Comunes.
    private readonly ComboBox _kindCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _idBox = new();
    private readonly TextBox _nameBox = new();
    private readonly TextBox _descriptionBox = new() { Multiline = true, Height = 70, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox _imageBox = new();
    private readonly Button _browseImageButton = new() { Text = "Examinar..." };

    // Paneles por Kind + compositor de efectos compartido.
    private readonly MonsterPanel _monsterPanel;
    private readonly SpellPanel _spellPanel;
    private readonly TrapPanel _trapPanel;
    private readonly EffectComposerPanel _effectComposerPanel;

    // Validacion y acciones.
    private readonly ListBox _errorsList = new() { Height = 90, BackColor = Color.FromArgb(40, 20, 20), ForeColor = Color.FromArgb(255, 150, 150) };
    private readonly Button _saveButton = new() { Text = "Guardar" };
    private readonly Button _newButton = new() { Text = "Nueva" };
    private readonly Button _duplicateButton = new() { Text = "Duplicar" };
    private readonly Button _deleteButton = new() { Text = "Eliminar" };
    private readonly Button _reloadButton = new() { Text = "Recargar del disco" };
    private readonly Button _exportCardButton = new() { Text = "Exportar carta..." };
    private readonly Button _importCardButton = new() { Text = "Importar carta..." };
    private readonly Button _exportPackButton = new() { Text = "Exportar paquete..." };
    private readonly Button _importPackButton = new() { Text = "Importar paquete..." };
    private readonly Button _deckEditorButton = new() { Text = "Editor de Mazos..." };

    private int? _editingOriginalId; // null = carta nueva, todavia no guardada
    private bool _suppressEvents;

    public MainForm(string dbPath)
    {
        _dbPath = dbPath;
        _repository = new CardRepository(dbPath);
        _typeRepo = new TypeRepository(dbPath);
        _fieldTypeRepo = new FieldTypeRepository(dbPath);
        _preview.ArtRoot = Path.Combine(Path.GetDirectoryName(dbPath)!, "Art");

        _monsterPanel = new MonsterPanel(_typeRepo);
        _spellPanel = new SpellPanel(_fieldTypeRepo, _typeRepo);
        _trapPanel = new TrapPanel();
        _effectComposerPanel = new EffectComposerPanel(_typeRepo);

        Text = "MonstersGame — Editor de Cartas";
        Width = 1280;
        Height = 860;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1080, 720);

        BuildLayout(dbPath);
        WireEvents();

        _kindFilter.Items.AddRange(new object[] { "Todas", "Monster", "Spell", "Trap" });
        _kindFilter.SelectedIndex = 0;
        _kindCombo.Items.AddRange(new object[] { "Monster", "Spell", "Trap" });

        RefreshList();
        LoadIntoForm(_repository.NewBlankCard("Monster"), originalId: null);
    }

    // ------------------------------------------------------------- Layout

    private void BuildLayout(string dbPath)
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        root.Controls.Add(BuildListPanel(dbPath), 0, 0);
        root.Controls.Add(BuildPreviewPanel(), 1, 0);
        root.Controls.Add(BuildPropertiesPanel(), 2, 0);
    }

    private Control BuildListPanel(string dbPath)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

        var top = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, AutoSize = true };
        var pathLabel = new Label
        {
            Text = "Catalogo:\n" + dbPath,
            AutoSize = false,
            Height = 40,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 7.5f)
        };
        _searchBox.Dock = DockStyle.Top;
        _kindFilter.Dock = DockStyle.Top;
        top.Controls.Add(pathLabel);
        top.Controls.Add(_kindFilter);
        top.Controls.Add(_searchBox);

        _cardList.Dock = DockStyle.Fill;
        _cardList.IntegralHeight = false;

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.TopDown };
        foreach (var b in new[] { _newButton, _duplicateButton, _deleteButton, _reloadButton })
        {
            b.Width = 230;
            buttons.Controls.Add(b);
        }

        var packButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.TopDown };
        foreach (var b in new[] { _exportCardButton, _importCardButton, _exportPackButton, _importPackButton })
        {
            b.Width = 230;
            packButtons.Controls.Add(b);
        }

        var deckButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.TopDown };
        _deckEditorButton.Width = 230;
        deckButtons.Controls.Add(_deckEditorButton);

        panel.Controls.Add(_cardList);
        panel.Controls.Add(top);
        panel.Controls.Add(packButtons);
        panel.Controls.Add(buttons);
        panel.Controls.Add(deckButtons);
        return panel;
    }

    private Control BuildPreviewPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        _preview.Dock = DockStyle.Fill;
        _preview.BorderStyle = BorderStyle.FixedSingle;
        panel.Controls.Add(_preview);
        return panel;
    }

    private Control BuildPropertiesPanel()
    {
        var outer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8), AutoScroll = true };

        var layout = EditorLayout.TwoColumnLayout();
        EditorLayout.AddRow(layout, "Tipo de carta", _kindCombo);
        EditorLayout.AddRow(layout, "Id", _idBox);
        EditorLayout.AddRow(layout, "Nombre", _nameBox);
        EditorLayout.AddRow(layout, "Imagen", BuildImageRow());
        EditorLayout.AddRow(layout, "Descripcion", _descriptionBox);

        _saveButton.Width = 150;
        _saveButton.Height = 32;

        var stack = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        stack.Controls.Add(layout);
        stack.Controls.Add(_monsterPanel);
        stack.Controls.Add(_spellPanel);
        stack.Controls.Add(_trapPanel);
        stack.Controls.Add(_effectComposerPanel);
        stack.Controls.Add(new Label { Text = "Validacion:", AutoSize = true, Margin = new Padding(0, 10, 0, 2) });
        stack.Controls.Add(_errorsList);
        _errorsList.Width = 620;
        stack.Controls.Add(_saveButton);

        foreach (Control c in new Control[] { _kindCombo, _idBox, _nameBox, _imageBox, _descriptionBox })
            c.Width = 460;
        _monsterPanel.Width = 620;
        _spellPanel.Width = 620;
        _trapPanel.Width = 620;
        _effectComposerPanel.Width = 620;

        outer.Controls.Add(stack);
        return outer;
    }

    /// <summary>
    /// Campo Imagen + boton "Examinar..." para elegir un archivo con el
    /// dialogo de Abrir en vez de escribir la ruta a mano.
    /// </summary>
    private Control BuildImageRow()
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _imageBox.Dock = DockStyle.Fill;
        _imageBox.Margin = new Padding(0);
        row.Controls.Add(_imageBox, 0, 0);

        _browseImageButton.Width = 90;
        _browseImageButton.Margin = new Padding(4, 0, 0, 0);
        row.Controls.Add(_browseImageButton, 1, 0);

        return row;
    }

    // -------------------------------------------------------------- Eventos

    private void WireEvents()
    {
        _searchBox.TextChanged += (_, _) => RefreshList();
        _kindFilter.SelectedIndexChanged += (_, _) => RefreshList();
        _cardList.SelectedIndexChanged += (_, _) => OnListSelectionChanged();

        _kindCombo.SelectedIndexChanged += (_, _) => { UpdateActivePanel(); OnFormFieldChanged(); };
        foreach (var c in new Control[] { _idBox, _nameBox, _imageBox, _descriptionBox })
            c.TextChanged += (_, _) => OnFormFieldChanged();
        _browseImageButton.Click += (_, _) => OnBrowseImageClicked();

        _monsterPanel.Changed += (_, _) => { UpdateActivePanel(); OnFormFieldChanged(); };
        _monsterPanel.TypeEditorRequested += (_, _) => OnTypeEditorClicked();
        _spellPanel.Changed += (_, _) => { UpdateActivePanel(); OnFormFieldChanged(); };
        _spellPanel.FieldTypeEditorRequested += (_, _) => OnFieldTypeEditorClicked();
        _trapPanel.Changed += (_, _) => { UpdateActivePanel(); OnFormFieldChanged(); };
        _effectComposerPanel.Changed += (_, _) => OnFormFieldChanged();

        _newButton.Click += (_, _) => LoadIntoForm(_repository.NewBlankCard("Monster"), originalId: null);
        _duplicateButton.Click += (_, _) => OnDuplicateClicked();
        _deleteButton.Click += (_, _) => OnDeleteClicked();
        _reloadButton.Click += (_, _) => OnReloadClicked();
        _saveButton.Click += (_, _) => OnSaveClicked();
        _exportCardButton.Click += (_, _) => OnExportCardClicked();
        _importCardButton.Click += (_, _) => OnImportCardClicked();
        _exportPackButton.Click += (_, _) => OnExportPackClicked();
        _importPackButton.Click += (_, _) => OnImportPackClicked();
        _deckEditorButton.Click += (_, _) => OnDeckEditorClicked();
    }

    // --------------------------------------------------------- Lista/filtros

    private void RefreshList()
    {
        string search = _searchBox.Text.Trim();
        string kindFilter = _kindFilter.SelectedItem as string ?? "Todas";

        var filtered = _repository.Cards
            .Where(c => kindFilter == "Todas" || c.Kind.Equals(kindFilter, StringComparison.OrdinalIgnoreCase))
            .Where(c => search.Length == 0 || c.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Id)
            .ToList();

        _cardList.BeginUpdate();
        _cardList.Items.Clear();
        foreach (var c in filtered)
            _cardList.Items.Add(new CardListItem(c));
        _cardList.EndUpdate();
    }

    private void OnListSelectionChanged()
    {
        if (_cardList.SelectedItems.Count == 1 && _cardList.SelectedItem is CardListItem item)
            LoadIntoForm(CardDtoCloning.Clone(item.Dto), originalId: item.Dto.Id);
    }

    private void SelectInList(int id)
    {
        for (int i = 0; i < _cardList.Items.Count; i++)
            if (_cardList.Items[i] is CardListItem item && item.Dto.Id == id)
            {
                _cardList.SelectedIndex = i;
                return;
            }
    }

    // ------------------------------------------------------------ Formulario

    private void LoadIntoForm(CardDto dto, int? originalId)
    {
        _suppressEvents = true;
        _editingOriginalId = originalId;

        EditorHelpers.SelectComboText(_kindCombo, dto.Kind);
        _idBox.Text = dto.Id.ToString();
        _idBox.Enabled = originalId == null; // Id inmutable una vez guardada.
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
        string kind = _kindCombo.SelectedItem as string ?? "Monster";
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
        string kind = _kindCombo.SelectedItem as string ?? "Monster";
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
        _errorsList.Items.Clear();
        foreach (var e in errors) _errorsList.Items.Add(e);
        _saveButton.Enabled = errors.Count == 0;
    }

    // -------------------------------------------------------------- Acciones

    private void OnSaveClicked()
    {
        var dto = BuildDtoFromForm();
        var errors = _repository.Save(dto, _editingOriginalId);
        if (errors.Count > 0)
        {
            _errorsList.Items.Clear();
            foreach (var e in errors) _errorsList.Items.Add(e);
            _saveButton.Enabled = false;
            return;
        }

        _editingOriginalId = dto.Id;
        _idBox.Enabled = false;
        RefreshList();
        SelectInList(dto.Id);
        MessageBox.Show(this, $"\"{dto.Name}\" guardada.", "MonstersGame — Editor de Cartas",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnDuplicateClicked()
    {
        if (_cardList.SelectedItem is not CardListItem item)
        {
            MessageBox.Show(this, "Selecciona una carta de la lista para duplicarla.", "MonstersGame — Editor de Cartas");
            return;
        }
        LoadIntoForm(_repository.Duplicate(item.Dto), originalId: null);
    }

    private void OnDeleteClicked()
    {
        var selected = _cardList.SelectedItems.Cast<CardListItem>().ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "Selecciona 1 o mas cartas de la lista para eliminarlas.", "MonstersGame — Editor de Cartas");
            return;
        }

        string names = string.Join(", ", selected.Select(s => s.Dto.Name));
        var confirm = MessageBox.Show(this,
            $"¿Eliminar definitivamente {selected.Count} carta(s)?\n\n{names}\n\nEsto borra el archivo del disco y no se puede deshacer.",
            "Confirmar eliminacion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes) return;

        foreach (var item in selected)
            _repository.Delete(item.Dto.Id);

        RefreshList();
        LoadIntoForm(_repository.NewBlankCard("Monster"), originalId: null);
    }

    private void OnReloadClicked()
    {
        var confirm = MessageBox.Show(this,
            "Recargar del disco descarta cualquier cambio sin guardar en la carta actual. ¿Continuar?",
            "Recargar", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        _repository.Reload();
        RefreshList();
        LoadIntoForm(_repository.NewBlankCard("Monster"), originalId: null);
    }

    /// <summary>
    /// Abre el dialogo de Abrir Archivo para elegir una imagen en vez de
    /// escribir la ruta a mano. Guarda solo el nombre de archivo (no la ruta
    /// completa de esta maquina) para que el dato siga siendo portable.
    /// </summary>
    private void OnBrowseImageClicked()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Imagenes (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Todos los archivos (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _imageBox.Text = Path.GetFileName(dialog.FileName);
    }

    /// <summary>
    /// Abre el mantenimiento de Tipos de Monstruo como ventana modal. Al
    /// cerrarla, se refresca el combo de Tipo por si se agrego/renombro/
    /// elimino algo mientras estaba abierta.
    /// </summary>
    private void OnTypeEditorClicked()
    {
        using var form = new TypeEditorForm(_dbPath);
        form.ShowDialog(this);
        _typeRepo.Reload();
        _monsterPanel.RefreshTypeCombo();
        _spellPanel.RefreshTypes();
        _effectComposerPanel.RefreshTypes();
    }

    /// <summary>Abre el mantenimiento de tipos de Campo. Al cerrarla, se refresca el combo por si cambio algo.</summary>
    private void OnFieldTypeEditorClicked()
    {
        using var form = new FieldTypeEditorForm(_dbPath);
        form.ShowDialog(this);
        _fieldTypeRepo.Reload();
        _spellPanel.RefreshFieldTypeCombo();
    }

    /// <summary>
    /// Abre el Editor de Mazos como ventana modal: comparte <see cref="_repository"/>
    /// (para el catalogo de cartas disponibles) pero tiene su propio
    /// <see cref="DeckRepository"/> — mazos y cartas son catalogos independientes.
    /// </summary>
    private void OnDeckEditorClicked()
    {
        using var form = new DeckEditorForm(_dbPath, _repository);
        form.ShowDialog(this);
    }

    private void OnExportCardClicked()
    {
        if (_cardList.SelectedItem is not CardListItem item)
        {
            MessageBox.Show(this, "Selecciona una carta guardada de la lista para exportarla.", "MonstersGame — Editor de Cartas");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Carta MonstersGame (*.json)|*.json",
            FileName = $"{item.Dto.Id}__{JsonCardWriter.Slugify(item.Dto.Name)}.json"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        PackService.ExportCard(item.Dto, dialog.FileName);
        MessageBox.Show(this, "Carta exportada.", "MonstersGame — Editor de Cartas");
    }

    private void OnImportCardClicked()
    {
        using var dialog = new OpenFileDialog { Filter = "Carta MonstersGame (*.json)|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        CardDto imported;
        try { imported = PackService.ImportCard(dialog.FileName); }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"No se pudo leer el archivo:\n{ex.Message}", "MonstersGame — Editor de Cartas",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (_repository.Cards.Any(c => c.Id == imported.Id))
        {
            var choice = MessageBox.Show(this,
                $"Ya existe una carta con Id {imported.Id} en este catalogo.\n\n" +
                "Si: importar con un Id nuevo.   No: cancelar la importacion.",
                "Id duplicado", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (choice != DialogResult.Yes) return;
            imported.Id = _repository.NextId();
        }

        // Se carga en el formulario para revision, sin guardar todavia: el
        // usuario confirma con Guardar (nunca se sobrescribe en silencio).
        LoadIntoForm(imported, originalId: null);
        MessageBox.Show(this, "Carta importada al formulario. Revisala y pulsa Guardar para agregarla al catalogo.",
            "MonstersGame — Editor de Cartas");
    }

    private void OnExportPackClicked()
    {
        var selected = _cardList.SelectedItems.Cast<CardListItem>().Select(i => i.Dto).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "Selecciona 1 o mas cartas de la lista (Ctrl+clic) para armar el paquete.", "MonstersGame — Editor de Cartas");
            return;
        }

        string? packName = PromptText("Nombre del paquete:", "Mi Paquete");
        if (packName == null) return;

        using var dialog = new SaveFileDialog
        {
            Filter = "Paquete MonstersGame (*.zip)|*.zip",
            FileName = JsonCardWriter.Slugify(packName) + ".zip"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        PackService.ExportPack(selected, packName, Environment.UserName, dialog.FileName);
        MessageBox.Show(this, $"Paquete exportado con {selected.Count} carta(s).", "MonstersGame — Editor de Cartas");
    }

    private void OnImportPackClicked()
    {
        using var dialog = new OpenFileDialog { Filter = "Paquete MonstersGame (*.zip)|*.zip" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        List<CardDto> imported;
        try { imported = PackService.ImportPack(dialog.FileName); }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"No se pudo leer el paquete:\n{ex.Message}", "MonstersGame — Editor de Cartas",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

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
        MessageBox.Show(this, summary, "MonstersGame — Editor de Cartas");
    }

    private string? PromptText(string label, string initialValue)
    {
        using var prompt = new Form
        {
            Width = 360,
            Height = 140,
            Text = "MonstersGame — Editor de Cartas",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false
        };
        var lbl = new Label { Left = 12, Top = 12, Width = 320, Text = label };
        var box = new TextBox { Left = 12, Top = 36, Width = 320, Text = initialValue };
        var ok = new Button { Text = "Aceptar", Left = 176, Top = 66, Width = 75, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancelar", Left = 257, Top = 66, Width = 75, DialogResult = DialogResult.Cancel };
        prompt.Controls.AddRange(new Control[] { lbl, box, ok, cancel });
        prompt.AcceptButton = ok;
        prompt.CancelButton = cancel;
        return prompt.ShowDialog(this) == DialogResult.OK ? box.Text : null;
    }

    private sealed class CardListItem
    {
        public CardDto Dto { get; }
        public CardListItem(CardDto dto) => Dto = dto;
        public override string ToString() => $"[{Dto.Id}] {Dto.Name} ({Dto.Kind})";
    }
}
