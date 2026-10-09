using GodotGame.Core.Battle;
using GodotGame.Core.Effects.Monster;
using GodotGame.Core.Entities;
using GodotGame.Core.Services;

namespace GodotGame.Core.AI;

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

    /// <summary>Efectos ya intentados este turno (carta + indice), para no repetir en bucle un efecto sin "una vez por turno".</summary>
    private readonly HashSet<string> _effectsTriedThisTurn = new();
    private int _effectsTurn = -1;

    private static bool ChooseGuardianStars(DuelEngine engine, int selfIndex)
    {
        var me = engine.State.Players[selfIndex];
        var opponent = engine.State.Players[1 - selfIndex];
        var opponents = opponent.MonsterZones.Where(m => m != null).Select(m => m!).ToList();
        for (int zone = 0; zone < me.MonsterZones.Length; zone++)
        {
            var monster = me.MonsterZones[zone];
            if (monster == null || !monster.SummonedThisTurn || monster.GuardianStarChosen) continue;
            var star = GodotGame.Core.Rules.GuardianStars.BestAgainst(monster.Card, opponents);
            if (engine.ChooseGuardianStar(me.Side, zone, star).Success) return true;
        }
        return false;
    }

    public bool Step(DuelEngine engine, int selfIndex)
    {
        if (engine.IsOver) return false;
        var state = engine.State;
        var mySide = state.Players[selfIndex].Side;

        if (_effectsTurn != state.TurnNumber)
        {
            _effectsTurn = state.TurnNumber;
            _effectsTriedThisTurn.Clear();
        }

        // Una decision pendiente a mitad de un efecto bloquea todo lo demas.
        if (state.PendingChoice is { } choice)
            return choice.Chooser == mySide && AnswerChoice(engine, choice, mySide);

        // La Cadena puede pedirle Prioridad a la CPU aunque no sea su turno
        // (por ejemplo, si el humano activa una carta). Se comprueba primero,
        // antes que la guarda de "es mi turno".
        if (state.Chain.Count > 0)
        {
            if (state.ChainPendingResponder != mySide) return false;
            return RespondToChain(engine);
        }

        // Estrella Guardiana de cualquier monstruo propio recien Invocado
        // (incluso uno Invocado de Modo Especial en el turno rival): la que
        // mas conviene contra los monstruos rivales boca arriba.
        if (ChooseGuardianStars(engine, selfIndex)) return true;

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
    /// Prioridad en una Cadena: solo responde a algo del rival, con un efecto
    /// Rapido propio o con una Trampa/Magia de Juego Rapido Colocada que tenga
    /// efecto por datos (las heredadas con objetivo no se activan solas).
    /// </summary>
    private bool RespondToChain(DuelEngine engine)
    {
        var state = engine.State;
        var mySide = state.ChainPendingResponder!.Value;
        if (state.Chain[^1].Controller == mySide) return engine.PassPriority().Success;
        if (TryActivateEffect(engine, mySide, quickOnly: true)) return true;
        if (TryActivateSetCard(engine, mySide)) return true;
        return engine.PassPriority().Success;
    }

    /// <summary>Activa la primera carta Colocada con efecto por datos que se pueda activar ahora.</summary>
    private bool TryActivateSetCard(DuelEngine engine, PlayerSide mySide)
    {
        var me = engine.State.GetPlayer(mySide);
        for (int zone = 0; zone < me.SpellTrapZones.Length; zone++)
        {
            var instance = me.SpellTrapZones[zone];
            if (instance == null || instance.FaceUp || instance.Card.ActivationEffect == null) continue;
            string key = $"set:{instance.Card.Id}:{zone}";
            if (!_effectsTriedThisTurn.Add(key)) continue;
            if (engine.ActivateSetCard(zone).Success) return true;
        }
        return false;
    }

    /// <summary>Activa el primer efecto de Monstruo disponible que no haya intentado ya este turno.</summary>
    private bool TryActivateEffect(DuelEngine engine, PlayerSide mySide, bool quickOnly)
    {
        foreach (var effect in engine.GetActivatableEffects(mySide))
        {
            if (quickOnly != (effect.Effect.Type == MonsterEffectType.Quick)) continue;
            string key = $"{effect.Card.Card.Id}:{effect.EffectIndex}:{effect.Card.Zone}:{effect.Card.Index}";
            if (!_effectsTriedThisTurn.Add(key)) continue;
            if (engine.ActivateMonsterEffect(effect).Success) return true;
        }
        return false;
    }

    /// <summary>
    /// Responde una decision a mitad de efecto con una heuristica simple:
    /// siempre acepta lo opcional; al elegir cartas, se queda con las mas
    /// fuertes si salen ganando (o si perjudican al rival) y entrega las mas
    /// debiles si las pierde ella.
    /// </summary>
    private static bool AnswerChoice(DuelEngine engine, ChoiceRequest choice, PlayerSide mySide)
    {
        switch (choice.Kind)
        {
            case ChoiceKind.YesNo:
                return engine.AnswerYesNo(true).Success;
            case ChoiceKind.Reveal:
                return engine.AcknowledgeReveal().Success;
            case ChoiceKind.SelectOption:
                // Ventana de respuesta a un ataque: la opcion 0 es "No activar nada".
                return engine.AnswerOption(choice.IsResponseWindow && choice.Options.Count > 1 ? 1 : 0).Success;
        }

        var ranked = choice.Candidates
            .Select((card, index) => (card, index, value: CardValue(card.Card)))
            .ToList();
        bool losingOwnCards = choice.Purpose == ChoicePurpose.Harm && ranked.Count > 0 && ranked.All(c => c.card.Side == mySide);
        int count = losingOwnCards ? choice.Min : choice.Max;

        // Perjudicar: primero las del rival (la mas fuerte), despues las propias (la mas debil).
        // Beneficiar: primero las propias (la mas fuerte), despues las del rival (la mas debil).
        bool harm = choice.Purpose == ChoicePurpose.Harm;
        var picked = ranked
            .OrderBy(c => (c.card.Side == mySide) == harm ? 1 : 0)
            .ThenBy(c => (c.card.Side == mySide) == harm ? c.value : -c.value)
            .Take(count)
            .Select(c => c.index)
            .ToArray();
        return engine.AnswerCards(picked).Success;
    }

    private static int CardValue(Card card) => card is MonsterCard m ? m.Attack + m.Defense / 2 : 1500;

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

        // Efectos de Encendido / No clasificados disponibles (buscar, Invocarse, etc.).
        if (TryActivateEffect(engine, me.Side, quickOnly: false)) return true;

        // Magias con efecto por datos (y de Campo) desde la mano.
        if (TryActivateSpellFromHand(engine, me)) return true;

        if (!me.HasNormalSummonedThisTurn && me.FirstFreeMonsterZone() != -1)
        {
            if (TryBestFusion(engine, me)) return true;
            if (TryBestSummon(engine, me, opp)) return true;
        }

        // Trampas y Magias de Juego Rapido: se Colocan para usarlas en el turno rival.
        if (TrySetForLater(engine, me)) return true;

        // Sin mas jugadas de invocacion: avanzar de fase o terminar el turno.
        if (state.Phase == DuelPhase.Main1)
            return Advance(engine);

        engine.EndTurn();
        return false;
    }

    /// <summary>
    /// Activa la primera Magia de la mano que tenga efecto por datos (o una
    /// de Campo si no tiene ninguna) y que el motor acepte ahora. Las
    /// heredadas que piden objetivo (Equipo, "destruye 1 monstruo") no se
    /// activan solas.
    /// </summary>
    private bool TryActivateSpellFromHand(DuelEngine engine, Player me)
    {
        for (int i = 0; i < me.Hand.Count; i++)
        {
            if (me.Hand[i] is not SpellCard spell) continue;
            bool useful = spell.ActivationEffect != null
                          || (spell.SubType == SpellSubType.Field && me.FieldZone == null)
                          || (spell.SubType == SpellSubType.Continuous && spell.Effects.Count > 0);
            if (!useful || spell.SubType is SpellSubType.Ritual or SpellSubType.Equip) continue;
            string key = $"hand:{spell.Id}";
            if (!_effectsTriedThisTurn.Add(key)) continue;
            if (engine.ActivateSpell(i).Success) return true;
        }
        return false;
    }

    /// <summary>Coloca una Trampa o Magia de Juego Rapido con efecto por datos (como mucho una por paso).</summary>
    private bool TrySetForLater(DuelEngine engine, Player me)
    {
        if (me.FirstFreeSpellTrapZone() == -1) return false;
        for (int i = 0; i < me.Hand.Count; i++)
        {
            bool settable = me.Hand[i] is TrapCard { ActivationEffect: not null } or SpellCard { SubType: SpellSubType.QuickPlay, ActivationEffect: not null };
            if (!settable) continue;
            if (engine.SetSpellOrTrap(i).Success) return true;
        }
        return false;
    }

    /// <summary>
    /// Cuantas cartas de la mano considera como maximo al buscar una receta
    /// de Fusion generica (Fase 1: <c>FusionService.TryFuseMany</c>). Es un
    /// limite heuristico de la IA, no una regla del motor -- acota la
    /// busqueda combinatoria; en la practica el tamano de mano (6-7 cartas)
    /// casi nunca la alcanza.
    /// </summary>
    private const int MaxFusionMaterialsConsidered = 5;

    /// <summary>
    /// Realiza la mejor fusion disponible si supera al mejor monstruo simple.
    /// Evalua tanto pares exactos (receta legacy o generica de 2 huecos, via
    /// <see cref="DuelEngine.Fuse"/>) como combinaciones de 3+ cartas contra
    /// recetas genericas (<see cref="DuelEngine.FuseMany"/>), y elige la que
    /// produzca el resultado de mayor ATK.
    /// </summary>
    private bool TryBestFusion(DuelEngine engine, Player me)
    {
        var handMonsters = IndexedHandMonsters(me);
        if (handMonsters.Count < 2) return false;

        int maxSize = Math.Min(handMonsters.Count, MaxFusionMaterialsConsidered);
        List<int>? bestIndices = null;
        int bestResultAtk = -1;

        for (int size = 2; size <= maxSize; size++)
        {
            foreach (var combo in Combinations(handMonsters, size))
            {
                var cards = combo.Select(h => h.card).ToList();

                MonsterCard? result = size == 2 ? _fusion.TryFuse(cards[0], cards[1]) : null;
                if (result == null)
                {
                    var generic = _fusion.TryFuseMany(cards, out var used);
                    if (generic != null && used.Count == cards.Count)
                        result = generic;
                }

                if (result != null && result.Attack > bestResultAtk)
                {
                    bestResultAtk = result.Attack;
                    bestIndices = combo.Select(h => h.index).ToList();
                }
            }
        }

        if (bestIndices == null) return false;

        // Solo fusiona si el resultado iguala o supera al mejor monstruo que
        // podria invocar sin sacrificios.
        int bestSimpleAtk = handMonsters
            .Where(h => h.card.RequiredTributes == 0)
            .Select(h => h.card.Attack)
            .DefaultIfEmpty(0)
            .Max();

        if (bestResultAtk < bestSimpleAtk) return false;

        return bestIndices.Count == 2
            ? engine.Fuse(bestIndices[0], bestIndices[1]).Success
            : engine.FuseMany(bestIndices.ToArray()).Success;
    }

    /// <summary>Todas las combinaciones (sin importar el orden) de <paramref name="size"/> elementos de <paramref name="items"/>.</summary>
    private static IEnumerable<List<T>> Combinations<T>(IReadOnlyList<T> items, int size)
    {
        if (size == 0)
        {
            yield return new List<T>();
            yield break;
        }

        for (int i = 0; i <= items.Count - size; i++)
        {
            var rest = items.Skip(i + 1).ToList();
            foreach (var tail in Combinations(rest, size - 1))
            {
                var combo = new List<T> { items[i] };
                combo.AddRange(tail);
                yield return combo;
            }
        }
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
