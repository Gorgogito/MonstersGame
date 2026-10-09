using GodotGame.Core.Effects.Monster;
using GodotGame.Core.Entities;

namespace GodotGame.Core.Battle;

/// <summary>
/// Punto unico para mover cartas entre zonas. Ademas de mover, registra los
/// <see cref="TriggerEvent"/> que pueden disparar efectos de Monstruo
/// ("si esta carta es descartada", "si es destruida en batalla", "si es
/// desterrada"...) y encola los <see cref="DuelEvent"/> visuales. El motor y
/// los pasos de efecto usan esto en vez de tocar las listas directamente, para
/// que ningun movimiento se quede sin su evento.
/// </summary>
public static class CardMover
{
    /// <summary>Dueño real de la carta referida (puede no ser quien la controla si esta en el Campo rival).</summary>
    public static PlayerSide OwnerOf(DuelState state, CardRef card) =>
        card.Zone == CardZone.MonsterZone ? state.GetPlayer(card.Side).MonsterZones[card.Index]?.Owner ?? card.Side : card.Side;

    /// <summary>
    /// Donde esta ahora la carta referida: la misma referencia si no se movio,
    /// otra posicion de la misma zona si solo cambio de indice (mano,
    /// Cementerio, Deck, Destierro), o null si ya no esta en esa zona.
    /// </summary>
    public static CardRef? Locate(DuelState state, CardRef card)
    {
        var player = state.GetPlayer(card.Side);
        switch (card.Zone)
        {
            case CardZone.Hand: return LocateInList(player.Hand, card);
            case CardZone.Deck: return LocateInList(player.Deck, card);
            case CardZone.Graveyard: return LocateInList(player.Graveyard, card);
            case CardZone.Banished: return LocateInList(player.Banished, card);
            case CardZone.MonsterZone:
                return card.Index >= 0 && card.Index < player.MonsterZones.Length && ReferenceEquals(player.MonsterZones[card.Index]?.Card, card.Card) ? card : null;
            case CardZone.SpellTrapZone:
                return card.Index >= 0 && card.Index < player.SpellTrapZones.Length && ReferenceEquals(player.SpellTrapZones[card.Index]?.Card, card.Card) ? card : null;
            case CardZone.FieldZone:
                return ReferenceEquals(player.FieldZone?.Card, card.Card) ? card : null;
            default: return null;
        }
    }

    private static CardRef? LocateInList(List<Card> list, CardRef card)
    {
        if (card.Index >= 0 && card.Index < list.Count && ReferenceEquals(list[card.Index], card.Card)) return card;
        int index = list.FindIndex(c => ReferenceEquals(c, card.Card));
        return index >= 0 ? card with { Index = index } : null;
    }

    /// <summary>Saca la carta de su zona actual (sin eventos). Devuelve el dueño real, o null si ya no estaba.</summary>
    private static PlayerSide? Detach(DuelState state, CardRef card, out CardInstance? monster)
    {
        monster = null;
        var located = Locate(state, card);
        if (located == null) return null;

        var player = state.GetPlayer(located.Side);
        switch (located.Zone)
        {
            case CardZone.Hand: player.Hand.RemoveAt(located.Index); return located.Side;
            case CardZone.Deck: player.Deck.RemoveAt(located.Index); return located.Side;
            case CardZone.Graveyard: player.Graveyard.RemoveAt(located.Index); return located.Side;
            case CardZone.Banished: player.Banished.RemoveAt(located.Index); return located.Side;
            case CardZone.MonsterZone:
                monster = player.MonsterZones[located.Index];
                player.MonsterZones[located.Index] = null;
                EquipCleanup.DetachEquipsTargeting(state, located.Side, located.Index);
                return monster?.Owner ?? located.Side;
            case CardZone.SpellTrapZone:
                var spellTrap = player.SpellTrapZones[located.Index];
                player.SpellTrapZones[located.Index] = null;
                if (spellTrap != null) RemoveEquipModifiers(state, spellTrap);
                return located.Side;
            case CardZone.FieldZone:
                player.FieldZone = null;
                return located.Side;
            default: return null;
        }
    }

    /// <summary>Una Magia de Equipo que deja el Campo se lleva su modificador.</summary>
    private static void RemoveEquipModifiers(DuelState state, SpellTrapInstance equip)
    {
        foreach (var player in state.Players)
            foreach (var monster in player.MonsterZones)
                monster?.ActiveModifiers.RemoveAll(m => ReferenceEquals(m.EquipSource, equip));
    }

