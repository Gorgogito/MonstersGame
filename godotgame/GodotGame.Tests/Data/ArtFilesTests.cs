using GodotGame.Data;
using GodotGame.Tests.Data.TestSupport;

namespace GodotGame.Tests.Data;

public class ArtFilesTests
{
    [Fact]
    public void Resolve_PrefersTheRootFolder()
    {
        using var dir = new TempCardDirectory();
        Directory.CreateDirectory(Path.Combine(dir.Path, "Sub"));
        File.WriteAllText(Path.Combine(dir.Path, "a.png"), "");
        File.WriteAllText(Path.Combine(dir.Path, "Sub", "a.png"), "");

        Assert.Equal(Path.Combine(dir.Path, "a.png"), ArtFiles.Resolve(dir.Path, "a.png"));
    }

    [Fact]
    public void Resolve_FindsTheFileInANestedSubfolder()
    {
        using var dir = new TempCardDirectory();
        string nested = Path.Combine(dir.Path, "Mundo oscuro", "Jefes");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "Brron.png"), "");

        Assert.Equal(Path.Combine(nested, "Brron.png"), ArtFiles.Resolve(dir.Path, "Brron.png"));
    }

    [Fact]
    public void Resolve_ReturnsTheDirectPath_WhenTheFileDoesNotExist()
    {
        using var dir = new TempCardDirectory();
        Directory.CreateDirectory(Path.Combine(dir.Path, "Sub"));

        Assert.Equal(Path.Combine(dir.Path, "x.png"), ArtFiles.Resolve(dir.Path, "x.png"));
    }
}
