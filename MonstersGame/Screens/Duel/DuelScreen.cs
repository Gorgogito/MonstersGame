using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonstersGame.Core.AI;
using MonstersGame.Core.Battle;
using MonstersGame.Core.Effects;
using MonstersGame.Core.Entities;
using MonstersGame.Graphics;
using MonstersGame.Screens.Result;

namespace MonstersGame.Screens.Duel;

/// <summary>
/// Pantalla de Duelo: dibuja el campo de ambos jugadores (incluidas las Zonas
/// de Magia/Trampa y la Zona de Campo), la mano del humano, LP, turno, fase y
/// la Cadena en curso, y traduce la entrada del usuario en acciones del motor.
/// La CPU juega automaticamente mediante la IA. La pantalla NO contiene
/// reglas: solo orquesta la presentacion y delega toda decision al
/// <see cref="DuelEngine"/>.
/// </summary>
public sealed class DuelScreen : Screen
{
    private const int HumanIndex = 0;
    private const int CpuIndex = 1;
    private const double AiStepDelay = 0.55;   // segundos entre jugadas de la CPU
    private const double GameOverDelay = 1.8;  // pausa antes de mostrar resultado

    private const int CardW = 92;
    private const int CardH = 118;
    private const int Gap = 8;
    private const int SmallH = 58;      // alto de la Zona de Magia/Trampa y la Zona de Campo
    private const int FieldZoneW = 100;
    private const int FieldGap = 24;    // separacion visual entre la fila de M/T y la Zona de Campo

    private readonly DuelEngine _engine;
    private readonly IDuelAI _ai;

    // Estado de interaccion del jugador humano.
    private int _selectedHand = -1;
    private int _selectedZone = -1;       // monstruo propio seleccionado (Main)
    private int _selectedAttacker = -1;   // atacante seleccionado (Battle)
    private bool _fusionMode;
    private int _fusionFirst = -1;
    private bool _ritualMode;
    private int _ritualFirst = -1;
    private string _statusMessage = "";

    // Seleccion de objetivo al activar una Magia/Trampa cuyo efecto lo requiere.
    private bool _targetMode;
    private bool _targetIsSetCard; // true = ActivateSetCard, false = ActivateSpell
    private int _targetSourceIndex;
    private EffectTargetKind _pendingTargetKind;
    private int _graveyardCursor = -1;

    // Seleccion manual de Sacrificios al Invocar/Colocar un monstruo de Nivel 5+.
    private bool _tributeMode;
    private bool _tributeIsSet; // true = Colocar, false = Invocacion Normal
    private readonly List<int> _selectedTributes = new();

    // Seleccion manual de cartas a descartar por limite de mano (End Phase).
    private readonly List<int> _selectedDiscards = new();

    // Zona de Magia/Trampa propia seleccionada con un clic directo sobre su
    // casilla en el tablero (ver UpdateSpellTrapZoneInput).
    private int _spellTrapCursor = -1;

    private double _aiTimer;
    private double _overTimer;
    private DuelPhase _lastPhase;
    private int _lastActive;

    // Animaciones y audio (Bloque 10): puramente cosmeticos, se detectan por
    // diferencia frame a frame contra el estado del motor — asi cubren tanto
    // las acciones del humano como las de la CPU sin duplicar logica en cada
    // punto de llamada.
    private const float ZoneFlashDuration = 0.3f;
    private const float DamageFlashDuration = 0.4f;
    private const int LpEasePerSecond = 6000;

    private int _lastHumanLp = -1, _lastCpuLp = -1;
    private int _displayedHumanLp, _displayedCpuLp;
    private float _humanDamageFlashTimer, _cpuDamageFlashTimer;
    private int _lastChainCount;
    private bool _resultSoundPlayed;

    private readonly bool[] _lastHumanZoneOccupied = new bool[Player.MonsterZoneCount];
    private readonly bool[] _lastCpuZoneOccupied = new bool[Player.MonsterZoneCount];
    private readonly bool[] _lastHumanZoneFaceUp = new bool[Player.MonsterZoneCount];
    private readonly bool[] _lastCpuZoneFaceUp = new bool[Player.MonsterZoneCount];
    private readonly float[] _humanZoneFlashTimer = new float[Player.MonsterZoneCount];
    private readonly float[] _cpuZoneFlashTimer = new float[Player.MonsterZoneCount];

    // Choque de batalla (Bloque 17): al declararse un ataque (del humano o de
    // la CPU, da igual — ambos pasan por DuelEngine.DeclareAttack), el
    // atacante y el objetivo convergen hacia el punto medio entre sus dos
    // Zonas, se sostienen un instante y vuelven; el o los monstruos
    // destruidos se encogen y desaparecen en su lugar en vez de
    // "teletransportarse" a vacio de golpe. Se detecta comparando
    // DuelState.LastAttack contra el ultimo que ya vimos — el mismo patron de
    // diferencia frame a frame que el resto de estas animaciones, pero
    // leyendo un dato que el motor ya calculo (zonas, cartas, quien murio) en
    // vez de inferirlo.
    private const float ClashConvergeDuration = 0.22f;
    private const float ClashHoldDuration = 0.16f;
    private const float ClashReturnDuration = 0.22f;
    private const float ClashMoveTotal = ClashConvergeDuration + ClashHoldDuration + ClashReturnDuration;
    private const float ClashFadeDuration = 0.30f;
    private const float ClashTotalDuration = ClashMoveTotal + ClashFadeDuration;
    private const float ClashConvergeFraction = 0.12f; // que tanto de la distancia entre zonas se recorre
    private const float DirectLungeDistance = 18f;      // ataque directo: no hay objetivo con quien converger

    private AttackInfo? _lastObservedAttack;
    private bool _clashActive;
    private float _clashTimer;
    private bool _clashAttackerIsHuman;
    private int _clashAttackerZone = -1;
    private int _clashDefenderZone = -1; // -1 = ataque directo, sin objetivo
    private MonsterCard? _clashAttackerCard;
    private MonsterCard? _clashDefenderCard;
    private BattlePosition? _clashDefenderPosition;
    private bool _clashAttackerDestroyed;
    private bool _clashDefenderDestroyed;

    // Layout calculado a partir del tamano de pantalla (1280x720). De arriba
    // a abajo: HUD de la CPU, su Zona de M/T + Zona de Campo, sus Monstruos,
    // la franja central (fase/turno), los Monstruos propios, la propia Zona
    // de M/T + Zona de Campo, el HUD propio, dos lineas de estado, la mano y
    // los controles. La columna derecha aloja el panel de Cadena y el registro.
    private int _fieldStartX;
    private int _cpuHudY, _cpuFieldRowY, _cpuMonsterY;
    private int _centerY;
    private int _playerMonsterY, _playerFieldRowY, _playerHudY;
    private int _statusY, _statusY2;
    private int _handY, _handAreaW;
    private int _controlsY;
    private int _rightColX, _rightColW;
    private int _chainPanelY, _chainPanelH, _logY, _logH;

    public DuelScreen(GameContext ctx, DuelEngine engine, IDuelAI ai) : base(ctx)
    {
        _engine = engine;
        _ai = ai;
    }

    public override void OnEnter()
    {
        _fieldStartX = 40;

        _cpuHudY = 6;
        _cpuFieldRowY = 46;
        _cpuMonsterY = 110;
        _centerY = 228;
        _playerMonsterY = 278;
        _playerFieldRowY = 402;
        _playerHudY = 464;
        _statusY = 504;
        _statusY2 = 517;
        _handY = 530;
        _controlsY = 652;
        _handAreaW = 600;

        _rightColX = _fieldStartX + 5 * (CardW + Gap) + FieldGap + FieldZoneW + 24;
        _rightColW = Ctx.ScreenWidth - _rightColX - 40;
        _chainPanelY = _cpuFieldRowY;
        _chainPanelH = 220;
        _logY = _chainPanelY + _chainPanelH + 10;
        _logH = _handY - 20 - _logY;

        _lastPhase = _engine.Phase;
        _lastActive = _engine.ActiveIndex;

        _lastHumanLp = -1;
        _lastCpuLp = -1;
        _lastChainCount = 0;
        _resultSoundPlayed = false;
        _humanDamageFlashTimer = 0;
        _cpuDamageFlashTimer = 0;
        _lastObservedAttack = null;
        _clashActive = false;
        _clashTimer = 0;
        _clashAttackerZone = -1;
        _clashDefenderZone = -1;
        Array.Clear(_lastHumanZoneOccupied);
        Array.Clear(_lastCpuZoneOccupied);
        Array.Clear(_lastHumanZoneFaceUp);
        Array.Clear(_lastCpuZoneFaceUp);
        Array.Clear(_humanZoneFlashTimer);
        Array.Clear(_cpuZoneFlashTimer);
    }

