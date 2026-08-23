using MonstersGame.Core.Effects;
using MonstersGame.Core.Entities;
using MonstersGame.Core.Rules;
using MonstersGame.Core.Services;

namespace MonstersGame.Core.Battle;

/// <summary>
/// Motor del duelo. Contiene TODA la logica de reglas y es completamente
/// independiente de la interfaz grafica: opera sobre <see cref="DuelState"/> y
/// expone acciones que devuelven <see cref="ActionResult"/>. La UI y la IA usan
/// exclusivamente esta API publica.
/// </summary>
public sealed class DuelEngine
{
    private readonly DuelConfig _config;
    private readonly FusionService _fusion;

    public DuelState State { get; private set; } = null!;

    public DuelEngine(DuelConfig config, FusionService fusion)
    {
        _config = config;
        _fusion = fusion;
    }

    // Atajos de lectura usados por UI e IA.
    public DuelPhase Phase => State.Phase;
    public int ActiveIndex => State.ActiveIndex;
    public bool IsOver => State.IsOver;
    public GameLog Log => State.Log;

    // ----------------------------------------------------------------- Inicio

    /// <summary>
    /// Inicia un duelo. Los decks ya deben venir barajados por el llamador.
    /// </summary>
    public void StartDuel(Player human, Player cpu, int firstPlayerIndex)
    {
        State = new DuelState(human, cpu)
        {
            FirstPlayerIndex = firstPlayerIndex,
            ActiveIndex = firstPlayerIndex,
            TurnNumber = 1
        };

        human.LifePoints = _config.StartingLifePoints;
        cpu.LifePoints = _config.StartingLifePoints;

        // Mano inicial (pagina 29): cada jugador roba 5 cartas.
        for (int i = 0; i < _config.StartingHandSize; i++)
        {
            human.DrawCard();
            cpu.DrawCard();
        }

        Log.Add($"Comienza el duelo. Inicia {State.ActivePlayer.Name}.");
        BeginTurn();
    }

    // ----------------------------------------------------------- Flujo de turno

    /// <summary>Prepara el turno del jugador activo y ejecuta la Draw Phase.</summary>
    private void BeginTurn()
    {
        var player = State.ActivePlayer;
        player.HasNormalSummonedThisTurn = false;
        foreach (var zone in player.MonsterZones)
            zone?.ResetTurnFlags();
        foreach (var zone in player.SpellTrapZones)
            zone?.ResetTurnFlags();
        player.FieldZone?.ResetTurnFlags();

        State.Phase = DuelPhase.Draw;
        Log.Add($"--- Turno {State.TurnNumber}: {player.Name} ---");

        // Draw Phase (pagina 31). El primer jugador no roba en su primer turno.
        bool skipDraw = _config.FirstPlayerSkipsFirstDraw && State.IsFirstTurnOfStartingPlayer;
        if (!skipDraw)
        {
            if (!player.DrawCard())
            {
                // Deck-out: pierde quien no puede robar (pagina 28).
                EndDuel(State.InactivePlayer.Side, $"{player.Name} no puede robar.");
                return;
            }
            Log.Add($"{player.Name} roba una carta.");
        }

        // Standby Phase se omite (sin efectos en esta version) y pasamos a Main 1.
        State.Phase = DuelPhase.Main1;
    }

    /// <summary>
    /// Avanza a la siguiente fase logica. Main1 -> Battle -> Main2 -> End/Turno.
    /// </summary>
    public ActionResult AdvancePhase()
    {
        if (State.IsOver) return ActionResult.Fail("El duelo ha terminado.");
        var chainCheck = ValidateNoOpenChain();
        if (!chainCheck.Success) return chainCheck;

        switch (State.Phase)
        {
            case DuelPhase.Main1:
                if (CanEnterBattlePhase())
                {
                    State.Phase = DuelPhase.Battle;
                    Log.Add($"{State.ActivePlayer.Name} entra a la Battle Phase.");
                }
                else
                {
                    // Primer turno del jugador inicial: no hay Battle Phase.
                    State.Phase = DuelPhase.Main2;
                    Log.Add("No se puede batallar este turno.");
                }
                return ActionResult.Ok();

            case DuelPhase.Battle:
                State.Phase = DuelPhase.Main2;
                Log.Add($"{State.ActivePlayer.Name} pasa a la Main Phase 2.");
                return ActionResult.Ok();

            case DuelPhase.Main2:
                return EndTurn();

            default:
                return ActionResult.Fail($"No se puede avanzar desde la fase {State.Phase}.");
        }
    }

    private bool CanEnterBattlePhase()
    {
        if (_config.FirstPlayerSkipsFirstBattle && State.IsFirstTurnOfStartingPlayer)
            return false;
        return true;
    }

    /// <summary>
    /// Termina el turno actual y comienza el del adversario. Si la mano supera
    /// el limite (pagina 36), el turno NO termina todavia: queda a la espera de
    /// que el jugador elija que descartar mediante <see cref="DiscardForEndPhase"/>.
    /// </summary>
    public ActionResult EndTurn()
    {
        if (State.IsOver) return ActionResult.Fail("El duelo ha terminado.");
        var chainCheck = ValidateNoOpenChain();
        if (!chainCheck.Success) return chainCheck;
        if (State.PendingDiscardCount > 0)
            return ActionResult.Fail($"Debes descartar {State.PendingDiscardCount} carta(s) antes de terminar el turno.");

        State.Phase = DuelPhase.End;
        var player = State.ActivePlayer;

        int excess = player.Hand.Count - _config.MaxHandSize;
        if (excess > 0)
        {
            State.PendingDiscardCount = excess;
            Log.Add($"{player.Name} debe descartar {excess} carta(s) (limite de mano).");
            return ActionResult.Ok("Selecciona cartas para descartar.");
        }

        return FinishEndTurn();
    }

