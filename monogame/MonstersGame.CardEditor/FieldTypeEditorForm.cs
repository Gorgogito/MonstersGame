using MonstersGame.Data.Loaders;

namespace MonstersGame.CardEditor;

/// <summary>
/// Ventana secundaria para administrar el catalogo de tipos de Campo (Fase
/// 4/5): cada uno define su color/fondo de referencia (Fase 6), a que
/// Monstruos afecta y su modificador de ATK/DEF. Cualquier Magia de Campo
/// referencia uno por Id desde <c>MainForm</c>.
/// </summary>
public sealed class FieldTypeEditorForm : Form
{
    private readonly FieldTypeRepository _repo;
    private readonly string _fieldsArtDir;

    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _idBox = new();
    private readonly TextBox _nameBox = new();
    private readonly TextBox _descriptionBox = new();
    private readonly TextBox _colorBox = new();
    private readonly TextBox _backgroundImageBox = new();
    private readonly Button _browseBackgroundImageButton = new() { Text = "Examinar..." };
    private readonly TextBox _visualEffectsKeyBox = new();
    private readonly NumericUpDown _statAmountBox = new() { Minimum = -9999, Maximum = 9999 };
    private readonly ComboBox _statKindCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TargetFilterEditorControl _affectedFilter = new();
    private readonly NumericUpDown _opposedAmountBox = new() { Minimum = -9999, Maximum = 9999 };
    private readonly TargetFilterEditorControl _opposedFilter = new();
    private readonly Label _statusLabel = new() { AutoSize = false, Height = 32, ForeColor = Color.FromArgb(255, 150, 150) };
    private readonly Button _newButton = new() { Text = "Nuevo" };
    private readonly Button _saveButton = new() { Text = "Guardar" };
    private readonly Button _deleteButton = new() { Text = "Eliminar seleccionado" };

    private string? _editingOriginalId;

    public FieldTypeEditorForm(string dbPath)
    {
        _repo = new FieldTypeRepository(dbPath);
        // Mismo directorio que MonstersGame.exe lee en tiempo de ejecucion
        // (ver TextureCache.FieldBackground) -- dbPath es ".../MonstersGame/Data/monstersgame.db".
        _fieldsArtDir = Path.Combine(Path.GetDirectoryName(dbPath)!, "Art", "Fields");
        var availableTypes = new TypeRepository(dbPath).Types;
        _affectedFilter.SetAvailableTypes(availableTypes);
        _opposedFilter.SetAvailableTypes(availableTypes);

        Text = "MonstersGame — Tipos de Campo";
        Width = 760;
        Height = 620;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(640, 520);

        BuildLayout();
        WireEvents();
        _statKindCombo.Items.AddRange(new object[] { "None", "Attack", "Defense", "Both" });

        RefreshList();
        LoadIntoForm(_repo.NewBlank());
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(10) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var listPanel = new Panel { Dock = DockStyle.Fill };
        _list.Dock = DockStyle.Fill;
        listPanel.Controls.Add(_list);
        root.Controls.Add(listPanel, 0, 0);

        var form = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        EditorLayout.AddRow(form, "Id", _idBox);
        EditorLayout.AddRow(form, "Nombre", _nameBox);
        EditorLayout.AddRow(form, "Descripcion", _descriptionBox);
        EditorLayout.AddRow(form, "Color (#RRGGBB)", _colorBox);
        EditorLayout.AddRow(form, "Imagen de fondo", BuildBackgroundImageRow());
        EditorLayout.AddRow(form, "Clave visual (Fase 6)", _visualEffectsKeyBox);
        EditorLayout.AddRow(form, "Modificador", _statAmountBox);
        EditorLayout.AddRow(form, "Aplica a", _statKindCombo);
        EditorLayout.AddRow(form, "Penalizacion (terreno elemental)", _opposedAmountBox);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        foreach (var b in new[] { _newButton, _saveButton, _deleteButton })
        {
            b.Width = 160;
            b.Margin = new Padding(0, 4, 6, 4);
            buttons.Controls.Add(b);
        }

        _statusLabel.Dock = DockStyle.Top;

        var right = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        right.Controls.Add(form);
        right.Controls.Add(new Label { Text = "Monstruos afectados (reciben el Modificador):", AutoSize = true, Margin = new Padding(0, 8, 0, 2) });
        right.Controls.Add(_affectedFilter);
        right.Controls.Add(new Label
        {
            Text = "Monstruos penalizados (reciben la Penalizacion -- ej. el Atributo opuesto; vacio = ninguno):",
            AutoSize = true, Margin = new Padding(0, 8, 0, 2)
        });
        right.Controls.Add(_opposedFilter);
        right.Controls.Add(buttons);
        right.Controls.Add(_statusLabel);
        foreach (Control c in new Control[] { _idBox, _nameBox, _descriptionBox, _colorBox, _visualEffectsKeyBox })
            c.Width = 380;
        _affectedFilter.Width = 500;
        _opposedFilter.Width = 500;
        root.Controls.Add(right, 1, 0);
    }

