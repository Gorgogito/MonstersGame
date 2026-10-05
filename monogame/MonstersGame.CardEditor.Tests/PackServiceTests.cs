using MonstersGame.CardEditor;
using MonstersGame.CardEditor.Tests.TestSupport;
using MonstersGame.Data.Loaders;

namespace MonstersGame.CardEditor.Tests;

/// <summary>
/// Pruebas de exportar/importar cartas individuales y paquetes
/// <c>MonsterCardPack</c>. Antes del Bloque 8 esto solo se habia verificado
/// manualmente contra el ejecutable del editor.
/// </summary>
public class PackServiceTests
{
    [Fact]
    public void ExportCard_ThenImportCard_RoundTripsAllFields()
    {
        using var dir = new TempCardDirectory();
        var path = Path.Combine(dir.Path, "carta.json");
        var original = TestDtos.Monster(42, "Guerrero de Prueba", attack: 1900, defense: 1400, level: 5);
        original.EffectId = "draw_1";

        PackService.ExportCard(original, path);
        var imported = PackService.ImportCard(path);

        Assert.Equal(original.Id, imported.Id);
        Assert.Equal(original.Name, imported.Name);
        Assert.Equal(original.Attack, imported.Attack);
        Assert.Equal(original.Defense, imported.Defense);
        Assert.Equal(original.Level, imported.Level);
        Assert.Equal(original.EffectId, imported.EffectId);
    }

    [Fact]
    public void ImportCard_MissingFile_Throws()
    {
        using var dir = new TempCardDirectory();
        var missing = Path.Combine(dir.Path, "no_existe.json");

        Assert.ThrowsAny<IOException>(() => PackService.ImportCard(missing));
    }

    [Fact]
    public void ExportPack_ThenImportPack_RoundTripsEveryCard()
    {
        using var dir = new TempCardDirectory();
        var zipPath = Path.Combine(dir.Path, "paquete.zip");
        var cards = new List<CardDto>
        {
            TestDtos.Monster(1, "Uno"),
            TestDtos.Monster(2, "Dos"),
            TestDtos.Monster(3, "Tres")
        };

        PackService.ExportPack(cards, "Paquete de Prueba", "Autor", zipPath);
        var imported = PackService.ImportPack(zipPath);

        Assert.Equal(3, imported.Count);
        Assert.Equal(cards.Select(c => c.Id).OrderBy(id => id), imported.Select(c => c.Id).OrderBy(id => id));
        Assert.Equal(cards.Select(c => c.Name).OrderBy(n => n), imported.Select(c => c.Name).OrderBy(n => n));
    }

    [Fact]
    public void ExportPack_OverwritesAnExistingZipAtTheSamePath()
    {
        using var dir = new TempCardDirectory();
        var zipPath = Path.Combine(dir.Path, "paquete.zip");

        PackService.ExportPack(new[] { TestDtos.Monster(1, "Uno") }, "V1", "Autor", zipPath);
        PackService.ExportPack(new[] { TestDtos.Monster(2, "Dos"), TestDtos.Monster(3, "Tres") }, "V2", "Autor", zipPath);

        var imported = PackService.ImportPack(zipPath);
        Assert.Equal(2, imported.Count);
    }

    [Fact]
    public void ImportPack_DoesNotTreatTheManifestAsACard()
    {
        using var dir = new TempCardDirectory();
        var zipPath = Path.Combine(dir.Path, "paquete.zip");
        PackService.ExportPack(new[] { TestDtos.Monster(1, "Unica") }, "Mi Paquete", "Autor", zipPath);

        var imported = PackService.ImportPack(zipPath);

        Assert.Single(imported);
        Assert.Equal("Unica", imported[0].Name);
    }
}
