using GodotGame.Core.Battle;
using GodotGame.Core.Entities;

namespace GodotGame.Core.Effects.Monster;

/// <summary>
/// Un paso atomico de un efecto de Monstruo (costo o accion). Es una
/// corrutina: devuelve las <see cref="ChoiceRequest"/> que necesita que un
/// jugador responda; el motor pausa el duelo hasta tener la respuesta y luego
/// continua el paso. Debe ser defensivo: si lo que buscaba ya no esta, no hace
/// nada (y no marca <see cref="EffectActivation.LastStepSucceeded"/>).
/// </summary>
public interface IMonsterEffectStep
{
    /// <summary>Si el paso puede realizarse ahora (para costos: si se puede pagar).</summary>
    bool CanRun(MonsterEffectContext ctx, EffectActionParams p) => true;

    IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p);
}

/// <summary>A quien afecta un paso sin seleccion de cartas (robar, descartar, LP).</summary>
public enum WhoKind { Controller, Opponent, Both }

/// <summary>A que Campo va una Invocacion Especial.</summary>
public enum FieldChoice { Own, Opponent, Choose }

/// <summary>En que posicion se Invoca.</summary>
public enum PositionChoice { Attack, Defense, Choose }

/// <summary>Sobre que actua un paso que modifica ATK/DEF.</summary>
public enum ApplyTo { Self, Targets, Select, AllMatching }

internal static class StepHelpers
{
    public static IEnumerable<PlayerSide> Sides(MonsterEffectContext ctx, WhoKind who)
    {
        if (who is WhoKind.Controller or WhoKind.Both) yield return ctx.ControllerSide;
        if (who is WhoKind.Opponent or WhoKind.Both) yield return ctx.OpponentSide;
    }

    /// <summary>Los objetivos elegidos al activar que siguen donde estaban ("si los hay").</summary>
    public static List<CardRef> LiveTargets(MonsterEffectContext ctx) =>
        ctx.Activation.Targets.Select(t => CardMover.Locate(ctx.State, t)).Where(t => t != null).Select(t => t!).ToList();

    public static string Describe(CardRef card) => $"{card.Card.Name} ({CardRef.ZoneName(card.Zone)})";

    public static string PlayerName(MonsterEffectContext ctx, PlayerSide side) => ctx.State.GetPlayer(side).Name;

    /// <summary>
    /// Candidatos (o los objetivos, si <c>UseTargets</c>) y la eleccion
    /// correspondiente. El llamador hace <c>yield return</c> si no esta respondida.
    /// </summary>
    public static ChoiceRequest Choose(MonsterEffectContext ctx, EffectActionParams p, CardQuery query, string prompt, ChoicePurpose purpose, Func<CardRef, bool>? extra = null)
    {
        List<CardRef> candidates = p.GetBool("UseTargets")
            ? LiveTargets(ctx)
            : query.Candidates(ctx.State, ctx.ControllerSide, ctx.Source, ctx.CurrentSourceRef() ?? ctx.Activation.SourceRef);
        if (extra != null) candidates = candidates.Where(extra).ToList();

        // Los objetivos ya estan elegidos: se usan todos sin volver a preguntar.
        if (p.GetBool("UseTargets"))
        {
            var all = ChoiceRequest.Cards(ctx.ControllerSide, prompt, ctx.Source, candidates, candidates.Count, candidates.Count, purpose);
            all.AnswerCards(Enumerable.Range(0, candidates.Count).ToList());
            return all;
        }

        PlayerSide owner = candidates.Count > 0 ? candidates[0].Side : ctx.ControllerSide;
        return ctx.SelectCards(query.Chooser, owner, prompt, candidates, query.Min, query.Count, purpose);
    }

    public static void Succeeded(MonsterEffectContext ctx, IEnumerable<Card> affected)
    {
        ctx.Activation.LastAffected.Clear();
        ctx.Activation.LastAffected.AddRange(affected);
        ctx.Activation.LastStepSucceeded = ctx.Activation.LastAffected.Count > 0;
    }
}

// ------------------------------------------------------------------ Robar / descartar