    // ----------------------------------------------------------------- Update

    public override void Update(GameTime gameTime)
    {
        double dt = gameTime.ElapsedGameTime.TotalSeconds;
        UpdateAnimations(dt);

        if (_engine.IsOver)
        {
            _overTimer += dt;
            if (_overTimer >= GameOverDelay)
                Ctx.Screens.Set(new ResultScreen(Ctx, _engine.State.Winner));
            return;
        }

        // Limpiar seleccion cuando cambia el turno o la fase.
        if (_engine.Phase != _lastPhase || _engine.ActiveIndex != _lastActive)
        {
            ClearSelections();
            _lastPhase = _engine.Phase;
            _lastActive = _engine.ActiveIndex;
        }

        // Una Cadena abierta le puede pedir Prioridad a cualquiera de los dos
        // jugadores, sin importar de quien sea el turno.
        if (_engine.State.Chain.Count > 0)
        {
            if (_engine.State.ChainPendingResponder == PlayerSide.Cpu)
                UpdateCpu(dt);
            else
                UpdatePlayer();
            return;
        }

        if (_engine.ActiveIndex == CpuIndex)
        {
            UpdateCpu(dt);
            return;
        }

        UpdatePlayer();
    }

    private void UpdateCpu(double dt)
    {
        ClearSelections();
        _aiTimer += dt;
        if (_aiTimer >= AiStepDelay)
        {
            _aiTimer = 0;
            _ai.Step(_engine, CpuIndex);
        }
    }

    private void UpdatePlayer()
    {
        var input = Ctx.Input;

        // El limite de mano en End Phase exige elegir que descartar antes de
        // poder hacer cualquier otra cosa: se resuelve con su propia UI.
        if (_engine.State.PendingDiscardCount > 0)
        {
            UpdateDiscardInput();
            return;
        }

        // Elegir el objetivo de un efecto tiene prioridad sobre cualquier otra
        // interaccion (incluida la Cadena, si esta activacion es una respuesta).
        if (_targetMode)
        {
            UpdateTargetSelectionInput();
            return;
        }

        bool chainOpen = _engine.State.Chain.Count > 0;

        if (chainOpen)
        {
            // Con la Cadena abierta, avanzar de fase o terminar el turno no
            // son acciones validas: solo se puede encadenar otra carta o
            // pasar la Prioridad.
            if (Widgets.Clicked(Ctx, ChainPassRect) && _engine.State.ChainPendingResponder == PlayerSide.Human)
                Apply(_engine.PassPriority());
        }
        else
        {
            if (input.KeyPressed(Keys.Escape)) ClearSelections();
            if (input.KeyPressed(Keys.Space)) { Apply(_engine.AdvancePhase()); ClearSelections(); }
            if (input.KeyPressed(Keys.Enter)) { Apply(_engine.EndTurn()); ClearSelections(); }

            // Botones de control de fase.
            if (Widgets.Clicked(Ctx, PhaseButtonRect)) { Apply(_engine.AdvancePhase()); ClearSelections(); }
            if (Widgets.Clicked(Ctx, EndTurnButtonRect)) { Apply(_engine.EndTurn()); ClearSelections(); }
        }

        if (_engine.Phase is DuelPhase.Main1 or DuelPhase.Main2)
            UpdateMainPhaseInput();
        else if (_engine.Phase == DuelPhase.Battle)
            UpdateBattlePhaseInput();

        // Activar una Magia/Trampa ya Colocada esta disponible en Main1,
        // Battle y Main2 (las de Velocidad de Hechizo 2/3 no se limitan a la
        // Main Phase); el motor valida la legalidad exacta.
        if (!_tributeMode && !_fusionMode && !_ritualMode && _engine.Phase is DuelPhase.Main1 or DuelPhase.Battle or DuelPhase.Main2)
            UpdateSpellTrapZoneInput();
    }

    private void UpdateMainPhaseInput()
    {
        var input = Ctx.Input;
        var human = _engine.State.Players[HumanIndex];

        if (_tributeMode)
        {
            UpdateTributeSelectionInput();
            return;
        }

        // Acciones sobre la carta de mano seleccionada.
        if (_selectedHand >= 0 && _selectedHand < human.Hand.Count
            && human.Hand[_selectedHand] is MonsterCard monsterCard)
        {
            if (!_fusionMode)
            {
                if (Widgets.Clicked(Ctx, ActionRect(0))) BeginSummon(monsterCard, isSet: false);
                if (Widgets.Clicked(Ctx, ActionRect(1))) BeginSummon(monsterCard, isSet: true);
                if (Widgets.Clicked(Ctx, ActionRect(2)))
                {
                    _fusionMode = true;
                    _fusionFirst = _selectedHand;
                    _statusMessage = "FUSION: ELIGE LA SEGUNDA CARTA";
                }
            }
            else if (Widgets.Clicked(Ctx, ActionRect(2)))
            {
                ClearFusion();
            }
        }
        else if (_selectedHand >= 0 && _selectedHand < human.Hand.Count
            && human.Hand[_selectedHand] is SpellCard { SubType: SpellSubType.Ritual })
        {
            if (!_ritualMode && Widgets.Clicked(Ctx, ActionRect(0)))
            {
                _ritualMode = true;
                _ritualFirst = _selectedHand;
                _statusMessage = "INVOCACION RITUAL: ELIGE AL MONSTRUO DE RITUAL EN TU MANO";
            }
            else if (_ritualMode && Widgets.Clicked(Ctx, ActionRect(2)))
            {
                ClearRitual();
            }
        }
        else if (_selectedHand >= 0 && _selectedHand < human.Hand.Count
            && human.Hand[_selectedHand] is SpellCard or TrapCard)
        {
            if (Widgets.Clicked(Ctx, ActionRect(0)))
                Apply(_engine.SetSpellOrTrap(_selectedHand), clearOnSuccess: true);
            if (human.Hand[_selectedHand] is SpellCard spellCard
                && Widgets.Clicked(Ctx, ActionRect(1)))
                BeginActivation(isSetCard: false, _selectedHand, spellCard.EffectId);
        }

        // Cambio de posicion / Invocacion por Volteo del monstruo propio seleccionado.
        if (_selectedZone >= 0)
        {
            var m = human.MonsterZones[_selectedZone];
            if (m is { Position: BattlePosition.DefenseFaceDown, SummonedThisTurn: false })
            {
                if (Widgets.Clicked(Ctx, ActionRect(0)))
                    Apply(_engine.FlipSummon(_selectedZone), clearOnSuccess: true);
            }
            else if (m is { IsFaceUp: true })
            {
                if (m.IsDefending && Widgets.Clicked(Ctx, ActionRect(0)))
                    Apply(_engine.ChangePosition(_selectedZone, BattlePosition.Attack), clearOnSuccess: true);
                if (!m.IsDefending && Widgets.Clicked(Ctx, ActionRect(1)))
                    Apply(_engine.ChangePosition(_selectedZone, BattlePosition.DefenseFaceUp), clearOnSuccess: true);
            }
        }

        // Clic en cartas de la mano.
        int handCount = human.Hand.Count;
        for (int i = 0; i < handCount; i++)
        {
            if (!input.ClickedIn(HandRect(i, handCount))) continue;

            if (_fusionMode)
            {
                if (i != _fusionFirst)
                {
                    Apply(_engine.Fuse(_fusionFirst, i));
                    ClearSelections();
                }
            }
            else if (_ritualMode)
            {
                if (i != _ritualFirst)
                {
                    Apply(_engine.RitualSummon(_ritualFirst, i));
                    ClearSelections();
                }
            }
            else
            {
                _selectedHand = i;
                _selectedZone = -1;
                _spellTrapCursor = -1;
                _statusMessage = "";
            }
            return;
        }

        // Clic en monstruo propio (para cambiar posicion).
        for (int z = 0; z < Player.MonsterZoneCount; z++)
        {
            if (input.ClickedIn(PlayerZone(z)) && human.MonsterZones[z] != null)
            {
                _selectedZone = z;
                _selectedHand = -1;
                _spellTrapCursor = -1;
                _fusionMode = false;
                _statusMessage = "";
                return;
            }
        }
    }

