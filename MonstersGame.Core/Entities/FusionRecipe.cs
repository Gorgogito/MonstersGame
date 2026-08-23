namespace MonstersGame.Core.Entities;

/// <summary>
/// Receta de fusion: dos materiales producen un resultado. El emparejamiento es
/// independiente del orden (A+B == B+A).
///
/// Decision asumida (inspirada en Forbidden Memories): la fusion se realiza
/// combinando dos cartas directamente, sin requerir "Polimerizacion". El
/// reglamento (pagina 18) describe la fusion del TCG con carta de invocacion;
/// FM permite fusionar en cadena durante la invocacion. Aqui se modela la
/// version FM de 2 materiales, ampliable a N materiales en el futuro.
/// </summary>
public sealed class FusionRecipe
{
    public int MaterialAId { get; }
    public int MaterialBId { get; }
    public int ResultId { get; }

    public FusionRecipe(int materialAId, int materialBId, int resultId)
    {
        MaterialAId = materialAId;
        MaterialBId = materialBId;
        ResultId = resultId;
    }

    /// <summary>Indica si esta receta corresponde al par de ids dado (en cualquier orden).</summary>
    public bool Matches(int first, int second)
    {
        return (MaterialAId == first && MaterialBId == second)
            || (MaterialAId == second && MaterialBId == first);
    }
}
