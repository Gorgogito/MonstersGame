using MonstersGame.Core.Entities;
using MonstersGame.Data.Loaders;

namespace MonstersGame.CardEditor;

/// <summary>
/// Editor generico y reutilizable de un <see cref="FilterDto"/> (forma normal
/// disyuntiva: grupos OR de condiciones AND) -- la pieza de mayor
/// apalancamiento de la Fase 5, embebida en Fusion, Ritual, Equip, Field y el
/// compositor de efectos. Cada fila de la grilla es una condicion; el numero
/// de Grupo la agrupa (mismo numero = AND entre si; numeros distintos = OR
/// entre grupos), sin ningun modelo intermedio: se lee/escribe directo desde
/// la grilla.
///
/// La columna "Valor" cambia de editor segun el "Tipo" (Kind) elegido en esa
/// misma fila (ver <see cref="ApplyValueEditor"/>): antes era un cuadro de
/// texto libre para cualquier Kind, obligando a adivinar que string exacto
/// esperaba el motor (p. ej. "Dragon" para Tipo, "Fusion" para Categoria,
/// "owner"/"opponent" para Lado) -- ahora se convierte en una lista
/// desplegable con los valores validos para Categoria/Atributo/Lado, y con
/// el catalogo real de Tipos (<see cref="SetAvailableTypes"/>) para Tipo.
/// SpecificCard sigue siendo texto libre (Id numerico de carta) y Any no
/// necesita ningun valor.
/// </summary>
public sealed class TargetFilterEditorControl : UserControl
{
    private static readonly string[] Kinds = { "SpecificCard", "Type", "Category", "Attribute", "ControllerSide", "Any" };
    private static readonly string[] CategoryValues = Enum.GetNames<MonsterCategory>();
    private static readonly string[] AttributeValues = Enum.GetNames<MonsterAttribute>();
    private static readonly string[] ControllerSideValues = { "Owner", "Opponent" };

    private const int KindColumnIndex = 1;
    private const int ValueColumnIndex = 3;