    /// <summary>
    /// Resuelve el descarte pendiente del limite de mano eligiendo exactamente
    /// las cartas indicadas por indice de mano, y recien entonces termina el
    /// turno. Solo valido cuando <see cref="DuelState.PendingDiscardCount"/> es
    /// mayor que 0.
    /// </summary>
    public ActionResult DiscardForEndPhase(int[] handIndices)
    {
        if (State.IsOver) return ActionResult.Fail("El duelo ha terminado.");
        if (State.PendingDiscardCount <= 0)
            return ActionResult.Fail("No hay ningun descarte pendiente.");

        var player = State.ActivePlayer;
        var indices = (handIndices ?? Array.Empty<int>()).Distinct().ToList();

        if (indices.Count != State.PendingDiscardCount)
            return ActionResult.Fail($"Debes seleccionar exactamente {State.PendingDiscardCount} carta(s).");
        foreach (int i in indices)
            if (i < 0 || i >= player.Hand.Count)
                return ActionResult.Fail("Indice de mano invalido.");

        foreach (int i in indices.OrderByDescending(i => i))
        {
            var discard = player.Hand[i];
            player.Hand.RemoveAt(i);
            player.SendToGraveyard(discard);
            Log.Add($"{player.Name} descarta {discard.Name} (limite de mano).");
        }

        State.PendingDiscardCount = 0;
        return FinishEndTurn();
    }

    private ActionResult FinishEndTurn()
    {
        State.ActiveIndex = 1 - State.ActiveIndex;
        State.TurnNumber++;
        BeginTurn();
        return ActionResult.Ok();
    }

    // --------------------------------------------------------- Acciones de Main

    /// <summary>
    /// Invocacion Normal de un monstruo desde la mano (pagina 20). Aplica
    /// Sacrificios para Nivel 5+ usando los monstruos indicados.
    /// </summary>
    public ActionResult NormalSummon(int handIndex, BattlePosition position, int[]? tributeZones = null)
    {
        var check = ValidateMainPhaseAction();
        if (!check.Success) return check;

        var player = State.ActivePlayer;
        if (handIndex < 0 || handIndex >= player.Hand.Count)
            return ActionResult.Fail("Indice de mano invalido.");

        if (player.Hand[handIndex] is not MonsterCard monster)
            return ActionResult.Fail("Solo se pueden invocar cartas de monstruo en esta version.");

        if (monster.Category is MonsterCategory.Fusion or MonsterCategory.Ritual)
            return ActionResult.Fail("Este monstruo solo puede Invocarse de Modo Especial.");

        if (player.HasNormalSummonedThisTurn)
            return ActionResult.Fail("Ya realizaste tu Invocacion Normal o Colocacion este turno.");

        if (position == BattlePosition.DefenseFaceDown)
            return ActionResult.Fail("Usa SetMonster para Colocar boca abajo.");

        // Resolver Sacrificios requeridos.
        int required = monster.RequiredTributes;
        var tributeResult = ResolveTributes(player, required, tributeZones);
        if (!tributeResult.Success) return tributeResult;

        int freeZone = player.FirstFreeMonsterZone();
        if (freeZone == -1)
            return ActionResult.Fail("No hay Zonas de Monstruo libres.");

        player.Hand.RemoveAt(handIndex);
        var instance = new CardInstance(monster, position) { SummonedThisTurn = true };
        player.MonsterZones[freeZone] = instance;
        player.HasNormalSummonedThisTurn = true;

        Log.Add($"{player.Name} invoca a {monster.Name} ({monster.Attack}/{monster.Defense}) en Ataque.");
        return ActionResult.Ok();
    }

    /// <summary>Colocar un monstruo boca abajo en Defensa (pagina 20).</summary>
    public ActionResult SetMonster(int handIndex, int[]? tributeZones = null)
    {
        var check = ValidateMainPhaseAction();
        if (!check.Success) return check;

        var player = State.ActivePlayer;
        if (handIndex < 0 || handIndex >= player.Hand.Count)
            return ActionResult.Fail("Indice de mano invalido.");

        if (player.Hand[handIndex] is not MonsterCard monster)
            return ActionResult.Fail("Solo se pueden colocar cartas de monstruo.");

        if (monster.Category is MonsterCategory.Fusion or MonsterCategory.Ritual)
            return ActionResult.Fail("Este monstruo solo puede Invocarse de Modo Especial.");

        if (player.HasNormalSummonedThisTurn)
            return ActionResult.Fail("Ya realizaste tu Invocacion Normal o Colocacion este turno.");

        var tributeResult = ResolveTributes(player, monster.RequiredTributes, tributeZones);
        if (!tributeResult.Success) return tributeResult;

        int freeZone = player.FirstFreeMonsterZone();
        if (freeZone == -1)
            return ActionResult.Fail("No hay Zonas de Monstruo libres.");

        player.Hand.RemoveAt(handIndex);
        var instance = new CardInstance(monster, BattlePosition.DefenseFaceDown) { SummonedThisTurn = true };
        player.MonsterZones[freeZone] = instance;
        player.HasNormalSummonedThisTurn = true;

        Log.Add($"{player.Name} coloca un monstruo boca abajo.");
        return ActionResult.Ok();
    }

