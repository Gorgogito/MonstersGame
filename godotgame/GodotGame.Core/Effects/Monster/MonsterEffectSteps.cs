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

/// <summary>Sobre que actua un paso que modifica ATK/DEF (o un pasivo).</summary>
public enum ApplyTo { Self, Targets, Select, AllMatching, Equipped, LastAffected }

/// <summary>Multiplicador de un cambio de ATK/DEF/Nivel.</summary>
public enum ScaleKind
{
    /// <summary>El valor tal cual.</summary>
    None,

    /// <summary>"... por cada carta descartada": valor x cantidad de cartas afectadas por el paso anterior (o el costo).</summary>
    PerLastAffected,

    /// <summary>"ATK igual al Nivel del monstruo descartado x 100": valor x Nivel de la carta afectada por el paso anterior.</summary>
    LastAffectedLevel
}

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

    /// <summary>Las cartas del paso anterior que siguen donde quedaron y cumplen el filtro de la busqueda ("si es un monstruo, Invocalo").</summary>
    public static List<CardRef> LiveLastAffected(MonsterEffectContext ctx, CardQuery? filter = null) =>
        ctx.Activation.LastAffectedRefs.Select(t => CardMover.Locate(ctx.State, t)).Where(t => t != null).Select(t => t!)
            .Where(t => filter == null || filter.Matches(ctx.State, t, ctx.Source)).ToList();

    /// <summary>Candidatos de un paso: los objetivos, las cartas del paso anterior o la busqueda.</summary>
    public static List<CardRef> Candidates(MonsterEffectContext ctx, EffectActionParams p, CardQuery query)
    {
        if (p.GetBool("UseTargets")) return LiveTargets(ctx);
        if (p.GetBool("UseLastAffected")) return LiveLastAffected(ctx, query);
        return query.Candidates(ctx.State, ctx.ControllerSide, ctx.Source, ctx.CurrentSourceRef() ?? ctx.Activation.SourceRef);
    }

    /// <summary>
    /// Candidatos (o los objetivos, si <c>UseTargets</c>) y la eleccion
    /// correspondiente. El llamador hace <c>yield return</c> si no esta respondida.
    /// </summary>
    public static ChoiceRequest Choose(MonsterEffectContext ctx, EffectActionParams p, CardQuery query, string prompt, ChoicePurpose purpose, Func<CardRef, bool>? extra = null)
    {
        List<CardRef> candidates = Candidates(ctx, p, query);
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
        ctx.Activation.LastAffectedRefs.Clear();
        ctx.Activation.LastAffectedBySide.Clear();
        ctx.Activation.LastStepSucceeded = ctx.Activation.LastAffected.Count > 0;
    }

    /// <summary>Igual que <see cref="Succeeded(MonsterEffectContext, IEnumerable{Card})"/>, recordando donde quedo cada carta y de quien era.</summary>
    public static void Succeeded(MonsterEffectContext ctx, IReadOnlyList<CardRef> affected)
    {
        Succeeded(ctx, affected.Select(a => a.Card));
        ctx.Activation.LastAffectedRefs.AddRange(affected);
        foreach (var group in affected.GroupBy(a => a.Side))
            ctx.Activation.LastAffectedBySide[group.Key] = group.Count();
    }

    /// <summary>Si el jugador humano necesita ver cartas ocultas (de la CPU en mano/Deck), una ventana para mostrarselas.</summary>
    public static ChoiceRequest? RevealToHuman(MonsterEffectContext ctx, string prompt, IReadOnlyList<CardRef> cards)
    {
        if (!cards.Any(c => c.Side == PlayerSide.Cpu && c.Zone is CardZone.Hand or CardZone.Deck)) return null;
        return ChoiceRequest.Reveal(PlayerSide.Human, prompt, ctx.Source, cards);
    }
}

// ------------------------------------------------------------------ Robar / descartar

