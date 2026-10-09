using System.Text.Json;
using Godot;
using GodotGame.Core.Effects.Monster;
using GodotGame.Core.Entities;
using GodotGame.Data.Loaders;

namespace GodotGame.Editor.Controls;

/// <summary>Fuentes de datos que necesitan los formularios de efectos (Tipos de monstruo y cartas del catalogo).</summary>
public sealed class EffectEditorContext
{
    public required Func<IReadOnlyList<string>> Types { get; init; }
    public required Func<IReadOnlyList<CardDto>> Cards { get; init; }

    public string? CardName(int id) => Cards().FirstOrDefault(c => c.Id == id)?.Name;
}

internal static class EffectEditorUi
{
    public static Label Header(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", new Color(0.95f, 0.8f, 0.45f));
        return label;
    }

    public static Label Description(string text) => new()
    {
        Text = text,
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
        Modulate = new Color(1, 1, 1, 0.65f),
        CustomMinimumSize = new Vector2(200, 0)
    };

    public static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    public static PanelContainer Box(Control content, float alpha = 0.04f)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(1, 1, 1, alpha),
            BorderColor = new Color(1, 1, 1, 0.15f),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 6, ContentMarginBottom = 6
        });
        panel.AddChild(content);
        return panel;
    }

    /// <summary>Desplegable que NO se ensancha al item mas largo (nombres de carta largos desbordarian el panel).</summary>
    public static OptionButton Combo() => new()
    {
        FitToLongestItem = false,
        ClipText = true,
        TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        CustomMinimumSize = new Vector2(120, 0)
    };

    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}

/// <summary>
/// Formulario generado a partir de las descripciones de parametros de
/// <see cref="MonsterEffectCatalog"/>: numeros, casillas, listas, zonas, una
/// carta del catalogo, un Tipo o un Atributo. Edita en el sitio el
/// diccionario de valores que recibe.
/// </summary>
internal sealed partial class ParamForm : GridContainer
{
    private readonly EffectEditorContext _context;
    public event Action? Changed;