    /// <summary>
    /// Fusiona dos cartas de la mano del jugador activo. Decision asumida
    /// (estilo Forbidden Memories): la fusion cuenta como la jugada de monstruo
    /// del turno (ocupa la Invocacion Normal) y se invoca de Modo Especial.
    /// </summary>
    public ActionResult Fuse(int handIndexA, int handIndexB, BattlePosition position = BattlePosition.Attack)
    {
        var check = ValidateMainPhaseAction();
        if (!check.Success) return check;

        var player = State.ActivePlayer;
        if (handIndexA == handIndexB)
            return ActionResult.Fail("Debes seleccionar dos cartas distintas.");
        if (!IsValidHandIndex(player, handIndexA) || !IsValidHandIndex(player, handIndexB))
            return ActionResult.Fail("Indice de mano invalido.");

        if (player.Hand[handIndexA] is not MonsterCard a || player.Hand[handIndexB] is not MonsterCard b)
            return ActionResult.Fail("Ambas cartas deben ser monstruos.");

        var result = _fusion.TryFuse(a, b);
        if (result == null)
            return ActionResult.Fail($"No existe fusion para {a.Name} + {b.Name}.");

        if (player.HasNormalSummonedThisTurn)
            return ActionResult.Fail("Ya usaste tu jugada de monstruo este turno.");

        int freeZone = player.FirstFreeMonsterZone();
        if (freeZone == -1)
            return ActionResult.Fail("No hay Zonas de Monstruo libres.");

        // Enviar materiales al cementerio (mayor indice primero para no desplazar).
        int hi = Math.Max(handIndexA, handIndexB);
        int lo = Math.Min(handIndexA, handIndexB);
        var matHi = player.Hand[hi];
        var matLo = player.Hand[lo];
        player.Hand.RemoveAt(hi);
        player.Hand.RemoveAt(lo);
        player.SendToGraveyard(matHi);
        player.SendToGraveyard(matLo);

        var instance = new CardInstance(result, position) { SummonedThisTurn = true };
        player.MonsterZones[freeZone] = instance;
        player.HasNormalSummonedThisTurn = true;

        Log.Add($"{player.Name} fusiona {a.Name} + {b.Name} => {result.Name} ({result.Attack}/{result.Defense}).");
        return ActionResult.Ok();
    }

    /// <summary>
    /// Invocacion Ritual (pagina 9): activa una Carta Magica de Ritual desde
    /// la mano junto con su Monstruo de Ritual correspondiente, tambien en la
    /// mano, y Sacrifica monstruos (de la mano y/o del Campo) cuya suma de
    /// Niveles alcance el minimo exigido por la Carta Magica. A diferencia de
    /// <see cref="Fuse"/> (que en este proyecto consume la jugada de monstruo
    /// del turno como decision asumida), la Invocacion Ritual es una
    /// Invocacion Especial independiente y no la consume, siguiendo la regla
    /// general de "Invocacion Especial" del reglamento. No entra en la Cadena
    /// (misma decision de alcance que las Cartas Magicas de Campo).
    /// </summary>
    public ActionResult RitualSummon(
        int handIndexSpell,
        int handIndexMonster,
        int[]? handTributeIndices = null,
        int[]? fieldTributeZones = null,
        BattlePosition position = BattlePosition.Attack)
    {
        var check = ValidateMainPhaseAction();
        if (!check.Success) return check;

        var player = State.ActivePlayer;

        if (!IsValidHandIndex(player, handIndexSpell) || !IsValidHandIndex(player, handIndexMonster))
            return ActionResult.Fail("Indice de mano invalido.");
        if (handIndexSpell == handIndexMonster)
            return ActionResult.Fail("La Carta Magica y el Monstruo de Ritual deben ser cartas distintas.");
        if (player.Hand[handIndexSpell] is not SpellCard { SubType: SpellSubType.Ritual } spell)
            return ActionResult.Fail("Selecciona una Carta Magica de Ritual.");
        if (player.Hand[handIndexMonster] is not MonsterCard monster || monster.Category != MonsterCategory.Ritual)
            return ActionResult.Fail("Selecciona un Monstruo de Ritual.");
        if (monster.Id != spell.RitualMonsterId)
            return ActionResult.Fail($"{spell.Name} no puede invocar a {monster.Name}.");
        if (position == BattlePosition.DefenseFaceDown)
            return ActionResult.Fail("Un Monstruo de Ritual se invoca boca arriba, en Ataque o Defensa.");

        List<int> handTributes;
        List<int> fieldTributes;
        if (handTributeIndices == null && fieldTributeZones == null)
        {
            // Sin seleccion explicita: auto-selecciona (Campo primero, para no
            // gastar cartas de la mano si el Campo ya alcanza), igual que
            // ResolveTributes hace para la Invocacion Normal/por Sacrificio.
            (handTributes, fieldTributes) = AutoSelectRitualTributes(player, spell.RequiredRitualLevel, handIndexSpell, handIndexMonster);
        }
        else
        {
            handTributes = (handTributeIndices ?? Array.Empty<int>()).Distinct().ToList();
            fieldTributes = (fieldTributeZones ?? Array.Empty<int>()).Distinct().ToList();
        }

        if (handTributes.Contains(handIndexSpell) || handTributes.Contains(handIndexMonster))
            return ActionResult.Fail("No puedes Sacrificar la propia Carta Magica de Ritual ni al Monstruo de Ritual.");

        var handTributeCards = new List<MonsterCard>();
        foreach (int i in handTributes)
        {
            if (!IsValidHandIndex(player, i) || player.Hand[i] is not MonsterCard handMonster)
                return ActionResult.Fail("Sacrificio invalido: indice de mano invalido.");
            handTributeCards.Add(handMonster);
        }

        var fieldTributeInstances = new List<CardInstance>();
        foreach (int z in fieldTributes)
        {
            var instance = GetZone(player, z);
            if (instance == null) return ActionResult.Fail("Sacrificio invalido: zona vacia.");
            fieldTributeInstances.Add(instance);
        }

        int totalLevel = handTributeCards.Sum(c => c.Level) + fieldTributeInstances.Sum(i => i.Card.Level);
        if (totalLevel < spell.RequiredRitualLevel)
            return ActionResult.Fail($"Los Sacrificios elegidos suman Nivel {totalLevel}; {spell.Name} exige al menos {spell.RequiredRitualLevel}.");

        int freeZonesAfterTributes = player.MonsterZones.Count(z => z == null) + fieldTributes.Count;
        if (freeZonesAfterTributes == 0)
            return ActionResult.Fail("No hay Zonas de Monstruo libres.");

        // Sacrificar (mano primero, por indice descendente para no desplazar; luego Campo).
        foreach (int i in handTributes.OrderByDescending(x => x))
        {
            var card = player.Hand[i];
            player.Hand.RemoveAt(i);
            player.SendToGraveyard(card);
            Log.Add($"{player.Name} sacrifica a {card.Name} para el Ritual.");
        }
        foreach (int z in fieldTributes)
        {
            var instance = player.MonsterZones[z]!;
            player.MonsterZones[z] = null;
            player.SendToGraveyard(instance.Card);
            Log.Add($"{player.Name} sacrifica a {instance.Card.Name} para el Ritual.");
        }

        // Se retiran por referencia (no por indice): los sacrificios de mano
        // ya pudieron desplazar los indices originales de spell/monster.
        player.Hand.Remove(spell);
        player.Hand.Remove(monster);

        int summonZone = player.FirstFreeMonsterZone();
        player.MonsterZones[summonZone] = new CardInstance(monster, position) { SummonedThisTurn = true };
        Log.Add($"{player.Name} invoca por Ritual a {monster.Name} ({monster.Attack}/{monster.Defense}).");

        player.SendToGraveyard(spell);
        Log.Add($"{spell.Name} se manda al Cementerio tras la Invocacion Ritual.");

        return ActionResult.Ok();
    }

