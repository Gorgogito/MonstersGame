namespace MonstersGame.CardEditor;

/// <summary>
/// Utilidades de WinForms compartidas entre <c>MainForm</c> y los paneles que
/// extrajo (Fase 5, paso de refactor puro): seleccionar un item de texto en
/// un <see cref="ComboBox"/>, resolver el <see cref="EffectOption"/>
/// seleccionado, y acotar un valor al rango de un <see cref="NumericUpDown"/>.
/// </summary>
internal static class EditorHelpers
{
    public static void SelectComboText(ComboBox combo, string text)
    {
        int idx = combo.Items.IndexOf(text);
        combo.SelectedIndex = idx >= 0 ? idx : (combo.Items.Count > 0 ? 0 : -1);
    }

    public static void SelectEffectOption(ComboBox combo, string effectId)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is EffectOption opt && opt.Id == effectId)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        combo.SelectedIndex = combo.Items.Count > 0 ? 0 : -1; // "(Sin efecto)" es siempre el primero.
    }

    public static string SelectedEffectId(ComboBox combo) =>
        combo.SelectedItem is EffectOption opt ? opt.Id : string.Empty;

    public static decimal Clamp(int value, NumericUpDown box) =>
        Math.Max(box.Minimum, Math.Min(box.Maximum, value));
}
