using Godot;
using GodotGame.Core.Entities;
using GodotGame.Core.Rules;
using GodotGame.Data.Loaders;
using GodotGame.Editor.Data;

namespace GodotGame.Editor.Controls;

/// <summary>
/// Panel de propiedades de una carta Monstruo: ATK/DEF/Nivel/Tipo/Atributo/
/// Categoria/Efecto, mas los materiales de Fusion (visibles solo si
/// Categoria = Fusion). Adaptacion Godot de <c>MonsterPanel</c> de
/// MonstersGame.CardEditor.
/// </summary>
public sealed partial class MonsterPanel : VBoxContainer
{
    private readonly TypeRepository _typeRepo;

    private readonly SpinBox _attackBox = new() { MinValue = 0, MaxValue = 99999 };
    private readonly SpinBox _defenseBox = new() { MinValue = 0, MaxValue = 99999 };
    private readonly SpinBox _levelBox = new() { MinValue = 1, MaxValue = 12 };
    private readonly OptionButton _typeCombo = new();
    private readonly Button _typeEditorButton = new() { Text = "...", CustomMinimumSize = new Vector2(32, 0) };
    private readonly OptionButton _attributeCombo = new();
    private readonly OptionButton _categoryCombo = new();
    private readonly OptionButton _effectCombo = new();
    private readonly OptionButton _star1Combo = new();
    private readonly OptionButton _star2Combo = new();

    private readonly VBoxContainer _fusionGroup;
    private Control _fusionSectionRoot = null!;
    private readonly ItemList _fusionSlotsList = new() { CustomMinimumSize = new Vector2(0, 70) };
    private readonly TargetFilterEditor _fusionSlotFilter = new();
    private readonly SpinBox _fusionSlotMin = new() { MinValue = 1, MaxValue = 9, Value = 1 };
    private readonly SpinBox _fusionSlotMax = new() { MinValue = 1, MaxValue = 9, Value = 1 };
    private List<FusionSlotDto> _currentFusionSlots = new();

    private bool _suppressEvents;

    public event Action? Changed;
    public event Action? TypeEditorRequested;

    public string SelectedEffectId => SelectedEffectOption();

    public MonsterPanel(TypeRepository typeRepo)
    {
        _typeRepo = typeRepo;
        AddThemeConstantOverride("separation", 8);

        var layout = EditorLayout.TwoColumnLayout();
        EditorLayout.AddRow(layout, "ATK", _attackBox);
        EditorLayout.AddRow(layout, "DEF", _defenseBox);
        EditorLayout.AddRow(layout, "Nivel", _levelBox);
        EditorLayout.AddRow(layout, "Tipo", BuildTypeRow());
        EditorLayout.AddRow(layout, "Atributo", _attributeCombo);
        EditorLayout.AddRow(layout, "Categoria", _categoryCombo);
        EditorLayout.AddRow(layout, "Efecto (Volteo)", _effectCombo);
        EditorLayout.AddRow(layout, "Estrella Guardiana 1", _star1Combo);
        EditorLayout.AddRow(layout, "Estrella Guardiana 2", _star2Combo);
        AddChild(layout);

        var section = EditorLayout.Section("Materiales de Fusion", out _fusionGroup);
        BuildFusionGroup();
        _fusionSectionRoot = section;
        AddChild(section);
        _fusionSectionRoot.Visible = false; // seccion completa oculta hasta Categoria = Fusion

        foreach (string name in Enum.GetNames<MonsterAttribute>()) _attributeCombo.AddItem(name);
        foreach (string name in Enum.GetNames<MonsterCategory>()) _categoryCombo.AddItem(name);
        foreach (var option in EffectCatalog.Options) _effectCombo.AddItem(option.Label);
        // Texto en castellano para mostrar; el Id del item es el valor del enum (lo que se guarda).
        foreach (var star in Enum.GetValues<GuardianStar>())
        {
            string label = $"{GuardianStars.DisplayName(star)}  (vence a {GuardianStars.DisplayName(GuardianStars.BeatsWhich(star))})";
            _star1Combo.AddItem(label, (int)star);
            _star2Combo.AddItem(label, (int)star);
        }
        RefreshTypeCombo();

        WireEvents();
    }

