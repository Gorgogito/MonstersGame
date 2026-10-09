namespace GodotGame.Data;

/// <summary>
/// Ubica archivos de arte por nombre. <c>Card.Image</c> guarda solo el nombre
/// del archivo, pero el arte puede organizarse en subcarpetas (ej.
/// <c>Art/Cards/Mundo oscuro/</c>): se busca primero en la carpeta raiz y,
/// si no esta, en sus subcarpetas (sin distinguir mayusculas).
/// </summary>
public static class ArtFiles
{
    /// <summary>Ruta del archivo <paramref name="fileName"/> dentro de <paramref name="dir"/> o de alguna de sus subcarpetas; si no existe en ningun lado, la ruta directa (que no existe).</summary>
    public static string Resolve(string dir, string fileName)
    {
        string direct = Path.Combine(dir, fileName);
        if (File.Exists(direct) || !Directory.Exists(dir)) return direct;

        try
        {
            foreach (var subdir in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories))
            {
                string candidate = Path.Combine(subdir, fileName);
                if (File.Exists(candidate)) return candidate;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return direct;
    }
}