    /// <summary>
    /// Manda la carta al Cementerio de su dueño. <paramref name="discard"/> =
    /// es un descarte (desde la mano); <paramref name="destroy"/> = fue
    /// destruida (desde el Campo).
    /// </summary>
    public static bool SendToGraveyard(DuelState state, CardRef card, MoveCause cause, bool discard = false, bool destroy = false)
    {
        var owner = Detach(state, card, out _);
        if (owner == null) return false;

        state.GetPlayer(owner.Value).Graveyard.Add(card.Card);
        var to = CardZone.Graveyard;
        Record(state, EffectEvent.SentToGraveyard, card, owner.Value, to, cause);

        if (discard && card.Zone == CardZone.Hand)
        {
            Record(state, EffectEvent.Discarded, card, owner.Value, to, cause);
            if (cause.Kind == CauseKind.Effect)
                Record(state, EffectEvent.DiscardedByCardEffect, card, owner.Value, to, cause);
            state.Events.Enqueue(new CardDiscardedEvent(owner.Value, card.Card));
        }

        if (destroy)
        {
            Record(state, EffectEvent.Destroyed, card, owner.Value, to, cause);
            if (cause.Kind == CauseKind.Battle) Record(state, EffectEvent.DestroyedByBattle, card, owner.Value, to, cause);
            if (cause.Kind == CauseKind.Effect) Record(state, EffectEvent.DestroyedByEffect, card, owner.Value, to, cause);
        }

        if (card.Zone == CardZone.MonsterZone && card.Card is MonsterCard monster)
            state.Events.Enqueue(new MonsterDestroyedEvent(card.Side, card.Index, monster, ToDestructionCause(cause, destroy)));
        return true;
    }

    /// <summary>Destierra la carta (al Destierro de su dueño).</summary>
    public static bool Banish(DuelState state, CardRef card, MoveCause cause)
    {
        var owner = Detach(state, card, out _);
        if (owner == null) return false;

        state.GetPlayer(owner.Value).Banished.Add(card.Card);
        Record(state, EffectEvent.Banished, card, owner.Value, CardZone.Banished, cause);
        state.Events.Enqueue(new CardBanishedEvent(owner.Value, card.Card));
        if (card.Zone == CardZone.MonsterZone && card.Card is MonsterCard monster)
            state.Events.Enqueue(new MonsterDestroyedEvent(card.Side, card.Index, monster, DestructionCause.Effect));
        return true;
    }

    /// <summary>Añade la carta a la mano de su dueño (desde el Deck, Cementerio, Destierro o el Campo).</summary>
    public static bool AddToHand(DuelState state, CardRef card, MoveCause cause)
    {
        var owner = Detach(state, card, out _);
        if (owner == null) return false;
        state.GetPlayer(owner.Value).Hand.Add(card.Card);
        return true;
    }

    /// <summary>Pone la carta en la parte inferior del Deck de su dueño.</summary>
    public static bool ToDeckBottom(DuelState state, CardRef card, MoveCause cause)
    {
        var owner = Detach(state, card, out _);
        if (owner == null) return false;
        state.GetPlayer(owner.Value).Deck.Add(card.Card);
        return true;
    }

    /// <summary>
    /// Invoca de Modo Especial la carta (debe ser un Monstruo) al Campo de
    /// <paramref name="toSide"/>. Devuelve la Zona usada, o -1 si no hay
    /// Zona libre o la carta ya no estaba.
    /// </summary>
    public static int SpecialSummon(DuelState state, CardRef card, PlayerSide toSide, BattlePosition position, MoveCause cause)
    {
        if (card.Card is not MonsterCard monster) return -1;
        var target = state.GetPlayer(toSide);
        int zone = target.FirstFreeMonsterZone();
        if (zone == -1) return -1;

        // Si la carta esta en el Campo propio y se reinvoca, liberar su zona primero.
        var owner = Detach(state, card, out _);
        if (owner == null) return -1;
        zone = target.FirstFreeMonsterZone();
        if (zone == -1) return -1;

        target.MonsterZones[zone] = new CardInstance(monster, position)
        {
            SummonedThisTurn = true,
            Owner = owner.Value != toSide ? owner.Value : null
        };

        RecordSummon(state, monster, toSide, zone, card.Zone, cause, EffectEvent.SpecialSummoned);
        state.Events.Enqueue(new MonsterSummonedEvent(toSide, zone, monster, SummonKind.Special));
        return zone;
    }

    /// <summary>Registra los eventos de una Invocacion (<paramref name="specific"/>: Normal o Especial).</summary>
    public static void RecordSummon(DuelState state, MonsterCard monster, PlayerSide side, int zone, CardZone from, MoveCause cause, EffectEvent specific)
    {
        var where = new CardRef(monster, side, from, -1);
        Record(state, EffectEvent.Summoned, where, side, CardZone.MonsterZone, cause);
        Record(state, specific, where, side, CardZone.MonsterZone, cause);
        if (specific == EffectEvent.SpecialSummoned && cause.Kind == CauseKind.Effect)
            Record(state, EffectEvent.SpecialSummonedByEffect, where, side, CardZone.MonsterZone, cause);
    }

    /// <summary>Registra un evento que no es un movimiento (volteo, daño de batalla, fase).</summary>
    public static void RecordInPlace(DuelState state, EffectEvent kind, Card card, PlayerSide side, CardZone zone, MoveCause cause) =>
        state.TriggerEvents.Add(new TriggerEvent(kind, card, side, zone, zone, cause));

    private static void Record(DuelState state, EffectEvent kind, CardRef from, PlayerSide controllerAfter, CardZone to, MoveCause cause) =>
        state.TriggerEvents.Add(new TriggerEvent(kind, from.Card, controllerAfter, from.Zone, to, cause));

    private static DestructionCause ToDestructionCause(MoveCause cause, bool destroy) => cause.Kind switch
    {
        CauseKind.Battle => DestructionCause.Battle,
        CauseKind.Effect when destroy => DestructionCause.Effect,
        CauseKind.Effect => DestructionCause.Effect,
        _ => DestructionCause.Cost
    };
}
