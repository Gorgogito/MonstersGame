using MonstersGame.Core.Effects;
using MonstersGame.Data.Loaders;

namespace MonstersGame.CardEditor;

/// <summary>
/// Compositor de efectos compartido entre Monstruo (Volteo)/Magia/Trampa: se
/// activa cuando el combo de Efecto correspondiente elige "★ Componer efecto
/// nuevo..." (ver <see cref="EffectCatalog.ComposeNew"/>). Extraido de
/// <c>MainForm</c> (Fase 5, paso de refactor puro) -- a diferencia de
/// <see cref="MonsterPanel"/>/<see cref="SpellPanel"/>/<see cref="TrapPanel"/>,
/// <see cref="ApplyTo"/> solo escribe los campos de efecto compuesto: quien
/// llama decide si corresponde llamarlo (ver <c>MainForm.BuildDtoFromForm</c>).
/// </summary>
public sealed class EffectComposerPanel : UserControl
{
    private readonly TypeRepository _typeRepo;

    private readonly ComboBox _triggerCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _visualProfileKeyBox = new() { PlaceholderText = "Vacio = perfil generico segun tipo de evento" };
    private readonly CheckBox _requiresTargetCheck = new() { Text = "Este efecto requiere elegir un objetivo" };
    private readonly ComboBox _targetKindCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TargetFilterEditorControl _targetFilter = new();
    private readonly ListBox _stepsList = new() { Height = 70 };
    private readonly ComboBox _actionKindCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _paramsBox = new() { PlaceholderText = "Clave=Valor;Clave2=Valor2 (opcional)" };
    private readonly Button _addStepButton = new() { Text = "Agregar paso" };
    private readonly Button _removeStepButton = new() { Text = "Quitar paso" };
    private readonly Button _moveStepUpButton = new() { Text = "Subir" };
    private readonly Button _moveStepDownButton = new() { Text = "Bajar" };
    private List<EffectActionStepDto> _currentSteps = new();

    private bool _suppressEvents;

    public event EventHandler? Changed;