/// <summary>"Roba N carta(s)" / "ambos jugadores roban N".</summary>
internal sealed class DrawStep : IMonsterEffectStep
{
    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        bool sameAsLast = p.GetBool("SameAsLastPerPlayer");
        var lastBySide = new Dictionary<PlayerSide, int>(ctx.Activation.LastAffectedBySide);
        var drawn = new List<CardRef>();
        foreach (var side in StepHelpers.Sides(ctx, p.GetEnum("Who", WhoKind.Controller)))
        {
            var player = ctx.State.GetPlayer(side);
            int count = sameAsLast ? lastBySide.GetValueOrDefault(side) : Math.Max(1, p.GetInt("Count", 1));
            int got = 0;
            for (int i = 0; i < count; i++)
            {
                if (!player.DrawCard()) break;
                drawn.Add(new CardRef(player.Hand[^1], side, CardZone.Hand, player.Hand.Count - 1));
                got++;
            }
            if (count > 0) ctx.Log($"{player.Name} roba {got} carta(s) por el efecto de {ctx.Source.Name}.");
        }
        StepHelpers.Succeeded(ctx, drawn);
        yield break;
    }
}

/// <summary>"Descarta N carta(s)" (quien descarta puede ser el controlador, el adversario o ambos; la carta la elige el dueño, el controlador o el azar).</summary>
internal sealed class DiscardStep : IMonsterEffectStep
{
    /// <summary>
    /// Como costo, todos los jugadores indicados deben poder descartar lo
    /// pedido. Como accion basta con que alguno pueda ("cada jugador que tenga
    /// cartas en la mano descarta 1 carta").
    /// </summary>
    public bool CanRun(MonsterEffectContext ctx, EffectActionParams p)
    {
        int needed = p.GetBool("All") ? 1 : p.GetBool("AnyNumber") ? 1 : Math.Max(1, p.GetInt("Count", 1));
        var sides = StepHelpers.Sides(ctx, p.GetEnum("Who", WhoKind.Controller)).ToList();
        return ctx.Activation.PayingCost
            ? sides.All(side => HandCandidates(ctx, p, side).Count >= needed)
            : sides.Any(side => HandCandidates(ctx, p, side).Count > 0);
    }

    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        int count = Math.Max(1, p.GetInt("Count", 1));
        bool all = p.GetBool("All");
        bool anyNumber = p.GetBool("AnyNumber");
        var chooser = p.GetEnum("Chooser", ChooserKind.Owner);

        // Primero se eligen las cartas de todos (ej. "cada uno elige 1 carta de la mano de su adversario") y despues se descartan juntas.
        var chosen = new List<CardRef>();
        foreach (var side in StepHelpers.Sides(ctx, p.GetEnum("Who", WhoKind.Controller)).ToList())
        {
            var candidates = HandCandidates(ctx, p, side);
            if (candidates.Count == 0) continue;
            if (all)
            {
                chosen.AddRange(candidates);
                continue;
            }
            string who = StepHelpers.PlayerName(ctx, side);
            int min = anyNumber ? 1 : Math.Min(count, candidates.Count);
            int max = anyNumber ? candidates.Count : count;
            string howMany = anyNumber ? "cualquier número de" : count.ToString();
            var request = ctx.SelectCards(chooser, side, $"{ctx.Source.Name}: elige {howMany} carta(s) de la mano de {who} para descartar.",
                candidates, min, max, ChoicePurpose.Harm);
            if (request.Chooser != side) request.RevealCandidates = true;
            if (!request.Answered) yield return request;
            chosen.AddRange(request.SelectedCards);
        }