    /// <summary>
    /// Cambia la posicion de batalla de un monstruo boca arriba (pagina 32).
    /// </summary>
    public ActionResult ChangePosition(int zoneIndex, BattlePosition newPosition)
    {
        var check = ValidateMainPhaseAction();
        if (!check.Success) return check;

        var player = State.ActivePlayer;
        var monster = GetZone(player, zoneIndex);
        if (monster == null) return ActionResult.Fail("No hay monstruo en esa zona.");

        if (newPosition == BattlePosition.DefenseFaceDown)
            return ActionResult.Fail("No se puede volver a colocar boca abajo.");
        if (monster.Position == BattlePosition.DefenseFaceDown)
            return ActionResult.Fail("Usa la Invocacion por Volteo para voltear un monstruo boca abajo.");
        if (monster.SummonedThisTurn)
            return ActionResult.Fail("No puedes cambiar la posicion de un monstruo jugado este turno.");
        if (monster.PositionChangedThisTurn)
            return ActionResult.Fail("Ese monstruo ya cambio de posicion este turno.");
        if (monster.HasAttackedThisTurn)
            return ActionResult.Fail("Un monstruo que ataco no puede cambiar de posicion.");
        if (monster.Position == newPosition)
            return ActionResult.Fail("El monstruo ya esta en esa posicion.");

        monster.Position = newPosition;
        monster.PositionChangedThisTurn = true;
        Log.Add($"{player.Name} cambia a {monster.Card.Name} a posicion {Describe(newPosition)}.");
        return ActionResult.Ok();
    }

    /// <summary>
    /// Invocacion por Volteo (pagina 20): voltea a Posicion de Ataque boca
    /// arriba un monstruo propio Colocado. Ilimitada por turno, pero no
    /// aplicable el mismo turno en que la carta fue Colocada, y cuenta como el
    /// cambio de posicion de batalla del turno para ese monstruo.
    /// </summary>
    public ActionResult FlipSummon(int zoneIndex)
    {
        var check = ValidateMainPhaseAction();
        if (!check.Success) return check;

        var player = State.ActivePlayer;
        var monster = GetZone(player, zoneIndex);
        if (monster == null) return ActionResult.Fail("No hay monstruo en esa zona.");

        if (monster.Position != BattlePosition.DefenseFaceDown)
            return ActionResult.Fail("Solo se puede Invocar por Volteo un monstruo boca abajo.");
        if (monster.SummonedThisTurn)
            return ActionResult.Fail("No puedes Invocar por Volteo un monstruo Colocado este mismo turno.");

        monster.Position = BattlePosition.Attack;
        monster.PositionChangedThisTurn = true;
        Log.Add($"{player.Name} invoca por Volteo a {monster.Card.Name} ({monster.Card.Attack}/{monster.Card.Defense}).");
        TriggerFlipEffect(player, monster);
        return ActionResult.Ok();
    }

    /// <summary>
    /// Resuelve el efecto de Volteo de un Monstruo de Efecto, si lo tiene
    /// (pagina 9): se dispara cada vez que la carta pasa de boca abajo a boca
    /// arriba, ya sea por Invocacion por Volteo o por ser atacada. Sin
    /// objetivo en esta version (los Monstruos de Volteo con objetivo quedan
    /// para una iteracion posterior).
    /// </summary>
    private void TriggerFlipEffect(Player player, CardInstance monster)
    {
        if (monster.Card.Category != MonsterCategory.Effect) return;
        var action = EffectRegistry.Get(monster.Card.EffectId);
        if (action == null) return;

        Log.Add($"VOLTEO: se activa el efecto de {monster.Card.Name}.");
        action.Resolve(new EffectContext { State = State, Controller = player, Source = monster.Card });
    }

    // ----------------------------------------------------- Acciones de Magia/Trampa

