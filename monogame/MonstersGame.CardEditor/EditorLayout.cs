namespace MonstersGame.CardEditor;

/// <summary>
/// Helpers de layout compartidos entre <c>MainForm</c> y los paneles/formularios
/// que extrajo (Fase 5, paso de refactor puro): una tabla de 2 columnas
/// (etiqueta fija + valor a lo ancho) y el acople de un GroupBox/panel para
/// que ocupe el ancho completo del contenedor y su alto se ajuste al
/// contenido. Antes de esta extraccion, <c>MainForm</c> y <c>FieldTypeEditorForm</c>
/// tenian cada uno su propia copia casi identica de <see cref="AddRow"/>.
/// </summary>
internal static class EditorLayout
{
    /// <summary>
    /// Tabla de 2 columnas, con las opciones que la hacen expandirse
    /// correctamente dentro de un contenedor acoplado con <c>Dock = DockStyle.Top</c>
    /// (ver <see cref="SetupGroup"/>): sin esto, una columna "Percent" dentro
    /// de un contenedor con AutoSize se colapsa a un ancho casi nulo.
    /// </summary>
    public static TableLayoutPanel TwoColumnLayout()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return layout;
    }

    /// <summary>
    /// Acopla un GroupBox/Panel de propiedades con <c>Dock = DockStyle.Top</c>
    /// para que ocupe el ancho completo de su contenedor, con <c>AutoSize</c>
    /// para que su alto se ajuste al contenido.
    /// </summary>
    public static void SetupGroup(GroupBox group)
    {
        group.Dock = DockStyle.Top;
        group.AutoSize = true;
        group.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        group.Margin = new Padding(0, 6, 0, 0);
    }

    public static void AddRow(TableLayoutPanel layout, string label, Control control)
    {
        int row = layout.RowCount;
        layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) }, 0, row);
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        control.Margin = new Padding(3, 4, 3, 4);
        layout.Controls.Add(control, 1, row);
    }
}