    /// <summary>
    /// Inicia la jugada de un monstruo de mano. Si no requiere Sacrificios se
    /// resuelve de inmediato; si requiere, entra en modo de seleccion manual
    /// de Sacrificios en vez de auto-elegirlos.
    /// </summary>
    private void BeginSummon(MonsterCard monster, bool isSet)
    {
        if (monster.RequiredTributes == 0)
        {
            var result = isSet
                ? _engine.SetMonster(_selectedHand)
                : _engine.NormalSummon(_selectedHand, BattlePosition.Attack);
            Apply(result, clearOnSuccess: true);
            return;
        }

        _tributeMode = true;
        _tributeIsSet = isSet;
        _selectedTributes.Clear();
        _statusMessage = $"ELIGE {monster.RequiredTributes} SACRIFICIO(S) EN TU CAMPO";
    }

    private void UpdateTributeSelectionInput()
    {
        var input = Ctx.Input;
        var human = _engine.State.Players[HumanIndex];

        if (_selectedHand < 0 || _selectedHand >= human.Hand.Count
            || human.Hand[_selectedHand] is not MonsterCard monster)
        {
            CancelTributeSelection();
            return;
        }

        // Clic en un monstruo propio: lo marca/desmarca como Sacrificio.
        for (int z = 0; z < Player.MonsterZoneCount; z++)
        {
            if (!input.ClickedIn(PlayerZone(z))) continue;
            if (human.MonsterZones[z] != null)
                ToggleTribute(z, monster.RequiredTributes);
            return;
        }

        if (_selectedTributes.Count == monster.RequiredTributes
            && Widgets.Clicked(Ctx, ActionRect(0)))
        {
            var result = _tributeIsSet
                ? _engine.SetMonster(_selectedHand, _selectedTributes.ToArray())
                : _engine.NormalSummon(_selectedHand, BattlePosition.Attack, _selectedTributes.ToArray());
            Apply(result, clearOnSuccess: true);
            return;
        }

        if (Widgets.Clicked(Ctx, ActionRect(2)))
            CancelTributeSelection();
    }

    private void ToggleTribute(int zone, int required)
    {
        if (_selectedTributes.Contains(zone))
            _selectedTributes.Remove(zone);
        else if (_selectedTributes.Count < required)
            _selectedTributes.Add(zone);
    }

    private void CancelTributeSelection()
    {
        _tributeMode = false;
        _selectedTributes.Clear();
        _statusMessage = "";
    }

    private void ClearRitual()
    {
        _ritualMode = false;
        _ritualFirst = -1;
        _statusMessage = "";
    }

    /// <summary>
    /// Comienza a activar una Magia/Trampa (desde la mano o desde una
    /// Colocacion previa). Si su efecto registrado requiere un objetivo, no
    /// llama al motor todavia: entra en modo de seleccion de objetivo, cuyo
    /// tipo de control (Zona de Monstruos o Cementerio propio) lo decide la
    /// propia carta via <see cref="ITargetedEffectAction.TargetKind"/> — la
    /// pantalla no necesita saber que hace cada efecto en concreto.
    /// </summary>
    private void BeginActivation(bool isSetCard, int sourceIndex, string effectId)
    {
        if (EffectRegistry.Get(effectId) is ITargetedEffectAction targeted)
        {
            _targetMode = true;
            _targetIsSetCard = isSetCard;
            _targetSourceIndex = sourceIndex;
            _pendingTargetKind = targeted.TargetKind;
            _graveyardCursor = -1;
            _statusMessage = targeted.TargetKind == EffectTargetKind.MonsterZone
                ? "ELIGE 1 MONSTRUO EN EL CAMPO COMO OBJETIVO"
                : "ELIGE 1 MONSTRUO DE TU CEMENTERIO COMO OBJETIVO";
            return;
        }

        var result = isSetCard ? _engine.ActivateSetCard(sourceIndex) : _engine.ActivateSpell(sourceIndex);
        Apply(result, clearOnSuccess: true);
    }

    private void UpdateTargetSelectionInput()
    {
        var input = Ctx.Input;
        var human = _engine.State.Players[HumanIndex];
        var cpu = _engine.State.Players[CpuIndex];

        if (Widgets.Clicked(Ctx, CancelActionRect))
        {
            CancelTargetSelection();
            return;
        }

        if (_pendingTargetKind == EffectTargetKind.MonsterZone)
        {
            for (int z = 0; z < Player.MonsterZoneCount; z++)
            {
                if (input.ClickedIn(PlayerZone(z)) && human.MonsterZones[z] != null)
                {
                    ConfirmTarget(new EffectTarget { Side = PlayerSide.Human, ZoneIndex = z });
                    return;
                }
                if (input.ClickedIn(CpuZone(z)) && cpu.MonsterZones[z] != null)
                {
                    ConfirmTarget(new EffectTarget { Side = PlayerSide.Cpu, ZoneIndex = z });
                    return;
                }
            }
            return;
        }

        // EffectTargetKind.OwnGraveyard
        if (Widgets.Clicked(Ctx, GraveyardCycleRect))
            CycleGraveyardCursor(human);

        if (_graveyardCursor >= 0 && _graveyardCursor < human.Graveyard.Count
            && Widgets.Clicked(Ctx, GraveyardConfirmRect))
            ConfirmTarget(new EffectTarget { Side = PlayerSide.Human, ZoneIndex = _graveyardCursor });
    }

    private void CycleGraveyardCursor(Player human)
    {
        if (human.Graveyard.Count == 0) { _graveyardCursor = -1; return; }
        for (int step = 1; step <= human.Graveyard.Count; step++)
        {
            int candidate = (_graveyardCursor + step + human.Graveyard.Count) % human.Graveyard.Count;
            if (human.Graveyard[candidate] is MonsterCard)
            {
                _graveyardCursor = candidate;
                return;
            }
        }
        _graveyardCursor = -1; // no hay ningun Monstruo en el Cementerio
    }

    private void ConfirmTarget(EffectTarget target)
    {
        var result = _targetIsSetCard
            ? _engine.ActivateSetCard(_targetSourceIndex, target)
            : _engine.ActivateSpell(_targetSourceIndex, target);
        Apply(result, clearOnSuccess: true);
        if (result.Success) CancelTargetSelection();
    }

    private void CancelTargetSelection()
    {
        _targetMode = false;
        _graveyardCursor = -1;
        _statusMessage = "";
    }

    /// <summary>
    /// UI del descarte elegido por el jugador en la End Phase (pagina 36): se
    /// hace clic en cartas de la mano hasta marcar exactamente las requeridas
    /// y se confirma. Mientras haya descarte pendiente no se puede hacer nada
    /// mas (el motor ya bloquea el resto de acciones al estar en End Phase).
    /// </summary>
    private void UpdateDiscardInput()
    {
        var input = Ctx.Input;
        var human = _engine.State.Players[HumanIndex];
        int required = _engine.State.PendingDiscardCount;

        int handCount = human.Hand.Count;
        for (int i = 0; i < handCount; i++)
        {
            if (!input.ClickedIn(HandRect(i, handCount))) continue;

            if (_selectedDiscards.Contains(i))
                _selectedDiscards.Remove(i);
            else if (_selectedDiscards.Count < required)
                _selectedDiscards.Add(i);
            return;
        }

        if (_selectedDiscards.Count == required && Widgets.Clicked(Ctx, DiscardConfirmRect))
        {
            Apply(_engine.DiscardForEndPhase(_selectedDiscards.ToArray()));
            _selectedDiscards.Clear();
        }
    }