/// <summary>"Roba N carta(s)" / "ambos jugadores roban N".</summary>
internal sealed class DrawStep : IMonsterEffectStep
{
    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        int count = Math.Max(1, p.GetInt("Count", 1));
        var drawn = new List<Card>();
        foreach (var side in StepHelpers.Sides(ctx, p.GetEnum("Who", WhoKind.Controller)))
        {
            var player = ctx.State.GetPlayer(side);
            for (int i = 0; i < count; i++)
            {
                if (!player.DrawCard()) break;
                drawn.Add(player.Hand[^1]);
            }
            ctx.Log($"{player.Name} roba {count} carta(s) por el efecto de {ctx.Source.Name}.");
        }
        StepHelpers.Succeeded(ctx, drawn);
        yield break;
    }
}

/// <summary>"Descarta N carta(s)" (quien descarta puede ser el controlador, el adversario o ambos; la carta la elige el dueño, el controlador o el azar).</summary>
internal sealed class DiscardStep : IMonsterEffectStep
{
    public bool CanRun(MonsterEffectContext ctx, EffectActionParams p)
    {
        int count = Math.Max(1, p.GetInt("Count", 1));
        return StepHelpers.Sides(ctx, p.GetEnum("Who", WhoKind.Controller)).All(side => HandCandidates(ctx, p, side).Count >= count);
    }

    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        int count = Math.Max(1, p.GetInt("Count", 1));
        var chooser = p.GetEnum("Chooser", ChooserKind.Owner);
        var discarded = new List<Card>();

        foreach (var side in StepHelpers.Sides(ctx, p.GetEnum("Who", WhoKind.Controller)).ToList())
        {
            var candidates = HandCandidates(ctx, p, side);
            if (candidates.Count == 0) continue;
            string who = StepHelpers.PlayerName(ctx, side);
            var request = ctx.SelectCards(chooser, side, $"{ctx.Source.Name}: elige {count} carta(s) de la mano de {who} para descartar.",
                candidates, Math.Min(count, candidates.Count), count, ChoicePurpose.Harm);
            if (!request.Answered) yield return request;

            foreach (var card in request.SelectedCards)
            {
                if (CardMover.SendToGraveyard(ctx.State, card, ctx.Cause, discard: true))
                {
                    discarded.Add(card.Card);
                    ctx.Log($"{who} descarta {card.Card.Name}.");
                }
            }
        }
        StepHelpers.Succeeded(ctx, discarded);
    }

    private static List<CardRef> HandCandidates(MonsterEffectContext ctx, EffectActionParams p, PlayerSide side)
    {
        var query = new CardQuery(p.With("From", "Hand").With("Side", "Own"));
        return query.Candidates(ctx.State, side, ctx.Source, null);
    }
}

/// <summary>Costo "descarta esta carta" (esta carta debe estar en la mano).</summary>
internal sealed class DiscardSelfStep : IMonsterEffectStep
{
    public bool CanRun(MonsterEffectContext ctx, EffectActionParams p) => ctx.CurrentSourceRef()?.Zone == CardZone.Hand;

    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        var self = ctx.CurrentSourceRef();
        if (self?.Zone == CardZone.Hand && CardMover.SendToGraveyard(ctx.State, self, ctx.Cause, discard: true))
        {
            ctx.Log($"{ctx.Controller.Name} descarta {ctx.Source.Name}.");
            ctx.Activation.SourceRef = new CardRef(ctx.Source, ctx.ControllerSide, CardZone.Graveyard, ctx.Controller.Graveyard.Count - 1);
            StepHelpers.Succeeded(ctx, new[] { ctx.Source });
        }
        else StepHelpers.Succeeded(ctx, Array.Empty<Card>());
        yield break;
    }
}

/// <summary>Costo "muestra esta carta en tu mano".</summary>
internal sealed class RevealSelfStep : IMonsterEffectStep
{
    public bool CanRun(MonsterEffectContext ctx, EffectActionParams p) => ctx.CurrentSourceRef()?.Zone == CardZone.Hand;

    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        ctx.Log($"{ctx.Controller.Name} muestra {ctx.Source.Name} de su mano.");
        StepHelpers.Succeeded(ctx, new[] { ctx.Source });
        yield break;
    }
}

// ------------------------------------------------------------------ Invocaciones