    /// <summary>
    /// Campo de Imagen de fondo + boton "Examinar..." (mismo patron que
    /// <c>MainForm.BuildImageRow</c> para <c>Card.Image</c>), pero apuntando
    /// el dialogo directo a <c>Data/Art/Fields</c> -- el directorio que
    /// <c>TextureCache.FieldBackground</c> realmente lee en tiempo de
    /// ejecucion, creandolo si todavia no existe para que el dialogo no
    /// falle en una instalacion nueva.
    /// </summary>
    private Control BuildBackgroundImageRow()
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _backgroundImageBox.Dock = DockStyle.Fill;
        _backgroundImageBox.Margin = new Padding(0);
        row.Controls.Add(_backgroundImageBox, 0, 0);

        _browseBackgroundImageButton.Width = 90;
        _browseBackgroundImageButton.Margin = new Padding(4, 0, 0, 0);
        row.Controls.Add(_browseBackgroundImageButton, 1, 0);

        return row;
    }

    private void OnBrowseBackgroundImageClicked()
    {
        Directory.CreateDirectory(_fieldsArtDir);
        using var dialog = new OpenFileDialog
        {
            InitialDirectory = _fieldsArtDir,
            Filter = "Imagenes (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Todos los archivos (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _backgroundImageBox.Text = Path.GetFileName(dialog.FileName);
    }

    private void WireEvents()
    {
        _list.SelectedIndexChanged += (_, _) =>
        {
            if (_list.SelectedItem is FieldTypeDto dto) LoadIntoForm(dto);
        };
        _newButton.Click += (_, _) => LoadIntoForm(_repo.NewBlank());
        _saveButton.Click += (_, _) => OnSaveClicked();
        _deleteButton.Click += (_, _) => OnDeleteClicked();
        _browseBackgroundImageButton.Click += (_, _) => OnBrowseBackgroundImageClicked();
    }

    private void LoadIntoForm(FieldTypeDto dto)
    {
        _editingOriginalId = string.IsNullOrEmpty(dto.Id) ? null : dto.Id;
        _idBox.Text = dto.Id;
        _idBox.Enabled = _editingOriginalId == null;
        _nameBox.Text = dto.Name;
        _descriptionBox.Text = dto.Description;
        _colorBox.Text = dto.Color;
        _backgroundImageBox.Text = dto.BackgroundImage;
        _visualEffectsKeyBox.Text = dto.VisualEffectsKey;
        _statAmountBox.Value = Math.Max(_statAmountBox.Minimum, Math.Min(_statAmountBox.Maximum, dto.StatModifierAmount));
        _statKindCombo.SelectedItem = dto.StatModifierStat;
        if (_statKindCombo.SelectedIndex < 0) _statKindCombo.SelectedIndex = 0;
        _affectedFilter.SetFilter(dto.AffectedFilter);
        _opposedAmountBox.Value = Math.Max(_opposedAmountBox.Minimum, Math.Min(_opposedAmountBox.Maximum, dto.OpposedStatModifierAmount));
        _opposedFilter.SetFilter(dto.OpposedFilter);
    }

    private FieldTypeDto BuildDtoFromForm() => new()
    {
        Id = _idBox.Text.Trim(),
        Name = _nameBox.Text.Trim(),
        Description = _descriptionBox.Text,
        Color = string.IsNullOrWhiteSpace(_colorBox.Text) ? "#FFFFFF" : _colorBox.Text.Trim(),
        BackgroundImage = _backgroundImageBox.Text,
        VisualEffectsKey = _visualEffectsKeyBox.Text,
        StatModifierAmount = (int)_statAmountBox.Value,
        StatModifierStat = _statKindCombo.SelectedItem as string ?? "None",
        AffectedFilter = _affectedFilter.GetFilter(),
        OpposedStatModifierAmount = (int)_opposedAmountBox.Value,
        OpposedFilter = _opposedFilter.GetFilter()
    };

    private void OnSaveClicked()
    {
        var dto = BuildDtoFromForm();
        var error = _repo.Save(dto);
        ShowStatus(error);
        if (error != null) return;

        RefreshList();
        SelectInList(dto.Id);
    }

    private void OnDeleteClicked()
    {
        if (_list.SelectedItem is not FieldTypeDto dto)
        {
            ShowStatus("Selecciona un tipo de Campo de la lista para eliminarlo.");
            return;
        }
        var error = _repo.Delete(dto.Id);
        ShowStatus(error);
        if (error == null)
        {
            RefreshList();
            LoadIntoForm(_repo.NewBlank());
        }
    }

    private void ShowStatus(string? error) => _statusLabel.Text = error ?? "";

    private void RefreshList()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var dto in _repo.FieldTypes) _list.Items.Add(dto);
        _list.EndUpdate();
    }

    private void SelectInList(string id)
    {
        for (int i = 0; i < _list.Items.Count; i++)
            if (_list.Items[i] is FieldTypeDto dto && dto.Id == id) { _list.SelectedIndex = i; return; }
    }
}
