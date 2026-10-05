namespace MonstersGame.CardEditor;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(FindDatabasePath()));
    }

    /// <summary>
    /// Ubica <c>MonstersGame/Data/monstersgame.db</c> a partir de la carpeta
    /// de salida del editor, subiendo hasta encontrar <c>MonstersGame.sln</c>.
    /// Asi el editor siempre apunta al mismo catalogo que compila y lee el
    /// juego, sin depender de cuantos niveles de profundidad tenga el
    /// directorio bin/Debug (que puede cambiar si cambia el TargetFramework).
    /// </summary>
    private static string FindDatabasePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MonstersGame.sln")))
            dir = dir.Parent;

        if (dir == null)
        {
            MessageBox.Show(
                "No se encontro MonstersGame.sln en ningun directorio superior a " + AppContext.BaseDirectory,
                "MonstersGame — Editor de Cartas", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(1);
        }

        return Path.Combine(dir!.FullName, "MonstersGame", "Data", "monstersgame.db");
    }
}
