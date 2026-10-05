using Godot;
using GodotGame.Core.Effects;
using GodotGame.Data.Loaders;
using GodotGame.Editor.Data;

namespace GodotGame.Editor.Controls;

/// <summary>
/// Compositor de efectos compartido entre Monstruo (Volteo)/Magia/Trampa: se
/// activa cuando el combo de Efecto correspondiente elige "★ Componer efecto
/// nuevo...". Adaptacion Godot de <c>EffectComposerPanel</c> de
/// MonstersGame.CardEditor -- <see cref="ApplyTo"/> solo escribe los campos
/// de efecto compuesto: quien lo usa decide si corresponde llamarlo.
/// </summary>
public sealed partial class EffectComposerPanel : VBoxContainer
{
    private readonly TypeRepository _typeRepo;

    private readonly OptionButton _triggerCombo = new();
    private readonly LineEdit _visualProfileKeyBox = new() { PlaceholderText = "Vacio = perfil generico segun tipo de evento" };
    private readonly CheckBox _requiresTargetCheck = new() { Text = "Este efecto requiere elegir un objetivo" };
    private readonly OptionButton _targetKindCombo = new();
    private readonly TargetFilterEditor _targetFilter = new();
    private readonly ItemList _stepsList = new() { CustomMinimumSize = new Vector2(0, 70) };
    private readonly OptionButton _actionKindCombo = new();
    private readonly LineEdit _paramsBox = new() { PlaceholderText = "Clave=Valor;Clave2=Valor2 (opcional)" };
    private List<EffectActionStepDto> _currentSteps = new();

    private bool _suppressEvents;

    public event Action? Changed;

    public EffectComposerPanel(TypeRepository typeRepo)
    {
        _typeRepo = typeRepo;
        AddThemeConstantOverride("separation", 8);

        AddChild(EditorLayout.Hint("Elegi \"★ Componer efecto nuevo...\" en el combo de Efecto de la carta para activar este panel."));

        var triggerLayout = EditorLayout.TwoColumnLayout();
        EditorLayout.AddRow(triggerLayout, "Trigger", _triggerCombo);
        EditorLayout.AddRow(triggerLayout, "Clave visual", _visualProfileKeyBox);
        AddChild(triggerLayout);

        AddChild(_requiresTargetCheck);

        var targetLayout = EditorLayout.TwoColumnLayout();
        EditorLayout.AddRow(targetLayout, "Tipo de objetivo", _targetKindCombo);
        AddChild(targetLayout);
        AddChild(_targetFilter);

        AddChild(EditorLayout.Hint("Pasos de accion (en orden):"));
        AddChild(_stepsList);

        var stepEditorLayout = EditorLayout.TwoColumnLayout();
        EditorLayout.AddRow(stepEditorLayout, "Accion", _actionKindCombo);
        EditorLayout.AddRow(stepEditorLayout, "Parametros", _paramsBox);
        AddChild(stepEditorLayout);

        var addStepButton = new Button { Text = "Agregar paso" };
        var removeStepButton = new Button { Text = "Quitar paso" };
        var moveUpButton = new Button { Text = "Subir" };
        var moveDownButton = new Button { Text = "Bajar" };
        addStepButton.Pressed += OnAddStepClicked;
        removeStepButton.Pressed += OnRemoveStepClicked;
        moveUpButton.Pressed += () => OnMoveStepClicked(-1);
        moveDownButton.Pressed += () => OnMoveStepClicked(1);
        var stepButtons = new HBoxContainer();
        foreach (var b in new[] { addStepButton, removeStepButton, moveUpButton, moveDownButton }) stepButtons.AddChild(b);
        AddChild(stepButtons);

        foreach (string name in Enum.GetNames<EffectTrigger>()) _triggerCombo.AddItem(name);
        foreach (string name in Enum.GetNames<EffectTargetKind>()) _targetKindCombo.AddItem(name);
        foreach (string kind in EffectActionCatalog.RegisteredKinds.OrderBy(k => k, StringComparer.Ordinal)) _actionKindCombo.AddItem(kind);
        RefreshTypes();

        WireEvents();
    }

    /// <summary>Repuebla la lista de Tipos disponible en el filtro de objetivo.</summary>
    public void RefreshTypes() => _targetFilter.SetAvailableTypes(_typeRepo.Types);