    /// <summary>
    /// Seleccion directa de una Zona de Magia/Trampa propia: un clic sobre su
    /// casilla en el tablero la selecciona (y limpia cualquier otra seleccion
    /// en curso, igual que un clic en la mano o en un Monstruo propio), y el
    /// boton contextual ACTIVAR aparece si esta boca abajo.
    /// </summary>
    private void UpdateSpellTrapZoneInput()
    {
        var input = Ctx.Input;
        var human = _engine.State.Players[HumanIndex];

        for (int z = 0; z < Player.SpellTrapZoneCount; z++)
        {
            if (!input.ClickedIn(PlayerSpellTrapZone(z))) continue;
            if (human.SpellTrapZones[z] == null) return;

            _spellTrapCursor = z;
            _selectedHand = -1;
            _selectedZone = -1;
            _selectedAttacker = -1;
            _fusionMode = false;
            _statusMessage = "";
            return;
        }

        if (_spellTrapCursor < 0 || _spellTrapCursor >= Player.SpellTrapZoneCount)
            return;

        var instance = human.SpellTrapZones[_spellTrapCursor];
        if (instance == null)
        {
            _spellTrapCursor = -1;
            return;
        }

        if (!instance.FaceUp && Widgets.Clicked(Ctx, ActionRect(1)))
        {
            string effectId = instance.Card switch
            {
                SpellCard s => s.EffectId,
                TrapCard t => t.EffectId,
                _ => ""
            };
            BeginActivation(isSetCard: true, _spellTrapCursor, effectId);
        }
    }

    private void UpdateBattlePhaseInput()
    {
        var input = Ctx.Input;
        var human = _engine.State.Players[HumanIndex];
        var cpu = _engine.State.Players[CpuIndex];

        // Seleccionar atacante.
        for (int z = 0; z < Player.MonsterZoneCount; z++)
        {
            if (!input.ClickedIn(PlayerZone(z))) continue;
            var m = human.MonsterZones[z];
            if (m is { Position: BattlePosition.Attack, HasAttackedThisTurn: false })
            {
                _selectedAttacker = z;
                _spellTrapCursor = -1;
                _statusMessage = "ELIGE OBJETIVO DE ATAQUE";
            }
            return;
        }

        if (_selectedAttacker < 0) return;

        // Ataque directo si la CPU no tiene monstruos.
        if (cpu.MonsterCount == 0)
        {
            if (Widgets.Clicked(Ctx, DirectAttackRect))
            {
                var result = _engine.DeclareAttack(_selectedAttacker, -1);
                Apply(result);
                _selectedAttacker = -1;
            }
            return;
        }

        // Ataque a un monstruo objetivo.
        for (int z = 0; z < Player.MonsterZoneCount; z++)
        {
            if (input.ClickedIn(CpuZone(z)) && cpu.MonsterZones[z] != null)
            {
                var result = _engine.DeclareAttack(_selectedAttacker, z);
                Apply(result);
                _selectedAttacker = -1;
                return;
            }
        }
    }

    private void Apply(ActionResult result, bool clearOnSuccess = false)
    {
        if (!result.Success)
        {
            _statusMessage = result.Message;
            Ctx.Audio.PlaySfx("error");
        }
        else if (clearOnSuccess)
            ClearSelections();
    }

    private void ClearSelections()
    {
        _selectedHand = -1;
        _selectedZone = -1;
        _selectedAttacker = -1;
        _spellTrapCursor = -1;
        ClearFusion();
        ClearRitual();
        CancelTributeSelection();
        CancelTargetSelection();
        _selectedDiscards.Clear();
    }

    private void ClearFusion()
    {
        _fusionMode = false;
        _fusionFirst = -1;
    }

    // ------------------------------------------------------------ Animaciones

    /// <summary>
    /// Actualiza todo el estado cosmetico (LP mostrado, destellos de dano,
    /// destellos de invocacion/volteo, animacion de ataque) comparando el
    /// estado del motor de este fotograma contra el anterior. Corre siempre,
    /// incluso durante el turno de la CPU o con la Cadena abierta, para que
    /// las animaciones sigan avanzando sin importar quien esta jugando.
    /// </summary>
    private void UpdateAnimations(double dt)
    {
        var human = _engine.State.Players[HumanIndex];
        var cpu = _engine.State.Players[CpuIndex];

        if (_lastHumanLp < 0) { _lastHumanLp = human.LifePoints; _displayedHumanLp = human.LifePoints; }
        if (_lastCpuLp < 0) { _lastCpuLp = cpu.LifePoints; _displayedCpuLp = cpu.LifePoints; }

        if (human.LifePoints < _lastHumanLp) { _humanDamageFlashTimer = DamageFlashDuration; Ctx.Audio.PlaySfx("damage"); }
        if (cpu.LifePoints < _lastCpuLp) { _cpuDamageFlashTimer = DamageFlashDuration; Ctx.Audio.PlaySfx("damage"); }
        _lastHumanLp = human.LifePoints;
        _lastCpuLp = cpu.LifePoints;

        _displayedHumanLp = EaseTowards(_displayedHumanLp, human.LifePoints, dt);
        _displayedCpuLp = EaseTowards(_displayedCpuLp, cpu.LifePoints, dt);

        _humanDamageFlashTimer = Math.Max(0, _humanDamageFlashTimer - (float)dt);
        _cpuDamageFlashTimer = Math.Max(0, _cpuDamageFlashTimer - (float)dt);

        if (_clashActive)
        {
            _clashTimer += (float)dt;
            if (_clashTimer >= ClashTotalDuration) _clashActive = false;
        }

        UpdateZoneFlashes(human.MonsterZones, _lastHumanZoneOccupied, _lastHumanZoneFaceUp, _humanZoneFlashTimer, dt);
        UpdateZoneFlashes(cpu.MonsterZones, _lastCpuZoneOccupied, _lastCpuZoneFaceUp, _cpuZoneFlashTimer, dt);

        DetectAttacks();

        if (_engine.State.Chain.Count > _lastChainCount)
            Ctx.Audio.PlaySfx("chain");
        _lastChainCount = _engine.State.Chain.Count;

        if (_engine.IsOver && !_resultSoundPlayed)
        {
            _resultSoundPlayed = true;
            Ctx.Audio.PlaySfx(_engine.State.Winner == PlayerSide.Human ? "win" : "lose");
        }
    }

    /// <summary>Acerca <paramref name="current"/> a <paramref name="target"/> a velocidad constante (LP/segundo), usado para que la barra de vida no salte de golpe.</summary>
    private static int EaseTowards(int current, int target, double dt)
    {
        if (current == target) return target;
        double step = Math.Sign(target - current) * Math.Min(Math.Abs(target - current), LpEasePerSecond * dt);
        int next = current + (int)step;
        return Math.Abs(target - next) < 5 ? target : next; // evita quedar oscilando por redondeo
    }

