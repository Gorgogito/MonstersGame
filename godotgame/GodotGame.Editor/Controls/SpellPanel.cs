using Godot;
using GodotGame.Core.Effects.Monster;
using GodotGame.Core.Entities;
using GodotGame.Data.Loaders;
using GodotGame.Editor.Data;

namespace GodotGame.Editor.Controls;

/// <summary>
/// Panel de propiedades de una carta Magica: subtipo (con la explicacion de
/// como se juega cada uno), los efectos por datos de la carta y los campos
/// propios de Ritual (visibles solo si SubType = Ritual), Equipo (SubType =
/// Equip) y Campo (SubType = Field).
/// </summary>
public sealed partial class SpellPanel : VBoxContainer
{
    private const string NoFieldTypeOption = "(Ninguno)";

    private readonly FieldTypeRepository _fieldTypeRepo;
    private readonly TypeRepository _typeRepo;

    private readonly OptionButton _subTypeCombo = new();
    private readonly Label _subTypeHelp = EditorLayout.Hint("");
    private readonly OptionButton _effectCombo = new() { FitToLongestItem = false, ClipText = true };
    private readonly MonsterEffectsEditor _effectsEditor;

    private readonly VBoxContainer _ritualSection;
    private readonly SpinBox _ritualMonsterIdBox = new() { MinValue = 0, MaxValue = 999999 };
    private readonly SpinBox _requiredRitualLevelBox = new() { MinValue = 0, MaxValue = 99 };
    private readonly TargetFilterEditor _ritualFilter = new();
    private Control _ritualSectionRoot = null!;

    private readonly VBoxContainer _equipGroup;
    private readonly TargetFilterEditor _equipTargetFilter = new();
    private readonly SpinBox _equipAttackBox = new() { MinValue = -9999, MaxValue = 9999 };
    private readonly SpinBox _equipDefenseBox = new() { MinValue = -9999, MaxValue = 9999 };
    private readonly OptionButton _equipDurationCombo = new();
    private readonly SpinBox _equipDurationTurnsBox = new() { MinValue = 0, MaxValue = 99 };
    private Control _equipSectionRoot = null!;

    private readonly VBoxContainer _fieldGroup;
    private readonly OptionButton _fieldTypeCombo = new();
    private readonly Button _fieldTypeEditorButton = new() { Text = "Tipos de Campo..." };
    private Control _fieldSectionRoot = null!;

    private bool _suppressEvents;

    public event Action? Changed;
    public event Action? FieldTypeEditorRequested;

    public string SelectedEffectId => SelectedEffectOption();
    public bool IsRitual => SelectedSubType() == nameof(SpellSubType.Ritual);

    public SpellPanel(FieldTypeRepository fieldTypeRepo, TypeRepository typeRepo, EffectEditorContext effectContext)
    {
        _fieldTypeRepo = fieldTypeRepo;
        _typeRepo = typeRepo;
        _effectsEditor = new MonsterEffectsEditor(effectContext, CardKind.Spell);
        AddThemeConstantOverride("separation", 8);

        var commonLayout = EditorLayout.TwoColumnLayout();
        EditorLayout.AddRow(commonLayout, "Subtipo", _subTypeCombo);
        EditorLayout.AddRow(commonLayout, "Efecto heredado (antiguo)", _effectCombo);
        AddChild(commonLayout);
        _subTypeHelp.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        AddChild(_subTypeHelp);

        var ritualSection = EditorLayout.Section("Solo para Magias de Ritual", out _ritualSection);
        var ritualLayout = EditorLayout.TwoColumnLayout();
        EditorLayout.AddRow(ritualLayout, "Id Monstruo Ritual", _ritualMonsterIdBox);
        EditorLayout.AddRow(ritualLayout, "Nivel Sacrificio", _requiredRitualLevelBox);
        _ritualSection.AddChild(ritualLayout);
        _ritualSection.AddChild(EditorLayout.Hint("Sacrificios validos (vacio = cualquiera):"));
        _ritualSection.AddChild(_ritualFilter);
        _ritualSectionRoot = ritualSection;
        AddChild(ritualSection);

        var equipSection = EditorLayout.Section("Objetivos y modificador de Equipo", out _equipGroup);
        BuildEquipGroup();
        _equipSectionRoot = equipSection;
        AddChild(equipSection);

        var fieldSection = EditorLayout.Section("Tipo de Campo", out _fieldGroup);
        BuildFieldGroup();
        _fieldSectionRoot = fieldSection;
        AddChild(fieldSection);

        var effectsSection = EditorLayout.Section("Efectos de la carta", out var effectsBody);
        effectsBody.AddChild(_effectsEditor);
        AddChild(effectsSection);

        foreach (var subType in SpellTrapCatalog.SpellSubTypes)
        {
            _subTypeCombo.AddItem(subType.Label);
            _subTypeCombo.SetItemMetadata(_subTypeCombo.ItemCount - 1, subType.Value.ToString());
        }
        foreach (var option in EffectCatalog.Options) _effectCombo.AddItem(option.Label);
        foreach (string name in Enum.GetNames<ModifierDuration>()) _equipDurationCombo.AddItem(name);
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

        _equipGroup.AddChild(EditorLayout.Hint("Objetivos permitidos (vacio = cualquier Monstruo):"));
        _equipGroup.AddChild(_equipTargetFilter);
        _equipGroup.AddChild(layout);
    }

