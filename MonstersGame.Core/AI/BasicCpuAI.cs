using MonstersGame.Core.Battle;
using MonstersGame.Core.Entities;
using MonstersGame.Core.Services;

namespace MonstersGame.Core.AI;

/// <summary>
/// IA basica para la CPU. Estrategia heuristica sencilla:
///  - Prioriza fusiones cuando producen un monstruo mas fuerte.
///  - Invoca el mejor monstruo disponible (con Sacrificios si conviene).
///  - Coloca en defensa cuando esta en desventaja de poder.
///  - Ataca cuando puede destruir un objetivo o golpear directamente.
///
/// Esta implementacion es deliberadamente simple y autocontenida para poder
/// reemplazarla por una IA mas avanzada en el futuro.
/// </summary>
public sealed class BasicCpuAI : IDuelAI
{
    private readonly FusionService _fusion;

    // Umbral estimado de DEF para monstruos boca abajo del rival (desconocida).
    private const int FaceDownDefenseEstimate = 1200;

    public BasicCpuAI(FusionService fusion) => _fusion = fusion;

    public bool Step(DuelEngine engine, int selfIndex)
    {
        if (engine.IsOver) return false;
        var state = engine.State;
        var mySide = state.Players[selfIndex].Side;

        // La Cadena puede pedirle Prioridad a la CPU aunque no sea su turno
        // (por ejemplo, si el humano activa una carta). Se comprueba primero,
        // antes que la guarda de "es mi turno".
        if (state.Chain.Count > 0)
        {
            if (state.ChainPendingResponder != mySide) return false;
            return RespondToChain(engine);
        }

        if (state.ActiveIndex != selfIndex) return false;

        // El descarte por limite de mano ahora lo elige el jugador (o la IA):
        // el turno no termina hasta resolverlo.
        if (state.PendingDiscardCount > 0)
            return engine.DiscardForEndPhase(ChooseDiscards(state.ActivePlayer, state.PendingDiscardCount)).Success;

        return state.Phase switch
        {
            DuelPhase.Main1 or DuelPhase.Main2 => MainStep(engine),
            DuelPhase.Battle => BattleStep(engine),
            // Draw/Standby/End son gestionados por el motor; avanzamos.
            _ => Advance(engine)
        };
    }

    /// <summary>
    /// Heuristica "honesta" para la Prioridad de la Cadena: hoy la CPU nunca
    /// Coloca ni activa Magias/Trampas por su cuenta (ver <see cref="MainStep"/>),
    /// asi que en la practica siempre pasa. Se deja preparado para el dia en
    /// que la IA sepa jugar Magias/Trampas: entonces bastara con que este
    /// metodo elija encadenar en vez de pasar cuando tenga algo legal.
    /// </summary>
    private static bool RespondToChain(DuelEngine engine) =>
        engine.PassPriority().Success;

    /// <summary>
    /// Elige que descartar por limite de mano: primero las cartas que no son
    /// monstruo (todavia no jugables), despues los monstruos de menor ATK.
    /// </summary>
    private static int[] ChooseDiscards(Player me, int count)
    {
        return me.Hand
            .Select((card, index) => (card, index))
            .OrderBy(h => h.card is MonsterCard m ? m.Attack : -1)
            .Take(count)
            .Select(h => h.index)
            .ToArray();
    }

    // --------------------------------------------------------------- Main Phase

    private bool MainStep(DuelEngine engine)
    {
        var state = engine.State;
        var me = state.ActivePlayer;
        var opp = state.InactivePlayer;

        if (!me.HasNormalSummonedThisTurn && me.FirstFreeMonsterZone() != -1)
        {
            if (TryBestFusion(engine, me)) return true;
            if (TryBestSummon(engine, me, opp)) return true;
        }

        // Sin mas jugadas de invocacion: avanzar de fase o terminar el turno.
        if (state.Phase == DuelPhase.Main1)
            return Advance(engine);

        engine.EndTurn();
        return false;
    }

    /// <summary>Realiza la mejor fusion disponible si supera al mejor monstruo simple.</summary>
    private bool TryBestFusion(DuelEngine engine, Player me)
    {
        var handMonsters = IndexedHandMonsters(me);

        int bestA = -1, bestB = -1, bestResultAtk = -1;
        for (int i = 0; i < handMonsters.Count; i++)
        {
            for (int j = i + 1; j < handMonsters.Count; j++)
            {
                var result = _fusion.TryFuse(handMonsters[i].card, handMonsters[j].card);
                if (result != null && result.Attack > bestResultAtk)
                {
                    bestResultAtk = result.Attack;
                    bestA = handMonsters[i].index;
                    bestB = handMonsters[j].index;
                }
            }
        }

        if (bestA == -1) return false;

        // Solo fusiona si el resultado iguala o supera al mejor monstruo que
        // podria invocar sin sacrificios.
        int bestSimpleAtk = handMonsters
            .Where(h => h.card.RequiredTributes == 0)
            .Select(h => h.card.Attack)
            .DefaultIfEmpty(0)
            .Max();

        if (bestResultAtk < bestSimpleAtk) return false;

        return engine.Fuse(bestA, bestB).Success;
    }