    /// <summary>
    /// Detecta, por Zona de Monstruos, una transicion de vacio-a-ocupado o de
    /// boca-abajo-a-boca-arriba (Invocacion, Colocacion o Volteo) y dispara un
    /// destello breve mas el sonido correspondiente.
    /// </summary>
    private void UpdateZoneFlashes(CardInstance?[] zones, bool[] lastOccupied, bool[] lastFaceUp, float[] flashTimers, double dt)
    {
        for (int z = 0; z < zones.Length; z++)
        {
            var inst = zones[z];
            bool occupied = inst != null;
            bool faceUp = inst?.IsFaceUp ?? false;

            bool justSummoned = occupied && !lastOccupied[z];
            bool justFlipped = occupied && faceUp && !lastFaceUp[z] && lastOccupied[z];
            if (justSummoned || justFlipped)
            {
                flashTimers[z] = ZoneFlashDuration;
                Ctx.Audio.PlaySfx(faceUp ? "summon" : "set");
            }

            lastOccupied[z] = occupied;
            lastFaceUp[z] = faceUp;
            flashTimers[z] = Math.Max(0, flashTimers[z] - (float)dt);
        }
    }

    /// <summary>
    /// Detecta un ataque nuevo (del humano o de la CPU — ambos declarados via
    /// <see cref="DuelEngine.DeclareAttack"/>) comparando <c>State.LastAttack</c>
    /// contra el ultimo que ya vimos, y dispara el choque visual. El motor ya
    /// sabe exactamente que zonas participaron y que se destruyo: no hace
    /// falta que la pantalla lo infiera ni distinga entre humano y CPU.
    /// </summary>
    private void DetectAttacks()
    {
        var attack = _engine.State.LastAttack;
        if (attack == null || attack.Equals(_lastObservedAttack)) return;
        _lastObservedAttack = attack;
        BeginClash(attack.Value);
    }

    private void BeginClash(AttackInfo attack)
    {
        _clashActive = true;
        _clashTimer = 0f;
        _clashAttackerIsHuman = attack.AttackerSide == _engine.State.Players[HumanIndex].Side;
        _clashAttackerZone = attack.AttackerZone;
        _clashDefenderZone = attack.DefenderZone;
        _clashAttackerCard = attack.AttackerCard;
        _clashDefenderCard = attack.DefenderCard;
        _clashDefenderPosition = attack.DefenderPosition;
        _clashAttackerDestroyed = attack.AttackerDestroyed;
        _clashDefenderDestroyed = attack.DefenderDestroyed;

        Ctx.Audio.PlaySfx("attack");
        if (attack.DefenderZone >= 0)
        {
            // Reutiliza el destello blanco de Zona (Bloque 10) como "impacto" del choque.
            var attackerFlash = _clashAttackerIsHuman ? _humanZoneFlashTimer : _cpuZoneFlashTimer;
            var defenderFlash = _clashAttackerIsHuman ? _cpuZoneFlashTimer : _humanZoneFlashTimer;
            attackerFlash[attack.AttackerZone] = ZoneFlashDuration;
            defenderFlash[attack.DefenderZone] = ZoneFlashDuration;
        }
    }

    /// <summary>Curva de avance (0 a 1 a 0) del movimiento de convergencia: ida, sostenida en el choque, vuelta.</summary>
    private static float ClashMoveEnvelope(float t)
    {
        if (t <= 0f || t >= ClashMoveTotal) return 0f;
        if (t < ClashConvergeDuration) return t / ClashConvergeDuration;
        if (t < ClashConvergeDuration + ClashHoldDuration) return 1f;
        return 1f - (t - ClashConvergeDuration - ClashHoldDuration) / ClashReturnDuration;
    }

    /// <summary>Escala del fantasma de un monstruo destruido: 1 durante el choque, se encoge a 0 despues.</summary>
    private static float ClashFadeEnvelope(float t) =>
        t <= ClashMoveTotal ? 1f : MathHelper.Clamp(1f - (t - ClashMoveTotal) / ClashFadeDuration, 0f, 1f);

    /// <summary>Desplazamiento actual del choque para esta Zona (atacante y objetivo convergen; cero para el resto), o el impulso generico si fue un ataque directo.</summary>
    private Vector2 ClashOffset(bool isHuman, int zone)
    {
        if (!_clashActive) return Vector2.Zero;
        bool isAttacker = isHuman == _clashAttackerIsHuman && zone == _clashAttackerZone;
        bool isDefender = _clashDefenderZone >= 0 && isHuman != _clashAttackerIsHuman && zone == _clashDefenderZone;
        if (!isAttacker && !isDefender) return Vector2.Zero;

        float envelope = ClashMoveEnvelope(_clashTimer);
        if (envelope <= 0f) return Vector2.Zero;

        if (_clashDefenderZone < 0)
        {
            var toward = _clashAttackerIsHuman ? new Vector2(0, -1) : new Vector2(0, 1);
            return toward * DirectLungeDistance * envelope;
        }

        var attackerCenter = (_clashAttackerIsHuman ? PlayerZone(_clashAttackerZone) : CpuZone(_clashAttackerZone)).Center.ToVector2();
        var defenderCenter = (_clashAttackerIsHuman ? CpuZone(_clashDefenderZone) : PlayerZone(_clashDefenderZone)).Center.ToVector2();
        var delta = defenderCenter - attackerCenter;

        return isAttacker ? delta * ClashConvergeFraction * envelope : -delta * ClashConvergeFraction * envelope;
    }

    /// <summary>
    /// Si esta Zona esta vacia porque el monstruo que tenia acaba de morir en
    /// el choque de batalla en curso, devuelve su snapshot (carta y Posicion)
    /// para seguir dibujandolo ahi -encogiendose, ver <see cref="ClashFadeEnvelope"/>-
    /// en vez de saltar a vacio de golpe.
    /// </summary>
    private bool TryGetClashGhost(bool isHuman, int zone, out MonsterCard card, out BattlePosition position)
    {
        card = null!;
        position = BattlePosition.Attack;
        if (!_clashActive) return false;

        if (isHuman == _clashAttackerIsHuman && zone == _clashAttackerZone && _clashAttackerDestroyed)
        {
            card = _clashAttackerCard!;
            position = BattlePosition.Attack; // solo un monstruo en Ataque puede atacar
            return true;
        }
        if (_clashDefenderZone >= 0 && isHuman != _clashAttackerIsHuman && zone == _clashDefenderZone
            && _clashDefenderDestroyed && _clashDefenderCard != null)
        {
            card = _clashDefenderCard;
            position = _clashDefenderPosition ?? BattlePosition.Attack;
            return true;
        }
        return false;
    }

    private static Rectangle Offset(Rectangle rect, Vector2 offset) =>
        new(rect.X + (int)offset.X, rect.Y + (int)offset.Y, rect.Width, rect.Height);

    // ------------------------------------------------------------------- Draw

    public override void Draw(GameTime gameTime)
    {
        var sb = Ctx.SpriteBatch;
        var state = _engine.State;
        var human = state.Players[HumanIndex];
        var cpu = state.Players[CpuIndex];
        bool playerTurn = _engine.ActiveIndex == HumanIndex && !_engine.IsOver;

        DrawTopInfo(cpu);
        DrawField(cpu, human, playerTurn);
        DrawHand(human, playerTurn);
        DrawChainPanel(state);
        DrawLog();
        DrawPhaseBanner(state);
        DrawControls(playerTurn, human, cpu);
        DrawPlayerInfo(human);

        if (_engine.ActiveIndex == CpuIndex && !_engine.IsOver)
            Ctx.Font.DrawCentered(sb, "TURNO DE LA CPU...", Ctx.ScreenWidth / 2,
                _centerY + 34, Theme.CpuAccent, 2);
    }

    private void DrawTopInfo(Player cpu)
    {
        var sb = Ctx.SpriteBatch;
        Ctx.Font.Draw(sb, cpu.Name, new Vector2(_fieldStartX, _cpuHudY), Theme.CpuAccent, 2);
        DrawLifeBar(new Rectangle(_fieldStartX, _cpuHudY + 18, 360, 16), _displayedCpuLp, Theme.CpuAccent, _cpuDamageFlashTimer);
        Ctx.Font.Draw(sb, $"MANO {cpu.Hand.Count}  DECK {cpu.Deck.Count}",
            new Vector2(_fieldStartX + 420, _cpuHudY + 4), Theme.TextDim, 1);
    }