internal static class SummonHelpers
{
    public static IEnumerable<ChoiceRequest> ChooseFieldAndPosition(MonsterEffectContext ctx, EffectActionParams p, MonsterCard monster, Action<PlayerSide, BattlePosition> result)
    {
        var field = p.GetEnum("ToField", FieldChoice.Own);
        PlayerSide toSide = field == FieldChoice.Opponent ? ctx.OpponentSide : ctx.ControllerSide;
        if (field == FieldChoice.Choose)
        {
            bool ownFree = ctx.Controller.FirstFreeMonsterZone() != -1;
            bool oppFree = ctx.Opponent.FirstFreeMonsterZone() != -1;
            if (ownFree && oppFree)
            {
                var request = ChoiceRequest.Pick(ctx.ControllerSide, $"¿A qué Campo Invocas a {monster.Name}?", ctx.Source, new[] { "Tu Campo", "Campo del adversario" });
                yield return request;
                toSide = request.Option == 1 ? ctx.OpponentSide : ctx.ControllerSide;
            }
            else toSide = oppFree ? ctx.OpponentSide : ctx.ControllerSide;
        }

        var position = p.GetEnum("Position", PositionChoice.Attack);
        BattlePosition battlePosition = position == PositionChoice.Defense ? BattlePosition.DefenseFaceUp : BattlePosition.Attack;
        if (position == PositionChoice.Choose)
        {
            var request = ChoiceRequest.Pick(ctx.ControllerSide, $"¿En qué posición Invocas a {monster.Name}?", ctx.Source, new[] { "Ataque", "Defensa" });
            yield return request;
            battlePosition = request.Option == 1 ? BattlePosition.DefenseFaceUp : BattlePosition.Attack;
        }

        result(toSide, battlePosition);
    }

    public static bool HasFreeZone(MonsterEffectContext ctx, EffectActionParams p) => p.GetEnum("ToField", FieldChoice.Own) switch
    {
        FieldChoice.Opponent => ctx.Opponent.FirstFreeMonsterZone() != -1,
        FieldChoice.Choose => ctx.Controller.FirstFreeMonsterZone() != -1 || ctx.Opponent.FirstFreeMonsterZone() != -1,
        _ => ctx.Controller.FirstFreeMonsterZone() != -1
    };

    public static void LogSummon(MonsterEffectContext ctx, MonsterCard monster, PlayerSide toSide, CardZone from) =>
        ctx.Log($"{monster.Name} es Invocado de Modo Especial desde {CardRef.ZoneName(from)} al Campo de {ctx.State.GetPlayer(toSide).Name}.");
}

/// <summary>"Invoca esta carta de Modo Especial" (desde donde este: mano, Cementerio, Destierro...).</summary>
internal sealed class SpecialSummonSelfStep : IMonsterEffectStep
{
    public bool CanRun(MonsterEffectContext ctx, EffectActionParams p) =>
        ctx.Source is MonsterCard && ctx.CurrentSourceRef() is { Zone: not CardZone.MonsterZone } && SummonHelpers.HasFreeZone(ctx, p);

    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        var self = ctx.CurrentSourceRef();
        if (self == null || self.Zone == CardZone.MonsterZone || ctx.Source is not MonsterCard monster || !SummonHelpers.HasFreeZone(ctx, p))
        {
            StepHelpers.Succeeded(ctx, Array.Empty<Card>());
            yield break;
        }

        PlayerSide toSide = ctx.ControllerSide;
        BattlePosition position = BattlePosition.Attack;
        foreach (var r in SummonHelpers.ChooseFieldAndPosition(ctx, p, monster, (s, pos) => { toSide = s; position = pos; })) yield return r;

        self = ctx.CurrentSourceRef();
        int zone = self == null ? -1 : CardMover.SpecialSummon(ctx.State, self, toSide, position, ctx.Cause);
        if (zone >= 0)
        {
            SummonHelpers.LogSummon(ctx, monster, toSide, self!.Zone);
            ctx.Activation.SourceRef = new CardRef(monster, toSide, CardZone.MonsterZone, zone);
            StepHelpers.Succeeded(ctx, new[] { ctx.Source });
        }
        else StepHelpers.Succeeded(ctx, Array.Empty<Card>());
    }
}

/// <summary>"Invoca de Modo Especial N monstruo(s) ... desde ... a ... Campo".</summary>
internal sealed class SpecialSummonStep : IMonsterEffectStep
{
    public bool CanRun(MonsterEffectContext ctx, EffectActionParams p) => SummonHelpers.HasFreeZone(ctx, p);

    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        var query = new CardQuery(p, defaultFrom: "Graveyard", defaultKind: CardKindFilter.Monster);
        var request = StepHelpers.Choose(ctx, p, query, $"{ctx.Source.Name}: elige que monstruo(s) Invocar de Modo Especial.", ChoicePurpose.Benefit,
            c => c.Card is MonsterCard && c.Zone != CardZone.MonsterZone);
        if (!request.Answered) yield return request;

