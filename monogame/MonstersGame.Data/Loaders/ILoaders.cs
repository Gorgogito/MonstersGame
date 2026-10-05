using MonstersGame.Core.Entities;

namespace MonstersGame.Data.Loaders;

/// <summary>
/// Carga las definiciones de cartas. Abstrae el origen (JSON, SQLite, etc.)
/// para que la logica del juego no dependa del formato de persistencia.
/// </summary>
public interface ICardLoader
{
    IReadOnlyList<Card> LoadCards();
}

/// <summary>Carga las definiciones de mazos disponibles.</summary>
public interface IDeckLoader
{
    IReadOnlyList<DeckDefinition> LoadDecks();
}

/// <summary>Carga las recetas de fusion.</summary>
public interface IFusionLoader
{
    IReadOnlyList<FusionRecipe> LoadFusions();
}
