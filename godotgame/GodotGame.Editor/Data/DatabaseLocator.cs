namespace GodotGame.Editor.Data;

/// <summary>
/// Ubica <c>GodotGame/Data/monstersgame.db</c> a partir de la carpeta de
/// salida del editor, subiendo hasta encontrar <c>GodotGame.slnx</c>. Asi el
/// editor siempre apunta al mismo catalogo que compila y lee el juego, sin
/// depender de cuantos niveles de profundidad tenga <c>.godot/mono/temp/bin</c>.
/// Equivalente Godot de <c>Program.FindDatabasePath</c> de MonstersGame.CardEditor.
/// </summary>
public static class DatabaseLocator
{
    public static string? Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "GodotGame.slnx")))
            dir = dir.Parent;

        return dir == null ? null : Path.Combine(dir.FullName, "GodotGame", "Data", "monstersgame.db");
    }
}