    public EffectComposerPanel(TypeRepository typeRepo)
    {
        _typeRepo = typeRepo;

        var triggerLayout = EditorLayout.TwoColumnLayout();
        EditorLayout.AddRow(triggerLayout, "Trigger", _triggerCombo);
        EditorLayout.AddRow(triggerLayout, "Clave visual (Fase 6)", _visualProfileKeyBox);

        _requiresTargetCheck.Dock = DockStyle.Top;
        _requiresTargetCheck.Margin = new Padding(3, 6, 3, 2);

        var targetLayout = EditorLayout.TwoColumnLayout();
        EditorLayout.AddRow(targetLayout, "Tipo de objetivo", _targetKindCombo);

        var stepsLabel = new Label { Text = "Pasos de accion (en orden):", AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(3, 8, 3, 2) };
        _stepsList.Dock = DockStyle.Top;

        var stepEditorLayout = EditorLayout.TwoColumnLayout();
        EditorLayout.AddRow(stepEditorLayout, "Accion", _actionKindCombo);
        EditorLayout.AddRow(stepEditorLayout, "Parametros", _paramsBox);

        var stepButtons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        foreach (var b in new[] { _addStepButton, _removeStepButton, _moveStepUpButton, _moveStepDownButton })
        {
            b.Width = 110;
            b.Margin = new Padding(0, 4, 6, 4);
            stepButtons.Controls.Add(b);
        }

        var hint = new Label
        {
            Text = "Elegi \"★ Componer efecto nuevo...\" en el combo de Efecto de la carta para activar este panel.",
            AutoSize = false, Height = 30, ForeColor = Color.Gray, Dock = DockStyle.Top
        };

        Dock = DockStyle.Top;
        AutoSize = true;
        // Orden inverso al visual.
        Controls.Add(_targetFilter);
        Controls.Add(stepButtons);
        Controls.Add(stepEditorLayout);
        Controls.Add(_stepsList);
        Controls.Add(stepsLabel);
        Controls.Add(targetLayout);
        Controls.Add(_requiresTargetCheck);
        Controls.Add(triggerLayout);
        Controls.Add(hint);

        _triggerCombo.Items.AddRange(Enum.GetNames<EffectTrigger>());
        _targetKindCombo.Items.AddRange(Enum.GetNames<EffectTargetKind>());
        _actionKindCombo.Items.AddRange(EffectActionCatalog.RegisteredKinds.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        RefreshTypes();

        WireEvents();
    }

    /// <summary>Repuebla la lista de Tipos disponible en el filtro de objetivo. Publico: <c>MainForm</c> lo llama al cerrar <see cref="TypeEditorForm"/>.</summary>
    public void RefreshTypes() => _targetFilter.SetAvailableTypes(_typeRepo.Types);

    private void WireEvents()
    {
        _triggerCombo.SelectedIndexChanged += (_, _) => RaiseChanged();
        _visualProfileKeyBox.TextChanged += (_, _) => RaiseChanged();
        _requiresTargetCheck.CheckedChanged += (_, _) => { UpdateVisibility(); RaiseChanged(); };
        _targetKindCombo.SelectedIndexChanged += (_, _) => RaiseChanged();
        _targetFilter.FilterChanged += (_, _) => RaiseChanged();
        _addStepButton.Click += (_, _) => OnAddStepClicked();
        _removeStepButton.Click += (_, _) => OnRemoveStepClicked();
        _moveStepUpButton.Click += (_, _) => OnMoveStepClicked(-1);
        _moveStepDownButton.Click += (_, _) => OnMoveStepClicked(1);
    }

    private void RaiseChanged()
    {
        if (!_suppressEvents) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateVisibility()
    {
        _targetKindCombo.Enabled = _requiresTargetCheck.Checked;
        _targetFilter.Enabled = _requiresTargetCheck.Checked;
    }

    private void OnAddStepClicked()
    {
        if (_actionKindCombo.SelectedItem is not string kind) return;
        _currentSteps.Add(new EffectActionStepDto { ActionKind = kind, ParamsText = _paramsBox.Text.Trim() });
        _paramsBox.Text = "";
        RefreshStepsList();
        RaiseChanged();
    }

    private void OnRemoveStepClicked()
    {
        if (_stepsList.SelectedIndex < 0) return;
        _currentSteps.RemoveAt(_stepsList.SelectedIndex);
        RefreshStepsList();
        RaiseChanged();
    }

    private void OnMoveStepClicked(int direction)
    {
        int i = _stepsList.SelectedIndex;
        int j = i + direction;
        if (i < 0 || j < 0 || j >= _currentSteps.Count) return;
        (_currentSteps[i], _currentSteps[j]) = (_currentSteps[j], _currentSteps[i]);
        RefreshStepsList();
        _stepsList.SelectedIndex = j;
        RaiseChanged();
    }

    private void RefreshStepsList()
    {
        _stepsList.Items.Clear();
        foreach (var step in _currentSteps)
            _stepsList.Items.Add(string.IsNullOrEmpty(step.ParamsText) ? step.ActionKind : $"{step.ActionKind} ({step.ParamsText})");
    }

    public void LoadFrom(CardDto dto)
    {
        _suppressEvents = true;

        _currentSteps = dto.EffectActionSteps.Select(CardDtoCloning.CloneStep).ToList();
        RefreshStepsList();
        EditorHelpers.SelectComboText(_triggerCombo, dto.EffectTrigger);
        _visualProfileKeyBox.Text = dto.EffectVisualProfileKey;
        _requiresTargetCheck.Checked = dto.EffectRequiresTarget;
        EditorHelpers.SelectComboText(_targetKindCombo, dto.EffectTargetKind);
        _targetFilter.SetFilter(dto.EffectTargetFilter);

        _suppressEvents = false;
        UpdateVisibility();
    }

    /// <summary>Escribe los campos del efecto compuesto en <paramref name="dto"/>. El llamador decide si corresponde invocarlo (ver <see cref="MainForm"/>).</summary>
    public void ApplyTo(CardDto dto)
    {
        dto.EffectTrigger = _triggerCombo.SelectedItem as string ?? nameof(EffectTrigger.Any);
        dto.EffectVisualProfileKey = _visualProfileKeyBox.Text.Trim();
        dto.EffectRequiresTarget = _requiresTargetCheck.Checked;
        dto.EffectTargetKind = _targetKindCombo.SelectedItem as string ?? nameof(EffectTargetKind.MonsterZone);
        dto.EffectTargetFilter = _targetFilter.GetFilter();
        dto.EffectActionSteps = _currentSteps.Select(CardDtoCloning.CloneStep).ToList();
    }
}