        var summoned = new List<Card>();
        foreach (var chosen in request.SelectedCards)
        {
            if (chosen.Card is not MonsterCard monster || !SummonHelpers.HasFreeZone(ctx, p)) continue;
            PlayerSide toSide = ctx.ControllerSide;
            BattlePosition position = BattlePosition.Attack;
            foreach (var r in SummonHelpers.ChooseFieldAndPosition(ctx, p, monster, (s, pos) => { toSide = s; position = pos; })) yield return r;

            var live = CardMover.Locate(ctx.State, chosen);
            if (live == null) continue;
            if (CardMover.SpecialSummon(ctx.State, live, toSide, position, ctx.Cause) >= 0)
            {
                SummonHelpers.LogSummon(ctx, monster, toSide, live.Zone);
                summoned.Add(monster);
            }
        }
        StepHelpers.Succeeded(ctx, summoned);
    }
}

// ------------------------------------------------------------------ Mover cartas

/// <summary>Base para los pasos "elige cartas que cumplan X y haz Y con ellas".</summary>
internal abstract class MoveCardsStep : IMonsterEffectStep
{
    protected abstract string DefaultFrom { get; }
    protected virtual RelativeSide DefaultSide => RelativeSide.Own;
    protected abstract ChoicePurpose Purpose { get; }
    protected abstract string Verb { get; }
    protected abstract bool Move(MonsterEffectContext ctx, CardRef card);

    public bool CanRun(MonsterEffectContext ctx, EffectActionParams p)
    {
        if (p.GetBool("UseTargets")) return StepHelpers.LiveTargets(ctx).Count > 0;
        var query = new CardQuery(p, DefaultFrom, DefaultSide);
        return query.Candidates(ctx.State, ctx.ControllerSide, ctx.Source, ctx.CurrentSourceRef() ?? ctx.Activation.SourceRef).Count >= Math.Max(1, query.Min);
    }

    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        var query = new CardQuery(p, DefaultFrom, DefaultSide);
        var request = StepHelpers.Choose(ctx, p, query, $"{ctx.Source.Name}: elige que carta(s) {Verb}.", Purpose);
        if (!request.Answered) yield return request;

        var moved = new List<Card>();
        foreach (var chosen in request.SelectedCards)
        {
            var live = CardMover.Locate(ctx.State, chosen);
            if (live != null && Move(ctx, live)) moved.Add(live.Card);
        }
        StepHelpers.Succeeded(ctx, moved);
    }
}

internal sealed class AddToHandStep : MoveCardsStep
{
    protected override string DefaultFrom => "Deck";
    protected override ChoicePurpose Purpose => ChoicePurpose.Benefit;
    protected override string Verb => "añadir a la mano";

    protected override bool Move(MonsterEffectContext ctx, CardRef card)
    {
        if (!CardMover.AddToHand(ctx.State, card, ctx.Cause)) return false;
        ctx.Log($"{card.Card.Name} se añade a la mano desde {CardRef.ZoneName(card.Zone)}.");
        return true;
    }
}

internal sealed class ReturnToHandStep : MoveCardsStep
{
    protected override string DefaultFrom => "MonsterZone";
    protected override ChoicePurpose Purpose => ChoicePurpose.Harm;
    protected override string Verb => "devolver a la mano";

    protected override bool Move(MonsterEffectContext ctx, CardRef card)
    {
        if (!CardMover.AddToHand(ctx.State, card, ctx.Cause)) return false;
        ctx.Log($"{card.Card.Name} vuelve a la mano.");
        return true;
    }
}

internal sealed class DestroyStep : MoveCardsStep
{
    protected override string DefaultFrom => "MonsterZone";
    protected override RelativeSide DefaultSide => RelativeSide.Opponent;
    protected override ChoicePurpose Purpose => ChoicePurpose.Harm;
    protected override string Verb => "destruir";

    protected override bool Move(MonsterEffectContext ctx, CardRef card)
    {
        if (card.Zone is not (CardZone.MonsterZone or CardZone.SpellTrapZone or CardZone.FieldZone)) return false;
        if (!CardMover.SendToGraveyard(ctx.State, card, ctx.Cause, destroy: true)) return false;
        ctx.Log($"{ctx.Source.Name} destruye {card.Card.Name}.");
        return true;
    }
}