    private IReadOnlyList<string> _availableTypes = Array.Empty<string>();

    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        Height = 120
    };
    private readonly Button _addConditionButton = new() { Text = "+ Condicion (Y)" };
    private readonly Button _addGroupButton = new() { Text = "+ Grupo (O)" };
    private readonly Button _removeButton = new() { Text = "Quitar seleccionada" };
    private readonly Label _hintLabel = new()
    {
        Text = "Sin condiciones = cualquier Monstruo. Misma fila de \"Grupo\" = deben cumplirse todas (Y); grupos distintos = alcanza con uno (O).",
        AutoSize = false,
        Height = 28,
        ForeColor = Color.Gray
    };

    public event EventHandler? FilterChanged;

    public TargetFilterEditorControl()
    {
        Dock = DockStyle.Top;
        Height = 190;
        BuildLayout();
        WireEvents();
    }

    private void BuildLayout()
    {
        var groupColumn = new DataGridViewTextBoxColumn { Name = "Group", HeaderText = "Grupo", FillWeight = 15, ReadOnly = false };
        var kindColumn = new DataGridViewComboBoxColumn { Name = "Kind", HeaderText = "Tipo", FillWeight = 30, DataSource = Kinds.Clone() };
        var negateColumn = new DataGridViewCheckBoxColumn { Name = "Negate", HeaderText = "Negar (NO)", FillWeight = 15 };
        var valueColumn = new DataGridViewTextBoxColumn { Name = "Value", HeaderText = "Valor", FillWeight = 40 };

        _grid.Columns.AddRange(groupColumn, kindColumn, negateColumn, valueColumn);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        foreach (var b in new[] { _addConditionButton, _addGroupButton, _removeButton })
        {
            b.Width = 140;
            b.Margin = new Padding(0, 0, 6, 4);
            buttons.Controls.Add(b);
        }

        _hintLabel.Dock = DockStyle.Top;

        // Dock = Top acopla el ULTIMO agregado mas cerca del borde superior:
        // se agregan en orden inverso al que se ven (mismo truco que MainForm).
        Controls.Add(_grid);
        Controls.Add(buttons);
        Controls.Add(_hintLabel);
    }

    private void WireEvents()
    {
        _addConditionButton.Click += (_, _) => AddCondition();
        _addGroupButton.Click += (_, _) => AddCondition(newGroup: true);
        _removeButton.Click += (_, _) => RemoveSelected();
        _grid.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == KindColumnIndex)
                ApplyValueEditor(_grid.Rows[e.RowIndex]);
            FilterChanged?.Invoke(this, EventArgs.Empty);
        };
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
    }

    /// <summary>
    /// Catalogo real de Tipos de Monstruo (ver <see cref="TypeRepository"/>)
    /// para poblar la lista desplegable de Valor cuando el Tipo (Kind) de la
    /// condicion es "Type". Publico: el formulario dueno lo llama al
    /// construirse y de nuevo cada vez que el catalogo de Tipos puede haber
    /// cambiado (ver <c>MainForm.OnTypeEditorClicked</c>).
    /// </summary>
    public void SetAvailableTypes(IReadOnlyList<string> types)
    {
        _availableTypes = types;
        foreach (DataGridViewRow row in _grid.Rows)
            ApplyValueEditor(row);
    }

    /// <summary>
    /// Reemplaza la celda de Valor de <paramref name="row"/> por el editor
    /// que corresponde a su Kind: lista desplegable para Categoria/Atributo/
    /// Lado (valores fijos) y Tipo (catalogo real via <see cref="SetAvailableTypes"/>),
    /// sin edicion para Any (el motor lo ignora igual), y texto libre para
    /// SpecificCard (Id numerico) o si el catalogo de Tipos todavia esta
    /// vacio. Se llama al agregar una fila, al cargar un filtro guardado, al
    /// cambiar el Kind de una fila existente, y al refrescar el catalogo de
    /// Tipos.
    /// </summary>
    private void ApplyValueEditor(DataGridViewRow row)
    {
        if (row.IsNewRow) return;

        string kind = row.Cells[KindColumnIndex].Value?.ToString() ?? "Type";
        string currentValue = row.Cells[ValueColumnIndex].Value?.ToString() ?? "";

        if (kind == "Any")
        {
            if (row.Cells[ValueColumnIndex] is not DataGridViewTextBoxCell) row.Cells[ValueColumnIndex] = new DataGridViewTextBoxCell();
            row.Cells[ValueColumnIndex].Value = "";
            row.Cells[ValueColumnIndex].ReadOnly = true;
            return;
        }

        IReadOnlyList<string>? options = kind switch
        {
            "Type" when _availableTypes.Count > 0 => _availableTypes,
            "Category" => CategoryValues,
            "Attribute" => AttributeValues,
            "ControllerSide" => ControllerSideValues,
            _ => null
        };

        if (options == null)
        {
            if (row.Cells[ValueColumnIndex] is DataGridViewComboBoxCell)
                row.Cells[ValueColumnIndex] = new DataGridViewTextBoxCell { Value = currentValue };
            else
                row.Cells[ValueColumnIndex].ReadOnly = false;
            return;
        }

        var comboCell = new DataGridViewComboBoxCell();
        comboCell.Items.AddRange(options.Cast<object>().ToArray());
        row.Cells[ValueColumnIndex] = comboCell;
        comboCell.Value = options.Contains(currentValue) ? currentValue : options[0];
    }

    private int NextGroupIndex() =>
        _grid.Rows.Cast<DataGridViewRow>().Select(r => ParseGroup(r)).DefaultIfEmpty(-1).Max() + 1;

    private int LastGroupIndex() =>
        _grid.Rows.Cast<DataGridViewRow>().Select(r => ParseGroup(r)).DefaultIfEmpty(0).Max();

    private static int ParseGroup(DataGridViewRow row) =>
        int.TryParse(row.Cells[0].Value?.ToString(), out int g) ? g : 0;

    private void AddCondition(bool newGroup = false)
    {
        int group = newGroup ? NextGroupIndex() : LastGroupIndex();
        int idx = _grid.Rows.Add();
        _grid.Rows[idx].Cells[0].Value = group.ToString();
        _grid.Rows[idx].Cells[1].Value = "Type";
        _grid.Rows[idx].Cells[2].Value = false;
        _grid.Rows[idx].Cells[3].Value = "";
        ApplyValueEditor(_grid.Rows[idx]);
        FilterChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveSelected()
    {
        foreach (DataGridViewRow row in _grid.SelectedRows.Cast<DataGridViewRow>().ToList())
            _grid.Rows.Remove(row);
        FilterChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Lee la grilla y arma el <see cref="FilterDto"/> resultante, renormalizando los numeros de Grupo a 0..N en el orden en que aparecen.</summary>
    public FilterDto GetFilter()
    {
        var dto = new FilterDto();
        var groupOrder = new List<int>();
        var byGroup = new Dictionary<int, List<FilterConditionDto>>();

        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.IsNewRow) continue;
            int group = ParseGroup(row);
            if (!byGroup.TryGetValue(group, out var list))
            {
                list = new List<FilterConditionDto>();
                byGroup[group] = list;
                groupOrder.Add(group);
            }
            list.Add(new FilterConditionDto
            {
                Kind = row.Cells[1].Value?.ToString() ?? "Type",
                Negate = row.Cells[2].Value is true,
                Value = row.Cells[3].Value?.ToString() ?? ""
            });
        }

        foreach (int group in groupOrder)
            dto.OrGroups.Add(byGroup[group]);

        return dto;
    }

    /// <summary>Puebla la grilla desde un <see cref="FilterDto"/> ya guardado (para reeditar).</summary>
    public void SetFilter(FilterDto? filter)
    {
        _grid.Rows.Clear();
        if (filter == null) return;

        for (int g = 0; g < filter.OrGroups.Count; g++)
        {
            foreach (var condition in filter.OrGroups[g])
            {
                int idx = _grid.Rows.Add();
                _grid.Rows[idx].Cells[0].Value = g.ToString();
                _grid.Rows[idx].Cells[1].Value = condition.Kind;
                _grid.Rows[idx].Cells[2].Value = condition.Negate;
                _grid.Rows[idx].Cells[3].Value = condition.Value;
                ApplyValueEditor(_grid.Rows[idx]);
            }
        }
    }
}