    private void WireEvents()
    {
        _triggerCombo.ItemSelected += _ => RaiseChanged();
        _visualProfileKeyBox.TextChanged += _ => RaiseChanged();
        _requiresTargetCheck.Toggled += _ => { UpdateVisibility(); RaiseChanged(); };
        _targetKindCombo.ItemSelected += _ => RaiseChanged();
        _targetFilter.FilterChanged += RaiseChanged;
    }

    private void RaiseChanged()
    {
        if (!_suppressEvents) Changed?.Invoke();
    }

    private void UpdateVisibility()
    {
        _targetKindCombo.Disabled = !_requiresTargetCheck.ButtonPressed;
        _targetFilter.Modulate = _requiresTargetCheck.ButtonPressed ? Colors.White : new Color(1, 1, 1, 0.5f);
    }

    private void OnAddStepClicked()
    {
        if (_actionKindCombo.Selected < 0) return;
        string kind = _actionKindCombo.GetItemText(_actionKindCombo.Selected);
        _currentSteps.Add(new EffectActionStepDto { ActionKind = kind, ParamsText = _paramsBox.Text.Trim() });
        _paramsBox.Text = "";
        RefreshStepsList();
        RaiseChanged();
    }

    private void OnRemoveStepClicked()
    {
        var selected = _stepsList.GetSelectedItems();
        if (selected.Length == 0) return;
        _currentSteps.RemoveAt(selected[0]);
        RefreshStepsList();
        RaiseChanged();
    }

    private void OnMoveStepClicked(int direction)
    {
        var selected = _stepsList.GetSelectedItems();
        if (selected.Length == 0) return;
        int i = selected[0];
        int j = i + direction;
        if (j < 0 || j >= _currentSteps.Count) return;
        (_currentSteps[i], _currentSteps[j]) = (_currentSteps[j], _currentSteps[i]);
        RefreshStepsList();
        _stepsList.Select(j);
        RaiseChanged();
    }

    private void RefreshStepsList()
    {
        _stepsList.Clear();
        foreach (var step in _currentSteps)
            _stepsList.AddItem(string.IsNullOrEmpty(step.ParamsText) ? step.ActionKind : $"{step.ActionKind} ({step.ParamsText})");
    }

    public void LoadFrom(CardDto dto)
    {
        _suppressEvents = true;

        _currentSteps = dto.EffectActionSteps.Select(CardDtoCloning.CloneStep).ToList();
        RefreshStepsList();
        SelectComboText(_triggerCombo, dto.EffectTrigger);
        _visualProfileKeyBox.Text = dto.EffectVisualProfileKey;
        _requiresTargetCheck.ButtonPressed = dto.EffectRequiresTarget;
        SelectComboText(_targetKindCombo, dto.EffectTargetKind);
        _targetFilter.SetFilter(dto.EffectTargetFilter);

        _suppressEvents = false;
        UpdateVisibility();
    }

    /// <summary>Escribe los campos del efecto compuesto en <paramref name="dto"/>. Quien lo usa decide si corresponde invocarlo.</summary>
    public void ApplyTo(CardDto dto)
    {
        dto.EffectTrigger = _triggerCombo.Selected >= 0 ? _triggerCombo.GetItemText(_triggerCombo.Selected) : nameof(EffectTrigger.Any);
        dto.EffectVisualProfileKey = _visualProfileKeyBox.Text.Trim();
        dto.EffectRequiresTarget = _requiresTargetCheck.ButtonPressed;
        dto.EffectTargetKind = _targetKindCombo.Selected >= 0 ? _targetKindCombo.GetItemText(_targetKindCombo.Selected) : nameof(EffectTargetKind.MonsterZone);
        dto.EffectTargetFilter = _targetFilter.GetFilter();
        dto.EffectActionSteps = _currentSteps.Select(CardDtoCloning.CloneStep).ToList();
    }

    private static void SelectComboText(OptionButton combo, string text)
    {
        for (int i = 0; i < combo.ItemCount; i++)
            if (combo.GetItemText(i) == text) { combo.Selected = i; return; }
        combo.Selected = combo.ItemCount > 0 ? 0 : -1;
    }
}