        var discarded = new List<CardRef>();
        foreach (var card in chosen)
        {
            var live = CardMover.Locate(ctx.State, card);
            if (live == null || !CardMover.SendToGraveyard(ctx.State, live, ctx.Cause, discard: true)) continue;
            var owner = ctx.State.GetPlayer(live.Side);
            discarded.Add(new CardRef(live.Card, live.Side, CardZone.Graveyard, owner.Graveyard.Count - 1));
            ctx.Log($"{owner.Name} descarta {live.Card.Name}.");
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
    public bool CanRun(MonsterEffectContext ctx, EffectActionParams p)
    {
        if (!SummonHelpers.HasFreeZone(ctx, p)) return false;
        if (!p.GetBool("UseLastAffected")) return true;
        var query = new CardQuery(p, defaultFrom: "Graveyard", defaultKind: CardKindFilter.Monster);
        return StepHelpers.Candidates(ctx, p, query).Any(c => c.Card is MonsterCard && c.Zone != CardZone.MonsterZone);
    }

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

    public virtual bool CanRun(MonsterEffectContext ctx, EffectActionParams p)
    {
        var query = new CardQuery(p, DefaultFrom, DefaultSide);
        if (p.GetBool("UseTargets") || p.GetBool("UseLastAffected")) return StepHelpers.Candidates(ctx, p, query).Count > 0;
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
        int scale = p.GetEnum("Scale", ScaleKind.None) switch
        {
            ScaleKind.PerLastAffected => ctx.Activation.LastAffected.Count,
            ScaleKind.LastAffectedLevel => ctx.Activation.LastAffected.OfType<MonsterCard>().Select(m => m.Level).FirstOrDefault(),
            _ => 1
        };
        int attack = p.GetInt("Attack") * scale;
        int defense = p.GetInt("Defense") * scale;
        int level = p.GetInt("Level") * scale;
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
            case ApplyTo.LastAffected:
                monsters.AddRange(StepHelpers.LiveLastAffected(ctx).Where(t => t.Zone == CardZone.MonsterZone));
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
            instance.ActiveModifiers.Add(new ActiveStatModifier(attack, defense, duration) { LevelAmount = level });
            ctx.State.Events.Enqueue(new StatModifierAppliedEvent(monster.Side, monster.Index, attack, defense, ctx.Source.Name));
            ctx.Log($"{instance.Card.Name} {(attack >= 0 ? "gana" : "pierde")} {Math.Abs(attack)} ATK / {Math.Abs(defense)} DEF"
                + (level != 0 ? $" y {(level > 0 ? "gana" : "pierde")} {Math.Abs(level)} Nivel(es)." : "."));
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

// ------------------------------------------------------------------ Esta carta

/// <summary>"Destierra esta carta" (costo o accion), desde donde este: Cementerio, mano o Campo.</summary>
internal sealed class BanishSelfStep : IMonsterEffectStep
{
    public bool CanRun(MonsterEffectContext ctx, EffectActionParams p) => ctx.CurrentSourceRef() is { Zone: not CardZone.Banished };

    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        var self = ctx.CurrentSourceRef();
        if (self != null && self.Zone != CardZone.Banished && CardMover.Banish(ctx.State, self, ctx.Cause))
        {
            ctx.Log($"{ctx.Source.Name} es desterrada.");
            var banished = ctx.State.Players.First(pl => pl.Banished.Any(c => ReferenceEquals(c, ctx.Source)));
            ctx.Activation.SourceRef = new CardRef(ctx.Source, banished.Side, CardZone.Banished, banished.Banished.Count - 1);
            StepHelpers.Succeeded(ctx, new[] { ctx.Source });
        }
        else StepHelpers.Succeeded(ctx, Array.Empty<Card>());
        yield break;
    }
}

/// <summary>"Añade esta carta a tu mano" (desde el Cementerio, el Destierro o el Campo).</summary>
internal sealed class AddSelfToHandStep : IMonsterEffectStep
{
    public bool CanRun(MonsterEffectContext ctx, EffectActionParams p) => ctx.CurrentSourceRef() is { Zone: not CardZone.Hand };

    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        var self = ctx.CurrentSourceRef();
        if (self != null && self.Zone != CardZone.Hand && CardMover.AddToHand(ctx.State, self, ctx.Cause))
        {
            var owner = ctx.State.Players.First(pl => pl.Hand.Any(c => ReferenceEquals(c, ctx.Source)));
            ctx.Log($"{ctx.Source.Name} se añade a la mano de {owner.Name}.");
            ctx.Activation.SourceRef = new CardRef(ctx.Source, owner.Side, CardZone.Hand, owner.Hand.Count - 1);
            StepHelpers.Succeeded(ctx, new[] { ctx.Source });
        }
        else StepHelpers.Succeeded(ctx, Array.Empty<Card>());
        yield break;
    }
}

// ------------------------------------------------------------------ Sacrificar / Colocar

/// <summary>"Sacrifica N monstruo(s)" (normalmente como costo): los manda de tu Campo al Cementerio.</summary>
internal sealed class TributeStep : MoveCardsStep
{
    protected override string DefaultFrom => "MonsterZone";
    protected override ChoicePurpose Purpose => ChoicePurpose.Harm;
    protected override string Verb => "Sacrificar";