internal sealed class BanishStep : MoveCardsStep
{
    protected override string DefaultFrom => "Graveyard";
    protected override RelativeSide DefaultSide => RelativeSide.Opponent;
    protected override ChoicePurpose Purpose => ChoicePurpose.Harm;
    protected override string Verb => "desterrar";

    protected override bool Move(MonsterEffectContext ctx, CardRef card)
    {
        if (!CardMover.Banish(ctx.State, card, ctx.Cause)) return false;
        ctx.Log($"{card.Card.Name} es desterrada.");
        return true;
    }
}

internal sealed class SendToGraveyardStep : MoveCardsStep
{
    protected override string DefaultFrom => "Deck";
    protected override ChoicePurpose Purpose => ChoicePurpose.Neutral;
    protected override string Verb => "mandar al Cementerio";

    protected override bool Move(MonsterEffectContext ctx, CardRef card)
    {
        if (!CardMover.SendToGraveyard(ctx.State, card, ctx.Cause)) return false;
        ctx.Log($"{card.Card.Name} es mandada al Cementerio.");
        return true;
    }
}

/// <summary>"Destruye todas las cartas que cumplan X" (sin elegir).</summary>
internal sealed class DestroyAllStep : IMonsterEffectStep
{
    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        var query = new CardQuery(p, "SpellTrapZone", RelativeSide.Opponent);
        var all = query.Candidates(ctx.State, ctx.ControllerSide, ctx.Source, ctx.CurrentSourceRef() ?? ctx.Activation.SourceRef)
            .Where(c => c.Zone is CardZone.MonsterZone or CardZone.SpellTrapZone or CardZone.FieldZone)
            .ToList();
        var destroyed = new List<Card>();
        foreach (var card in all)
        {
            var live = CardMover.Locate(ctx.State, card);
            if (live != null && CardMover.SendToGraveyard(ctx.State, live, ctx.Cause, destroy: true))
                destroyed.Add(live.Card);
        }
        if (destroyed.Count > 0) ctx.Log($"{ctx.Source.Name} destruye {destroyed.Count} carta(s).");
        StepHelpers.Succeeded(ctx, destroyed);
        yield break;
    }
}

/// <summary>"Pone N carta(s) de su mano en la parte inferior de su Deck" (las elige el dueño).</summary>
internal sealed class DeckBottomStep : IMonsterEffectStep
{
    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        int count = Math.Max(1, p.GetInt("Count", 1));
        var moved = new List<Card>();
        foreach (var side in StepHelpers.Sides(ctx, p.GetEnum("Who", WhoKind.Opponent)).ToList())
        {
            var player = ctx.State.GetPlayer(side);
            var candidates = CardQuery.Enumerate(player, side, CardZone.Hand).ToList();
            if (candidates.Count == 0) continue;
            var request = ctx.SelectCards(p.GetEnum("Chooser", ChooserKind.Owner), side,
                $"{ctx.Source.Name}: elige {count} carta(s) de la mano de {player.Name} para poner en la parte inferior del Deck.",
                candidates, Math.Min(count, candidates.Count), count, ChoicePurpose.Harm);
            if (!request.Answered) yield return request;

            foreach (var card in request.SelectedCards)
            {
                var live = CardMover.Locate(ctx.State, card);
                if (live != null && CardMover.ToDeckBottom(ctx.State, live, ctx.Cause)) moved.Add(live.Card);
            }
            ctx.Log($"{player.Name} pone {moved.Count} carta(s) en la parte inferior de su Deck.");
        }
        StepHelpers.Succeeded(ctx, moved);
    }
}

// ------------------------------------------------------------------ ATK/DEF y LP

/// <summary>"(Esta carta / ese objetivo / los monstruos que ...) gana(n) X ATK / Y DEF".</summary>
internal sealed class ModifyStatsStep : IMonsterEffectStep
{
    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        int attack = p.GetInt("Attack");
        int defense = p.GetInt("Defense");
        var duration = p.GetEnum("Duration", ModifierDuration.Permanent);
        if (duration is ModifierDuration.WhileEquipped or ModifierDuration.ForNTurns) duration = ModifierDuration.Permanent;