    public ParamForm(EffectEditorContext context)
    {
        _context = context;
        Columns = 2;
        AddThemeConstantOverride("h_separation", 10);
        AddThemeConstantOverride("v_separation", 4);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    public void Build(IReadOnlyList<ParamInfo> infos, Dictionary<string, string> values)
    {
        EffectEditorUi.Clear(this);
        foreach (var info in infos)
        {
            if (!values.ContainsKey(info.Key)) values[info.Key] = info.Default;
            var label = new Label
            {
                Text = info.Label, CustomMinimumSize = new Vector2(170, 0), VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart, TooltipText = info.Help, MouseFilter = MouseFilterEnum.Pass
            };
            if (info.Help.Length > 0) label.Text += " (?)";
            AddChild(label);
            var control = BuildControl(info, values);
            control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            control.TooltipText = info.Help;
            AddChild(control);
        }
    }

    private void Set(Dictionary<string, string> values, string key, string value)
    {
        values[key] = value;
        Changed?.Invoke();
    }

    private Control BuildControl(ParamInfo info, Dictionary<string, string> values)
    {
        string current = values[info.Key];
        switch (info.Type)
        {
            case ParamType.Int:
            {
                var spin = new SpinBox { MinValue = -99999, MaxValue = 99999, Step = 1, Value = int.TryParse(current, out int n) ? n : 0 };
                spin.ValueChanged += v => Set(values, info.Key, ((int)v).ToString());
                return spin;
            }
            case ParamType.Bool:
            {
                var check = new CheckBox { ButtonPressed = current == "true", Text = "Sí" };
                check.Toggled += on => Set(values, info.Key, on ? "true" : "false");
                return check;
            }
            case ParamType.Zones:
            {
                var flow = new HFlowContainer();
                var selected = CardQuery.ParseZones(current).Select(z => z.ToString()).ToHashSet();
                foreach (var option in info.Options ?? Array.Empty<ParamOption>())
                {
                    var check = new CheckBox { Text = option.Label, ButtonPressed = selected.Contains(option.Value) };
                    check.Toggled += on =>
                    {
                        if (on) selected.Add(option.Value); else selected.Remove(option.Value);
                        var ordered = (info.Options ?? Array.Empty<ParamOption>()).Select(o => o.Value).Where(selected.Contains);
                        Set(values, info.Key, string.Join(",", ordered));
                    };
                    flow.AddChild(check);
                }
                return flow;
            }
            case ParamType.Card:
            {
                var combo = EffectEditorUi.Combo();
                combo.AddItem("(ninguna: cualquier carta)", 0);
                foreach (var card in _context.Cards().OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase))
                    combo.AddItem($"{card.Name}  (#{card.Id})", card.Id);
                int.TryParse(current, out int id);
                int index = combo.GetItemIndex(id);
                combo.Selected = index >= 0 ? index : 0;
                combo.ItemSelected += i => Set(values, info.Key, combo.GetItemId((int)i).ToString());
                return combo;
            }
            case ParamType.MonsterType:
                return TextCombo(values, info.Key, current, "(cualquiera)", _context.Types());
            case ParamType.Attribute:
                return TextCombo(values, info.Key, current, "(cualquiera)", Enum.GetNames<MonsterAttribute>());
            default:
            {
                var combo = EffectEditorUi.Combo();
                var options = info.Options ?? Array.Empty<ParamOption>();
                for (int i = 0; i < options.Count; i++) combo.AddItem(options[i].Label, i);
                int index = options.ToList().FindIndex(o => o.Value == current);
                combo.Selected = index >= 0 ? index : 0;
                if (index < 0 && options.Count > 0) values[info.Key] = options[0].Value;
                combo.ItemSelected += i => Set(values, info.Key, options[(int)i].Value);
                return combo;
            }
        }
    }

    private OptionButton TextCombo(Dictionary<string, string> values, string key, string current, string emptyLabel, IEnumerable<string> items)
    {
        var combo = EffectEditorUi.Combo();
        var list = items.ToList();
        combo.AddItem(emptyLabel);
        foreach (var item in list) combo.AddItem(item);
        int index = list.FindIndex(t => string.Equals(t, current, StringComparison.OrdinalIgnoreCase));
        combo.Selected = index >= 0 ? index + 1 : 0;
        combo.ItemSelected += i => Set(values, key, i == 0 ? "" : list[(int)i - 1]);
        return combo;
    }
}

/// <summary>Lista editable de condiciones ("si ...", con su "Negar" y parametros).</summary>
internal sealed partial class ConditionListEditor : VBoxContainer
{
    private readonly EffectEditorContext _context;
    private readonly string _title;
    private readonly string _hint;
    private List<EffectConditionDto> _conditions = new();
    public event Action? Changed;

    public ConditionListEditor(EffectEditorContext context, string title, string hint)
    {
        _context = context;
        _title = title;
        _hint = hint;
        AddThemeConstantOverride("separation", 4);
    }

    public void Bind(List<EffectConditionDto> conditions)
    {
        _conditions = conditions;
        Rebuild();
    }

