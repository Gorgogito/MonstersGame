namespace GodotGame.Core.Entities;

/// <summary>
/// Estado de un duelista durante el Duelo: LP, Deck, mano, zonas de campo y
/// cementerio. No contiene logica de reglas (eso vive en el motor); solo
/// almacena el estado y ofrece operaciones basicas.
/// </summary>
public sealed class Player
{
    public const int MonsterZoneCount = 5;
    public const int SpellTrapZoneCount = 5;

    public PlayerSide Side { get; }
    public string Name { get; }

    /// <summary>Life Points actuales (pagina 28: se inicia con 8000).</summary>
    public int LifePoints { get; set; }

    /// <summary>Deck Principal: pila de la que se roban cartas.</summary>
    public List<Card> Deck { get; } = new();

    /// <summary>Cartas en la mano.</summary>
    public List<Card> Hand { get; } = new();

    /// <summary>Zona de Monstruos (5 espacios, pagina 4).</summary>
    public CardInstance?[] MonsterZones { get; } = new CardInstance?[MonsterZoneCount];

    /// <summary>Zona de Magia y Trampas (5 espacios, pagina 4).</summary>
    public SpellTrapInstance?[] SpellTrapZones { get; } = new SpellTrapInstance?[SpellTrapZoneCount];

    /// <summary>
    /// Zona del Campo (pagina 4): 1 espacio dedicado para una Carta Magica de
    /// Campo, fuera del limite de la Zona de Magia y Trampas.
    /// </summary>
    public SpellTrapInstance? FieldZone { get; set; }

    /// <summary>Cementerio (cartas usadas o destruidas).</summary>
    public List<Card> Graveyard { get; } = new();

    /// <summary>Cartas desterradas (fuera del juego, boca arriba).</summary>
    public List<Card> Banished { get; } = new();

    /// <summary>Indica si ya realizo su Invocacion Normal / Colocacion este turno.</summary>
    public bool HasNormalSummonedThisTurn { get; set; }

    /// <summary>
    /// Verdadero si este turno ya Invoco algun monstruo (Normal, por Volteo o
    /// de Modo Especial; Colocar no cuenta). Para condiciones como "no puedes
    /// activar esta carta si Invocaste este turno".
    /// </summary>
    public bool HasSummonedThisTurn { get; set; }

    /// <summary>
    /// "No puedes Invocar monstruos el resto de este turno" (pero si Colocar):
    /// lo pone un efecto y se levanta al empezar el turno siguiente.
    /// </summary>
    public bool CannotSummonThisTurn { get; set; }

    public Player(PlayerSide side, string name)
    {
        Side = side;
        Name = name;
        LifePoints = 8000;
    }

    /// <summary>Cantidad de monstruos en el campo.</summary>
    public int MonsterCount => MonsterZones.Count(z => z != null);

    /// <summary>Indice de la primera Zona de Monstruos libre, o -1 si todas estan ocupadas.</summary>
    public int FirstFreeMonsterZone()
    {
        for (int i = 0; i < MonsterZones.Length; i++)
            if (MonsterZones[i] == null) return i;
        return -1;
    }

    /// <summary>Cantidad de Cartas Magicas/Trampa en el campo (sin contar la Zona del Campo).</summary>
    public int SpellTrapCount => SpellTrapZones.Count(z => z != null);

    /// <summary>Indice de la primera Zona de Magia/Trampa libre, o -1 si todas estan ocupadas.</summary>
    public int FirstFreeSpellTrapZone()
    {
        for (int i = 0; i < SpellTrapZones.Length; i++)
            if (SpellTrapZones[i] == null) return i;
        return -1;
    }

    /// <summary>Roba una carta del Deck a la mano. Devuelve false si el Deck esta vacio.</summary>
    public bool DrawCard()
    {
        if (Deck.Count == 0) return false;
        var card = Deck[0];
        Deck.RemoveAt(0);
        Hand.Add(card);
        return true;
    }

    /// <summary>Manda una carta al cementerio.</summary>
    public void SendToGraveyard(Card card) => Graveyard.Add(card);
}