    private void BuildFieldGroup()
    {
        var row = new HBoxContainer();
        _fieldTypeCombo.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(_fieldTypeCombo);
        row.AddChild(_fieldTypeEditorButton);

        var layout = EditorLayout.TwoColumnLayout();
        EditorLayout.AddRow(layout, "Tipo de Campo", row);
        _fieldGroup.AddChild(layout);
    }

    private void WireEvents()
    {
        _subTypeCombo.ItemSelected += _ => { UpdateVisibility(); RaiseChanged(); };
        _effectCombo.ItemSelected += _ => RaiseChanged();
        _effectsEditor.Changed += RaiseChanged;
        _ritualMonsterIdBox.ValueChanged += _ => RaiseChanged();
        _requiredRitualLevelBox.ValueChanged += _ => RaiseChanged();
        _ritualFilter.FilterChanged += RaiseChanged;

        _equipTargetFilter.FilterChanged += RaiseChanged;
        _equipAttackBox.ValueChanged += _ => RaiseChanged();
        _equipDefenseBox.ValueChanged += _ => RaiseChanged();
        _equipDurationCombo.ItemSelected += _ => RaiseChanged();
        _equipDurationTurnsBox.ValueChanged += _ => RaiseChanged();

        _fieldTypeCombo.ItemSelected += _ => RaiseChanged();
        _fieldTypeEditorButton.Pressed += () => FieldTypeEditorRequested?.Invoke();
    }

    private void RaiseChanged()
    {
        if (!_suppressEvents) Changed?.Invoke();
    }

    private void UpdateVisibility()
    {
        bool isRitual = IsRitual;
        _ritualMonsterIdBox.Editable = isRitual;
        _requiredRitualLevelBox.Editable = isRitual;
        _ritualSectionRoot.Visible = isRitual;
        // Una Magia de Ritual no usa EffectId: su "efecto" es la Invocacion
        // Ritual en si (DuelEngine.RitualSummon), no algo compuesto/registrado.
        _effectCombo.Disabled = isRitual;

        string subType = SelectedSubType();
        _equipSectionRoot.Visible = subType == nameof(SpellSubType.Equip);
        _fieldSectionRoot.Visible = subType == nameof(SpellSubType.Field);
        var info = SpellTrapCatalog.SpellSubTypes.FirstOrDefault(s => s.Value.ToString() == subType);
        _subTypeHelp.Text = info?.Description ?? "";
    }

    /// <summary>El subtipo elegido, como nombre del enum (lo que se guarda).</summary>
    private string SelectedSubType() =>
        _subTypeCombo.Selected >= 0 ? _subTypeCombo.GetItemMetadata(_subTypeCombo.Selected).AsString() : nameof(SpellSubType.Normal);

    private void SelectSubType(string value)
    {
        for (int i = 0; i < _subTypeCombo.ItemCount; i++)
            if (string.Equals(_subTypeCombo.GetItemMetadata(i).AsString(), value, StringComparison.OrdinalIgnoreCase)) { _subTypeCombo.Selected = i; return; }
        _subTypeCombo.Selected = 0;
    }

