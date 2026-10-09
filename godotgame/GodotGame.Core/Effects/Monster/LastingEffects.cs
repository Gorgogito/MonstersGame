using GodotGame.Core.Battle;
using GodotGame.Core.Entities;

namespace GodotGame.Core.Effects.Monster;

/// <summary>
/// "Mira todas las cartas que robe hasta el final de su 3er turno y destruye
/// las que ...": un efecto que dura varios turnos del jugador vigilado.
/// <see cref="StartTurn"/> es el turno en que se resolvio (ese no cuenta).
/// </summary>
public sealed class DrawWatch
{
    public required Card Source { get; init; }
    public required PlayerSide Controller { get; init; }
    public required PlayerSide Watched { get; init; }
    public required EffectActionParams Filter { get; init; }
    public required int StartTurn { get; init; }
    public int TurnsLeft { get; set; }
}

/// <summary>
/// Efectos que no son un paso que se ejecuta una vez: vigilancia de robos,
/// sustitucion de destruccion desde el Cementerio y negacion de los efectos
/// de los monstruos boca arriba. El motor y <see cref="CardMover"/> los
/// consultan en los momentos correspondientes.
/// </summary>
public static class LastingEffects
{
    // ------------------------------------------------------------------ Robos vigilados

    /// <summary>Despues de que <paramref name="side"/> robe <paramref name="card"/>: la muestra y la destruye si un efecto vigila sus robos y cumple el filtro.</summary>
    public static void OnDraw(DuelState state, PlayerSide side, Card card)
    {
        foreach (var watch in state.DrawWatches.Where(w => w.Watched == side).ToList())
        {
            var player = state.GetPlayer(side);
            int index = player.Hand.FindLastIndex(c => ReferenceEquals(c, card));
            if (index < 0) return;
            var drawn = new CardRef(card, side, CardZone.Hand, index);
            state.Log.Add($"{player.Name} muestra la carta robada por {watch.Source.Name}: {card.Name}.");
            if (!new CardQuery(watch.Filter.With("From", "Hand"), "Hand").Matches(state, drawn, watch.Source)) continue;
            if (CardMover.SendToGraveyard(state, drawn, new MoveCause(CauseKind.Effect, watch.Controller, watch.Source), destroy: true))
                state.Log.Add($"{watch.Source.Name} destruye {card.Name}.");
            return;
        }
    }

    /// <summary>Al terminar un turno: descuenta los turnos de los efectos que vigilan al jugador del turno.</summary>
    public static void OnTurnEnd(DuelState state)
    {
        var active = state.ActivePlayer.Side;
        foreach (var watch in state.DrawWatches.Where(w => w.Watched == active && state.TurnNumber > w.StartTurn))
            watch.TurnsLeft--;
        state.DrawWatches.RemoveAll(w => w.TurnsLeft <= 0);
    }

    // ------------------------------------------------------------------ Sustituir destruccion

    /// <summary>
    /// "Si un monstruo ... que controlas fuera a ser destruido en batalla o por
    /// efecto de una carta del adversario, puedes desterrar esta carta de tu
    /// Cementerio en su lugar": si alguna carta del Cementerio de quien lo
    /// controla lo permite (y no la uso este turno), la destierra y devuelve
    /// verdadero (el monstruo no se destruye). Se aplica sola.
    /// </summary>
    public static bool TrySubstituteDestruction(DuelState state, CardRef monster, MoveCause cause)
    {
        if (monster.Zone != CardZone.MonsterZone) return false;
        var controller = monster.Side;
        bool battle = cause.Kind == CauseKind.Battle;
        bool opponentEffect = cause.Kind == CauseKind.Effect && cause.By is { } by && by != controller;
        var player = state.GetPlayer(controller);

        for (int g = 0; g < player.Graveyard.Count; g++)
        {
            var card = player.Graveyard[g];
            for (int i = 0; i < card.Effects.Count; i++)
            {
                var effect = card.Effects[i];
                if (effect.Type != MonsterEffectType.Continuous) continue;
                foreach (var step in effect.Steps.Where(s => s.ActionKind == "destruction_substitute"))
                {
                    var p = step.Params;
                    if (!(battle && p.GetBool("ByBattle", true) || opponentEffect && p.GetBool("ByOpponentEffect", true) || cause.Kind == CauseKind.Effect && p.GetBool("ByAnyEffect"))) continue;
                    var query = new CardQuery(p.With("From", "MonsterZone"));
                    if (!CardQuery.Sides(query.Side, controller).Contains(controller) || !query.Matches(state, monster, card)) continue;
                    string key = $"{controller}:{card.Name.Trim().ToLowerInvariant()}#{i}";
                    if (p.GetBool("OncePerTurn", true) && state.UsedOncePerTurn.Contains(key)) continue;

                    if (!CardMover.Banish(state, new CardRef(card, controller, CardZone.Graveyard, g), new MoveCause(CauseKind.Cost, controller, card))) continue;
                    if (p.GetBool("OncePerTurn", true)) state.UsedOncePerTurn.Add(key);
                    state.Log.Add($"{card.Name} se destierra del Cementerio en lugar de que {monster.Card.Name} sea destruido.");
                    return true;
                }
            }
        }
        return false;
    }

    // ------------------------------------------------------------------ Negar efectos de monstruos

    /// <summary>
    /// Verdadero si una Magia/Trampa boca arriba niega los efectos de todos
    /// los monstruos boca arriba en el Campo (ej. "Drenaje de Habilidad").
    /// </summary>
    public static bool MonsterEffectsNegated(DuelState state)
    {
        foreach (var player in state.Players)
        {
            var sources = player.SpellTrapZones.Where(z => z is { FaceUp: true }).Select(z => z!.Card)
                .Concat(player.FieldZone is { FaceUp: true } f ? new[] { f.Card } : Array.Empty<Card>());
            foreach (var card in sources)
                if (card.Effects.Any(e => e.Type == MonsterEffectType.Continuous && e.Steps.Any(s => s.ActionKind == "negate_monster_effects")))
                    return true;
        }
        return false;
    }
}
