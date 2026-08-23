using MonstersGame.Core.Entities;
using MonstersGame.Data.Loaders;

namespace MonstersGame.CardEditor;

/// <summary>
/// Ventana unica del editor: lista de cartas | previsualizacion | propiedades
/// (seccion 8-10 del brief de adaptacion). El panel de propiedades es
/// dinamico segun Kind/SubType, y el boton Guardar se deshabilita mientras
/// haya un error bloqueante — el editor nunca escribe una carta invalida.
/// </summary>
public sealed class MainForm : Form
{
    private readonly CardRepository _repository;
    private readonly TypeRepository _typeRepo;
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

    // Monstruo.
    private readonly GroupBox _monsterGroup = new() { Text = "Monstruo" };
    private readonly NumericUpDown _attackBox = new() { Minimum = 0, Maximum = 99999 };
    private readonly NumericUpDown _defenseBox = new() { Minimum = 0, Maximum = 99999 };
    private readonly NumericUpDown _levelBox = new() { Minimum = 1, Maximum = 12 };
    private readonly ComboBox _typeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _typeEditorButton = new() { Text = "..." };
    private readonly ComboBox _attributeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _categoryCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _monsterEffectCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    // Magia.
    private readonly GroupBox _spellGroup = new() { Text = "Magia" };
    private readonly ComboBox _spellSubTypeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _spellEffectCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _ritualLabel = new() { Text = "Solo para Magias de Ritual:", AutoSize = true };
    private readonly NumericUpDown _ritualMonsterIdBox = new() { Minimum = 0, Maximum = 999999 };
    private readonly NumericUpDown _requiredRitualLevelBox = new() { Minimum = 0, Maximum = 99 };

