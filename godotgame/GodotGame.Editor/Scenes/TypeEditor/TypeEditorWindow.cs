using Godot;
using GodotGame.Editor.Data;

namespace GodotGame.Editor.Scenes.TypeEditor;

/// <summary>
/// Ventana secundaria para administrar el catalogo de Tipos de Monstruo:
/// agregar uno nuevo, renombrar uno existente (actualiza toda carta que lo
/// tenga asignado), o eliminarlo si ninguna carta lo esta usando. Cambios
/// inmediatos: cada accion ya escribe en la base. Adaptacion Godot de
/// <c>TypeEditorForm</c> de MonstersGame.CardEditor (un <see cref="Window"/>
/// nativo en vez de un <c>Form</c> modal de WinForms).
/// </summary>
public sealed partial class TypeEditorWindow : Window
{
    private readonly Data.TypeRepository _repo;

    private readonly ItemList _typeList = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
    private readonly LineEdit _nameBox = new();
    private readonly Label _statusLabel = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1f, 0.6f, 0.6f) };

    /// <summary>El usuario cerro la ventana (boton Cerrar o la X del titulo).</summary>
    public event Action? Closed;

    public TypeEditorWindow(string dbPath)
    {
        _repo = new Data.TypeRepository(dbPath);

        Title = "GodotGame — Tipos de Monstruo";
        Size = new Vector2I(420, 500);
        Unresizable = false;
        Exclusive = true;

        BuildUi();
        RefreshList();

        CloseRequested += () => Closed?.Invoke();
    }

    private void BuildUi()
    {
        var root = new VBoxContainer { AnchorRight = 1, AnchorBottom = 1, OffsetLeft = 10, OffsetTop = 10, OffsetRight = -10, OffsetBottom = -10 };
        root.AddThemeConstantOverride("separation", 6);
        AddChild(root);

        root.AddChild(new Label { Text = "Cada carta de Monstruo elige su Tipo de esta lista. Los cambios se guardan al instante.", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        root.AddChild(_typeList);
        root.AddChild(_nameBox);

        var buttons = new HBoxContainer();
        var addButton = new Button { Text = "Agregar" };
        var renameButton = new Button { Text = "Renombrar seleccionado" };
        var deleteButton = new Button { Text = "Eliminar seleccionado" };
        addButton.Pressed += OnAddClicked;
        renameButton.Pressed += OnRenameClicked;
        deleteButton.Pressed += OnDeleteClicked;
        foreach (var b in new[] { addButton, renameButton, deleteButton }) buttons.AddChild(b);
        root.AddChild(buttons);

        root.AddChild(_statusLabel);

        var closeButton = new Button { Text = "Cerrar" };
        closeButton.Pressed += () => Closed?.Invoke();
        var closeRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        closeRow.AddChild(closeButton);
        root.AddChild(closeRow);

        _typeList.ItemSelected += i => _nameBox.Text = _typeList.GetItemText((int)i);
    }

    private void OnAddClicked()
    {
        var error = _repo.Add(_nameBox.Text);
        ShowStatus(error);
        if (error == null) RefreshList();
    }

    private void OnRenameClicked()
    {
        var selected = _typeList.GetSelectedItems();
        if (selected.Length == 0) { ShowStatus("Selecciona un Tipo de la lista para renombrarlo."); return; }

        var error = _repo.Rename(_typeList.GetItemText(selected[0]), _nameBox.Text);
        ShowStatus(error);
        if (error == null) RefreshList(selectName: _nameBox.Text.Trim());
    }

    private void OnDeleteClicked()
    {
        var selected = _typeList.GetSelectedItems();
        if (selected.Length == 0) { ShowStatus("Selecciona un Tipo de la lista para eliminarlo."); return; }

        var error = _repo.Delete(_typeList.GetItemText(selected[0]));
        ShowStatus(error);
        if (error == null) RefreshList();
    }

    private void ShowStatus(string? error) => _statusLabel.Text = error ?? "";

    private void RefreshList(string? selectName = null)
    {
        _typeList.Clear();
        foreach (var type in _repo.Types) _typeList.AddItem(type);

        if (selectName != null)
            for (int i = 0; i < _typeList.ItemCount; i++)
                if (_typeList.GetItemText(i) == selectName) { _typeList.Select(i); break; }
    }
}
