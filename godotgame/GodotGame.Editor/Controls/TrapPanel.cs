using Godot;
using GodotGame.Core.Effects.Monster;
using GodotGame.Core.Entities;
using GodotGame.Data.Loaders;
using GodotGame.Editor.Data;

namespace GodotGame.Editor.Controls;

/// <summary>
/// Panel de propiedades de una carta Trampa: subtipo (con su explicacion), el
/// efecto heredado y los efectos por datos de la carta.
/// </summary>
public sealed partial class TrapPanel : VBoxContainer
{
    private readonly OptionButton _subTypeCombo = new();
    private readonly Label _subTypeHelp = EditorLayout.Hint("");
    private readonly OptionButton _effectCombo = new() { FitToLongestItem = false, ClipText = true };
    private readonly MonsterEffectsEditor _effectsEditor;

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

    public TrapPanel(EffectEditorContext effectContext)
    {
        AddThemeConstantOverride("separation", 8);
        _effectsEditor = new MonsterEffectsEditor(effectContext, CardKind.Trap);

        var layout = EditorLayout.TwoColumnLayout();
        EditorLayout.AddRow(layout, "Subtipo", _subTypeCombo);
        EditorLayout.AddRow(layout, "Efecto heredado (antiguo)", _effectCombo);
        AddChild(layout);
        _subTypeHelp.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        AddChild(_subTypeHelp);

        var effectsSection = EditorLayout.Section("Efectos de la carta", out var effectsBody);
        effectsBody.AddChild(_effectsEditor);
        AddChild(effectsSection);

        foreach (var subType in SpellTrapCatalog.TrapSubTypes)
        {
            _subTypeCombo.AddItem(subType.Label);
            _subTypeCombo.SetItemMetadata(_subTypeCombo.ItemCount - 1, subType.Value.ToString());
        }
        foreach (var option in EffectCatalog.Options) _effectCombo.AddItem(option.Label);

        _subTypeCombo.ItemSelected += _ => { UpdateHelp(); RaiseChanged(); };
        _effectCombo.ItemSelected += _ => RaiseChanged();
        _effectsEditor.Changed += RaiseChanged;
    }

    private string SelectedSubType() =>
        _subTypeCombo.Selected >= 0 ? _subTypeCombo.GetItemMetadata(_subTypeCombo.Selected).AsString() : nameof(TrapSubType.Normal);

    private void UpdateHelp() =>
        _subTypeHelp.Text = SpellTrapCatalog.TrapSubTypes.FirstOrDefault(s => s.Value.ToString() == SelectedSubType())?.Description ?? "";

    private void RaiseChanged()
    {
        if (!_suppressEvents) Changed?.Invoke();
    }

    public void LoadFrom(CardDto dto)
    {
        _suppressEvents = true;

        _subTypeCombo.Selected = 0;
        for (int i = 0; i < _subTypeCombo.ItemCount; i++)
            if (string.Equals(_subTypeCombo.GetItemMetadata(i).AsString(), dto.SubType, StringComparison.OrdinalIgnoreCase)) _subTypeCombo.Selected = i;
        string effectIdForCombo = dto.Kind == "Trap" && dto.ComposeCustomEffect ? EffectCatalog.ComposeNew.Id : dto.EffectId;
        SelectEffectOption(_effectCombo, dto.Kind == "Trap" ? effectIdForCombo : dto.EffectId);
        _effectsEditor.LoadFrom(dto.Kind == "Trap" ? dto.MonsterEffects : new List<MonsterEffectDto>());
        UpdateHelp();

        _suppressEvents = false;
    }

    public void ApplyTo(CardDto dto)
    {
        dto.SubType = SelectedSubType();
        dto.EffectId = SelectedEffectId;
        dto.MonsterEffects = _effectsEditor.GetEffects();
    }

    private static void SelectEffectOption(OptionButton combo, string effectId)
    {
        var options = EffectCatalog.Options;
        for (int i = 0; i < options.Count; i++)
            if (options[i].Id == effectId) { combo.Selected = i; return; }
        combo.Selected = options.Count > 0 ? 0 : -1;
    }
}
