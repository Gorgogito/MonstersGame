using Godot;
using GodotGame.Core.Entities;
using GodotGame.Data.Loaders;

namespace GodotGame.Editor.Controls;

/// <summary>
/// Editor generico y reutilizable de un <see cref="FilterDto"/> (forma normal
/// disyuntiva: grupos OR de condiciones AND) -- embebido en Fusion, Ritual,
/// Equip, Campo (afectados/penalizados) y el compositor de efectos.
///
/// Adaptacion de la grilla <see cref="System.Windows.Forms.DataGridView"/> de
/// MonstersGame.CardEditor: Godot no tiene una grilla con un editor de celda
/// distinto por fila tan directo, asi que cada condicion es una fila real
/// (<see cref="HBoxContainer"/>) con controles nativos -- un <see cref="OptionButton"/>
/// para "Tipo" (Kind) que reemplaza dinamicamente el control de "Valor" segun
/// se elija (lista desplegable para Categoria/Atributo/Lado/Tipo con el
/// catalogo real, texto libre para SpecificCard, deshabilitado para Any) --
/// mismo comportamiento funcional, con un boton "X" por fila en vez de
/// "seleccionar fila + Quitar".
/// </summary>
public sealed partial class TargetFilterEditor : VBoxContainer
{
    private static readonly string[] Kinds = { "SpecificCard", "Type", "Category", "Attribute", "ControllerSide", "Any" };
    private static readonly string[] CategoryValues = Enum.GetNames<MonsterCategory>();
    private static readonly string[] AttributeValues = Enum.GetNames<MonsterAttribute>();
    private static readonly string[] ControllerSideValues = { "Owner", "Opponent" };

    private readonly VBoxContainer _rowsBox = new();
    private readonly List<FilterRow> _rows = new();
    private IReadOnlyList<string> _availableTypes = Array.Empty<string>();

    public event Action? FilterChanged;

    public TargetFilterEditor()
    {
        AddThemeConstantOverride("separation", 4);

        var buttons = new HBoxContainer();
        var addConditionButton = new Button { Text = "+ Condicion (Y)" };
        var addGroupButton = new Button { Text = "+ Grupo (O)" };
        addConditionButton.Pressed += () => AddCondition(newGroup: false);
        addGroupButton.Pressed += () => AddCondition(newGroup: true);
        buttons.AddChild(addConditionButton);
        buttons.AddChild(addGroupButton);

        AddChild(EditorLayout.Hint("Sin condiciones = cualquier Monstruo. Misma fila de \"Grupo\" = deben cumplirse todas (Y); grupos distintos = alcanza con uno (O)."));
        AddChild(_rowsBox);
        AddChild(buttons);
    }

    /// <summary>Catalogo real de Tipos de Monstruo para poblar el desplegable de Valor cuando el Kind de la fila es "Type".</summary>
    public void SetAvailableTypes(IReadOnlyList<string> types)
    {
        _availableTypes = types;
        foreach (var row in _rows) row.RefreshValueEditor(_availableTypes);
    }

    private int NextGroupIndex() => _rows.Count == 0 ? 0 : _rows.Max(r => r.Group) + 1;
    private int LastGroupIndex() => _rows.Count == 0 ? 0 : _rows.Max(r => r.Group);

    private void AddCondition(bool newGroup)
    {
        int group = newGroup ? NextGroupIndex() : LastGroupIndex();
        AddRow(group, "Type", false, "");
        FilterChanged?.Invoke();
    }

    private void AddRow(int group, string kind, bool negate, string value)
    {
        var row = new FilterRow(group, kind, negate, value, Kinds, CategoryValues, AttributeValues, ControllerSideValues, _availableTypes);
        row.Changed += () => FilterChanged?.Invoke();
        row.RemoveRequested += () => RemoveRow(row);
        _rows.Add(row);
        _rowsBox.AddChild(row.Root);
    }

    private void RemoveRow(FilterRow row)
    {
        _rows.Remove(row);
        row.Root.QueueFree();
        FilterChanged?.Invoke();
    }

    /// <summary>Lee todas las filas y arma el <see cref="FilterDto"/> resultante, renormalizando los numeros de Grupo a 0..N en el orden en que aparecen.</summary>
    public FilterDto GetFilter()
    {
        var dto = new FilterDto();
        var groupOrder = new List<int>();
        var byGroup = new Dictionary<int, List<FilterConditionDto>>();

        foreach (var row in _rows)
        {
            int group = row.Group;
            if (!byGroup.TryGetValue(group, out var list))
            {
                list = new List<FilterConditionDto>();
                byGroup[group] = list;
                groupOrder.Add(group);
            }
            list.Add(new FilterConditionDto { Kind = row.Kind, Negate = row.Negate, Value = row.Value });
        }

        foreach (int group in groupOrder)
            dto.OrGroups.Add(byGroup[group]);

        return dto;
    }

