using Godot;
using GodotGame.Core.Entities;
using GodotGame.Data.Loaders;
using GodotGame.Editor.Data;

namespace GodotGame.Editor.Controls;

/// <summary>
/// Panel de propiedades de una carta Trampa: SubType y Efecto. Adaptacion
/// Godot de <c>TrapPanel</c> de MonstersGame.CardEditor -- el mas simple de
/// los tres, sin sub-panel condicional propio.
/// </summary>
public sealed partial class TrapPanel : VBoxContainer
{
    private readonly OptionButton _subTypeCombo = new();
    private readonly OptionButton _effectCombo = new();

    private bool _suppressEvents;

    public event Action? Changed;

    public string SelectedEffectId
    {
        get
        {
            var options = EffectCatalog.Options;
            return _effectCombo.Selected >= 0 && _effectCombo.Selected < options.Count ? options[_effectCombo.Selected].Id : "";
        }
    }

    public TrapPanel()
    {
        var layout = EditorLayout.TwoColumnLayout();
        EditorLayout.AddRow(layout, "SubType", _subTypeCombo);
        EditorLayout.AddRow(layout, "Efecto", _effectCombo);
        AddChild(layout);

        foreach (string name in Enum.GetNames<TrapSubType>()) _subTypeCombo.AddItem(name);
        foreach (var option in EffectCatalog.Options) _effectCombo.AddItem(option.Label);

        _subTypeCombo.ItemSelected += _ => RaiseChanged();
        _effectCombo.ItemSelected += _ => RaiseChanged();
    }

    private void RaiseChanged()
    {
        if (!_suppressEvents) Changed?.Invoke();
    }

    public void LoadFrom(CardDto dto)
    {
        _suppressEvents = true;

        SelectComboText(_subTypeCombo, dto.SubType);
        string effectIdForCombo = dto.Kind == "Trap" && dto.ComposeCustomEffect ? EffectCatalog.ComposeNew.Id : dto.EffectId;
        SelectEffectOption(_effectCombo, dto.Kind == "Trap" ? effectIdForCombo : dto.EffectId);

        _suppressEvents = false;
    }

    public void ApplyTo(CardDto dto)
    {
        dto.SubType = _subTypeCombo.Selected >= 0 ? _subTypeCombo.GetItemText(_subTypeCombo.Selected) : "Normal";
        dto.EffectId = SelectedEffectId;
    }

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
}
