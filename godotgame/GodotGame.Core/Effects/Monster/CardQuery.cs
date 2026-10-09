using GodotGame.Core.Battle;
using GodotGame.Core.Entities;

namespace GodotGame.Core.Effects.Monster;

/// <summary>Lado relativo a quien controla el efecto.</summary>
public enum RelativeSide { Own, Opponent, Both }

/// <summary>Clase de carta buscada.</summary>
public enum CardKindFilter { Any, Monster, Spell, Trap, SpellTrap }

/// <summary>Visibilidad buscada (solo aplica a cartas en el Campo).</summary>
public enum FaceFilter { Any, FaceUp, FaceDown }

/// <summary>Quien elige las cartas de una seleccion.</summary>
public enum ChooserKind
{
    /// <summary>Quien controla el efecto.</summary>
    Controller,

    /// <summary>El adversario de quien controla el efecto.</summary>
    Opponent,

    /// <summary>El dueño de las cartas (ej. "tu adversario descarta 1 carta": la elige el).</summary>
    Owner,

    /// <summary>Al azar.</summary>
    Random,

    /// <summary>El adversario del dueño de las cartas (ej. "cada jugador elige 1 carta de la mano de su adversario").</summary>
    OwnersOpponent
}

/// <summary>Subtipo de Magia/Trampa buscado.</summary>
public enum SubTypeFilter { Any, Normal, Continuous, Equip, Field, QuickPlay, Ritual, Counter }

/// <summary>
/// Busqueda de cartas parametrizada por datos (los mismos parametros que usan
/// objetivos y acciones en el editor): zonas, lado, clase, Nivel, Tipo,
/// Atributo, carta concreta, nombre, boca arriba/abajo y cuantas.
/// </summary>
public sealed class CardQuery
{
    public IReadOnlyList<CardZone> From { get; }
    public RelativeSide Side { get; }
    public CardKindFilter Kind { get; }
    public int LevelMin { get; }
    public int LevelMax { get; }
    public string Type { get; }
    public MonsterAttribute? Attribute { get; }
    public int CardId { get; }
    public bool SameNameAsSource { get; }
    public bool ExcludeSourceName { get; }
    public bool ExcludeSource { get; }
    public string NameContains { get; }
    public int AttackMin { get; }
    public int AttackMax { get; }
    public SubTypeFilter SubType { get; }
    public FaceFilter Face { get; }
    public int Count { get; }
    public int Min { get; }
    public ChooserKind Chooser { get; }

    public CardQuery(EffectActionParams p, string defaultFrom = "MonsterZone", RelativeSide defaultSide = RelativeSide.Own,
        CardKindFilter defaultKind = CardKindFilter.Any, ChooserKind defaultChooser = ChooserKind.Controller)
    {
        From = ParseZones(p.GetString("From", defaultFrom));
        Side = p.GetEnum("Side", defaultSide);
        Kind = p.GetEnum("CardKind", defaultKind);
        LevelMin = p.GetInt("LevelMin");
        LevelMax = p.GetInt("LevelMax");
        Type = p.GetString("Type");
        Attribute = Enum.TryParse<MonsterAttribute>(p.GetString("Attribute"), true, out var attribute) ? attribute : null;
        CardId = p.GetInt("CardId");
        SameNameAsSource = p.GetBool("SameNameAsSource");
        ExcludeSourceName = p.GetBool("ExcludeSourceName");
        ExcludeSource = p.GetBool("ExcludeSource");
        NameContains = p.GetString("NameContains").Trim().Trim('"', '\'', '“', '”');
        SubType = p.GetEnum("SubType", SubTypeFilter.Any);
        AttackMin = p.GetInt("AttackMin", -1);
        AttackMax = p.GetInt("AttackMax", -1);
        Face = p.GetEnum("Face", FaceFilter.Any);
        Count = Math.Max(1, p.GetInt("Count", 1));
        Min = Math.Clamp(p.GetInt("Min", Count), 0, Count);
        Chooser = p.GetEnum("Chooser", defaultChooser);
    }

