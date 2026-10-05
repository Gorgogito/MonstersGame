using MonstersGame.Core.Entities;
using MonstersGame.Data.Loaders;

namespace MonstersGame.CardEditor;

/// <summary>
/// Panel de propiedades de una carta Monstruo: ATK/DEF/Nivel/Tipo/Atributo/
/// Categoria/Efecto, mas los materiales de Fusion (visibles solo si
/// Categoria = Fusion). Extraido de <c>MainForm</c> (Fase 5, paso de refactor
/// puro): mismo contenido y comportamiento, ahora en su propia clase con
/// <see cref="LoadFrom"/>/<see cref="ApplyTo"/> en vez de estar entrelazado
/// con los otros Kind en un unico formulario de 1000+ lineas.
/// </summary>
public sealed class MonsterPanel : UserControl
{
    private readonly TypeRepository _typeRepo;

    private readonly NumericUpDown _attackBox = new() { Minimum = 0, Maximum = 99999 };
    private readonly NumericUpDown _defenseBox = new() { Minimum = 0, Maximum = 99999 };
    private readonly NumericUpDown _levelBox = new() { Minimum = 1, Maximum = 12 };
    private readonly ComboBox _typeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _typeEditorButton = new() { Text = "..." };
    private readonly ComboBox _attributeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _categoryCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _effectCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    private readonly GroupBox _fusionGroup = new() { Text = "Materiales de Fusion" };
    private readonly ListBox _fusionSlotsList = new() { Height = 70 };
    private readonly TargetFilterEditorControl _fusionSlotFilter = new();
    private readonly NumericUpDown _fusionSlotMin = new() { Minimum = 1, Maximum = 9, Value = 1 };
    private readonly NumericUpDown _fusionSlotMax = new() { Minimum = 1, Maximum = 9, Value = 1 };
    private readonly Button _fusionAddSlotButton = new() { Text = "Agregar hueco" };
    private readonly Button _fusionRemoveSlotButton = new() { Text = "Quitar hueco seleccionado" };
    private List<FusionSlotDto> _currentFusionSlots = new();

    private bool _suppressEvents;

    /// <summary>Se dispara con cualquier cambio del usuario (no durante <see cref="LoadFrom"/>).</summary>
    public event EventHandler? Changed;

    /// <summary>El usuario pidio abrir el mantenimiento de Tipos de Monstruo.</summary>
    public event EventHandler? TypeEditorRequested;

    /// <summary>El EffectId seleccionado en el combo (puede ser el sentinela <see cref="EffectCatalog.ComposeNew"/>).</summary>
    public string SelectedEffectId => EditorHelpers.SelectedEffectId(_effectCombo);

    public MonsterPanel(TypeRepository typeRepo)
    {
        _typeRepo = typeRepo;

        var layout = EditorLayout.TwoColumnLayout();
        EditorLayout.AddRow(layout, "ATK", _attackBox);
        EditorLayout.AddRow(layout, "DEF", _defenseBox);
        EditorLayout.AddRow(layout, "Nivel", _levelBox);
        EditorLayout.AddRow(layout, "Tipo", BuildTypeRow());
        EditorLayout.AddRow(layout, "Atributo", _attributeCombo);
        EditorLayout.AddRow(layout, "Categoria", _categoryCombo);
        EditorLayout.AddRow(layout, "Efecto (Volteo)", _effectCombo);

        BuildFusionGroup();

        Dock = DockStyle.Top;
        AutoSize = true;
        // Orden inverso al visual (Dock = Top acopla el ultimo mas arriba).
        Controls.Add(_fusionGroup);
        Controls.Add(layout);

        _attributeCombo.Items.AddRange(Enum.GetNames<MonsterAttribute>());
        _categoryCombo.Items.AddRange(Enum.GetNames<MonsterCategory>());
        _effectCombo.Items.AddRange(EffectCatalog.Options.Cast<object>().ToArray());
        RefreshTypeCombo();
        _fusionSlotFilter.SetAvailableTypes(_typeRepo.Types);

        WireEvents();
    }

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

    private void BuildFusionGroup()
    {
        var slotLayout = EditorLayout.TwoColumnLayout();
        EditorLayout.AddRow(slotLayout, "Cantidad", BuildMinMaxRow());

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        foreach (var b in new[] { _fusionAddSlotButton, _fusionRemoveSlotButton })
        {
            b.Width = 200;
            b.Margin = new Padding(0, 4, 6, 4);
            buttons.Controls.Add(b);
        }

        _fusionSlotsList.Dock = DockStyle.Top;
        var hint = new Label
        {
            Text = "Cada hueco es un material requerido. Configura el filtro de abajo y presiona \"Agregar hueco\".",
            AutoSize = false, Height = 30, ForeColor = Color.Gray, Dock = DockStyle.Top
        };

        _fusionGroup.Controls.Add(_fusionSlotFilter);
        _fusionGroup.Controls.Add(slotLayout);
        _fusionGroup.Controls.Add(buttons);
        _fusionGroup.Controls.Add(_fusionSlotsList);
        _fusionGroup.Controls.Add(hint);
        EditorLayout.SetupGroup(_fusionGroup);
    }

