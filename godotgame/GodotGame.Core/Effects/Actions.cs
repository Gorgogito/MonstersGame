using GodotGame.Core.Battle;
using GodotGame.Core.Effects.Monster;
using GodotGame.Core.Entities;

namespace GodotGame.Core.Effects;

/// <summary>Roba <see cref="Count"/> carta(s) para quien controla el efecto. Sin objetivo.</summary>
public sealed class DrawCardAction : IEffectAction
{
    public required int Count { get; init; }

    public void Resolve(EffectContext ctx)
    {
        for (int i = 0; i < Count; i++)
            if (!ctx.Controller.DrawCard()) break;
    }
}

/// <summary>Destruye 1 monstruo objetivo (propio o del rival) y lo manda al Cementerio.</summary>
public sealed class DestroyTargetMonsterAction : ITargetedEffectAction
{
    public EffectTargetKind TargetKind => EffectTargetKind.MonsterZone;

    public bool IsValidTarget(DuelState state, Player controller, EffectTarget target)
    {
        var player = state.GetPlayer(target.Side);
        if (target.ZoneIndex < 0 || target.ZoneIndex >= player.MonsterZones.Length) return false;
        return player.MonsterZones[target.ZoneIndex] != null;
    }

    public void Resolve(EffectContext ctx)
    {
        if (ctx.Target is not { } target) return;
        var player = ctx.State.GetPlayer(target.Side);
        if (target.ZoneIndex < 0 || target.ZoneIndex >= player.MonsterZones.Length) return;
        var instance = player.MonsterZones[target.ZoneIndex];
        if (instance == null) return;

        CardMover.SendToGraveyard(ctx.State, new CardRef(instance.Card, target.Side, CardZone.MonsterZone, target.ZoneIndex),
            new MoveCause(CauseKind.Effect, ctx.Controller.Side, ctx.Source), destroy: true);
        ctx.State.Log.Add($"{ctx.Source.Name} destruye a {instance.Card.Name}.");
    }
}

/// <summary>
/// Invoca de Modo Especial, en Posicion de Ataque, un Monstruo objetivo desde
/// el propio Cementerio. Simplificacion del Bloque 5: solo el Cementerio del
/// controlador (el reglamento real tambien permite el del rival) y siempre en
/// Ataque (el real permite elegir Ataque o Defensa).
/// </summary>
public sealed class SpecialSummonFromOwnGraveyardAction : ITargetedEffectAction
{
    public EffectTargetKind TargetKind => EffectTargetKind.OwnGraveyard;

    public bool IsValidTarget(DuelState state, Player controller, EffectTarget target)
    {
        if (target.Side != controller.Side) return false;
        if (target.ZoneIndex < 0 || target.ZoneIndex >= controller.Graveyard.Count) return false;
        if (controller.Graveyard[target.ZoneIndex] is not MonsterCard) return false;
        return controller.FirstFreeMonsterZone() != -1;
    }

    public void Resolve(EffectContext ctx)
    {
        if (ctx.Target is not { } target) return;
        if (target.Side != ctx.Controller.Side) return;
        if (target.ZoneIndex < 0 || target.ZoneIndex >= ctx.Controller.Graveyard.Count) return;
        if (ctx.Controller.Graveyard[target.ZoneIndex] is not MonsterCard monster) return;
        int freeZone = ctx.Controller.FirstFreeMonsterZone();
        if (freeZone == -1) return;

        CardMover.SpecialSummon(ctx.State, new CardRef(monster, ctx.Controller.Side, CardZone.Graveyard, target.ZoneIndex),
            ctx.Controller.Side, BattlePosition.Attack, new MoveCause(CauseKind.Effect, ctx.Controller.Side, ctx.Source));
        ctx.State.Log.Add($"{ctx.Controller.Name} invoca de Modo Especial a {monster.Name} desde el Cementerio.");
    }
}

/// <summary>
/// Niega la activacion a la que responde (solo tiene sentido en una Trampa de
/// Contraefecto, Velocidad de Hechizo 3). Sin objetivo propio: siempre niega
/// el eslabon de la Cadena directamente inferior, que es al que se respondio.
/// </summary>
public sealed class NegateActivationAction : IEffectAction
{
    public void Resolve(EffectContext ctx) => ctx.NegateRespondedLink?.Invoke();
}