        var monsters = new List<CardRef>();
        switch (p.GetEnum("Apply", ApplyTo.Self))
        {
            case ApplyTo.Self:
                if (ctx.CurrentSourceRef() is { Zone: CardZone.MonsterZone } self) monsters.Add(self);
                break;
            case ApplyTo.Targets:
                monsters.AddRange(StepHelpers.LiveTargets(ctx).Where(t => t.Zone == CardZone.MonsterZone));
                break;
            case ApplyTo.AllMatching:
                monsters.AddRange(new CardQuery(p.With("From", "MonsterZone"), "MonsterZone").Candidates(ctx.State, ctx.ControllerSide, ctx.Source, null)
                    .Where(c => c.IsFaceUp(ctx.State)));
                break;
            case ApplyTo.Select:
                var query = new CardQuery(p.With("From", "MonsterZone"), "MonsterZone");
                var request = StepHelpers.Choose(ctx, p.With("UseTargets", "false"), query, $"{ctx.Source.Name}: elige a que monstruo(s) aplicar el cambio de ATK/DEF.",
                    attack + defense >= 0 ? ChoicePurpose.Benefit : ChoicePurpose.Harm);
                if (!request.Answered) yield return request;
                monsters.AddRange(request.SelectedCards);
                break;
        }

        var affected = new List<Card>();
        foreach (var monster in monsters)
        {
            var instance = monster.MonsterInstance(ctx.State);
            if (instance == null) continue;
            instance.ActiveModifiers.Add(new ActiveStatModifier(attack, defense, duration));
            ctx.State.Events.Enqueue(new StatModifierAppliedEvent(monster.Side, monster.Index, attack, defense, ctx.Source.Name));
            ctx.Log($"{instance.Card.Name} {(attack >= 0 ? "gana" : "pierde")} {Math.Abs(attack)} ATK / {Math.Abs(defense)} DEF.");
            affected.Add(instance.Card);
        }
        StepHelpers.Succeeded(ctx, affected);
    }
}

/// <summary>"Inflige X puntos de daño".</summary>
internal sealed class DamageStep : IMonsterEffectStep
{
    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        int amount = Math.Max(0, p.GetInt("Amount"));
        foreach (var side in StepHelpers.Sides(ctx, p.GetEnum("Who", WhoKind.Opponent)))
        {
            var player = ctx.State.GetPlayer(side);
            player.LifePoints = Math.Max(0, player.LifePoints - amount);
            ctx.Log($"{ctx.Source.Name} inflige {amount} de daño a {player.Name}.");
        }
        ctx.Activation.LastStepSucceeded = amount > 0;
        yield break;
    }
}

/// <summary>"Ganas X LP".</summary>
internal sealed class GainLifeStep : IMonsterEffectStep
{
    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        int amount = Math.Max(0, p.GetInt("Amount"));
        foreach (var side in StepHelpers.Sides(ctx, p.GetEnum("Who", WhoKind.Controller)))
        {
            var player = ctx.State.GetPlayer(side);
            player.LifePoints += amount;
            ctx.Log($"{player.Name} gana {amount} LP por el efecto de {ctx.Source.Name}.");
        }
        ctx.Activation.LastStepSucceeded = amount > 0;
        yield break;
    }
}

/// <summary>Costo "paga X LP".</summary>
internal sealed class PayLifeStep : IMonsterEffectStep
{
    public bool CanRun(MonsterEffectContext ctx, EffectActionParams p) => ctx.Controller.LifePoints > p.GetInt("Amount");

    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        int amount = Math.Max(0, p.GetInt("Amount"));
        ctx.Controller.LifePoints = Math.Max(0, ctx.Controller.LifePoints - amount);
        ctx.Log($"{ctx.Controller.Name} paga {amount} LP.");
        ctx.Activation.LastStepSucceeded = true;
        yield break;
    }
}

/// <summary>"Niega la activacion" (efecto Rapido de negacion: niega el eslabon al que responde).</summary>
internal sealed class NegateActivationStep : IMonsterEffectStep
{
    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        ctx.Activation.NegateRespondedLink?.Invoke();
        ctx.Activation.LastStepSucceeded = ctx.Activation.NegateRespondedLink != null;
        yield break;
    }
}

/// <summary>Los pasos de un efecto Continuo no se ejecutan: el motor los consulta en vivo (ver <see cref="ContinuousEffects"/>).</summary>
internal sealed class PassiveStep : IMonsterEffectStep
{
    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p) { yield break; }
}
