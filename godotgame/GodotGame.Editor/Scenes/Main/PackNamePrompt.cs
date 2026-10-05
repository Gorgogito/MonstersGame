using Godot;

namespace GodotGame.Editor.Scenes.Main;

/// <summary>
/// Dialogo minimo "Nombre: [___] Aceptar/Cancelar", equivalente Godot del
/// <c>PromptText</c> ad-hoc que <c>MainForm.OnExportPackClicked</c> arma a
/// mano en WinForms.
/// </summary>
public sealed partial class PackNamePrompt : ConfirmationDialog
{
    private readonly LineEdit _box = new();
    private Action<string?>? _callback;

    public PackNamePrompt()
    {
        Title = "GodotGame — Editor de Cartas";
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        var box = new VBoxContainer();
        box.AddChild(new Label { Text = "Nombre del paquete:" });
        _box.CustomMinimumSize = new Vector2(300, 0);
        box.AddChild(_box);
        margin.AddChild(box);
        AddChild(margin);

        Confirmed += () => _callback?.Invoke(_box.Text);
        Canceled += () => _callback?.Invoke(null);
    }

    public void RequestName(string initialValue, Action<string?> callback)
    {
        _box.Text = initialValue;
        _callback = callback;
        PopupCentered();
        _box.GrabFocus();
    }
}