    private void DrawPlayerInfo(Player human)
    {
        var sb = Ctx.SpriteBatch;
        Ctx.Font.Draw(sb, human.Name, new Vector2(_fieldStartX, _playerHudY), Theme.PlayerAccent, 2);
        DrawLifeBar(new Rectangle(_fieldStartX, _playerHudY + 18, 360, 16), _displayedHumanLp, Theme.PlayerAccent, _humanDamageFlashTimer);
        Ctx.Font.Draw(sb, $"DECK {human.Deck.Count}",
            new Vector2(_fieldStartX + 420, _playerHudY + 4), Theme.TextDim, 1);
    }

    /// <summary>
    /// Dibuja la barra de vida. <paramref name="lp"/> es el valor mostrado
    /// (que se acerca suavemente al real, ver <see cref="EaseTowards"/>), no
    /// necesariamente el LP exacto del motor en este instante; y
    /// <paramref name="damageFlashTimer"/> > 0 superpone un destello rojo
    /// breve cuando el jugador acaba de perder LP.
    /// </summary>
    private void DrawLifeBar(Rectangle rect, int lp, Color color, float damageFlashTimer)
    {
        var sb = Ctx.SpriteBatch;
        Ctx.Primitives.FillRect(sb, rect, new Color(20, 24, 36));
        float ratio = MathHelper.Clamp(lp / 8000f, 0f, 1f);
        Ctx.Primitives.FillRect(sb, new Rectangle(rect.X, rect.Y, (int)(rect.Width * ratio), rect.Height), color);
        Ctx.Primitives.DrawBorder(sb, rect, 1, Theme.PanelBorder);
        Ctx.Font.Draw(sb, $"LP {lp}", new Vector2(rect.X + 6, rect.Y + 3), Theme.TextPrimary, 1);

        if (damageFlashTimer > 0)
            Ctx.Primitives.FillRectAlpha(sb, rect, Theme.Danger, damageFlashTimer / DamageFlashDuration * 0.55f);
    }

    private void DrawField(Player cpu, Player human, bool playerTurn)
    {
        var sb = Ctx.SpriteBatch;
        bool battle = _engine.Phase == DuelPhase.Battle;
        bool pickingMonsterTarget = playerTurn && _targetMode && _pendingTargetKind == EffectTargetKind.MonsterZone;

        // Zona de Magia/Trampa y Zona de Campo de la CPU: boca abajo muestra
        // el reverso oculto (informacion que el jugador humano no deberia ver).
        for (int z = 0; z < Player.SpellTrapZoneCount; z++)
            Ctx.CardRenderer.DrawSpellTrapZoneCard(sb, cpu.SpellTrapZones[z], CpuSpellTrapZone(z), revealFaceDown: false);
        Ctx.CardRenderer.DrawFieldZone(sb, cpu.FieldZone, CpuFieldZoneRect);

        // Zonas de Monstruos de la CPU.
        for (int z = 0; z < Player.MonsterZoneCount; z++)
        {
            var rect = Offset(CpuZone(z), ClashOffset(isHuman: false, z));
            var inst = cpu.MonsterZones[z];
            bool targetable = inst != null && (playerTurn && battle && _selectedAttacker >= 0 || pickingMonsterTarget);
            if (inst != null)
            {
                if (inst.IsFaceUp)
                    Ctx.CardRenderer.DrawMonster(sb, inst.Card, rect, targetable, inst.Position);
                else
                    Ctx.CardRenderer.DrawFaceDown(sb, rect, targetable);
            }
            else if (TryGetClashGhost(isHuman: false, z, out var ghostCard, out var ghostPosition))
                Ctx.CardRenderer.DrawMonster(sb, ghostCard, rect, false, ghostPosition, ClashFadeEnvelope(_clashTimer));
            else
                Ctx.CardRenderer.DrawEmptyZone(sb, rect);
            if (_cpuZoneFlashTimer[z] > 0)
                Ctx.Primitives.FillRectAlpha(sb, rect, Color.White, _cpuZoneFlashTimer[z] / ZoneFlashDuration * 0.4f);
        }

        // Zonas de Monstruos del jugador.
        for (int z = 0; z < Player.MonsterZoneCount; z++)
        {
            var rect = Offset(PlayerZone(z), ClashOffset(isHuman: true, z));
            var inst = human.MonsterZones[z];
            bool selected = z == _selectedZone || z == _selectedAttacker || _selectedTributes.Contains(z)
                || (pickingMonsterTarget && inst != null);
            if (inst != null)
            {
                if (inst.Position == BattlePosition.DefenseFaceDown)
                    Ctx.CardRenderer.DrawFaceDown(sb, rect, selected);
                else
                    Ctx.CardRenderer.DrawMonster(sb, inst.Card, rect, selected, inst.Position);
            }
            else if (TryGetClashGhost(isHuman: true, z, out var ghostCard, out var ghostPosition))
                Ctx.CardRenderer.DrawMonster(sb, ghostCard, rect, false, ghostPosition, ClashFadeEnvelope(_clashTimer));
            else
                Ctx.CardRenderer.DrawEmptyZone(sb, rect);
            if (_humanZoneFlashTimer[z] > 0)
                Ctx.Primitives.FillRectAlpha(sb, rect, Color.White, _humanZoneFlashTimer[z] / ZoneFlashDuration * 0.4f);
        }

        // Zona de Magia/Trampa y Zona de Campo del jugador: boca abajo revela
        // el nombre real (es su propia carta: siempre sabe que Coloco).
        for (int z = 0; z < Player.SpellTrapZoneCount; z++)
            Ctx.CardRenderer.DrawSpellTrapZoneCard(sb, human.SpellTrapZones[z], PlayerSpellTrapZone(z),
                revealFaceDown: true, highlighted: z == _spellTrapCursor);
        Ctx.CardRenderer.DrawFieldZone(sb, human.FieldZone, PlayerFieldZoneRect);
    }

    private void DrawHand(Player human, bool playerTurn)
    {
        var sb = Ctx.SpriteBatch;
        int count = human.Hand.Count;
        bool mainPhase = _engine.Phase is DuelPhase.Main1 or DuelPhase.Main2;
        bool discarding = playerTurn && _engine.State.PendingDiscardCount > 0;

        for (int i = 0; i < count; i++)
        {
            var rect = HandRect(i, count);
            bool highlight = discarding
                ? _selectedDiscards.Contains(i)
                : playerTurn && mainPhase && (i == _selectedHand
                    || (_fusionMode && i == _fusionFirst)
                    || (_ritualMode && i == _ritualFirst));

            if (human.Hand[i] is MonsterCard monster)
                Ctx.CardRenderer.DrawMonster(sb, monster, rect, highlight);
            else if (human.Hand[i] is SpellCard or TrapCard)
                Ctx.CardRenderer.DrawSpellTrap(sb, human.Hand[i], rect, highlight);
            else
                Ctx.CardRenderer.DrawFaceDown(sb, rect, highlight, defense: false);
        }
    }

    private void DrawLog()
    {
        var sb = Ctx.SpriteBatch;
        var rect = LogRect;
        Ctx.Primitives.Panel(sb, rect, new Color(24, 28, 42), Theme.PanelBorder, 2);
        Ctx.Font.Draw(sb, "REGISTRO", new Vector2(rect.X + 10, rect.Y + 8), Theme.TextDim, 1);

        int lineH = Ctx.Font.LineHeight(1) + 4;
        int maxLines = (rect.Height - 30) / lineH;
        int y = rect.Y + 26;
        foreach (var entry in _engine.Log.Tail(maxLines))
        {
            string line = Truncate(entry.ToUpperInvariant(), rect.Width - 20, 1);
            Ctx.Font.Draw(sb, line, new Vector2(rect.X + 10, y), Theme.TextPrimary, 1);
            y += lineH;
        }
    }