    /// <summary>Invoca el mejor monstruo posible, en ataque o defensa segun convenga.</summary>
    private bool TryBestSummon(DuelEngine engine, Player me, Player opp)
    {
        var handMonsters = IndexedHandMonsters(me);
        if (handMonsters.Count == 0) return false;

        int oppBestAtk = OpponentBestAttack(opp);

        // Elegir el monstruo con mayor ATK que sea invocable (tributos disponibles).
        (int index, MonsterCard card)? choice = null;
        foreach (var h in handMonsters.OrderByDescending(h => h.card.Attack))
        {
            int required = h.card.RequiredTributes;
            // Tras invocar por sacrificio el balance de zonas es neutro o positivo.
            if (required > 0 && me.MonsterCount < required) continue;
            choice = h;
            break;
        }

        if (choice == null) return false;

        var monster = choice.Value.card;
        int handIndex = choice.Value.index;
        int req = monster.RequiredTributes;
        int[]? tributes = req > 0 ? WeakestMonsterZones(me, req) : null;

        // Decision ataque/defensa: si no supera al rival y su defensa es mejor,
        // se coloca boca abajo en defensa.
        bool weak = monster.Attack < oppBestAtk;
        bool defensive = weak && monster.Defense >= monster.Attack;

        ActionResult result = defensive
            ? engine.SetMonster(handIndex, tributes)
            : engine.NormalSummon(handIndex, BattlePosition.Attack, tributes);

        return result.Success;
    }

    // ------------------------------------------------------------- Battle Phase

    private bool BattleStep(DuelEngine engine)
    {
        var state = engine.State;
        var me = state.ActivePlayer;
        var opp = state.InactivePlayer;

        for (int zone = 0; zone < me.MonsterZones.Length; zone++)
        {
            var attacker = me.MonsterZones[zone];
            if (attacker == null) continue;
            if (attacker.Position != BattlePosition.Attack) continue;
            if (attacker.HasAttackedThisTurn) continue;

            // Sin monstruos rivales: ataque directo.
            if (opp.MonsterCount == 0)
            {
                if (engine.DeclareAttack(zone, -1).Success) return true;
                continue;
            }

            int target = ChooseAttackTarget(attacker, opp);
            if (target >= 0)
            {
                if (engine.DeclareAttack(zone, target).Success) return true;
            }
        }

        // No hay ataques convenientes: pasar a Main Phase 2.
        return Advance(engine);
    }

    /// <summary>
    /// Elige un objetivo que el atacante pueda destruir sin ser destruido.
    /// Devuelve -1 si no hay objetivo conveniente.
    /// </summary>
    private static int ChooseAttackTarget(CardInstance attacker, Player opp)
    {
        int atk = attacker.Card.Attack;
        int bestTarget = -1;
        int bestValue = int.MinValue;

        for (int z = 0; z < opp.MonsterZones.Length; z++)
        {
            var def = opp.MonsterZones[z];
            if (def == null) continue;

            int effectiveStat;
            int damage;
            if (def.Position == BattlePosition.Attack)
            {
                effectiveStat = def.Card.Attack;
                damage = atk - effectiveStat; // daño a LP si ganamos
            }
            else if (def.Position == BattlePosition.DefenseFaceUp)
            {
                effectiveStat = def.Card.Defense;
                damage = 0;
            }
            else
            {
                // Boca abajo: defensa desconocida, se estima.
                effectiveStat = FaceDownDefenseEstimate;
                damage = 0;
            }

            bool weWin = atk > effectiveStat;
            if (!weWin) continue;

            // Preferimos el ataque que mas daño inflige; a igualdad, el rival
            // con mayor ATK (eliminar amenazas).
            int value = damage * 10 + def.Card.Attack;
            if (value > bestValue)
            {
                bestValue = value;
                bestTarget = z;
            }
        }

        return bestTarget;
    }

    // ------------------------------------------------------------------ Helpers

    private static bool Advance(DuelEngine engine)
    {
        engine.AdvancePhase();
        return true;
    }

    private static List<(int index, MonsterCard card)> IndexedHandMonsters(Player p)
    {
        var list = new List<(int, MonsterCard)>();
        for (int i = 0; i < p.Hand.Count; i++)
            if (p.Hand[i] is MonsterCard m)
                list.Add((i, m));
        return list;
    }

    private static int OpponentBestAttack(Player opp)
    {
        int best = 0;
        foreach (var z in opp.MonsterZones)
            if (z is { Position: BattlePosition.Attack } && z.Card.Attack > best)
                best = z.Card.Attack;
        return best;
    }

    /// <summary>Devuelve las zonas de los <paramref name="count"/> monstruos mas debiles.</summary>
    private static int[] WeakestMonsterZones(Player p, int count)
    {
        return Enumerable.Range(0, p.MonsterZones.Length)
            .Where(z => p.MonsterZones[z] != null)
            .OrderBy(z => p.MonsterZones[z]!.Card.Attack)
            .Take(count)
            .ToArray();
    }
}