    /// <summary>
    /// Coloca boca abajo una Carta Magica o de Trampa en la primera Zona de
    /// Magia/Trampa libre. Sin limite por turno (a diferencia de la Invocacion
    /// Normal), solo requiere espacio libre. Las Cartas Magicas de Campo no se
    /// Colocan aqui: se activan directamente en la Zona del Campo.
    /// </summary>
    public ActionResult SetSpellOrTrap(int handIndex)
    {
        var check = ValidateMainPhaseAction();
        if (!check.Success) return check;

        var player = State.ActivePlayer;
        if (handIndex < 0 || handIndex >= player.Hand.Count)
            return ActionResult.Fail("Indice de mano invalido.");

        var card = player.Hand[handIndex];
        if (card is SpellCard { SubType: SpellSubType.Field })
            return ActionResult.Fail("Las Cartas Magicas de Campo se activan directamente en la Zona del Campo.");
        if (card is SpellCard { SubType: SpellSubType.Ritual })
            return ActionResult.Fail("Las Cartas Magicas de Ritual se usan con la Invocacion Ritual, no se Colocan.");
        if (card is not (SpellCard or TrapCard))
            return ActionResult.Fail("Solo se pueden Colocar Cartas Magicas o de Trampa.");

        int freeZone = player.FirstFreeSpellTrapZone();
        if (freeZone == -1)
            return ActionResult.Fail("No hay Zonas de Magia/Trampa libres.");

        player.Hand.RemoveAt(handIndex);
        player.SpellTrapZones[freeZone] = new SpellTrapInstance(card, faceUp: false) { SetThisTurn = true };

        Log.Add($"{player.Name} coloca boca abajo una carta en su Zona de Magia/Trampa.");
        return ActionResult.Ok();
    }

    /// <summary>
    /// Activa una Carta Magica directamente desde la mano. Las Cartas de
    /// Trampa nunca se activan desde la mano (deben Colocarse primero); las de
    /// Ritual requieren la Invocacion Ritual (<see cref="RitualSummon"/>). Las
    /// de Campo resuelven al instante y no entran en la Cadena (Decision de
    /// alcance del Bloque 4); el resto se agrega como nuevo eslabon y espera a
    /// que ambos jugadores pasen para resolverse. Si el efecto de la carta
    /// requiere un objetivo, hay que elegirlo aqui, no al resolverse.
    /// </summary>
    public ActionResult ActivateSpell(int handIndex, EffectTarget? target = null)
    {
        if (State.IsOver) return ActionResult.Fail("El duelo ha terminado.");

        var player = ChainActingPlayer();
        if (handIndex < 0 || handIndex >= player.Hand.Count)
            return ActionResult.Fail("Indice de mano invalido.");
        if (player.Hand[handIndex] is not SpellCard spell)
            return ActionResult.Fail("Solo se pueden activar Cartas Magicas desde la mano.");
        if (spell.SubType == SpellSubType.Ritual)
            return ActionResult.Fail("Los Monstruos de Ritual se invocan con la Invocacion Ritual.");

        if (spell.SubType == SpellSubType.Field)
        {
            if (State.Chain.Count > 0)
                return ActionResult.Fail("Una Magia de Campo no puede activarse dentro de una Cadena.");
            var mainCheck = ValidateMainPhaseAction();
            if (!mainCheck.Success) return mainCheck;

            player.Hand.RemoveAt(handIndex);
            PlaceFieldSpell(player, spell);
            return ActionResult.Ok();
        }

        var legality = ValidateChainableSpeed(spell.SpellSpeed);
        if (!legality.Success) return legality;

        var targetCheck = ValidateEffectTarget(spell.EffectId, player, target);
        if (!targetCheck.Success) return targetCheck;

        int freeZone = player.FirstFreeSpellTrapZone();
        if (freeZone == -1)
            return ActionResult.Fail("No hay Zonas de Magia/Trampa libres.");

        player.Hand.RemoveAt(handIndex);
        player.SpellTrapZones[freeZone] = new SpellTrapInstance(spell, faceUp: true);
        AddChainLink(player.Side, freeZone, target);
        return ActionResult.Ok();
    }

    /// <summary>
    /// Activa una Carta Magica o de Trampa que ya esta Colocada boca abajo en
    /// una Zona de Magia/Trampa (desde la mano de su propio dueño en un turno
    /// anterior). Las Trampas no pueden activarse el mismo turno en que
    /// fueron Colocadas. Se agrega como nuevo eslabon de la Cadena.
    /// </summary>
    public ActionResult ActivateSetCard(int zoneIndex, EffectTarget? target = null)
    {
        if (State.IsOver) return ActionResult.Fail("El duelo ha terminado.");

        var player = ChainActingPlayer();
        var instance = GetSpellTrapZone(player, zoneIndex);
        if (instance == null) return ActionResult.Fail("No hay ninguna carta en esa zona.");
        if (instance.FaceUp) return ActionResult.Fail("Esa carta ya esta boca arriba.");

        if (instance.Card is SpellCard spell)
        {
            if (spell.SubType == SpellSubType.Ritual)
                return ActionResult.Fail("Los Monstruos de Ritual se invocan con la Invocacion Ritual.");

            var legality = ValidateChainableSpeed(spell.SpellSpeed);
            if (!legality.Success) return legality;

            var targetCheck = ValidateEffectTarget(spell.EffectId, player, target);
            if (!targetCheck.Success) return targetCheck;

            instance.FaceUp = true;
            AddChainLink(player.Side, zoneIndex, target);
            return ActionResult.Ok();
        }

        if (instance.Card is TrapCard trap)
        {
            if (instance.SetThisTurn)
                return ActionResult.Fail("No puedes activar una Trampa el mismo turno en que la Colocaste.");

            var legality = ValidateChainableSpeed(trap.SpellSpeed);
            if (!legality.Success) return legality;

            var targetCheck = ValidateEffectTarget(trap.EffectId, player, target);
            if (!targetCheck.Success) return targetCheck;

            instance.FaceUp = true;
            AddChainLink(player.Side, zoneIndex, target);
            return ActionResult.Ok();
        }

        return ActionResult.Fail("Esa zona no contiene una Carta Magica o de Trampa.");
    }