    private Control BuildTypeRow()
    {
        var row = new HBoxContainer();
        _typeCombo.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(_typeCombo);
        row.AddChild(_typeEditorButton);
        return row;
    }

    private void BuildFusionGroup()
    {
        var minMaxRow = new HBoxContainer();
        minMaxRow.AddChild(new Label { Text = "Min", VerticalAlignment = VerticalAlignment.Center });
        minMaxRow.AddChild(_fusionSlotMin);
        minMaxRow.AddChild(new Label { Text = "Max", VerticalAlignment = VerticalAlignment.Center });
        minMaxRow.AddChild(_fusionSlotMax);

        var addSlotButton = new Button { Text = "Agregar hueco" };
        var removeSlotButton = new Button { Text = "Quitar hueco seleccionado" };
        addSlotButton.Pressed += OnFusionAddSlotClicked;
        removeSlotButton.Pressed += OnFusionRemoveSlotClicked;
        var buttons = new HBoxContainer();
        buttons.AddChild(addSlotButton);
        buttons.AddChild(removeSlotButton);

        _fusionGroup.AddChild(_fusionSlotFilter);
        _fusionGroup.AddChild(minMaxRow);
        _fusionGroup.AddChild(buttons);
        _fusionGroup.AddChild(_fusionSlotsList);
        _fusionGroup.AddChild(EditorLayout.Hint("Cada hueco es un material requerido. Configura el filtro de arriba y presiona \"Agregar hueco\"."));
    }

    private void WireEvents()
    {
        _attackBox.ValueChanged += _ => RaiseChanged();
        _defenseBox.ValueChanged += _ => RaiseChanged();
        _levelBox.ValueChanged += _ => RaiseChanged();
        _typeCombo.ItemSelected += _ => RaiseChanged();
        _typeEditorButton.Pressed += () => TypeEditorRequested?.Invoke();
        _attributeCombo.ItemSelected += _ => RaiseChanged();
        _categoryCombo.ItemSelected += _ => { UpdateVisibility(); RaiseChanged(); };
        _effectCombo.ItemSelected += _ => RaiseChanged();
        _star1Combo.ItemSelected += _ => RaiseChanged();
        _star2Combo.ItemSelected += _ => RaiseChanged();
        _fusionSlotFilter.FilterChanged += RaiseChanged;
    }

    private void RaiseChanged()
    {
        if (!_suppressEvents) Changed?.Invoke();
    }

    private void UpdateVisibility()
    {
        string category = SelectedItemText(_categoryCombo, "Normal");
        _fusionSectionRoot.Visible = category == nameof(MonsterCategory.Fusion);
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
        var selected = _fusionSlotsList.GetSelectedItems();
        if (selected.Length == 0) return;
        _currentFusionSlots.RemoveAt(selected[0]);
        RefreshFusionSlotsList();
        RaiseChanged();
    }

    private void RefreshFusionSlotsList()
    {
        _fusionSlotsList.Clear();
        foreach (var slot in _currentFusionSlots)
            _fusionSlotsList.AddItem($"[{slot.MinCount}-{slot.MaxCount}] {DescribeFilter(slot.Filter)}");
    }

    private static string DescribeFilter(FilterDto filter)
    {
        if (filter.IsEmpty) return "cualquier Monstruo";
        return string.Join(" O ", filter.OrGroups
            .Where(g => g.Count > 0)
            .Select(g => string.Join(" Y ", g.Select(c => $"{(c.Negate ? "NO " : "")}{c.Kind}={c.Value}"))));
    }