    protected override bool Move(MonsterEffectContext ctx, CardRef card)
    {
        if (card.Zone != CardZone.MonsterZone || !CardMover.SendToGraveyard(ctx.State, card, ctx.Cause)) return false;
        ctx.Log($"{ctx.Controller.Name} Sacrifica a {card.Card.Name}.");
        return true;
    }
}

/// <summary>"Coloca 1 Trampa/Magia en tu Campo desde tu mano o Deck".</summary>
internal sealed class SetSpellTrapStep : MoveCardsStep
{
    protected override string DefaultFrom => "Hand,Deck";
    protected override ChoicePurpose Purpose => ChoicePurpose.Benefit;
    protected override string Verb => "Colocar";

    public override bool CanRun(MonsterEffectContext ctx, EffectActionParams p) => ctx.Controller.FirstFreeSpellTrapZone() != -1 && base.CanRun(ctx, p);

    protected override bool Move(MonsterEffectContext ctx, CardRef card)
    {
        if (card.Zone is CardZone.SpellTrapZone or CardZone.FieldZone) return false;
        if (CardMover.SetSpellTrap(ctx.State, card, ctx.Cause) < 0) return false;
        ctx.Log($"{ctx.State.GetPlayer(card.Side).Name} Coloca una carta desde {CardRef.ZoneName(card.Zone)}.");
        return true;
    }
}

// ------------------------------------------------------------------ Ver cartas

/// <summary>"Ambos jugadores muestran sus manos" / "tu adversario muestra su mano".</summary>
internal sealed class RevealHandStep : IMonsterEffectStep
{
    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        var shown = new List<CardRef>();
        foreach (var side in StepHelpers.Sides(ctx, p.GetEnum("Who", WhoKind.Both)))
        {
            var player = ctx.State.GetPlayer(side);
            var hand = CardQuery.Enumerate(player, side, CardZone.Hand).ToList();
            shown.AddRange(hand);
            ctx.Log(hand.Count == 0 ? $"{player.Name} muestra su mano: está vacía." : $"{player.Name} muestra su mano: {string.Join(", ", hand.Select(c => c.Card.Name))}.");
        }
        var reveal = StepHelpers.RevealToHuman(ctx, $"{ctx.Source.Name}: la mano de tu adversario.", shown.Where(c => c.Side == PlayerSide.Cpu).ToList());
        if (reveal != null) yield return reveal;
        StepHelpers.Succeeded(ctx, shown);
    }
}

/// <summary>"Mira N carta(s) al azar de la mano de tu adversario" (quedan como "las cartas del paso anterior").</summary>
internal sealed class RevealRandomHandStep : IMonsterEffectStep
{
    public bool CanRun(MonsterEffectContext ctx, EffectActionParams p) =>
        StepHelpers.Sides(ctx, p.GetEnum("Who", WhoKind.Opponent)).Any(side => ctx.State.GetPlayer(side).Hand.Count > 0);

    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        int count = Math.Max(1, p.GetInt("Count", 1));
        var seen = new List<CardRef>();
        foreach (var side in StepHelpers.Sides(ctx, p.GetEnum("Who", WhoKind.Opponent)))
        {
            var hand = CardQuery.Enumerate(ctx.State.GetPlayer(side), side, CardZone.Hand).ToList();
            seen.AddRange(hand.OrderBy(_ => ctx.State.Rng.Next()).Take(count));
        }
        if (seen.Count > 0)
            ctx.Log($"{ctx.Controller.Name} mira al azar: {string.Join(", ", seen.Select(c => c.Card.Name))}.");
        if (ctx.ControllerSide == PlayerSide.Human)
        {
            var reveal = StepHelpers.RevealToHuman(ctx, $"{ctx.Source.Name}: carta(s) vista(s) al azar.", seen);
            if (reveal != null) yield return reveal;
        }
        StepHelpers.Succeeded(ctx, seen);
    }
}