    private void Rebuild()
    {
        EffectEditorUi.Clear(this);
        AddChild(EffectEditorUi.Header(_title));
        if (_hint.Length > 0) AddChild(EffectEditorUi.Description(_hint));

        for (int i = 0; i < _conditions.Count; i++)
        {
            int captured = i;
            var condition = _conditions[i];
            var column = new VBoxContainer();

            var row = new HBoxContainer();
            var kind = EffectEditorUi.Combo();
            var all = MonsterEffectCatalog.Conditions;
            for (int k = 0; k < all.Count; k++) kind.AddItem(all[k].Label, k);
            int selected = all.ToList().FindIndex(c => c.Kind == condition.Kind);
            kind.Selected = Math.Max(0, selected);
            if (selected < 0 && all.Count > 0) condition.Kind = all[0].Kind;
            kind.ItemSelected += k =>
            {
                condition.Kind = all[(int)k].Kind;
                condition.Params.Clear();
                Rebuild();
                Changed?.Invoke();
            };
            var negate = new CheckBox { Text = "Negar (NO)", ButtonPressed = condition.Negate };
            negate.Toggled += on => { condition.Negate = on; Changed?.Invoke(); };
            var remove = new Button { Text = "Quitar" };
            remove.Pressed += () => { _conditions.RemoveAt(captured); Rebuild(); Changed?.Invoke(); };
            row.AddChild(kind);
            row.AddChild(negate);
            row.AddChild(remove);
            column.AddChild(row);

            var info = MonsterEffectCatalog.Condition(condition.Kind);
            if (info != null && info.Description.Length > 0) column.AddChild(EffectEditorUi.Description(info.Description));
            if (info != null && info.Params.Count > 0)
            {
                var form = new ParamForm(_context);
                form.Build(info.Params, condition.Params);
                form.Changed += () => Changed?.Invoke();
                column.AddChild(form);
            }
            AddChild(EffectEditorUi.Box(column));
        }

        var add = new Button { Text = "+ Agregar condición", SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        add.Pressed += () =>
        {
            _conditions.Add(new EffectConditionDto { Kind = MonsterEffectCatalog.Conditions[0].Kind });
            Rebuild();
            Changed?.Invoke();
        };
        AddChild(add);
    }
}

/// <summary>Lista ordenada de pasos (costos o acciones) con su editor de detalle.</summary>
internal sealed partial class StepListEditor : VBoxContainer
{
    private readonly EffectEditorContext _context;
    private readonly ItemList _list = new() { CustomMinimumSize = new Vector2(0, 74), SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly VBoxContainer _detail = new();
    private readonly Label _title;
    private readonly Label _hint;
    private List<MonsterEffectStepDto> _steps = new();
    private StepUsage _usage = StepUsage.Action;
    private int _selected = -1;
    public event Action? Changed;

    public StepListEditor(EffectEditorContext context, string title, string hint)
    {
        _context = context;
        AddThemeConstantOverride("separation", 4);
        _title = EffectEditorUi.Header(title);
        _hint = EffectEditorUi.Description(hint);
        AddChild(_title);
        AddChild(_hint);
        AddChild(_list);

        var buttons = new HBoxContainer();
        foreach (var (text, action) in new (string, Action)[]
        {
            ("+ Agregar paso", OnAdd), ("Quitar", OnRemove), ("Subir", () => Move(-1)), ("Bajar", () => Move(1))
        })
        {
            var button = new Button { Text = text };
            button.Pressed += action;
            buttons.AddChild(button);
        }
        AddChild(buttons);
        _detail.AddThemeConstantOverride("separation", 4);
        AddChild(_detail);

        _list.ItemSelected += i => { _selected = (int)i; BuildDetail(); };
    }

    public void Bind(List<MonsterEffectStepDto> steps, StepUsage usage, string? title = null, string? hint = null)
    {
        _steps = steps;
        _usage = usage;
        if (title != null) _title.Text = title;
        if (hint != null) _hint.Text = hint;
        _selected = _steps.Count > 0 ? 0 : -1;
        RefreshList();
        BuildDetail();
    }

    private IReadOnlyList<StepInfo> Available => MonsterEffectCatalog.Steps.Where(s => (s.Usage & _usage) != 0).ToList();

    private void RefreshList()
    {
        _list.Clear();
        for (int i = 0; i < _steps.Count; i++)
        {
            string summary = $"{i + 1}. {MonsterEffectMapper.StepSummary(_steps[i], _context.CardName)}";
            _list.AddItem(summary);
            _list.SetItemTooltip(i, summary);
        }
        if (_selected >= 0 && _selected < _steps.Count) _list.Select(_selected);
    }

    private void RaiseChanged()
    {
        RefreshList();
        Changed?.Invoke();
    }

    private void OnAdd()
    {
        var first = Available.FirstOrDefault();
        if (first == null) return;
        _steps.Add(new MonsterEffectStepDto { ActionKind = first.Kind });
        _selected = _steps.Count - 1;
        RaiseChanged();
        BuildDetail();
    }

    private void OnRemove()
    {
        if (_selected < 0 || _selected >= _steps.Count) return;
        _steps.RemoveAt(_selected);
        _selected = Math.Min(_selected, _steps.Count - 1);
        RaiseChanged();
        BuildDetail();
    }

    private void Move(int direction)
    {
        int j = _selected + direction;
        if (_selected < 0 || j < 0 || j >= _steps.Count) return;
        (_steps[_selected], _steps[j]) = (_steps[j], _steps[_selected]);
        _selected = j;
        RaiseChanged();
        BuildDetail();
    }

    private void BuildDetail()
    {
        EffectEditorUi.Clear(_detail);
        if (_selected < 0 || _selected >= _steps.Count) return;
        var step = _steps[_selected];
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);

        var available = Available;
        var kind = EffectEditorUi.Combo();
        for (int i = 0; i < available.Count; i++) kind.AddItem(available[i].Label, i);
        int index = available.ToList().FindIndex(s => s.Kind == step.ActionKind);
        kind.Selected = Math.Max(0, index);
        if (index < 0 && available.Count > 0) step.ActionKind = available[0].Kind;
        kind.ItemSelected += i =>
        {
            step.ActionKind = available[(int)i].Kind;
            step.Params.Clear();
            RaiseChanged();
            BuildDetail();
        };
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = $"Paso {_selected + 1}:", VerticalAlignment = VerticalAlignment.Center });
        row.AddChild(kind);
        column.AddChild(row);

