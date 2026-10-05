using MonstersGame.Core.Entities;
using MonstersGame.Data.Loaders;

namespace MonstersGame.CardEditor;

/// <summary>
/// Panel de propiedades de una carta Magica: SubType/Efecto comunes, mas los
/// campos de Ritual (visibles solo si SubType = Ritual), Equipo (SubType =
/// Equip) y Campo (SubType = Field). Extraido de <c>MainForm</c> (Fase 5,
/// paso de refactor puro).
/// </summary>
public sealed class SpellPanel : UserControl
{
    private readonly FieldTypeRepository _fieldTypeRepo;
    private readonly TypeRepository _typeRepo;

    private readonly ComboBox _subTypeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _effectCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    private readonly Label _ritualLabel = new() { Text = "Solo para Magias de Ritual:", AutoSize = true };
    private readonly NumericUpDown _ritualMonsterIdBox = new() { Minimum = 0, Maximum = 999999 };
    private readonly NumericUpDown _requiredRitualLevelBox = new() { Minimum = 0, Maximum = 99 };
    private readonly Label _ritualFilterLabel = new() { Text = "Sacrificios validos (vacio = cualquiera):", AutoSize = true };
    private readonly TargetFilterEditorControl _ritualFilter = new();
    private TableLayoutPanel _ritualLayout = null!;

    private readonly GroupBox _equipGroup = new() { Text = "Objetivos y modificador de Equipo" };
    private readonly TargetFilterEditorControl _equipTargetFilter = new();
    private readonly NumericUpDown _equipAttackBox = new() { Minimum = -9999, Maximum = 9999 };
    private readonly NumericUpDown _equipDefenseBox = new() { Minimum = -9999, Maximum = 9999 };
    private readonly ComboBox _equipDurationCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _equipDurationTurnsBox = new() { Minimum = 0, Maximum = 99 };

    private readonly GroupBox _fieldGroup = new() { Text = "Tipo de Campo" };
    private readonly ComboBox _fieldTypeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _fieldTypeEditorButton = new() { Text = "Tipos de Campo..." };
    private const string NoFieldTypeOption = "(Ninguno)";

    private bool _suppressEvents;

    public event EventHandler? Changed;
    public event EventHandler? FieldTypeEditorRequested;

    public string SelectedEffectId => EditorHelpers.SelectedEffectId(_effectCombo);
    public bool IsRitual => (_subTypeCombo.SelectedItem as string) == nameof(SpellSubType.Ritual);

    public SpellPanel(FieldTypeRepository fieldTypeRepo, TypeRepository typeRepo)
    {
        _fieldTypeRepo = fieldTypeRepo;
        _typeRepo = typeRepo;

        var commonLayout = EditorLayout.TwoColumnLayout();
        EditorLayout.AddRow(commonLayout, "SubType", _subTypeCombo);
        EditorLayout.AddRow(commonLayout, "Efecto", _effectCombo);

        _ritualLayout = EditorLayout.TwoColumnLayout();
        EditorLayout.AddRow(_ritualLayout, "Id Monstruo Ritual", _ritualMonsterIdBox);
        EditorLayout.AddRow(_ritualLayout, "Nivel Sacrificio", _requiredRitualLevelBox);

        _ritualLabel.Dock = DockStyle.Top;
        _ritualLabel.Margin = new Padding(3, 10, 3, 4);
        _ritualFilterLabel.Dock = DockStyle.Top;
        _ritualFilterLabel.Margin = new Padding(3, 6, 3, 2);

        BuildEquipGroup();
        BuildFieldGroup();

        Dock = DockStyle.Top;
        AutoSize = true;
        // Con Dock = DockStyle.Top, WinForms acopla el ULTIMO control
        // agregado mas cerca del borde (aqui, arriba) — se agregan en el
        // orden inverso al que se ven.
        Controls.Add(_fieldGroup);
        Controls.Add(_equipGroup);
        Controls.Add(_ritualFilter);
        Controls.Add(_ritualFilterLabel);
        Controls.Add(_ritualLayout);
        Controls.Add(_ritualLabel);
        Controls.Add(commonLayout);

        _subTypeCombo.Items.AddRange(Enum.GetNames<SpellSubType>());
        _effectCombo.Items.AddRange(EffectCatalog.Options.Cast<object>().ToArray());
        _equipDurationCombo.Items.AddRange(Enum.GetNames<ModifierDuration>());
        RefreshFieldTypeCombo();
        RefreshTypes();

        WireEvents();
    }