/// <summary>Que se hace con la carta excavada si cumple el filtro.</summary>
public enum ExcavateAction { SetOnField, AddToHand, SpecialSummon, SendToGraveyard }

/// <summary>Donde va la carta excavada si no cumple el filtro.</summary>
public enum ExcavateOtherwise { Choose, Top, Bottom, Graveyard }

/// <summary>"Excava la carta superior de tu Deck y, si es ..., (Colocala / añadela a tu mano / ...). Si no, ponla arriba o abajo de tu Deck."</summary>
internal sealed class ExcavateStep : IMonsterEffectStep
{
    public bool CanRun(MonsterEffectContext ctx, EffectActionParams p) => ctx.Controller.Deck.Count > 0;

    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        if (ctx.Controller.Deck.Count == 0) { StepHelpers.Succeeded(ctx, Array.Empty<Card>()); yield break; }
        var top = new CardRef(ctx.Controller.Deck[0], ctx.ControllerSide, CardZone.Deck, 0);
        ctx.Log($"{ctx.Controller.Name} excava {top.Card.Name}.");
        if (ctx.ControllerSide == PlayerSide.Cpu)
        {
            var reveal = StepHelpers.RevealToHuman(ctx, $"{ctx.Source.Name}: carta excavada.", new[] { top });
            if (reveal != null) yield return reveal;
        }

        var filter = new CardQuery(p.With("From", "Deck"), "Deck");
        bool matches = filter.Matches(ctx.State, top, ctx.Source);
        if (matches)
        {
            bool done = p.GetEnum("IfMatch", ExcavateAction.SetOnField) switch
            {
                ExcavateAction.AddToHand => CardMover.AddToHand(ctx.State, top, ctx.Cause),
                ExcavateAction.SendToGraveyard => CardMover.SendToGraveyard(ctx.State, top, ctx.Cause),
                ExcavateAction.SpecialSummon => top.Card is MonsterCard && CardMover.SpecialSummon(ctx.State, top, ctx.ControllerSide, BattlePosition.Attack, ctx.Cause) >= 0,
                _ => CardMover.SetSpellTrap(ctx.State, top, ctx.Cause) >= 0
            };
            if (done)
            {
                ctx.Log($"{top.Card.Name} cumple: {MonsterEffectCatalog.ExcavateLabel(p.GetEnum("IfMatch", ExcavateAction.SetOnField)).ToLowerInvariant()}.");
                StepHelpers.Succeeded(ctx, new[] { top.Card });
                yield break;
            }
        }

        var otherwise = p.GetEnum("Otherwise", ExcavateOtherwise.Choose);
        if (otherwise == ExcavateOtherwise.Choose)
        {
            var ask = ChoiceRequest.Pick(ctx.ControllerSide, $"{top.Card.Name}: ¿dónde la pones?", ctx.Source, new[] { "Arriba del Deck", "Abajo del Deck" });
            yield return ask;
            otherwise = ask.Option == 1 ? ExcavateOtherwise.Bottom : ExcavateOtherwise.Top;
        }
        var live = CardMover.Locate(ctx.State, top);
        if (live != null)
        {
            switch (otherwise)
            {
                case ExcavateOtherwise.Bottom: CardMover.ToDeckBottom(ctx.State, live, ctx.Cause); ctx.Log($"{top.Card.Name} va abajo del Deck."); break;
                case ExcavateOtherwise.Graveyard: CardMover.SendToGraveyard(ctx.State, live, ctx.Cause); ctx.Log($"{top.Card.Name} va al Cementerio."); break;
                default: ctx.Log($"{top.Card.Name} vuelve arriba del Deck."); break;
            }
        }
        StepHelpers.Succeeded(ctx, Array.Empty<Card>());
    }
}

