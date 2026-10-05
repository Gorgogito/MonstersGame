using MonstersGame.Core.Entities;
using MonstersGame.Data.Loaders;

namespace MonstersGame.CardEditor;

/// <summary>
/// Panel de propiedades de una carta Trampa: SubType y Efecto. Extraido de
/// <c>MainForm</c> (Fase 5, paso de refactor puro) — el mas simple de los
/// tres, sin sub-panel condicional propio.
/// </summary>
public sealed class TrapPanel : UserControl
{
    private readonly ComboBox _subTypeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _effectCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    private bool _suppressEvents;

    public event EventHandler? Changed;

    public string SelectedEffectId => EditorHelpers.SelectedEffectId(_effectCombo);

    public TrapPanel()
    {
        var layout = EditorLayout.TwoColumnLayout();
        EditorLayout.AddRow(layout, "SubType", _subTypeCombo);
        EditorLayout.AddRow(layout, "Efecto", _effectCombo);

        Dock = DockStyle.Top;
        AutoSize = true;
        Controls.Add(layout);

        _subTypeCombo.Items.AddRange(Enum.GetNames<TrapSubType>());
        _effectCombo.Items.AddRange(EffectCatalog.Options.Cast<object>().ToArray());

        _subTypeCombo.SelectedIndexChanged += (_, _) => RaiseChanged();
        _effectCombo.SelectedIndexChanged += (_, _) => RaiseChanged();
    }

    private void RaiseChanged()
    {
        if (!_suppressEvents) Changed?.Invoke(this, EventArgs.Empty);
    }

    public void LoadFrom(CardDto dto)
    {
        _suppressEvents = true;

        EditorHelpers.SelectComboText(_subTypeCombo, dto.SubType);
        string effectIdForCombo = dto.Kind == "Trap" && dto.ComposeCustomEffect ? EffectCatalog.ComposeNew.Id : dto.EffectId;
        EditorHelpers.SelectEffectOption(_effectCombo, dto.Kind == "Trap" ? effectIdForCombo : dto.EffectId);

        _suppressEvents = false;
    }

    public void ApplyTo(CardDto dto)
    {
        dto.SubType = _subTypeCombo.SelectedItem as string ?? "Normal";
        dto.EffectId = SelectedEffectId;
    }
}