    private void BuildEquipGroup()
    {
        var layout = EditorLayout.TwoColumnLayout();
        EditorLayout.AddRow(layout, "Mod. ATK", _equipAttackBox);
        EditorLayout.AddRow(layout, "Mod. DEF", _equipDefenseBox);
        EditorLayout.AddRow(layout, "Duracion", _equipDurationCombo);
        EditorLayout.AddRow(layout, "Turnos (si aplica)", _equipDurationTurnsBox);

        var label = new Label { Text = "Objetivos permitidos (vacio = cualquier Monstruo):", AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(3, 6, 3, 2) };

        _equipGroup.Controls.Add(_equipTargetFilter);
        _equipGroup.Controls.Add(label);
        _equipGroup.Controls.Add(layout);
        EditorLayout.SetupGroup(_equipGroup);
    }

    private void BuildFieldGroup()
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _fieldTypeCombo.Dock = DockStyle.Fill;
        row.Controls.Add(_fieldTypeCombo, 0, 0);
        _fieldTypeEditorButton.Width = 130;
        _fieldTypeEditorButton.Margin = new Padding(4, 0, 0, 0);
        row.Controls.Add(_fieldTypeEditorButton, 1, 0);

        var layout = EditorLayout.TwoColumnLayout();
        EditorLayout.AddRow(layout, "Tipo de Campo", row);
        _fieldGroup.Controls.Add(layout);
        EditorLayout.SetupGroup(_fieldGroup);
    }

    private void WireEvents()
    {
        _subTypeCombo.SelectedIndexChanged += (_, _) => { UpdateVisibility(); RaiseChanged(); };
        _effectCombo.SelectedIndexChanged += (_, _) => RaiseChanged();
        _ritualMonsterIdBox.ValueChanged += (_, _) => RaiseChanged();
        _requiredRitualLevelBox.ValueChanged += (_, _) => RaiseChanged();
        _ritualFilter.FilterChanged += (_, _) => RaiseChanged();

        _equipTargetFilter.FilterChanged += (_, _) => RaiseChanged();
        _equipAttackBox.ValueChanged += (_, _) => RaiseChanged();
        _equipDefenseBox.ValueChanged += (_, _) => RaiseChanged();
        _equipDurationCombo.SelectedIndexChanged += (_, _) => RaiseChanged();
        _equipDurationTurnsBox.ValueChanged += (_, _) => RaiseChanged();

        _fieldTypeCombo.SelectedIndexChanged += (_, _) => RaiseChanged();
        _fieldTypeEditorButton.Click += (_, _) => FieldTypeEditorRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseChanged()
    {
        if (!_suppressEvents) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateVisibility()
    {
        bool isRitual = IsRitual;
        _ritualMonsterIdBox.Enabled = isRitual;
        _requiredRitualLevelBox.Enabled = isRitual;
        _ritualLabel.Visible = isRitual;
        _ritualLayout.Visible = isRitual;
        _ritualFilterLabel.Visible = isRitual;
        _ritualFilter.Visible = isRitual;
        // Una Magia de Ritual no usa EffectId: su "efecto" es la Invocacion
        // Ritual en si (DuelEngine.RitualSummon), no algo compuesto/registrado.
        _effectCombo.Enabled = !isRitual;

        string subType = _subTypeCombo.SelectedItem as string ?? "Normal";
        _equipGroup.Visible = subType == nameof(SpellSubType.Equip);
        _fieldGroup.Visible = subType == nameof(SpellSubType.Field);
    }

    /// <summary>Repuebla el combo de tipo de Campo. Publico: <c>MainForm</c> lo llama al cerrar <see cref="FieldTypeEditorForm"/>.</summary>
    public void RefreshFieldTypeCombo()
    {
        string? current = _fieldTypeCombo.SelectedItem as string;

        _fieldTypeCombo.Items.Clear();
        _fieldTypeCombo.Items.Add(NoFieldTypeOption);
        _fieldTypeCombo.Items.AddRange(_fieldTypeRepo.FieldTypes.Select(f => f.Id).Cast<object>().ToArray());

        if (current != null && _fieldTypeCombo.Items.Contains(current))
            _fieldTypeCombo.SelectedItem = current;
        else
            _fieldTypeCombo.SelectedIndex = 0;
    }

    /// <summary>Repuebla la lista de Tipos disponible en los filtros de Ritual/Equipo. Publico: <c>MainForm</c> lo llama al cerrar <see cref="TypeEditorForm"/>.</summary>
    public void RefreshTypes()
    {
        _ritualFilter.SetAvailableTypes(_typeRepo.Types);
        _equipTargetFilter.SetAvailableTypes(_typeRepo.Types);
    }

    public void LoadFrom(CardDto dto)
    {
        _suppressEvents = true;

        EditorHelpers.SelectComboText(_subTypeCombo, dto.SubType);
        _ritualMonsterIdBox.Value = EditorHelpers.Clamp(dto.RitualMonsterId, _ritualMonsterIdBox);
        _requiredRitualLevelBox.Value = EditorHelpers.Clamp(dto.RequiredRitualLevel, _requiredRitualLevelBox);
        _ritualFilter.SetFilter(dto.RitualFilter);

        _equipTargetFilter.SetFilter(dto.EquipTargetFilter);
        _equipAttackBox.Value = EditorHelpers.Clamp(dto.EquipAttackModifier, _equipAttackBox);
        _equipDefenseBox.Value = EditorHelpers.Clamp(dto.EquipDefenseModifier, _equipDefenseBox);
        EditorHelpers.SelectComboText(_equipDurationCombo, dto.EquipDuration);
        _equipDurationTurnsBox.Value = EditorHelpers.Clamp(dto.EquipDurationTurns, _equipDurationTurnsBox);

        EditorHelpers.SelectComboText(_fieldTypeCombo, dto.FieldTypeId ?? NoFieldTypeOption);

        string effectIdForCombo = dto.Kind == "Spell" && dto.ComposeCustomEffect ? EffectCatalog.ComposeNew.Id : dto.EffectId;
        EditorHelpers.SelectEffectOption(_effectCombo, dto.Kind == "Spell" ? effectIdForCombo : dto.EffectId);

        _suppressEvents = false;
        UpdateVisibility();
    }

    public void ApplyTo(CardDto dto)
    {
        dto.SubType = _subTypeCombo.SelectedItem as string ?? "Normal";
        dto.EffectId = SelectedEffectId;
        dto.RitualMonsterId = (int)_ritualMonsterIdBox.Value;
        dto.RequiredRitualLevel = (int)_requiredRitualLevelBox.Value;
        dto.RitualFilter = _ritualFilter.GetFilter();
        dto.EquipTargetFilter = _equipTargetFilter.GetFilter();
        dto.EquipAttackModifier = (int)_equipAttackBox.Value;
        dto.EquipDefenseModifier = (int)_equipDefenseBox.Value;
        dto.EquipDuration = _equipDurationCombo.SelectedItem as string ?? nameof(ModifierDuration.WhileEquipped);
        dto.EquipDurationTurns = (int)_equipDurationTurnsBox.Value;
        dto.FieldTypeId = _fieldTypeCombo.SelectedItem as string == NoFieldTypeOption ? null : _fieldTypeCombo.SelectedItem as string;
    }
}