// ------------------------------------------------------------------ Reglas del turno

/// <summary>"No puedes Invocar monstruos el resto de este turno (pero puedes Colocar)".</summary>
internal sealed class SummonLockStep : IMonsterEffectStep
{
    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        ctx.Controller.CannotSummonThisTurn = true;
        ctx.Log($"{ctx.Controller.Name} no puede Invocar monstruos el resto del turno.");
        ctx.Activation.LastStepSucceeded = true;
        yield break;
    }
}

/// <summary>
/// Efecto Rapido de "cambio de efecto": el efecto activado al que responde se
/// convierte en "Tu adversario descarta N carta(s)" (el adversario de quien
/// activo ese efecto, o sea, normalmente tu).
/// </summary>
internal sealed class ReplaceRespondedEffectStep : IMonsterEffectStep
{
    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        ctx.Activation.ReplaceRespondedLink?.Invoke(Math.Max(1, p.GetInt("Count", 1)));
        ctx.Activation.LastStepSucceeded = ctx.Activation.ReplaceRespondedLink != null;
        yield break;
    }
}

// ------------------------------------------------------------------ Fusion por efecto

/// <summary>Que se hace con los materiales de una Invocacion por Fusion por efecto.</summary>
public enum MaterialMove { Graveyard, Banish }

/// <summary>
/// "Invoca por Fusion, desde tu Deck Extra, 1 Monstruo de Fusion ...,
/// desterrando/mandando al Cementerio de tu Campo/Cementerio materiales
/// mencionados en el". En este juego los Monstruos de Fusion salen de las
/// recetas (no hay Deck Extra): se ofrecen los resultados cuyos materiales
/// esten disponibles en las zonas indicadas.
/// </summary>
internal sealed class FusionSummonStep : IMonsterEffectStep
{
    private sealed record Option(MonsterCard Result, List<CardRef> Materials);

    public bool CanRun(MonsterEffectContext ctx, EffectActionParams p) => Options(ctx, p).Count > 0;

    private static List<Option> Options(MonsterEffectContext ctx, EffectActionParams p)
    {
        var options = new List<Option>();
        if (ctx.State.Fusion == null) return options;

        var resultFilter = new CardQuery(p.With("From", "Deck").With("CardKind", "Monster"), "Deck");
        string handArchetype = p.GetString("HandIfNameContains").Trim();
        var zones = CardQuery.ParseZones(p.GetString("From", "MonsterZone,Graveyard"));
        List<CardRef> Pool(bool withHand)
        {
            var pool = new List<CardRef>();
            foreach (var zone in zones.Append(CardZone.Hand).Distinct())
            {
                if (zone == CardZone.Hand && !withHand && !zones.Contains(CardZone.Hand)) continue;
                pool.AddRange(CardQuery.Enumerate(ctx.Controller, ctx.ControllerSide, zone).Where(c => c.Card is MonsterCard));
            }
            return pool;
        }

        var basePool = Pool(false);
        var handPool = handArchetype.Length > 0 ? Pool(true) : basePool;
        foreach (var (result, pool) in ctx.State.Fusion.ResultsFor(handPool.Select(c => (MonsterCard)c.Card).ToList()).Select(r => (r, handPool))
                     .Concat(ctx.State.Fusion.ResultsFor(basePool.Select(c => (MonsterCard)c.Card).ToList()).Select(r => (r, basePool))))
        {
            if (!resultFilter.Matches(ctx.State, new CardRef(result.Result, ctx.ControllerSide, CardZone.Deck, -1), ctx.Source)) continue;
            bool handAllowed = ReferenceEquals(pool, basePool) || result.Result.Name.IndexOf(handArchetype, StringComparison.CurrentCultureIgnoreCase) >= 0;
            if (!handAllowed) continue;

            // Cada material usado se asigna a una carta concreta de las zonas (en el orden de la busqueda).
            var used = new List<CardRef>();
            foreach (var material in result.Materials)
            {
                var match = pool.FirstOrDefault(c => ReferenceEquals(c.Card, material) && !used.Contains(c));
                if (match == null) { used.Clear(); break; }
                used.Add(match);
            }
            if (used.Count == 0) continue;
            bool freesZone = used.Any(c => c.Zone == CardZone.MonsterZone);
            if (!freesZone && ctx.Controller.FirstFreeMonsterZone() == -1) continue;
            if (options.Any(o => o.Result.Id == result.Result.Id)) continue;
            options.Add(new Option(result.Result, used));
        }
        return options;
    }