    /// <summary>
    /// Si el efecto registrado para <paramref name="effectId"/> requiere
    /// objetivo, valida que se haya elegido uno y que sea legal ahora mismo.
    /// Las cartas sin efecto registrado (o con un efecto sin objetivo) no
    /// necesitan ninguno.
    /// </summary>
    private ActionResult ValidateEffectTarget(string effectId, Player controller, EffectTarget? target)
    {
        if (EffectRegistry.Get(effectId) is not ITargetedEffectAction targeted)
            return ActionResult.Ok();
        if (target is not { } chosen)
            return ActionResult.Fail("Esta carta necesita que elijas un objetivo.");
        if (!targeted.IsValidTarget(State, controller, chosen))
            return ActionResult.Fail("Objetivo invalido para esta carta.");
        return ActionResult.Ok();
    }

    /// <summary>
    /// Sin Cadena abierta: Velocidad 1 solo en tu Main Phase, Velocidad 2/3 en
    /// cualquier fase de tu turno (y el actor es el jugador activo). Con
    /// Cadena abierta: solo puede responder quien tenga la Prioridad, y solo
    /// con Velocidad de Hechizo mayor o igual a 2 y al eslabon superior.
    /// </summary>
    private ActionResult ValidateChainableSpeed(int spellSpeed)
    {
        if (State.Chain.Count > 0)
        {
            int required = Math.Max(2, TopChainLinkSpeed());
            return spellSpeed >= required
                ? ActionResult.Ok()
                : ActionResult.Fail($"Necesitas Velocidad de Hechizo {required} o mayor para responder en esta Cadena.");
        }

        if (spellSpeed >= 2)
            return State.Phase is DuelPhase.Main1 or DuelPhase.Battle or DuelPhase.Main2
                ? ActionResult.Ok()
                : ActionResult.Fail("Esta carta no se puede activar en esta fase.");
        return ValidateMainPhaseAction();
    }

    private int TopChainLinkSpeed()
    {
        var top = State.Chain[^1];
        var card = State.GetPlayer(top.Controller).SpellTrapZones[top.ZoneIndex]!.Card;
        return card switch
        {
            SpellCard s => s.SpellSpeed,
            TrapCard t => t.SpellSpeed,
            _ => 1
        };
    }

    /// <summary>Jugador que debe actuar ahora: el que tiene la Prioridad si hay una Cadena abierta, o el jugador activo.</summary>
    private Player ChainActingPlayer() =>
        State.Chain.Count > 0 ? State.GetPlayer(State.ChainPendingResponder!.Value) : State.ActivePlayer;

    private void AddChainLink(PlayerSide controller, int zoneIndex, EffectTarget? target)
    {
        var card = State.GetPlayer(controller).SpellTrapZones[zoneIndex]!.Card;
        State.Chain.Add(new ChainLink { Controller = controller, ZoneIndex = zoneIndex, Target = target });
        State.ChainConsecutivePasses = 0;
        State.ChainPendingResponder = Opponent(controller);
        Log.Add($"{State.GetPlayer(controller).Name} encadena {card.Name} (Eslabon {State.Chain.Count}).");
    }

    /// <summary>
    /// El jugador con la Prioridad declina responder o encadenar otra carta.
    /// Dos "paso" seguidos (uno de cada jugador) cierran la Cadena y la
    /// resuelven en orden inverso al de activacion.
    /// </summary>
    public ActionResult PassPriority()
    {
        if (State.IsOver) return ActionResult.Fail("El duelo ha terminado.");
        if (State.Chain.Count == 0) return ActionResult.Fail("No hay ninguna Cadena abierta.");

        State.ChainConsecutivePasses++;
        if (State.ChainConsecutivePasses >= 2)
        {
            ResolveChain();
            return ActionResult.Ok();
        }

        State.ChainPendingResponder = Opponent(State.ChainPendingResponder!.Value);
        return ActionResult.Ok();
    }

    /// <summary>
    /// Resuelve la Cadena completa, del ultimo eslabon activado al primero.
    /// Las Continuas/de Equipo simplemente quedan donde ya estaban (boca
    /// arriba desde la activacion); el resto ejecuta su efecto registrado (si
    /// tiene uno) y va al Cementerio, salvo que un Contraefecto ya la haya
    /// negado, en cuyo caso el efecto se salta pero la carta igual se resuelve.
    /// </summary>
    private void ResolveChain()
    {
        var negated = new HashSet<int>();

        for (int i = State.Chain.Count - 1; i >= 0; i--)
        {
            var link = State.Chain[i];
            var player = State.GetPlayer(link.Controller);
            var instance = player.SpellTrapZones[link.ZoneIndex];
            if (instance == null) continue; // ya no esta (defensivo)

            bool isNegated = negated.Contains(i);
            int capturedIndex = i;
            void NegateRespondedLink()
            {
                if (capturedIndex > 0) negated.Add(capturedIndex - 1);
            }

            if (instance.Card is SpellCard spell)
                ResolveSpell(player, spell, link, isNegated, NegateRespondedLink);
            else if (instance.Card is TrapCard trap)
                ResolveTrap(player, trap, link, isNegated, NegateRespondedLink);
        }

        State.Chain.Clear();
        State.ChainPendingResponder = null;
        State.ChainConsecutivePasses = 0;
    }

    private static PlayerSide Opponent(PlayerSide side) =>
        side == PlayerSide.Human ? PlayerSide.Cpu : PlayerSide.Human;

    private void ResolveSpell(Player player, SpellCard spell, ChainLink link, bool negated, Action negateRespondedLink)
    {
        if (spell.SubType is SpellSubType.Continuous or SpellSubType.Equip)
        {
            Log.Add($"{spell.Name} permanece boca arriba en el Campo de {player.Name}.");
            return;
        }

        ExecuteRegisteredEffect(spell.EffectId, spell, player, link, negated, negateRespondedLink);
        player.SpellTrapZones[link.ZoneIndex] = null;
        player.SendToGraveyard(spell);
        Log.Add($"{spell.Name} se resuelve y va al Cementerio.");
    }