    private Control BuildMinMaxRow()
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, AutoSize = true };
        row.Controls.Add(new Label { Text = "Min", AutoSize = true, Margin = new Padding(0, 6, 4, 0) }, 0, 0);
        _fusionSlotMin.Width = 60;
        row.Controls.Add(_fusionSlotMin, 1, 0);
        row.Controls.Add(new Label { Text = "Max", AutoSize = true, Margin = new Padding(10, 6, 4, 0) }, 2, 0);
        _fusionSlotMax.Width = 60;
        row.Controls.Add(_fusionSlotMax, 3, 0);
        return row;
    }

    private void WireEvents()
    {
        _attackBox.ValueChanged += (_, _) => RaiseChanged();
        _defenseBox.ValueChanged += (_, _) => RaiseChanged();
        _levelBox.ValueChanged += (_, _) => RaiseChanged();
        _typeCombo.SelectedIndexChanged += (_, _) => RaiseChanged();
        _typeEditorButton.Click += (_, _) => TypeEditorRequested?.Invoke(this, EventArgs.Empty);
        _attributeCombo.SelectedIndexChanged += (_, _) => RaiseChanged();
        _categoryCombo.SelectedIndexChanged += (_, _) => { UpdateVisibility(); RaiseChanged(); };
        _effectCombo.SelectedIndexChanged += (_, _) => RaiseChanged();

        _fusionAddSlotButton.Click += (_, _) => OnFusionAddSlotClicked();
        _fusionRemoveSlotButton.Click += (_, _) => OnFusionRemoveSlotClicked();
    }

    private void RaiseChanged()
    {
        if (!_suppressEvents) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateVisibility()
    {
        string category = _categoryCombo.SelectedItem as string ?? "Normal";
        _fusionGroup.Visible = category == nameof(MonsterCategory.Fusion);
    }

    private void OnFusionAddSlotClicked()
    {
        _currentFusionSlots.Add(new FusionSlotDto
        {
            Filter = _fusionSlotFilter.GetFilter(),
            MinCount = (int)_fusionSlotMin.Value,
            MaxCount = Math.Max((int)_fusionSlotMin.Value, (int)_fusionSlotMax.Value)
        });
        _fusionSlotFilter.SetFilter(null);
        _fusionSlotMin.Value = 1;
        _fusionSlotMax.Value = 1;
        RefreshFusionSlotsList();
        RaiseChanged();
    }

    private void OnFusionRemoveSlotClicked()
    {
        if (_fusionSlotsList.SelectedIndex < 0) return;
        _currentFusionSlots.RemoveAt(_fusionSlotsList.SelectedIndex);
        RefreshFusionSlotsList();
        RaiseChanged();
    }

    private void RefreshFusionSlotsList()
    {
        _fusionSlotsList.Items.Clear();
        foreach (var slot in _currentFusionSlots)
            _fusionSlotsList.Items.Add($"[{slot.MinCount}-{slot.MaxCount}] {DescribeFilter(slot.Filter)}");
    }

    private static string DescribeFilter(FilterDto filter)
    {
        if (filter.IsEmpty) return "cualquier Monstruo";
        return string.Join(" O ", filter.OrGroups
            .Where(g => g.Count > 0)
            .Select(g => string.Join(" Y ", g.Select(c => $"{(c.Negate ? "NO " : "")}{c.Kind}={c.Value}"))));
    }

    /// <summary>Repuebla el combo de Tipo desde <see cref="_typeRepo"/>, preservando la seleccion actual si el Tipo sigue existiendo. Publico: <c>MainForm</c> lo llama al cerrar <see cref="TypeEditorForm"/>.</summary>
    public void RefreshTypeCombo()
    {
        string? current = _typeCombo.SelectedItem as string;

        _typeCombo.Items.Clear();
        _typeCombo.Items.AddRange(_typeRepo.Types.Cast<object>().ToArray());

        if (current != null && _typeCombo.Items.Contains(current))
            _typeCombo.SelectedItem = current;
        else if (_typeCombo.Items.Count > 0)
            _typeCombo.SelectedIndex = 0;

        _fusionSlotFilter.SetAvailableTypes(_typeRepo.Types);
    }

    public void LoadFrom(CardDto dto)
    {
        _suppressEvents = true;

        _attackBox.Value = EditorHelpers.Clamp(dto.Attack, _attackBox);
        _defenseBox.Value = EditorHelpers.Clamp(dto.Defense, _defenseBox);
        _levelBox.Value = EditorHelpers.Clamp(dto.Level == 0 ? 1 : dto.Level, _levelBox);
        EditorHelpers.SelectComboText(_typeCombo, dto.Type);
        EditorHelpers.SelectComboText(_attributeCombo, dto.Attribute);
        EditorHelpers.SelectComboText(_categoryCombo, dto.Category);

        _currentFusionSlots = dto.FusionMaterials.Select(CardDtoCloning.CloneSlot).ToList();
        RefreshFusionSlotsList();
        _fusionSlotFilter.SetFilter(null);
        _fusionSlotMin.Value = 1;
        _fusionSlotMax.Value = 1;

        string effectIdForCombo = dto.Kind == "Monster" && dto.ComposeCustomEffect ? EffectCatalog.ComposeNew.Id : dto.EffectId;
        EditorHelpers.SelectEffectOption(_effectCombo, dto.Kind == "Monster" ? effectIdForCombo : dto.EffectId);

        _suppressEvents = false;
        UpdateVisibility();
    }

    public void ApplyTo(CardDto dto)
    {
        dto.Attack = (int)_attackBox.Value;
        dto.Defense = (int)_defenseBox.Value;
        dto.Level = (int)_levelBox.Value;
        dto.Type = _typeCombo.SelectedItem as string ?? "Unknown";
        dto.Attribute = _attributeCombo.SelectedItem as string ?? "Dark";
        dto.Category = _categoryCombo.SelectedItem as string ?? "Normal";
        dto.FusionMaterials = _currentFusionSlots.Select(CardDtoCloning.CloneSlot).ToList();
        dto.EffectId = SelectedEffectId;
    }
}