    /// <summary>
    /// Panel persistente con el estado de la Cadena: quien tiene la
    /// Prioridad y la lista de eslabones del ultimo activado (resuelve
    /// primero) al primero, en el mismo orden LIFO en que se resuelven.
    /// Siempre visible (no solo cuando hay una Cadena abierta) para que el
    /// jugador entienda el sistema aunque todavia no lo haya usado.
    /// </summary>
    private void DrawChainPanel(DuelState state)
    {
        var sb = Ctx.SpriteBatch;
        var rect = ChainPanelRect;
        Ctx.Primitives.Panel(sb, rect, new Color(24, 28, 42), Theme.PanelBorder, 2);
        Ctx.Font.Draw(sb, "CADENA", new Vector2(rect.X + 10, rect.Y + 8), Theme.TextDim, 1);

        if (state.Chain.Count == 0)
        {
            Ctx.Font.Draw(sb, "SIN CADENA ACTIVA", new Vector2(rect.X + 10, rect.Y + 26), Theme.TextDim, 1);
            return;
        }

        string turnLabel = state.ChainPendingResponder == PlayerSide.Human
            ? "TU PRIORIDAD: ENCADENA O RESUELVE"
            : "ESPERANDO A LA CPU...";
        Ctx.Font.Draw(sb, turnLabel, new Vector2(rect.X + 10, rect.Y + 26), Theme.Highlight, 1);

        int lineH = Ctx.Font.LineHeight(1) + 6;
        int y = rect.Y + 46;
        int maxLines = Math.Max(0, (rect.Bottom - 10 - y) / lineH);

        // Del ultimo eslabon activado al primero: es el orden real de
        // resolucion (LIFO), no el orden en que se activaron.
        int shown = 0;
        for (int i = state.Chain.Count - 1; i >= 0 && shown < maxLines; i--, shown++)
        {
            var link = state.Chain[i];
            var owner = state.GetPlayer(link.Controller);
            var card = owner.SpellTrapZones[link.ZoneIndex]?.Card;
            var color = link.Controller == PlayerSide.Human ? Theme.PlayerAccent : Theme.CpuAccent;
            string ownerTag = link.Controller == PlayerSide.Human ? "TU" : "CPU";
            string text = $"{i + 1}. [{ownerTag}] {card?.Name ?? "?"}";
            Ctx.Font.Draw(sb, Truncate(text.ToUpperInvariant(), rect.Width - 20, 1), new Vector2(rect.X + 10, y), color, 1);
            y += lineH;
        }
    }

    private void DrawPhaseBanner(DuelState state)
    {
        var sb = Ctx.SpriteBatch;
        int cx = Ctx.ScreenWidth / 2;
        string phase = state.Phase switch
        {
            DuelPhase.Draw => "ROBO",
            DuelPhase.Standby => "PREPARACION",
            DuelPhase.Main1 => "PRINCIPAL 1",
            DuelPhase.Battle => "BATALLA",
            DuelPhase.Main2 => "PRINCIPAL 2",
            DuelPhase.End => "FIN",
            _ => state.Phase.ToString()
        };
        Ctx.Font.DrawCentered(sb, $"TURNO {state.TurnNumber}  -  FASE {phase}", cx, _centerY + 16, Theme.Highlight, 2);
    }

    private void DrawControls(bool playerTurn, Player human, Player cpu)
    {
        var sb = Ctx.SpriteBatch;

        if (playerTurn && _engine.State.PendingDiscardCount > 0)
        {
            DrawDiscardControls();
            return;
        }

        if (playerTurn && _targetMode)
        {
            DrawTargetSelectionControls(human);
            return;
        }

        bool chainOpen = _engine.State.Chain.Count > 0;

        if (chainOpen)
        {
            DrawChainControls();
        }
        else
        {
            // Botones de fase.
            string phaseLabel = _engine.Phase switch
            {
                DuelPhase.Main1 => "IR A BATALLA",
                DuelPhase.Battle => "FIN DE BATALLA",
                _ => "TERMINAR TURNO"
            };
            Widgets.DrawButton(Ctx, PhaseButtonRect, phaseLabel, playerTurn);
            Widgets.DrawButton(Ctx, EndTurnButtonRect, "TERMINAR TURNO", playerTurn);
        }

        // Botones contextuales.
        if (playerTurn && _engine.Phase is DuelPhase.Main1 or DuelPhase.Main2)
        {
            if (_tributeMode && _selectedHand >= 0 && _selectedHand < human.Hand.Count
                && human.Hand[_selectedHand] is MonsterCard tm)
            {
                bool ready = _selectedTributes.Count == tm.RequiredTributes;
                Ctx.Font.Draw(sb, $"SACRIFICIOS: {_selectedTributes.Count}/{tm.RequiredTributes}", StatusPos, Theme.Highlight, 1);
                Widgets.DrawButton(Ctx, ActionRect(0), "CONFIRMAR", ready);
                Widgets.DrawButton(Ctx, ActionRect(2), "CANCELAR");
            }
            else if (_selectedHand >= 0 && _selectedHand < human.Hand.Count
                && human.Hand[_selectedHand] is MonsterCard m)
            {
                if (_fusionMode)
                {
                    Ctx.Font.Draw(sb, "FUSION: ELIGE OTRA CARTA", StatusPos, Theme.Highlight, 1);
                    Widgets.DrawButton(Ctx, ActionRect(2), "CANCELAR");
                }
                else
                {
                    Widgets.DrawButton(Ctx, ActionRect(0), "INVOCAR");
                    Widgets.DrawButton(Ctx, ActionRect(1), "COLOCAR");
                    Widgets.DrawButton(Ctx, ActionRect(2), "FUSIONAR");
                    if (m.RequiredTributes > 0)
                        Ctx.Font.Draw(sb, $"REQUIERE {m.RequiredTributes} SACRIFICIO(S)", StatusPos, Theme.TextDim, 1);
                }
            }
            else if (_selectedHand >= 0 && _selectedHand < human.Hand.Count
                && human.Hand[_selectedHand] is SpellCard { SubType: SpellSubType.Ritual })
            {
                if (_ritualMode)
                {
                    Ctx.Font.Draw(sb, "RITUAL: ELIGE AL MONSTRUO DE RITUAL EN TU MANO", StatusPos, Theme.Highlight, 1);
                    Widgets.DrawButton(Ctx, ActionRect(2), "CANCELAR");
                }
                else
                {
                    Widgets.DrawButton(Ctx, ActionRect(0), "INVOCAR RITUAL");
                }
            }
            else if (_selectedHand >= 0 && _selectedHand < human.Hand.Count
                && human.Hand[_selectedHand] is SpellCard or TrapCard)
            {
                Widgets.DrawButton(Ctx, ActionRect(0), "COLOCAR");
                Widgets.DrawButton(Ctx, ActionRect(1), "ACTIVAR");
            }
            else if (_selectedZone >= 0 && human.MonsterZones[_selectedZone] is { } sz)
            {
                if (sz.Position == BattlePosition.DefenseFaceDown && !sz.SummonedThisTurn)
                    Widgets.DrawButton(Ctx, ActionRect(0), "VOLTEAR");
                else if (sz.IsFaceUp)
                {
                    if (sz.IsDefending) Widgets.DrawButton(Ctx, ActionRect(0), "POS ATAQUE");
                    else Widgets.DrawButton(Ctx, ActionRect(1), "POS DEFENSA");
                }
            }
        }

        // Zona de Magia/Trampa seleccionada: disponible tambien en Battle
        // Phase (Trampas y Magias de Juego Rapido tienen Velocidad de
        // Hechizo 2+), igual que en UpdateSpellTrapZoneInput.
        if (playerTurn && !_tributeMode && !_fusionMode && !_ritualMode && _selectedHand < 0 && _selectedZone < 0
            && _engine.Phase is DuelPhase.Main1 or DuelPhase.Battle or DuelPhase.Main2)
            DrawSelectedSpellTrapZoneControls(human);

        // Boton de ataque directo.
        if (playerTurn && _engine.Phase == DuelPhase.Battle && _selectedAttacker >= 0 && cpu.MonsterCount == 0)
            Widgets.DrawButton(Ctx, DirectAttackRect, "ATAQUE DIRECTO");

        // Mensaje de estado / ayuda.
        if (!string.IsNullOrEmpty(_statusMessage))
            Ctx.Font.Draw(sb, _statusMessage.ToUpperInvariant(), StatusPos2, Theme.Danger, 1);
        else if (playerTurn)
            Ctx.Font.Draw(sb, HelpText(), StatusPos2, Theme.TextDim, 1);
    }