        var info = MonsterEffectCatalog.Step(step.ActionKind);
        if (info != null) column.AddChild(EffectEditorUi.Description(info.Description));

        if (_usage == StepUsage.Action)
        {
            var optional = new CheckBox { Text = "Opcional: \"puedes ...\" (se pregunta al resolverse)", ButtonPressed = step.Optional };
            optional.Toggled += on => { step.Optional = on; RaiseChanged(); };
            column.AddChild(optional);
        }

        if (info != null && info.Params.Count > 0)
        {
            var form = new ParamForm(_context);
            form.Build(info.Params, step.Params);
            form.Changed += RaiseChanged;
            column.AddChild(form);
            RefreshList();
        }

        if (_usage != StepUsage.Continuous)
        {
            var conditions = new ConditionListEditor(_context, "Solo si... (\"y después, si ...\")",
                "Todas deben cumplirse para hacer este paso. Ej.: \"si fue descartada por efecto de una carta del adversario\", \"si lo haces\" (el paso anterior se realizó).");
            conditions.Bind(step.Conditions);
            conditions.Changed += RaiseChanged;
            column.AddChild(conditions);
        }

        _detail.AddChild(EffectEditorUi.Box(column, 0.06f));
    }
}

/// <summary>
/// Editor de los efectos de una carta de Monstruo: lista de efectos y, para
/// el elegido, su tipo (con la explicacion de cada clasificacion), cuando o
/// desde donde se activa, condiciones, objetivos, costos y pasos. Todo se
/// arma a partir de <see cref="MonsterEffectCatalog"/>, asi que una accion o
/// condicion nueva del motor aparece aqui sin tocar este control.
/// </summary>
public sealed partial class MonsterEffectsEditor : VBoxContainer
{
    private readonly EffectEditorContext _context;
    private readonly ItemList _list = new() { CustomMinimumSize = new Vector2(0, 90), SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly VBoxContainer _detail = new();
    private List<MonsterEffectDto> _effects = new();
    private int _selected = -1;
    private bool _building;

    public event Action? Changed;

    public MonsterEffectsEditor(EffectEditorContext context)
    {
        _context = context;
        AddThemeConstantOverride("separation", 6);

        AddChild(EditorLayout.Hint("Cada efecto se clasifica como Continuo, de Encendido, Disparado, Rápido, de Volteo o No clasificado. Un monstruo puede tener varios. Elige uno de la lista para editarlo."));
        AddChild(_list);

        var buttons = new HBoxContainer();
        foreach (var (text, action) in new (string, Action)[]
        {
            ("+ Agregar efecto", OnAdd), ("Duplicar", OnDuplicate), ("Quitar", OnRemove), ("Subir", () => Move(-1)), ("Bajar", () => Move(1))
        })
        {
            var button = new Button { Text = text };
            button.Pressed += action;
            buttons.AddChild(button);
        }
        AddChild(buttons);
        _detail.AddThemeConstantOverride("separation", 8);
        AddChild(_detail);

        _list.ItemSelected += i => { _selected = (int)i; BuildDetail(); };
    }

    public int Count => _effects.Count;

    public void LoadFrom(IEnumerable<MonsterEffectDto> effects)
    {
        _effects = effects.Select(EffectEditorUi.Clone).ToList();
        _selected = _effects.Count > 0 ? 0 : -1;
        RefreshList();
        BuildDetail();
    }

    public List<MonsterEffectDto> GetEffects() => _effects.Select(EffectEditorUi.Clone).ToList();

    private void RefreshList()
    {
        _list.Clear();
        for (int i = 0; i < _effects.Count; i++)
        {
            string summary = $"{i + 1}. {MonsterEffectMapper.Summary(_effects[i], _context.CardName)}";
            _list.AddItem(summary);
            _list.SetItemTooltip(i, summary);
        }
        if (_selected >= 0 && _selected < _effects.Count) _list.Select(_selected);
    }

    private void RaiseChanged()
    {
        if (_building) return;
        RefreshList();
        Changed?.Invoke();
    }

    private void OnAdd()
    {
        _effects.Add(new MonsterEffectDto { Type = nameof(MonsterEffectType.Ignition), ActivationZone = nameof(EffectZone.Field) });
        _selected = _effects.Count - 1;
        RaiseChanged();
        BuildDetail();
    }

    private void OnDuplicate()
    {
        if (_selected < 0) return;
        _effects.Insert(_selected + 1, EffectEditorUi.Clone(_effects[_selected]));
        _selected++;
        RaiseChanged();
        BuildDetail();
    }

    private void OnRemove()
    {
        if (_selected < 0) return;
        _effects.RemoveAt(_selected);
        _selected = Math.Min(_selected, _effects.Count - 1);
        RaiseChanged();
        BuildDetail();
    }

    private void Move(int direction)
    {
        int j = _selected + direction;
        if (_selected < 0 || j < 0 || j >= _effects.Count) return;
        (_effects[_selected], _effects[j]) = (_effects[j], _effects[_selected]);
        _selected = j;
        RaiseChanged();
        BuildDetail();
    }

    private void BuildDetail()
    {
        EffectEditorUi.Clear(_detail);
        if (_selected < 0 || _selected >= _effects.Count)
        {
            _detail.AddChild(EditorLayout.Hint("Sin efectos. Presiona \"+ Agregar efecto\" para crear uno."));
            return;
        }

        _building = true;
        var effect = _effects[_selected];
        var type = CardDtoMapper.ParseEnum(effect.Type, MonsterEffectType.Ignition);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);

        // ---- Tipo
        var grid = EditorLayout.TwoColumnLayout();
        var typeCombo = EffectEditorUi.Combo();
        foreach (var t in MonsterEffectCatalog.Types) typeCombo.AddItem(t.Label, (int)t.Value);
        typeCombo.Selected = typeCombo.GetItemIndex((int)type);
        typeCombo.ItemSelected += i =>
        {
            var newType = (MonsterEffectType)typeCombo.GetItemId((int)i);
            effect.Type = newType.ToString();
            if (newType == MonsterEffectType.Flip) effect.TriggerEvent = nameof(EffectEvent.Flipped);
            else if (newType == MonsterEffectType.Trigger && effect.TriggerEvent is "None" or "Flipped") effect.TriggerEvent = nameof(EffectEvent.DiscardedByCardEffect);
            if (newType == MonsterEffectType.Continuous) { effect.Costs.Clear(); effect.HasTarget = false; }
            // Los pasos de un Continuo son pasivos: al cambiar de/hacia Continuo los pasos dejan de servir.
            bool wasContinuous = type == MonsterEffectType.Continuous;
            if (wasContinuous != (newType == MonsterEffectType.Continuous)) effect.Steps.Clear();
            RaiseChanged();
            BuildDetail();
        };
        EditorLayout.AddRow(grid, "Tipo de efecto", typeCombo);
        root.AddChild(grid);
        root.AddChild(EffectEditorUi.Description(MonsterEffectCatalog.Types.First(t => t.Value == type).Description));

        var when = EditorLayout.TwoColumnLayout();
        if (type == MonsterEffectType.Trigger)
        {
            var eventCombo = EffectEditorUi.Combo();
            var events = MonsterEffectCatalog.Events.Where(e => e.Value != EffectEvent.Flipped).ToList();
            for (int i = 0; i < events.Count; i++) eventCombo.AddItem(events[i].Label, (int)events[i].Value);
            var current = CardDtoMapper.ParseEnum(effect.TriggerEvent, EffectEvent.None);
            int idx = eventCombo.GetItemIndex((int)current);
            eventCombo.Selected = idx >= 0 ? idx : 0;
            if (idx < 0) effect.TriggerEvent = events[0].Value.ToString();
            eventCombo.ItemSelected += i => { effect.TriggerEvent = ((EffectEvent)eventCombo.GetItemId((int)i)).ToString(); RaiseChanged(); BuildDetail(); };
            EditorLayout.AddRow(when, "Se activa cuando esta carta...", eventCombo);

            var optional = new CheckBox { Text = "Opcional (\"puedes ...\")", TooltipText = "Se le pregunta al jugador si quiere activarlo.", ButtonPressed = effect.Optional };
            optional.Toggled += on => { effect.Optional = on; RaiseChanged(); };
            EditorLayout.AddRow(when, "", optional);
        }
        else if (type is MonsterEffectType.Ignition or MonsterEffectType.Quick or MonsterEffectType.Unclassified)
        {
            var zoneCombo = EffectEditorUi.Combo();
            foreach (var z in MonsterEffectCatalog.Zones) zoneCombo.AddItem(z.Label, (int)z.Value);
            var zone = CardDtoMapper.ParseEnum(effect.ActivationZone, EffectZone.Field);
            zoneCombo.Selected = zoneCombo.GetItemIndex((int)zone);
            zoneCombo.ItemSelected += i => { effect.ActivationZone = ((EffectZone)zoneCombo.GetItemId((int)i)).ToString(); RaiseChanged(); BuildDetail(); };
            EditorLayout.AddRow(when, "Se activa desde", zoneCombo);
        }
        if (type != MonsterEffectType.Continuous)
        {
            var opt = new CheckBox { Text = "Solo una vez por turno (por nombre de carta)", ButtonPressed = effect.OncePerTurn };
            opt.Toggled += on => { effect.OncePerTurn = on; RaiseChanged(); };
            EditorLayout.AddRow(when, "Límite", opt);
        }
        if (when.GetChildCount() > 0) root.AddChild(when);

        if (type == MonsterEffectType.Trigger)
            root.AddChild(EffectEditorUi.Description(MonsterEffectCatalog.Events.First(e => e.Value == CardDtoMapper.ParseEnum(effect.TriggerEvent, EffectEvent.None)).Description));
        else if (type is MonsterEffectType.Ignition or MonsterEffectType.Quick or MonsterEffectType.Unclassified)
            root.AddChild(EffectEditorUi.Description(MonsterEffectCatalog.Zones.First(z => z.Value == CardDtoMapper.ParseEnum(effect.ActivationZone, EffectZone.Field)).Description));

        // ---- Texto
        var text = new TextEdit { Text = effect.Text, CustomMinimumSize = new Vector2(0, 54), WrapMode = TextEdit.LineWrappingMode.Boundary, PlaceholderText = "Texto del efecto (opcional): se muestra al activarlo." };
        text.TextChanged += () => { effect.Text = text.Text; RaiseChanged(); };
        root.AddChild(text);

        // ---- Condiciones de activacion
        var conditions = new ConditionListEditor(_context, "Condiciones para activarlo",
            type == MonsterEffectType.Continuous
                ? "Opcional. El efecto solo se aplica mientras se cumplan (ej. \"durante tu turno\")."
                : "Opcional. Si no se cumplen, el efecto no se puede activar (ej. \"si fue descartada por efecto de una carta del adversario\").");
        conditions.Bind(effect.ActivationConditions);
        conditions.Changed += RaiseChanged;
        root.AddChild(EffectEditorUi.Box(conditions));

        if (type != MonsterEffectType.Continuous)
        {
            // ---- Objetivos
            var targetBox = new VBoxContainer();
            targetBox.AddChild(EffectEditorUi.Header("Objetivos (\"selecciona ...\")"));
            targetBox.AddChild(EffectEditorUi.Description("Se eligen al activar el efecto. Los pasos con \"Usar los objetivos seleccionados\" actúan sobre ellos (si siguen ahí al resolverse)."));
            var hasTarget = new CheckBox { Text = "Este efecto selecciona objetivo(s) al activarse", ButtonPressed = effect.HasTarget };
            targetBox.AddChild(hasTarget);
            var targetForm = new ParamForm(_context) { Visible = effect.HasTarget };
            targetForm.Build(MonsterEffectCatalog.TargetParams, effect.TargetParams);
            targetForm.Changed += RaiseChanged;
            targetBox.AddChild(targetForm);
            hasTarget.Toggled += on => { effect.HasTarget = on; targetForm.Visible = on; RaiseChanged(); };
            root.AddChild(EffectEditorUi.Box(targetBox));

            // ---- Costos
            var costs = new StepListEditor(_context, "Costos (se pagan al activar)",
                "Ej.: \"descarta esta carta\", \"muestra esta carta\", \"devuelve a la mano 1 monstruo que controles\", \"paga 500 LP\". Pagar un costo NO cuenta como \"por efecto de una carta\".");
            costs.Bind(effect.Costs, StepUsage.Cost);
            costs.Changed += RaiseChanged;
            root.AddChild(EffectEditorUi.Box(costs));
        }

        // ---- Pasos
        var steps = new StepListEditor(_context,
            type == MonsterEffectType.Continuous ? "Efecto pasivo" : "Qué hace (pasos en orden)",
            type == MonsterEffectType.Continuous
                ? "Se aplica mientras esta carta esté boca arriba en el Campo."
                : "Se ejecutan en orden al resolverse. Usa \"Solo si...\" para \"y después, si ...\" y \"Opcional\" para \"puedes ...\".");
        steps.Bind(effect.Steps, type == MonsterEffectType.Continuous ? StepUsage.Continuous : StepUsage.Action);
        steps.Changed += RaiseChanged;
        root.AddChild(EffectEditorUi.Box(steps));

        _detail.AddChild(root);
        _building = false;
        RefreshList();
    }
}