    /// <summary>Repuebla el combo de tipo de Campo.</summary>
    public void RefreshFieldTypeCombo()
    {
        string? current = _fieldTypeCombo.ItemCount > 0 ? _fieldTypeCombo.GetItemText(_fieldTypeCombo.Selected) : null;

        _fieldTypeCombo.Clear();
        _fieldTypeCombo.AddItem(NoFieldTypeOption);
        foreach (var f in _fieldTypeRepo.FieldTypes) _fieldTypeCombo.AddItem(f.Id);

        int idx = current != null ? FindItem(_fieldTypeCombo, current) : -1;
        _fieldTypeCombo.Selected = idx >= 0 ? idx : 0;
    }

    /// <summary>Repuebla la lista de Tipos disponible en los filtros de Ritual/Equipo.</summary>
    public void RefreshTypes()
    {
        _ritualFilter.SetAvailableTypes(_typeRepo.Types);
        _equipTargetFilter.SetAvailableTypes(_typeRepo.Types);
    }

    private static int FindItem(OptionButton combo, string text)
    {
        for (int i = 0; i < combo.ItemCount; i++)
            if (combo.GetItemText(i) == text) return i;
        return -1;
    }

    private static string SelectedItemText(OptionButton combo, string fallback) =>
        combo.Selected >= 0 && combo.ItemCount > 0 ? combo.GetItemText(combo.Selected) : fallback;

    private static void SelectComboText(OptionButton combo, string text)
    {
        int idx = FindItem(combo, text);
        combo.Selected = idx >= 0 ? idx : (combo.ItemCount > 0 ? 0 : -1);
    }

    private string SelectedEffectOption()
    {
        var options = EffectCatalog.Options;
        return _effectCombo.Selected >= 0 && _effectCombo.Selected < options.Count ? options[_effectCombo.Selected].Id : "";
    }

    private static void SelectEffectOption(OptionButton combo, string effectId)
    {
        var options = EffectCatalog.Options;
        for (int i = 0; i < options.Count; i++)
            if (options[i].Id == effectId) { combo.Selected = i; return; }
        combo.Selected = options.Count > 0 ? 0 : -1;
    }

    public void LoadFrom(CardDto dto)
    {
        _suppressEvents = true;

        SelectSubType(dto.SubType);
        _effectsEditor.LoadFrom(dto.Kind == "Spell" ? dto.MonsterEffects : new List<MonsterEffectDto>());
        _ritualMonsterIdBox.Value = dto.RitualMonsterId;
        _requiredRitualLevelBox.Value = dto.RequiredRitualLevel;
        _ritualFilter.SetFilter(dto.RitualFilter);

        _equipTargetFilter.SetFilter(dto.EquipTargetFilter);
        _equipAttackBox.Value = dto.EquipAttackModifier;
        _equipDefenseBox.Value = dto.EquipDefenseModifier;
        SelectComboText(_equipDurationCombo, dto.EquipDuration);
        _equipDurationTurnsBox.Value = dto.EquipDurationTurns;

        SelectComboText(_fieldTypeCombo, dto.FieldTypeId ?? NoFieldTypeOption);

        string effectIdForCombo = dto.Kind == "Spell" && dto.ComposeCustomEffect ? EffectCatalog.ComposeNew.Id : dto.EffectId;
        SelectEffectOption(_effectCombo, dto.Kind == "Spell" ? effectIdForCombo : dto.EffectId);

        _suppressEvents = false;
        UpdateVisibility();
    }

    public void ApplyTo(CardDto dto)
    {
        dto.SubType = SelectedSubType();
        dto.EffectId = SelectedEffectId;
        dto.MonsterEffects = _effectsEditor.GetEffects();
        dto.RitualMonsterId = (int)_ritualMonsterIdBox.Value;
        dto.RequiredRitualLevel = (int)_requiredRitualLevelBox.Value;
        dto.RitualFilter = _ritualFilter.GetFilter();
        dto.EquipTargetFilter = _equipTargetFilter.GetFilter();
        dto.EquipAttackModifier = (int)_equipAttackBox.Value;
        dto.EquipDefenseModifier = (int)_equipDefenseBox.Value;
        dto.EquipDuration = SelectedItemText(_equipDurationCombo, nameof(ModifierDuration.WhileEquipped));
        dto.EquipDurationTurns = (int)_equipDurationTurnsBox.Value;
        string fieldSelection = SelectedItemText(_fieldTypeCombo, NoFieldTypeOption);
        dto.FieldTypeId = fieldSelection == NoFieldTypeOption ? null : fieldSelection;
    }
}