    /// <summary>Repuebla el combo de Tipo desde <see cref="_typeRepo"/>, preservando la seleccion actual si el Tipo sigue existiendo.</summary>
    public void RefreshTypeCombo()
    {
        string? current = _typeCombo.ItemCount > 0 ? _typeCombo.GetItemText(_typeCombo.Selected) : null;

        _typeCombo.Clear();
        foreach (string t in _typeRepo.Types) _typeCombo.AddItem(t);

        int idx = current != null ? _typeRepo.Types.ToList().IndexOf(current) : -1;
        _typeCombo.Selected = idx >= 0 ? idx : (_typeCombo.ItemCount > 0 ? 0 : -1);

        _fusionSlotFilter.SetAvailableTypes(_typeRepo.Types);
    }

    private string SelectedEffectOption()
    {
        var options = EffectCatalog.Options;
        return _effectCombo.Selected >= 0 && _effectCombo.Selected < options.Count ? options[_effectCombo.Selected].Id : "";
    }

    private static string SelectedItemText(OptionButton combo, string fallback) =>
        combo.Selected >= 0 && combo.ItemCount > 0 ? combo.GetItemText(combo.Selected) : fallback;

    private static void SelectComboText(OptionButton combo, string text)
    {
        for (int i = 0; i < combo.ItemCount; i++)
            if (combo.GetItemText(i) == text) { combo.Selected = i; return; }
        combo.Selected = combo.ItemCount > 0 ? 0 : -1;
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

        _attackBox.Value = dto.Attack;
        _defenseBox.Value = dto.Defense;
        _levelBox.Value = dto.Level == 0 ? 1 : dto.Level;
        SelectComboText(_typeCombo, dto.Type);
        SelectComboText(_attributeCombo, dto.Attribute);
        SelectComboText(_categoryCombo, dto.Category);

        // Sin estrellas cargadas (carta nueva o anterior a esta mecanica): las de su Atributo.
        var defaults = GuardianStars.DefaultsFor(Enum.TryParse<MonsterAttribute>(dto.Attribute, true, out var attribute) ? attribute : MonsterAttribute.Dark);
        SelectStar(_star1Combo, dto.GuardianStar1, defaults.First);
        SelectStar(_star2Combo, dto.GuardianStar2, defaults.Second);

        _currentFusionSlots = dto.FusionMaterials.Select(CardDtoCloning.CloneSlot).ToList();
        RefreshFusionSlotsList();
        _fusionSlotFilter.SetFilter(null);
        _fusionSlotMin.Value = 1;
        _fusionSlotMax.Value = 1;

        string effectIdForCombo = dto.Kind == "Monster" && dto.ComposeCustomEffect ? EffectCatalog.ComposeNew.Id : dto.EffectId;
        SelectEffectOption(_effectCombo, dto.Kind == "Monster" ? effectIdForCombo : dto.EffectId);

        _suppressEvents = false;
        UpdateVisibility();
    }

    public void ApplyTo(CardDto dto)
    {
        dto.Attack = (int)_attackBox.Value;
        dto.Defense = (int)_defenseBox.Value;
        dto.Level = (int)_levelBox.Value;
        dto.Type = SelectedItemText(_typeCombo, "Unknown");
        dto.Attribute = SelectedItemText(_attributeCombo, "Dark");
        dto.Category = SelectedItemText(_categoryCombo, "Normal");
        dto.FusionMaterials = _currentFusionSlots.Select(CardDtoCloning.CloneSlot).ToList();
        dto.EffectId = SelectedEffectId;
        dto.GuardianStar1 = SelectedStar(_star1Combo).ToString();
        dto.GuardianStar2 = SelectedStar(_star2Combo).ToString();
    }

    private static void SelectStar(OptionButton combo, string value, GuardianStar fallback)
    {
        var star = Enum.TryParse<GuardianStar>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
        combo.Selected = combo.GetItemIndex((int)star);
    }

    private static GuardianStar SelectedStar(OptionButton combo) =>
        combo.Selected >= 0 ? (GuardianStar)combo.GetItemId(combo.Selected) : GuardianStar.Sun;
}
