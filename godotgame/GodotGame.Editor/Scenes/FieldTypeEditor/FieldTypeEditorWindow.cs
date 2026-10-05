using Godot;
using GodotGame.Data.Loaders;
using GodotGame.Editor.Controls;
using GodotGame.Editor.Data;

namespace GodotGame.Editor.Scenes.FieldTypeEditor;

/// <summary>
/// Ventana secundaria para administrar el catalogo de tipos de Campo: cada
/// uno define su color/fondo de referencia, a que Monstruos afecta y su
/// modificador de ATK/DEF (mas la penalizacion de terreno elemental
/// opuesta). Adaptacion Godot de <c>FieldTypeEditorForm</c> de
/// MonstersGame.CardEditor.
/// </summary>
public sealed partial class FieldTypeEditorWindow : Window
{
    private readonly FieldTypeRepository _repo;
    private readonly string _fieldsArtDir;

    private readonly ItemList _list = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(220, 0) };
    private readonly LineEdit _idBox = new();
    private readonly LineEdit _nameBox = new();
    private readonly LineEdit _descriptionBox = new();
    private readonly LineEdit _colorBox = new();
    private readonly LineEdit _backgroundImageBox = new();
    private readonly LineEdit _visualEffectsKeyBox = new();
    private readonly SpinBox _statAmountBox = new() { MinValue = -9999, MaxValue = 9999 };
    private readonly OptionButton _statKindCombo = new();
    private readonly TargetFilterEditor _affectedFilter = new();
    private readonly SpinBox _opposedAmountBox = new() { MinValue = -9999, MaxValue = 9999 };
    private readonly TargetFilterEditor _opposedFilter = new();
    private readonly Label _statusLabel = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1f, 0.6f, 0.6f) };
    private readonly FileDialog _browseBackgroundDialog;

    private readonly List<FieldTypeDto> _listed = new();
    private string? _editingOriginalId;

    public event Action? Closed;

    public FieldTypeEditorWindow(string dbPath)
    {
        _repo = new FieldTypeRepository(dbPath);
        _fieldsArtDir = Path.Combine(Path.GetDirectoryName(dbPath)!, "Art", "Fields");
        var availableTypes = new TypeRepository(dbPath).Types;
        _affectedFilter.SetAvailableTypes(availableTypes);
        _opposedFilter.SetAvailableTypes(availableTypes);

        Title = "GodotGame — Tipos de Campo";
        Size = new Vector2I(820, 680);
        Exclusive = true;

        _browseBackgroundDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            Filters = new[] { "*.png,*.jpg,*.jpeg,*.gif,*.bmp ; Imagenes" },
            Size = new Vector2I(700, 500)
        };
        _browseBackgroundDialog.FileSelected += path => _backgroundImageBox.Text = Path.GetFileName(path);

        BuildUi();
        _statKindCombo.AddItem("None");
        _statKindCombo.AddItem("Attack");
        _statKindCombo.AddItem("Defense");
        _statKindCombo.AddItem("Both");

        RefreshList();
        LoadIntoForm(_repo.NewBlank());

        CloseRequested += () => Closed?.Invoke();
    }

    private void BuildUi()
    {
        var root = new HBoxContainer { AnchorRight = 1, AnchorBottom = 1, OffsetLeft = 10, OffsetTop = 10, OffsetRight = -10, OffsetBottom = -10 };
        root.AddThemeConstantOverride("separation", 10);
        AddChild(root);
        AddChild(_browseBackgroundDialog);

        root.AddChild(_list);
        _list.ItemSelected += i => LoadIntoForm(_listed[(int)i]);

        var scroll = new ScrollContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(right);
        root.AddChild(scroll);

        var form = EditorLayout.TwoColumnLayout();
        EditorLayout.AddRow(form, "Id", _idBox);
        EditorLayout.AddRow(form, "Nombre", _nameBox);
        EditorLayout.AddRow(form, "Descripcion", _descriptionBox);
        EditorLayout.AddRow(form, "Color (#RRGGBB)", _colorBox);
        EditorLayout.AddRow(form, "Imagen de fondo", BuildBackgroundImageRow());
        EditorLayout.AddRow(form, "Clave visual", _visualEffectsKeyBox);
        EditorLayout.AddRow(form, "Modificador", _statAmountBox);
        EditorLayout.AddRow(form, "Aplica a", _statKindCombo);
        EditorLayout.AddRow(form, "Penalizacion (terreno elemental)", _opposedAmountBox);
        right.AddChild(form);

        right.AddChild(EditorLayout.Hint("Monstruos afectados (reciben el Modificador):"));
        right.AddChild(_affectedFilter);
        right.AddChild(EditorLayout.Hint("Monstruos penalizados (reciben la Penalizacion -- ej. el Atributo opuesto; vacio = ninguno):"));
        right.AddChild(_opposedFilter);

        var buttons = new HBoxContainer();
        var newButton = new Button { Text = "Nuevo" };
        var saveButton = new Button { Text = "Guardar" };
        var deleteButton = new Button { Text = "Eliminar seleccionado" };
        newButton.Pressed += () => LoadIntoForm(_repo.NewBlank());
        saveButton.Pressed += OnSaveClicked;
        deleteButton.Pressed += OnDeleteClicked;
        foreach (var b in new[] { newButton, saveButton, deleteButton }) buttons.AddChild(b);
        right.AddChild(buttons);

        right.AddChild(_statusLabel);
    }

    private Control BuildBackgroundImageRow()
    {
        var row = new HBoxContainer();
        _backgroundImageBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(_backgroundImageBox);
        var browseButton = new Button { Text = "Examinar..." };
        browseButton.Pressed += () =>
        {
            Directory.CreateDirectory(_fieldsArtDir);
            _browseBackgroundDialog.CurrentDir = _fieldsArtDir;
            _browseBackgroundDialog.Popup();
        };
        row.AddChild(browseButton);
        return row;
    }

    private void LoadIntoForm(FieldTypeDto dto)
    {
        _editingOriginalId = string.IsNullOrEmpty(dto.Id) ? null : dto.Id;
        _idBox.Text = dto.Id;
        _idBox.Editable = _editingOriginalId == null;
        _nameBox.Text = dto.Name;
        _descriptionBox.Text = dto.Description;
        _colorBox.Text = dto.Color;
        _backgroundImageBox.Text = dto.BackgroundImage;
        _visualEffectsKeyBox.Text = dto.VisualEffectsKey;
        _statAmountBox.Value = dto.StatModifierAmount;
        SelectComboText(_statKindCombo, dto.StatModifierStat);
        _affectedFilter.SetFilter(dto.AffectedFilter);
        _opposedAmountBox.Value = dto.OpposedStatModifierAmount;
        _opposedFilter.SetFilter(dto.OpposedFilter);
    }

    private FieldTypeDto BuildDtoFromForm() => new()
    {
        Id = _idBox.Text.Trim(),
        Name = _nameBox.Text.Trim(),
        Description = _descriptionBox.Text,
        Color = string.IsNullOrWhiteSpace(_colorBox.Text) ? "#FFFFFF" : _colorBox.Text.Trim(),
        BackgroundImage = _backgroundImageBox.Text,
        VisualEffectsKey = _visualEffectsKeyBox.Text,
        StatModifierAmount = (int)_statAmountBox.Value,
        StatModifierStat = _statKindCombo.Selected >= 0 ? _statKindCombo.GetItemText(_statKindCombo.Selected) : "None",
        AffectedFilter = _affectedFilter.GetFilter(),
        OpposedStatModifierAmount = (int)_opposedAmountBox.Value,
        OpposedFilter = _opposedFilter.GetFilter()
    };

    private void OnSaveClicked()
    {
        var dto = BuildDtoFromForm();
        var error = _repo.Save(dto);
        _statusLabel.Text = error ?? "";
        if (error != null) return;

        RefreshList();
        SelectInList(dto.Id);
    }

    private void OnDeleteClicked()
    {
        if (_editingOriginalId == null) { _statusLabel.Text = "Selecciona un tipo de Campo de la lista para eliminarlo."; return; }
        var error = _repo.Delete(_editingOriginalId);
        _statusLabel.Text = error ?? "";
        if (error == null)
        {
            RefreshList();
            LoadIntoForm(_repo.NewBlank());
        }
    }

    private void RefreshList()
    {
        _list.Clear();
        _listed.Clear();
        foreach (var dto in _repo.FieldTypes)
        {
            _list.AddItem(dto.ToString());
            _listed.Add(dto);
        }
    }

    private void SelectInList(string id)
    {
        for (int i = 0; i < _listed.Count; i++)
            if (_listed[i].Id == id) { _list.Select(i); return; }
    }

    private static void SelectComboText(OptionButton combo, string text)
    {
        for (int i = 0; i < combo.ItemCount; i++)
            if (combo.GetItemText(i) == text) { combo.Selected = i; return; }
        combo.Selected = combo.ItemCount > 0 ? 0 : -1;
    }
}
