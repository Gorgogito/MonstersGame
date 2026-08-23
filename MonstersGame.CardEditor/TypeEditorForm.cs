namespace MonstersGame.CardEditor;

/// <summary>
/// Ventana secundaria para administrar el catalogo de Tipos de Monstruo
/// (Bloque 13): agregar uno nuevo (p. ej. "Bestia Divina"), renombrar uno
/// existente (actualiza toda carta que lo tenga asignado), o eliminarlo si
/// ninguna carta lo esta usando. Cambios inmediatos: no hace falta "Guardar",
/// cada accion ya escribe en la base.
/// </summary>
public sealed class TypeEditorForm : Form
{
    private readonly TypeRepository _repo;

    private readonly ListBox _typeList = new();
    private readonly TextBox _nameBox = new();
    private readonly Button _addButton = new() { Text = "Agregar" };
    private readonly Button _renameButton = new() { Text = "Renombrar seleccionado" };
    private readonly Button _deleteButton = new() { Text = "Eliminar seleccionado" };
    private readonly Label _statusLabel = new() { AutoSize = false, Height = 40, ForeColor = Color.FromArgb(255, 150, 150) };

    public TypeEditorForm(string dbPath)
    {
        _repo = new TypeRepository(dbPath);

        Text = "MonstersGame — Tipos de Monstruo";
        Width = 420;
        Height = 500;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(360, 400);

        BuildLayout();
        WireEvents();
        RefreshList();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "Cada carta de Monstruo elige su Tipo de esta lista. Los cambios se guardan al instante.",
            AutoSize = false,
            Height = 40,
            Margin = new Padding(0, 0, 0, 4)
        }, 0, 0);

        _typeList.Dock = DockStyle.Fill;
        root.Controls.Add(_typeList, 0, 1);

        _nameBox.Dock = DockStyle.Top;
        _nameBox.Margin = new Padding(0, 8, 0, 4);
        root.Controls.Add(_nameBox, 0, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        foreach (var b in new[] { _addButton, _renameButton, _deleteButton })
        {
            b.Width = 180;
            b.Margin = new Padding(0, 0, 6, 6);
            buttons.Controls.Add(b);
        }
        root.Controls.Add(buttons, 0, 3);

        _statusLabel.Dock = DockStyle.Top;
        root.Controls.Add(_statusLabel, 0, 4);

        var closeButton = new Button { Text = "Cerrar", Width = 100, Dock = DockStyle.Right };
        closeButton.Click += (_, _) => Close();
        var closeRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        closeRow.Controls.Add(closeButton);
        root.Controls.Add(closeRow, 0, 5);
    }

    private void WireEvents()
    {
        _typeList.SelectedIndexChanged += (_, _) =>
        {
            if (_typeList.SelectedItem is string selected) _nameBox.Text = selected;
        };
        _addButton.Click += (_, _) => OnAddClicked();
        _renameButton.Click += (_, _) => OnRenameClicked();
        _deleteButton.Click += (_, _) => OnDeleteClicked();
    }

    private void OnAddClicked()
    {
        var error = _repo.Add(_nameBox.Text);
        ShowStatus(error);
        if (error == null) RefreshList();
    }

    private void OnRenameClicked()
    {
        if (_typeList.SelectedItem is not string selected)
        {
            ShowStatus("Selecciona un Tipo de la lista para renombrarlo.");
            return;
        }

        var error = _repo.Rename(selected, _nameBox.Text);
        ShowStatus(error);
        if (error == null) RefreshList(selectName: _nameBox.Text.Trim());
    }

    private void OnDeleteClicked()
    {
        if (_typeList.SelectedItem is not string selected)
        {
            ShowStatus("Selecciona un Tipo de la lista para eliminarlo.");
            return;
        }

        var error = _repo.Delete(selected);
        ShowStatus(error);
        if (error == null) RefreshList();
    }

    private void ShowStatus(string? error)
    {
        _statusLabel.Text = error ?? "";
        _statusLabel.ForeColor = Color.FromArgb(255, 150, 150);
    }

    private void RefreshList(string? selectName = null)
    {
        _typeList.BeginUpdate();
        _typeList.Items.Clear();
        foreach (var type in _repo.Types)
            _typeList.Items.Add(type);
        _typeList.EndUpdate();

        if (selectName != null) _typeList.SelectedItem = selectName;
    }
}
