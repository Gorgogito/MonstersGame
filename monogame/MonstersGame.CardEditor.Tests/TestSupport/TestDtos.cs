using MonstersGame.Data.Loaders;

namespace MonstersGame.CardEditor.Tests.TestSupport;

/// <summary>Fabricas de <see cref="CardDto"/> validos para pruebas, evitando repetir el boilerplate de campos obligatorios.</summary>
internal static class TestDtos
{
    public static CardDto Monster(int id, string name, int attack = 1500, int defense = 1200, int level = 4) => new()
    {
        Id = id,
        Kind = "Monster",
        Name = name,
        Attack = attack,
        Defense = defense,
        Level = level,
        Type = "Dragon",
        Attribute = "Light",
        Category = "Normal"
    };
}
