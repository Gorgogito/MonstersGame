using Godot;

namespace GodotGame.Editor.Controls;

/// <summary>
/// Helpers de layout compartidos entre los paneles del editor: una tabla de
/// 2 columnas (etiqueta fija + valor a lo ancho, via <see cref="GridContainer"/>)
/// y una "seccion" con titulo (equivalente Godot de un <c>GroupBox</c> de
/// WinForms -- Godot no tiene un contenedor con borde+titulo nativo, asi que
/// se simula con un <see cref="PanelContainer"/> bordeado + un <see cref="Label"/>).
/// </summary>
internal static class EditorLayout
{
    public static GridContainer TwoColumnLayout()
    {
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 6);
        return grid;
    }

    public static void AddRow(GridContainer grid, string label, Control control)
    {
        grid.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(150, 0), VerticalAlignment = VerticalAlignment.Center });
        control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        grid.AddChild(control);
    }

    /// <summary>Panel con borde propio + titulo, para agrupar visualmente un conjunto de campos condicionales (Fusion/Ritual/Equipo/Campo).</summary>
    public static VBoxContainer Section(string title, out VBoxContainer body)
    {
        var outer = new VBoxContainer();
        outer.AddThemeConstantOverride("separation", 4);
        outer.AddChild(new Label { Text = title, ThemeTypeVariation = "HeaderSmall" });

        var panel = new PanelContainer();
        var style = new StyleBoxFlat
        {
            BgColor = new Color(1, 1, 1, 0.03f),
            BorderColor = new Color(1, 1, 1, 0.18f),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 6,
            ContentMarginBottom = 6
        };
        panel.AddThemeStyleboxOverride("panel", style);

        body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);
        panel.AddChild(body);
        outer.AddChild(panel);

        return outer;
    }

    public static Label Hint(string text) => new()
    {
        Text = text,
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
        Modulate = new Color(1, 1, 1, 0.6f)
    };
}