    public IEnumerable<ChoiceRequest> Run(MonsterEffectContext ctx, EffectActionParams p)
    {
        var options = Options(ctx, p);
        if (options.Count == 0) { StepHelpers.Succeeded(ctx, Array.Empty<Card>()); yield break; }

        var chosen = options[0];
        if (options.Count > 1 || ctx.ControllerSide == PlayerSide.Human)
        {
            var pick = ChoiceRequest.Pick(ctx.ControllerSide, $"{ctx.Source.Name}: ¿qué Monstruo de Fusión Invocas?", ctx.Source,
                options.Select(o => $"{o.Result.Name} ({o.Result.Attack}/{o.Result.Defense}) — materiales: {string.Join(" + ", o.Materials.Select(m => $"{m.Card.Name} [{CardRef.ZoneName(m.Zone)}]"))}").ToList());
            yield return pick;
            chosen = options[Math.Clamp(pick.Option, 0, options.Count - 1)];
        }

        var move = p.GetEnum("MaterialMove", MaterialMove.Banish);
        var materials = new List<MonsterCard>();
        // Indices mayores primero dentro de cada zona, para no desplazar a los que faltan.
        foreach (var material in chosen.Materials.OrderByDescending(m => m.Index))
        {
            var live = CardMover.Locate(ctx.State, material);
            if (live == null) continue;
            bool moved = live.Zone == CardZone.Hand
                ? CardMover.SendToGraveyard(ctx.State, live, ctx.Cause, discard: true)
                : move == MaterialMove.Banish ? CardMover.Banish(ctx.State, live, ctx.Cause) : CardMover.SendToGraveyard(ctx.State, live, ctx.Cause);
            if (moved) materials.Add((MonsterCard)live.Card);
        }

        var position = p.GetEnum("Position", PositionChoice.Attack);
        BattlePosition battlePosition = position == PositionChoice.Defense ? BattlePosition.DefenseFaceUp : BattlePosition.Attack;
        if (position == PositionChoice.Choose)
        {
            var ask = ChoiceRequest.Pick(ctx.ControllerSide, $"¿En qué posición Invocas a {chosen.Result.Name}?", ctx.Source, new[] { "Ataque", "Defensa" });
            yield return ask;
            battlePosition = ask.Option == 1 ? BattlePosition.DefenseFaceUp : BattlePosition.Attack;
        }

        int zone = CardMover.SummonFromOutside(ctx.State, chosen.Result, ctx.ControllerSide, battlePosition, ctx.Cause, SummonMethod.Fusion);
        if (zone < 0) { StepHelpers.Succeeded(ctx, Array.Empty<Card>()); yield break; }
        ctx.State.Events.Enqueue(new FusionPerformedEvent(ctx.ControllerSide, zone, materials, chosen.Result));
        ctx.Log($"{ctx.Controller.Name} Invoca por Fusión a {chosen.Result.Name} ({string.Join(" + ", materials.Select(m => m.Name))}).");
        StepHelpers.Succeeded(ctx, new[] { new CardRef(chosen.Result, ctx.ControllerSide, CardZone.MonsterZone, zone) });
    }
}