    /// <summary>Puebla las filas desde un <see cref="FilterDto"/> ya guardado (para reeditar). No dispara <see cref="FilterChanged"/>.</summary>
    public void SetFilter(FilterDto? filter)
    {
        foreach (var row in _rows) row.Root.QueueFree();
        _rows.Clear();

        if (filter == null) return;

        for (int g = 0; g < filter.OrGroups.Count; g++)
            foreach (var condition in filter.OrGroups[g])
                AddRow(g, condition.Kind, condition.Negate, condition.Value);
    }

    /// <summary>Una fila real de la grilla: Grupo (SpinBox) + Tipo (OptionButton) + Negar (CheckBox) + Valor (control dinamico segun Tipo) + Quitar.</summary>
    private sealed class FilterRow
    {
        private readonly string[] _kinds;
        private readonly string[] _categoryValues;
        private readonly string[] _attributeValues;
        private readonly string[] _controllerSideValues;
        private IReadOnlyList<string> _availableTypes;

        private readonly SpinBox _groupBox = new() { MinValue = 0, MaxValue = 99, CustomMinimumSize = new Vector2(56, 0) };
        private readonly OptionButton _kindButton = new() { CustomMinimumSize = new Vector2(140, 0) };
        private readonly CheckBox _negateBox = new() { Text = "NO" };
        private readonly Control _valueSlot;
        private Control _valueControl = null!;

        public HBoxContainer Root { get; } = new();

        public event Action? Changed;
        public event Action? RemoveRequested;

        public int Group => (int)_groupBox.Value;
        public string Kind => _kindButton.ItemCount > 0 ? _kindButton.GetItemText(_kindButton.Selected) : "Type";
        public bool Negate => _negateBox.ButtonPressed;
        public string Value { get; private set; } = "";

        public FilterRow(int group, string kind, bool negate, string value,
            string[] kinds, string[] categoryValues, string[] attributeValues, string[] controllerSideValues,
            IReadOnlyList<string> availableTypes)
        {
            _kinds = kinds;
            _categoryValues = categoryValues;
            _attributeValues = attributeValues;
            _controllerSideValues = controllerSideValues;
            _availableTypes = availableTypes;
            Value = value;

            Root.AddThemeConstantOverride("separation", 6);

            _groupBox.Value = group;
            _groupBox.ValueChanged += _ => Changed?.Invoke();

            foreach (string k in _kinds) _kindButton.AddItem(k);
            int idx = Array.IndexOf(_kinds, kind);
            _kindButton.Selected = idx >= 0 ? idx : 0;
            _kindButton.ItemSelected += _ => { RefreshValueEditor(_availableTypes); Changed?.Invoke(); };

            _negateBox.ButtonPressed = negate;
            _negateBox.Toggled += _ => Changed?.Invoke();

            _valueSlot = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var removeButton = new Button { Text = "X", CustomMinimumSize = new Vector2(28, 0) };
            removeButton.Pressed += () => RemoveRequested?.Invoke();

            Root.AddChild(new Label { Text = "Grupo", VerticalAlignment = VerticalAlignment.Center });
            Root.AddChild(_groupBox);
            Root.AddChild(_kindButton);
            Root.AddChild(_negateBox);
            Root.AddChild(_valueSlot);
            Root.AddChild(removeButton);

            RefreshValueEditor(availableTypes);
        }

        /// <summary>
        /// Reemplaza el control de Valor por el que corresponde al Kind actual:
        /// lista desplegable para Categoria/Atributo/Lado (valores fijos) y
        /// Tipo (catalogo real), sin edicion para Any, y texto libre para
        /// SpecificCard (Id numerico) o si el catalogo de Tipos todavia esta vacio.
        /// </summary>
        public void RefreshValueEditor(IReadOnlyList<string> availableTypes)
        {
            _availableTypes = availableTypes;
            foreach (var child in _valueSlot.GetChildren().ToList()) child.QueueFree();

            string kind = Kind;
            if (kind == "Any")
            {
                Value = "";
                _valueControl = new Label { Text = "(sin valor)", Modulate = new Color(1, 1, 1, 0.5f) };
                _valueSlot.AddChild(_valueControl);
                return;
            }

            IReadOnlyList<string>? options = kind switch
            {
                "Type" when availableTypes.Count > 0 => availableTypes,
                "Category" => _categoryValues,
                "Attribute" => _attributeValues,
                "ControllerSide" => _controllerSideValues,
                _ => null
            };

            if (options == null)
            {
                var lineEdit = new LineEdit { Text = Value, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, PlaceholderText = "Id numerico de carta" };
                lineEdit.TextChanged += t => { Value = t; Changed?.Invoke(); };
                _valueControl = lineEdit;
                _valueSlot.AddChild(_valueControl);
                return;
            }

            var optionButton = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            for (int i = 0; i < options.Count; i++) optionButton.AddItem(options[i]);
            int selectedIdx = options.ToList().IndexOf(Value);
            optionButton.Selected = selectedIdx >= 0 ? selectedIdx : 0;
            Value = options[optionButton.Selected];
            optionButton.ItemSelected += i => { Value = options[(int)i]; Changed?.Invoke(); };
            _valueControl = optionButton;
            _valueSlot.AddChild(_valueControl);
        }
    }
}