    private void DrawDiscardControls()
    {
        var sb = Ctx.SpriteBatch;
        int required = _engine.State.PendingDiscardCount;
        bool ready = _selectedDiscards.Count == required;

        Ctx.Font.Draw(sb, $"LIMITE DE MANO: ELIGE {_selectedDiscards.Count}/{required} CARTA(S) PARA DESCARTAR", StatusPos, Theme.Danger, 1);
        Widgets.DrawButton(Ctx, DiscardConfirmRect, "DESCARTAR", ready);
    }

    /// <summary>
    /// Boton para pasar la Prioridad mientras la Cadena esta abierta (el
    /// estado detallado de la Cadena ya se muestra en <see cref="DrawChainPanel"/>,
    /// asi que aqui solo va el control). Ocupa el lugar del boton de fase,
    /// inutil mientras la Cadena esta abierta. Solo el jugador con la
    /// Prioridad puede pulsarlo.
    /// </summary>
    private void DrawChainControls()
    {
        bool humanTurn = _engine.State.ChainPendingResponder == PlayerSide.Human;
        Widgets.DrawButton(Ctx, ChainPassRect, "RESOLVER CADENA", humanTurn);
    }

    /// <summary>
    /// Controles para elegir el objetivo de un efecto (ver
    /// <see cref="UpdateTargetSelectionInput"/>): un click en el Campo si el
    /// objetivo es un monstruo, o el mismo par ciclar+confirmar si el
    /// objetivo esta en el Cementerio propio (que todavia no tiene una caja
    /// dedicada en el tablero).
    /// </summary>
    private void DrawTargetSelectionControls(Player human)
    {
        var sb = Ctx.SpriteBatch;

        if (_pendingTargetKind == EffectTargetKind.MonsterZone)
        {
            Ctx.Font.Draw(sb, "ELIGE 1 MONSTRUO EN EL CAMPO COMO OBJETIVO", StatusPos, Theme.Highlight, 1);
        }
        else
        {
            bool hasAny = human.Graveyard.Any(c => c is MonsterCard);
            Widgets.DrawButton(Ctx, GraveyardCycleRect, "SIGUIENTE CEMENTERIO", hasAny);

            string label = "SIN MONSTRUOS EN TU CEMENTERIO";
            bool canConfirm = false;
            if (_graveyardCursor >= 0 && _graveyardCursor < human.Graveyard.Count)
            {
                label = $"[{_graveyardCursor + 1}] {human.Graveyard[_graveyardCursor].Name}";
                canConfirm = true;
            }
            Ctx.Font.Draw(sb, label.ToUpperInvariant(), StatusPos, Theme.TextDim, 1);
            Widgets.DrawButton(Ctx, GraveyardConfirmRect, "CONFIRMAR OBJETIVO", canConfirm);
        }

        Widgets.DrawButton(Ctx, CancelActionRect, "CANCELAR");

        if (!string.IsNullOrEmpty(_statusMessage))
            Ctx.Font.Draw(sb, _statusMessage.ToUpperInvariant(), StatusPos2, Theme.Danger, 1);
    }

    /// <summary>
    /// Boton ACTIVAR para la carta de la Zona de Magia/Trampa seleccionada
    /// con un clic directo en su casilla del tablero (ver
    /// <see cref="UpdateSpellTrapZoneInput"/>).
    /// </summary>
    private void DrawSelectedSpellTrapZoneControls(Player human)
    {
        if (_spellTrapCursor < 0 || _spellTrapCursor >= Player.SpellTrapZoneCount) return;
        var instance = human.SpellTrapZones[_spellTrapCursor];
        if (instance == null) return;

        string faceLabel = instance.FaceUp ? "boca arriba" : "boca abajo";
        Ctx.Font.Draw(Ctx.SpriteBatch, $"[ZONA {_spellTrapCursor + 1}] {instance.Card.Name} ({faceLabel})".ToUpperInvariant(),
            StatusPos, Theme.TextDim, 1);

        if (!instance.FaceUp)
            Widgets.DrawButton(Ctx, ActionRect(1), "ACTIVAR");
    }

    private string HelpText() => _engine.Phase switch
    {
        DuelPhase.Battle => "CLIC EN TU MONSTRUO Y LUEGO EN EL OBJETIVO",
        _ => "CLIC EN UNA CARTA DE TU MANO O EN UNA ZONA PARA JUGARLA"
    };

    // --------------------------------------------------------------- Geometria

    private Rectangle CpuZone(int i) => new(_fieldStartX + i * (CardW + Gap), _cpuMonsterY, CardW, CardH);
    private Rectangle PlayerZone(int i) => new(_fieldStartX + i * (CardW + Gap), _playerMonsterY, CardW, CardH);

    private Rectangle CpuSpellTrapZone(int i) => new(_fieldStartX + i * (CardW + Gap), _cpuFieldRowY, CardW, SmallH);
    private Rectangle PlayerSpellTrapZone(int i) => new(_fieldStartX + i * (CardW + Gap), _playerFieldRowY, CardW, SmallH);

    private int FieldZoneX => _fieldStartX + Player.SpellTrapZoneCount * (CardW + Gap) + FieldGap;
    private Rectangle CpuFieldZoneRect => new(FieldZoneX, _cpuFieldRowY, FieldZoneW, SmallH);
    private Rectangle PlayerFieldZoneRect => new(FieldZoneX, _playerFieldRowY, FieldZoneW, SmallH);

    private Rectangle ChainPanelRect => new(_rightColX, _chainPanelY, _rightColW, _chainPanelH);
    private Rectangle LogRect => new(_rightColX, _logY, _rightColW, _logH);

    private Rectangle HandRect(int i, int count)
    {
        int step = count > 0 ? Math.Min(CardW + 6, _handAreaW / Math.Max(1, count)) : CardW + 6;
        return new Rectangle(_fieldStartX + i * step, _handY, CardW, CardH);
    }

    private Vector2 StatusPos => new(_fieldStartX, _statusY);
    private Vector2 StatusPos2 => new(_fieldStartX, _statusY2);

    private Rectangle ActionRect(int i) => new(_fieldStartX + i * 160, _controlsY, 150, 42);
    private Rectangle DirectAttackRect => new(_fieldStartX, _controlsY, 230, 42);
    private Rectangle PhaseButtonRect => new(Ctx.ScreenWidth - 400, _controlsY, 190, 42);
    private Rectangle EndTurnButtonRect => new(Ctx.ScreenWidth - 200, _controlsY, 160, 42);
    private Rectangle DiscardConfirmRect => PhaseButtonRect;
    // Reutiliza el lugar del boton de fase: mientras la Cadena esta abierta,
    // avanzar de fase no es una accion valida, asi que no hay conflicto.
    private Rectangle ChainPassRect => PhaseButtonRect;

    // Seleccion de objetivo en el Cementerio propio (todavia sin caja en el
    // tablero) y cancelar: se ubican fuera de la fila de ActionRect(0..2)
    // para no chocar con GraveyardConfirmRect, que es mas ancho.
    private Rectangle GraveyardCycleRect => new(_fieldStartX, _controlsY, 260, 42);
    private Rectangle GraveyardConfirmRect => new(_fieldStartX + 270, _controlsY, 240, 42);
    private Rectangle CancelActionRect => new(_fieldStartX + 520, _controlsY, 150, 42);

    private string Truncate(string text, int maxWidth, int scale)
    {
        if (Ctx.Font.Measure(text, scale) <= maxWidth) return text;
        while (text.Length > 1 && Ctx.Font.Measure(text + "...", scale) > maxWidth)
            text = text[..^1];
        return text + "...";
    }
}