    // Trampa.
    private readonly GroupBox _trapGroup = new() { Text = "Trampa" };
    private readonly ComboBox _trapSubTypeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _trapEffectCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };

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
        _preview.ArtRoot = Path.Combine(Path.GetDirectoryName(dbPath)!, "Art");

        Text = "MonstersGame — Editor de Cartas";
        Width = 1240;
        Height = 800;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1040, 680);

        BuildLayout(dbPath);
        WireEvents();

        _kindFilter.Items.AddRange(new object[] { "Todas", "Monster", "Spell", "Trap" });
        _kindFilter.SelectedIndex = 0;
        _kindCombo.Items.AddRange(new object[] { "Monster", "Spell", "Trap" });
        RefreshTypeCombo();
        _attributeCombo.Items.AddRange(Enum.GetNames<MonsterAttribute>());
        _categoryCombo.Items.AddRange(Enum.GetNames<MonsterCategory>());
        _spellSubTypeCombo.Items.AddRange(Enum.GetNames<SpellSubType>());
        _trapSubTypeCombo.Items.AddRange(Enum.GetNames<TrapSubType>());
        _monsterEffectCombo.Items.AddRange(EffectCatalog.Options.Cast<object>().ToArray());
        _spellEffectCombo.Items.AddRange(EffectCatalog.Options.Cast<object>().ToArray());
        _trapEffectCombo.Items.AddRange(EffectCatalog.Options.Cast<object>().ToArray());

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

        var layout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(layout, "Tipo de carta", _kindCombo);
        AddRow(layout, "Id", _idBox);
        AddRow(layout, "Nombre", _nameBox);
        AddRow(layout, "Imagen", BuildImageRow());
        AddRow(layout, "Descripcion", _descriptionBox);

        BuildMonsterGroup();
        BuildSpellGroup();
        BuildTrapGroup();

        _saveButton.Width = 150;
        _saveButton.Height = 32;

        var stack = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        stack.Controls.Add(layout);
        stack.Controls.Add(_monsterGroup);
        stack.Controls.Add(_spellGroup);
        stack.Controls.Add(_trapGroup);
        stack.Controls.Add(new Label { Text = "Validacion:", AutoSize = true, Margin = new Padding(0, 10, 0, 2) });
        stack.Controls.Add(_errorsList);
        _errorsList.Width = 560;
        stack.Controls.Add(_saveButton);

        foreach (Control c in new Control[] { _kindCombo, _idBox, _nameBox, _imageBox, _descriptionBox })
            c.Width = 420;

        outer.Controls.Add(stack);
        return outer;
    }

    /// <summary>
    /// Tabla de 2 columnas (etiqueta fija + valor a lo ancho) para una fila de
    /// propiedades, con las opciones que la hacen expandirse correctamente
    /// dentro de un GroupBox acoplado con <c>Dock = DockStyle.Top</c> (ver
    /// <see cref="SetupKindGroup"/>): sin esto, una columna "Percent" dentro
    /// de un contenedor con AutoSize se colapsa a un ancho casi nulo, en vez
    /// de expandirse — es el bug que reportaste (bloques vacios o datos
    /// amontonados en una columna angosta).
    /// </summary>
    private static TableLayoutPanel BuildTwoColumnLayout()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return layout;
    }

    /// <summary>
    /// Acopla un GroupBox de propiedades (Monstruo/Magia/Trampa) con
    /// <c>Dock = DockStyle.Top</c> para que ocupe el ancho completo del panel
    /// de propiedades (igual que la tabla de campos comunes de arriba) y
    /// <c>AutoSize</c> para que su alto se ajuste al contenido — sin esto el
    /// GroupBox queda con <c>Dock = None</c> (el valor por defecto) y su
    /// tamano depende de un calculo circular con la columna "Percent" de su
    /// tabla interna, que se resuelve a un ancho casi nulo.
    /// </summary>
    private static void SetupKindGroup(GroupBox group)
    {
        group.Dock = DockStyle.Top;
        group.AutoSize = true;
        group.AutoSizeMode = AutoSizeMode.GrowAndShrink;
    }

    private void BuildMonsterGroup()
    {
        var layout = BuildTwoColumnLayout();
        AddRow(layout, "ATK", _attackBox);
        AddRow(layout, "DEF", _defenseBox);
        AddRow(layout, "Nivel", _levelBox);
        AddRow(layout, "Tipo", BuildTypeRow());
        AddRow(layout, "Atributo", _attributeCombo);
        AddRow(layout, "Categoria", _categoryCombo);
        AddRow(layout, "Efecto (Volteo)", _monsterEffectCombo);
        _monsterGroup.Controls.Add(layout);
        SetupKindGroup(_monsterGroup);
    }

    private void BuildSpellGroup()
    {
        var commonLayout = BuildTwoColumnLayout();
        AddRow(commonLayout, "SubType", _spellSubTypeCombo);
        AddRow(commonLayout, "Efecto", _spellEffectCombo);

        var ritualLayout = BuildTwoColumnLayout();
        AddRow(ritualLayout, "Id Monstruo Ritual", _ritualMonsterIdBox);
        AddRow(ritualLayout, "Nivel Sacrificio", _requiredRitualLevelBox);

        _ritualLabel.Dock = DockStyle.Top;
        _ritualLabel.Margin = new Padding(3, 10, 3, 4);

        // Con Dock = DockStyle.Top, WinForms acopla el ULTIMO control
        // agregado mas cerca del borde (aqui, arriba) — el orden de
        // Controls.Add es el orden visual de abajo hacia arriba. Por eso se
        // agregan en el orden inverso al que se ven: primero lo que va mas
        // abajo (los campos de Ritual), al final lo que va mas arriba (los
        // campos comunes a toda Magia).
        _spellGroup.Controls.Add(ritualLayout);
        _spellGroup.Controls.Add(_ritualLabel);
        _spellGroup.Controls.Add(commonLayout);
        SetupKindGroup(_spellGroup);
    }

    private void BuildTrapGroup()
    {
        var layout = BuildTwoColumnLayout();
        AddRow(layout, "SubType", _trapSubTypeCombo);
        AddRow(layout, "Efecto", _trapEffectCombo);
        _trapGroup.Controls.Add(layout);
        SetupKindGroup(_trapGroup);
    }

    /// <summary>
    /// Campo Imagen + boton "Examinar..." para elegir un archivo con el
    /// dialogo de Abrir en vez de escribir la ruta a mano. Es solo un dato
    /// del catalogo: ni el juego ni la vista previa de este editor dibujan
    /// esta imagen todavia (ver seccion "Recursos graficos" del README —
    /// el arte es procedural desde el Bloque 10).
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

    /// <summary>
    /// Combo de Tipo + boton "..." que abre el mantenimiento de Tipos de
    /// Monstruo (Bloque 13). El Tipo dejo de ser una lista fija del codigo:
    /// se administra desde <see cref="TypeEditorForm"/> y vive en la tabla
    /// <c>Types</c> de la base.
    /// </summary>
    private Control BuildTypeRow()
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _typeCombo.Dock = DockStyle.Fill;
        _typeCombo.Margin = new Padding(0);
        row.Controls.Add(_typeCombo, 0, 0);

        _typeEditorButton.Width = 32;
        _typeEditorButton.Margin = new Padding(4, 0, 0, 0);
        row.Controls.Add(_typeEditorButton, 1, 0);

        return row;
    }

    private static void AddRow(TableLayoutPanel layout, string label, Control control)
    {
        int row = layout.RowCount;
        layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) }, 0, row);
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        control.Margin = new Padding(3, 4, 3, 4);
        layout.Controls.Add(control, 1, row);
    }

    // -------------------------------------------------------------- Eventos

    private void WireEvents()
    {
        _searchBox.TextChanged += (_, _) => RefreshList();
        _kindFilter.SelectedIndexChanged += (_, _) => RefreshList();
        _cardList.SelectedIndexChanged += (_, _) => OnListSelectionChanged();

        _kindCombo.SelectedIndexChanged += (_, _) => { UpdateKindPanels(); OnFormFieldChanged(); };
        foreach (var c in new Control[] { _idBox, _nameBox, _imageBox, _descriptionBox })
            c.TextChanged += (_, _) => OnFormFieldChanged();
        _browseImageButton.Click += (_, _) => OnBrowseImageClicked();
        _typeEditorButton.Click += (_, _) => OnTypeEditorClicked();

        _attackBox.ValueChanged += (_, _) => OnFormFieldChanged();
        _defenseBox.ValueChanged += (_, _) => OnFormFieldChanged();
        _levelBox.ValueChanged += (_, _) => OnFormFieldChanged();
        _typeCombo.SelectedIndexChanged += (_, _) => OnFormFieldChanged();
        _attributeCombo.SelectedIndexChanged += (_, _) => OnFormFieldChanged();
        _categoryCombo.SelectedIndexChanged += (_, _) => OnFormFieldChanged();
        _monsterEffectCombo.SelectedIndexChanged += (_, _) => OnFormFieldChanged();

        _spellSubTypeCombo.SelectedIndexChanged += (_, _) => { UpdateRitualFieldsEnabled(); OnFormFieldChanged(); };
        _spellEffectCombo.SelectedIndexChanged += (_, _) => OnFormFieldChanged();
        _ritualMonsterIdBox.ValueChanged += (_, _) => OnFormFieldChanged();
        _requiredRitualLevelBox.ValueChanged += (_, _) => OnFormFieldChanged();

        _trapSubTypeCombo.SelectedIndexChanged += (_, _) => OnFormFieldChanged();
        _trapEffectCombo.SelectedIndexChanged += (_, _) => OnFormFieldChanged();

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
            LoadIntoForm(Clone(item.Dto), originalId: item.Dto.Id);
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

        SelectComboText(_kindCombo, dto.Kind);
        _idBox.Text = dto.Id.ToString();
        _idBox.Enabled = originalId == null; // Id inmutable una vez guardada.
        _nameBox.Text = dto.Name;
        _imageBox.Text = dto.Image;
        _descriptionBox.Text = dto.Description;

        _attackBox.Value = Clamp(dto.Attack, _attackBox);
        _defenseBox.Value = Clamp(dto.Defense, _defenseBox);
        _levelBox.Value = Clamp(dto.Level == 0 ? 1 : dto.Level, _levelBox);
        SelectComboText(_typeCombo, dto.Type);
        SelectComboText(_attributeCombo, dto.Attribute);
        SelectComboText(_categoryCombo, dto.Category);
        SelectEffectOption(_monsterEffectCombo, dto.EffectId);

        SelectComboText(_spellSubTypeCombo, dto.SubType);
        SelectEffectOption(_spellEffectCombo, dto.EffectId);
        _ritualMonsterIdBox.Value = Clamp(dto.RitualMonsterId, _ritualMonsterIdBox);
        _requiredRitualLevelBox.Value = Clamp(dto.RequiredRitualLevel, _requiredRitualLevelBox);

        SelectComboText(_trapSubTypeCombo, dto.SubType);
        SelectEffectOption(_trapEffectCombo, dto.EffectId);

        _suppressEvents = false;

        UpdateKindPanels();
        UpdateRitualFieldsEnabled();
        OnFormFieldChanged();
    }

    private void UpdateKindPanels()
    {
        string kind = _kindCombo.SelectedItem as string ?? "Monster";
        _monsterGroup.Visible = kind == "Monster";
        _spellGroup.Visible = kind == "Spell";
        _trapGroup.Visible = kind == "Trap";
    }

    private void UpdateRitualFieldsEnabled()
    {
        bool isRitual = (_spellSubTypeCombo.SelectedItem as string) == nameof(SpellSubType.Ritual);
        _ritualMonsterIdBox.Enabled = isRitual;
        _requiredRitualLevelBox.Enabled = isRitual;
        // Una Magia de Ritual no usa EffectId: su "efecto" es la Invocacion
        // Ritual en si (DuelEngine.RitualSummon), no algo de EffectRegistry.
        _spellEffectCombo.Enabled = !isRitual;
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
            Description = _descriptionBox.Text,
            Attack = (int)_attackBox.Value,
            Defense = (int)_defenseBox.Value,
            Level = (int)_levelBox.Value,
            Type = _typeCombo.SelectedItem as string ?? "Unknown",
            Attribute = _attributeCombo.SelectedItem as string ?? "Dark",
            Category = _categoryCombo.SelectedItem as string ?? "Normal"
        };

        switch (kind)
        {
            case "Spell":
                dto.SubType = _spellSubTypeCombo.SelectedItem as string ?? "Normal";
                dto.EffectId = SelectedEffectId(_spellEffectCombo);
                dto.RitualMonsterId = (int)_ritualMonsterIdBox.Value;
                dto.RequiredRitualLevel = (int)_requiredRitualLevelBox.Value;
                break;
            case "Trap":
                dto.SubType = _trapSubTypeCombo.SelectedItem as string ?? "Normal";
                dto.EffectId = SelectedEffectId(_trapEffectCombo);
                break;
            default:
                dto.EffectId = SelectedEffectId(_monsterEffectCombo);
                break;
        }

        return dto;
    }

    private void OnFormFieldChanged()
    {
        if (_suppressEvents) return;

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
    /// completa de esta maquina) para que el dato siga siendo portable si el
    /// catalogo se comparte o se abre desde otra computadora.
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
        RefreshTypeCombo();
    }

    /// <summary>Repuebla el combo de Tipo desde <see cref="_typeRepo"/>, preservando la seleccion actual si el Tipo sigue existiendo.</summary>
    private void RefreshTypeCombo()
    {
        string? current = _typeCombo.SelectedItem as string;

        _typeCombo.Items.Clear();
        _typeCombo.Items.AddRange(_typeRepo.Types.Cast<object>().ToArray());

        if (current != null && _typeCombo.Items.Contains(current))
            _typeCombo.SelectedItem = current;
        else if (_typeCombo.Items.Count > 0)
            _typeCombo.SelectedIndex = 0;
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

    // ---------------------------------------------------------------- Utiles

    private static void SelectComboText(ComboBox combo, string text)
    {
        int idx = combo.Items.IndexOf(text);
        combo.SelectedIndex = idx >= 0 ? idx : (combo.Items.Count > 0 ? 0 : -1);
    }

    private static void SelectEffectOption(ComboBox combo, string effectId)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is EffectOption opt && opt.Id == effectId)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        combo.SelectedIndex = combo.Items.Count > 0 ? 0 : -1; // "(Sin efecto)" es siempre el primero.
    }

    private static string SelectedEffectId(ComboBox combo) =>
        combo.SelectedItem is EffectOption opt ? opt.Id : string.Empty;

    private static decimal Clamp(int value, NumericUpDown box) =>
        Math.Max(box.Minimum, Math.Min(box.Maximum, value));

    private static CardDto Clone(CardDto d) => new()
    {
        Id = d.Id,
        Kind = d.Kind,
        Name = d.Name,
        Attack = d.Attack,
        Defense = d.Defense,
        Level = d.Level,
        Type = d.Type,
        Attribute = d.Attribute,
        Image = d.Image,
        Description = d.Description,
        EffectId = d.EffectId,
        SubType = d.SubType,
        Category = d.Category,
        RitualMonsterId = d.RitualMonsterId,
        RequiredRitualLevel = d.RequiredRitualLevel
    };

    private sealed class CardListItem
    {
        public CardDto Dto { get; }
        public CardListItem(CardDto dto) => Dto = dto;
        public override string ToString() => $"[{Dto.Id}] {Dto.Name} ({Dto.Kind})";
    }
}