    private void ResolveTrap(Player player, TrapCard trap, ChainLink link, bool negated, Action negateRespondedLink)
    {
        if (trap.SubType == TrapSubType.Continuous)
        {
            Log.Add($"{trap.Name} permanece boca arriba en el Campo de {player.Name}.");
            return;
        }

        ExecuteRegisteredEffect(trap.EffectId, trap, player, link, negated, negateRespondedLink);
        player.SpellTrapZones[link.ZoneIndex] = null;
        player.SendToGraveyard(trap);
        Log.Add($"{trap.Name} se resuelve y va al Cementerio.");
    }

    private void ExecuteRegisteredEffect(string effectId, Card source, Player player, ChainLink link, bool negated, Action negateRespondedLink)
    {
        if (negated)
        {
            Log.Add($"La activacion de {source.Name} fue negada.");
            return;
        }

        var action = EffectRegistry.Get(effectId);
        if (action == null) return;

        var context = new EffectContext
        {
            State = State,
            Controller = player,
            Source = source,
            Target = link.Target,
            NegateRespondedLink = negateRespondedLink
        };
        action.Resolve(context);
    }

    /// <summary>
    /// Activa una Carta Magica de Campo: reemplaza la que hubiera en la Zona
    /// del Campo, mandando la anterior al Cementerio.
    /// </summary>
    private void PlaceFieldSpell(Player player, SpellCard spell)
    {
        if (player.FieldZone != null)
        {
            player.SendToGraveyard(player.FieldZone.Card);
            Log.Add($"{player.Name} manda al Cementerio su anterior Carta Magica de Campo.");
        }

        player.FieldZone = new SpellTrapInstance(spell, faceUp: true);
        Log.Add($"{player.Name} activa {spell.Name} en su Zona del Campo.");
    }

    // --------------------------------------------------------- Acciones de Batalla

    /// <summary>
    /// Declara un ataque. <paramref name="targetZone"/> = -1 indica ataque
    /// directo (solo si el adversario no controla monstruos, pagina 39).
    /// </summary>
    public ActionResult DeclareAttack(int attackerZone, int targetZone)
    {
        if (State.IsOver) return ActionResult.Fail("El duelo ha terminado.");
        var chainCheck = ValidateNoOpenChain();
        if (!chainCheck.Success) return chainCheck;
        if (State.Phase != DuelPhase.Battle)
            return ActionResult.Fail("Solo puedes atacar durante la Battle Phase.");

        var attackerPlayer = State.ActivePlayer;
        var defenderPlayer = State.InactivePlayer;

        var attacker = GetZone(attackerPlayer, attackerZone);
        if (attacker == null) return ActionResult.Fail("No hay atacante en esa zona.");
        if (attacker.IsDefending)
            return ActionResult.Fail("Solo los monstruos en Ataque pueden atacar.");
        if (attacker.HasAttackedThisTurn)
            return ActionResult.Fail("Ese monstruo ya ataco este turno.");
        if (attacker.SummonedThisTurn && attacker.Card.Attack == 0)
        {
            // No es regla, pero evita ataques sin sentido; permitido igualmente.
        }

        bool opponentHasMonsters = defenderPlayer.MonsterCount > 0;

        if (targetZone == -1)
        {
            if (opponentHasMonsters)
                return ActionResult.Fail("No puedes atacar directamente: el adversario tiene monstruos.");

            var directOutcome = BattleResolver.ResolveDirectAttack(attacker);
            attacker.HasAttackedThisTurn = true;
            ApplyDamage(defenderPlayer, directOutcome.DamageToDefender);
            Log.Add($"{attacker.Card.Name} ataca directamente: -{directOutcome.DamageToDefender} LP a {defenderPlayer.Name}.");
            CheckLifePoints();
            State.LastAttack = new AttackInfo(attackerPlayer.Side, attackerZone, -1,
                attacker.Card, null, null, false, false);
            return ActionResult.Ok();
        }

        var defender = GetZone(defenderPlayer, targetZone);
        if (defender == null) return ActionResult.Fail("No hay monstruo objetivo en esa zona.");

        attacker.HasAttackedThisTurn = true;

        // Atacar una carta boca abajo: se voltea a Defensa boca arriba (pagina 37).
        bool wasFlipped = defender.Position == BattlePosition.DefenseFaceDown;
        if (wasFlipped)
        {
            defender.Position = BattlePosition.DefenseFaceUp;
            Log.Add($"Se voltea {defender.Card.Name} ({defender.Card.Attack}/{defender.Card.Defense}).");
        }

        var outcome = BattleResolver.Resolve(attacker, defender);
        Log.Add($"{attacker.Card.Name} ataca a {defender.Card.Name}.");

        // Aplicar destrucciones.
        if (outcome.DefenderDestroyed)
        {
            DestroyMonster(defenderPlayer, targetZone);
            Log.Add($"{defender.Card.Name} es destruido.");
        }
        if (outcome.AttackerDestroyed)
        {
            DestroyMonster(attackerPlayer, attackerZone);
            Log.Add($"{attacker.Card.Name} es destruido.");
        }

        // Aplicar daño.
        if (outcome.DamageToDefender > 0)
        {
            ApplyDamage(defenderPlayer, outcome.DamageToDefender);
            Log.Add($"{defenderPlayer.Name} recibe {outcome.DamageToDefender} de daño.");
        }
        if (outcome.DamageToAttacker > 0)
        {
            ApplyDamage(attackerPlayer, outcome.DamageToAttacker);
            Log.Add($"{attackerPlayer.Name} recibe {outcome.DamageToAttacker} de daño.");
        }

        CheckLifePoints();

        // El efecto de Volteo se activa y resuelve despues del calculo de
        // daño (pagina 37), incluso si el monstruo volteado fue destruido en
        // esa misma batalla.
        if (wasFlipped && !State.IsOver)
            TriggerFlipEffect(defenderPlayer, defender);

        State.LastAttack = new AttackInfo(attackerPlayer.Side, attackerZone, targetZone,
            attacker.Card, defender.Card, defender.Position,
            outcome.AttackerDestroyed, outcome.DefenderDestroyed);
        return ActionResult.Ok();
    }