    public static IReadOnlyList<CardZone> ParseZones(string text)
    {
        var zones = new List<CardZone>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Enum.TryParse<CardZone>(part, true, out var zone) && !zones.Contains(zone))
                zones.Add(zone);
        return zones;
    }

    /// <summary>Lados concretos que cubre <see cref="Side"/> para quien controla el efecto.</summary>
    public static IEnumerable<PlayerSide> Sides(RelativeSide side, PlayerSide controller)
    {
        var opponent = controller == PlayerSide.Human ? PlayerSide.Cpu : PlayerSide.Human;
        if (side is RelativeSide.Own or RelativeSide.Both) yield return controller;
        if (side is RelativeSide.Opponent or RelativeSide.Both) yield return opponent;
    }

    /// <summary>Todas las cartas que cumplen la busqueda ahora mismo.</summary>
    public List<CardRef> Candidates(DuelState state, PlayerSide controller, Card source, CardRef? sourceRef)
    {
        var result = new List<CardRef>();
        foreach (var side in Sides(Side, controller))
        {
            var player = state.GetPlayer(side);
            foreach (var zone in From)
            {
                foreach (var candidate in Enumerate(player, side, zone))
                {
                    if (!Matches(state, candidate, source)) continue;
                    // "No es afectada por efectos de monstruos".
                    if (source is MonsterCard && candidate.MonsterInstance(state) is { UnaffectedByMonsterEffects: true }) continue;
                    if (ExcludeSource && sourceRef != null && candidate.Zone == sourceRef.Zone && candidate.Side == sourceRef.Side
                        && ReferenceEquals(candidate.Card, sourceRef.Card) && (candidate.Zone is CardZone.MonsterZone or CardZone.SpellTrapZone ? candidate.Index == sourceRef.Index : true))
                        continue;
                    result.Add(candidate);
                }
            }
        }

        // Excluir "esta carta" en mano/Cementerio: hay que excluir una sola copia, no todas las del mismo objeto.
        if (ExcludeSource && sourceRef != null && sourceRef.Zone is not (CardZone.MonsterZone or CardZone.SpellTrapZone))
        {
            int index = result.FindIndex(c => c.Zone == sourceRef.Zone && c.Side == sourceRef.Side && ReferenceEquals(c.Card, sourceRef.Card));
            if (index >= 0) result.RemoveAt(index);
        }
        return result;
    }

    public static IEnumerable<CardRef> Enumerate(Player player, PlayerSide side, CardZone zone)
    {
        switch (zone)
        {
            case CardZone.Hand:
                for (int i = 0; i < player.Hand.Count; i++) yield return new CardRef(player.Hand[i], side, zone, i);
                break;
            case CardZone.Deck:
                for (int i = 0; i < player.Deck.Count; i++) yield return new CardRef(player.Deck[i], side, zone, i);
                break;
            case CardZone.Graveyard:
                for (int i = 0; i < player.Graveyard.Count; i++) yield return new CardRef(player.Graveyard[i], side, zone, i);
                break;
            case CardZone.Banished:
                for (int i = 0; i < player.Banished.Count; i++) yield return new CardRef(player.Banished[i], side, zone, i);
                break;
            case CardZone.MonsterZone:
                for (int i = 0; i < player.MonsterZones.Length; i++)
                    if (player.MonsterZones[i] is { } m) yield return new CardRef(m.Card, side, zone, i);
                break;
            case CardZone.SpellTrapZone:
                for (int i = 0; i < player.SpellTrapZones.Length; i++)
                    if (player.SpellTrapZones[i] is { } s) yield return new CardRef(s.Card, side, zone, i);
                break;
            case CardZone.FieldZone:
                if (player.FieldZone is { } f) yield return new CardRef(f.Card, side, zone, 0);
                break;
        }
    }

    public bool Matches(DuelState state, CardRef candidate, Card source)
    {
        var card = candidate.Card;
        bool kindOk = Kind switch
        {
            CardKindFilter.Monster => card is MonsterCard,
            CardKindFilter.Spell => card is SpellCard,
            CardKindFilter.Trap => card is TrapCard,
            CardKindFilter.SpellTrap => card is SpellCard or TrapCard,
            _ => true
        };
        if (!kindOk) return false;

        if (LevelMin > 0 || LevelMax > 0 || !string.IsNullOrWhiteSpace(Type) || Attribute != null || AttackMin >= 0 || AttackMax >= 0)
        {
            if (card is not MonsterCard monster) return false;
            int level = candidate.MonsterInstance(state)?.EffectiveLevel ?? monster.Level;
            if (LevelMin > 0 && level < LevelMin) return false;
            if (LevelMax > 0 && level > LevelMax) return false;
            if (AttackMin >= 0 || AttackMax >= 0)
            {
                var instance = candidate.MonsterInstance(state);
                int attack = instance != null ? EffectiveStats.EffectiveAttack(instance, state, state.GetPlayer(candidate.Side)) : monster.Attack;
                if (AttackMin >= 0 && attack < AttackMin) return false;
                if (AttackMax >= 0 && attack > AttackMax) return false;
            }
            if (!string.IsNullOrWhiteSpace(Type) && !string.Equals(monster.Type, Type, StringComparison.OrdinalIgnoreCase)) return false;
            if (Attribute != null && monster.Attribute != Attribute) return false;
        }

        if (CardId > 0 && card.Id != CardId) return false;
        if (NameContains.Length > 0 && card.Name.IndexOf(NameContains, StringComparison.CurrentCultureIgnoreCase) < 0) return false;
        if (SubType != SubTypeFilter.Any && !MatchesSubType(card, SubType)) return false;
        if (SameNameAsSource && !SameName(card, source)) return false;
        if (ExcludeSourceName && SameName(card, source)) return false;

        if (Face != FaceFilter.Any && candidate.Zone is CardZone.MonsterZone or CardZone.SpellTrapZone or CardZone.FieldZone)
        {
            bool faceUp = candidate.IsFaceUp(state);
            if (Face == FaceFilter.FaceUp && !faceUp) return false;
            if (Face == FaceFilter.FaceDown && faceUp) return false;
        }
        return true;
    }

    private static bool MatchesSubType(Card card, SubTypeFilter subType) => card switch
    {
        SpellCard spell => subType.ToString() == spell.SubType.ToString(),
        TrapCard trap => subType.ToString() == trap.SubType.ToString(),
        _ => false
    };

    public static bool SameName(Card a, Card b) => string.Equals(a.Name.Trim(), b.Name.Trim(), StringComparison.OrdinalIgnoreCase);
}