    // ----------------------------------------------------------------- Helpers

    private ActionResult ValidateMainPhaseAction()
    {
        if (State.IsOver) return ActionResult.Fail("El duelo ha terminado.");
        var chainCheck = ValidateNoOpenChain();
        if (!chainCheck.Success) return chainCheck;
        if (State.Phase is not (DuelPhase.Main1 or DuelPhase.Main2))
            return ActionResult.Fail("Esta accion solo es valida en Main Phase.");
        return ActionResult.Ok();
    }

    /// <summary>
    /// Invocar, Sacrificar, cambiar posicion de batalla y pagar costes no son
    /// activaciones de efecto: el reglamento dice explicitamente que no se
    /// puede responder a esas acciones con una Cadena, así que tampoco pueden
    /// ejecutarse mientras una Cadena sigue abierta.
    /// </summary>
    private ActionResult ValidateNoOpenChain()
    {
        if (State.Chain.Count > 0)
            return ActionResult.Fail("Hay una Cadena sin resolver: responde o pasa la Prioridad.");
        return ActionResult.Ok();
    }

    private static bool IsValidHandIndex(Player p, int i) => i >= 0 && i < p.Hand.Count;

    private static CardInstance? GetZone(Player p, int zoneIndex)
    {
        if (zoneIndex < 0 || zoneIndex >= p.MonsterZones.Length) return null;
        return p.MonsterZones[zoneIndex];
    }

    private static SpellTrapInstance? GetSpellTrapZone(Player p, int zoneIndex)
    {
        if (zoneIndex < 0 || zoneIndex >= p.SpellTrapZones.Length) return null;
        return p.SpellTrapZones[zoneIndex];
    }

    /// <summary>
    /// Realiza los Sacrificios necesarios. Si no se especifican zonas, toma los
    /// primeros monstruos disponibles. Valida la cantidad requerida.
    /// </summary>
    private ActionResult ResolveTributes(Player player, int required, int[]? tributeZones)
    {
        if (required == 0) return ActionResult.Ok();

        var zones = tributeZones?.ToList() ?? AutoSelectTributes(player, required);
        zones = zones.Distinct().ToList();

        if (zones.Count != required)
            return ActionResult.Fail($"Se requieren {required} Sacrificio(s).");
        foreach (var z in zones)
            if (GetZone(player, z) == null)
                return ActionResult.Fail("Sacrificio invalido: zona vacia.");

        foreach (var z in zones)
        {
            var sacrificed = player.MonsterZones[z]!;
            player.MonsterZones[z] = null;
            player.SendToGraveyard(sacrificed.Card);
            Log.Add($"{player.Name} sacrifica a {sacrificed.Card.Name}.");
        }
        return ActionResult.Ok();
    }

    private static List<int> AutoSelectTributes(Player player, int required)
    {
        var result = new List<int>();
        for (int i = 0; i < player.MonsterZones.Length && result.Count < required; i++)
            if (player.MonsterZones[i] != null) result.Add(i);
        return result;
    }

    /// <summary>
    /// Auto-selecciona Sacrificios para la Invocacion Ritual hasta alcanzar
    /// el Nivel minimo exigido: primero monstruos del Campo (no gasta otras
    /// cartas de la mano si el Campo ya alcanza), despues de la mano.
    /// </summary>
    private static (List<int> HandIndices, List<int> FieldZones) AutoSelectRitualTributes(
        Player player, int requiredLevel, int excludeHandIndexA, int excludeHandIndexB)
    {
        var fieldZones = new List<int>();
        var handIndices = new List<int>();
        int sum = 0;

        for (int z = 0; z < player.MonsterZones.Length && sum < requiredLevel; z++)
        {
            var instance = player.MonsterZones[z];
            if (instance == null) continue;
            fieldZones.Add(z);
            sum += instance.Card.Level;
        }

        for (int i = 0; i < player.Hand.Count && sum < requiredLevel; i++)
        {
            if (i == excludeHandIndexA || i == excludeHandIndexB) continue;
            if (player.Hand[i] is not MonsterCard monster) continue;
            handIndices.Add(i);
            sum += monster.Level;
        }

        return (handIndices, fieldZones);
    }

    private void DestroyMonster(Player player, int zoneIndex)
    {
        var instance = player.MonsterZones[zoneIndex];
        if (instance == null) return;
        player.MonsterZones[zoneIndex] = null;
        player.SendToGraveyard(instance.Card);
    }

    private static void ApplyDamage(Player player, int amount)
    {
        if (amount <= 0) return;
        player.LifePoints = Math.Max(0, player.LifePoints - amount);
    }

    private void CheckLifePoints()
    {
        bool humanDead = State.Players[0].LifePoints <= 0;
        bool cpuDead = State.Players[1].LifePoints <= 0;

        if (humanDead && cpuDead)
        {
            EndDuel(null, "Ambos jugadores llegan a 0 LP.");
        }
        else if (humanDead)
        {
            EndDuel(State.Players[1].Side, $"{State.Players[0].Name} llega a 0 LP.");
        }
        else if (cpuDead)
        {
            EndDuel(State.Players[0].Side, $"{State.Players[1].Name} llega a 0 LP.");
        }
    }

    private void EndDuel(PlayerSide? winner, string reason)
    {
        State.IsOver = true;
        State.Winner = winner;
        Log.Add(winner.HasValue
            ? $"Fin del duelo: gana {State.GetPlayer(winner.Value).Name}. ({reason})"
            : $"Fin del duelo: empate. ({reason})");
    }

    private static string Describe(BattlePosition position) => position switch
    {
        BattlePosition.Attack => "Ataque",
        BattlePosition.DefenseFaceUp => "Defensa",
        BattlePosition.DefenseFaceDown => "Defensa boca abajo",
        _ => position.ToString()
    };
}
