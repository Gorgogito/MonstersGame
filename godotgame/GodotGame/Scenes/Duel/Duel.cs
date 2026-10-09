using Godot;
using GodotGame.Core.AI;
using GodotGame.Core.Battle;
using GodotGame.Core.Effects;
using GodotGame.Core.Effects.Monster;
using GodotGame.Core.Entities;
using GodotGame.Game;
using GodotGame.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GodotGame;

/// <summary>
/// Pantalla de Duelo (equivalente Godot de <c>DuelScreen</c>): el tablero
/// completo, la mano, los controles de fase/turno y todos los modos de
/// seleccion (Sacrificios, Fusion, Ritual, objetivo de efecto, descarte de
/// fin de turno). Traduce clics de <see cref="Button"/> nativos de Godot a la
/// misma API publica de <see cref="DuelEngine"/> que ya usaba MonoGame -- el
/// motor de reglas no cambio en absoluto, solo como se le llega.
///
/// A diferencia del modelo de MonoGame (sondear <c>Update()</c> contra
/// rectangulos dibujados a mano), aca cada boton dispara su propia senal
/// <c>Pressed</c> una vez por clic: no hace falta un bucle de sondeo para
/// detectar clics. <see cref="_Process"/> solo hace lo que de verdad necesita
/// repetirse cada fotograma: avanzar el timer de la IA y refrescar el estado
/// visual de todo el tablero contra <see cref="DuelState"/>.
/// </summary>
public partial class Duel : Control
{
    private const int HumanIndex = 0;
    private const int CpuIndex = 1;
    private const double AiStepDelay = 0.55;
    private const double GameOverDelay = 2.4;

    private DuelEngine _engine = null!;
    private IDuelAI _ai = null!;
    private AudioManager _audio = null!;
    private TextureCache _textures = null!;

    /// <summary>Partes visuales de un boton de carta (arte + nombre + estrellas de Nivel + estadisticas), indexadas por el propio boton para poder refrescarlas sin reconstruir el arbol cada frame.</summary>
    private sealed class CardVisual
    {
        public PanelContainer NameBar = null!;
        public Label Name = null!;
        public TextureRect Attribute = null!;
        public HBoxContainer Stars = null!;
        public PanelContainer ArtFrame = null!;
        public TextureRect Art = null!;
        public PanelContainer StatsBar = null!;
        public RichTextLabel Stats = null!;
        /// <summary>Texto de una Zona vacia ("(vacio)", "Campo"), en lugar de la carta.</summary>
        public Label Placeholder = null!;
        /// <summary>Simbolo de la Estrella Guardiana en uso, en la esquina del arte (solo Monstruos en el Campo cuya estrella se conoce).</summary>
        public Label Guardian = null!;
    }
    private readonly Dictionary<Button, CardVisual> _cardVisuals = new();

    /// <summary>Angulo de abanico "de reposo" de cada boton de zona (estilo Forbidden Memories) -- ver <see cref="BuildZoneArc"/>. Se compone con la rotacion de Defensa en <see cref="RefreshMonsterRow"/> en vez de reemplazarla.</summary>
    private readonly Dictionary<Button, float> _zoneBaseRotation = new();

    private Label _cpuName = null!, _cpuCounts = null!, _phaseBanner = null!, _statusLabel = null!, _playerName = null!;
    private ProgressBar _cpuLp = null!, _playerLp = null!;
    private Label _cpuLpLabel = null!, _playerLpLabel = null!;
    private PanelContainer _cpuPanel = null!, _playerPanel = null!;
    private HBoxContainer _handRow = null!;
    private ItemList _chainList = null!, _logList = null!;

    private Button[] _cpuMonsterButtons = null!;
    private Button[] _cpuSpellTrapButtons = null!;
    private Button[] _playerMonsterButtons = null!;
    private Button[] _playerSpellTrapButtons = null!;
    private Button _cpuFieldButton = null!, _playerFieldButton = null!;

    private Button _phaseButton = null!, _endTurnButton = null!, _chainPassButton = null!, _directAttackButton = null!, _cancelButton = null!;

    /// <summary>"EFECTOS (n)": abre la lista de efectos de Monstruo que puedes activar ahora (de cualquier zona).</summary>
    private Button _effectsButton = null!;

    /// <summary>Ventana modal de decisiones de efectos (Si/No, opciones, elegir cartas) y del menu de efectos.</summary>
    private EffectChoicePanel _choicePanel = null!;
    private bool _effectsMenuOpen;
    private Button _ctx0 = null!, _ctx1 = null!;
    private Action? _ctx0Action, _ctx1Action, _cancelAction, _directAttackAction, _chainPassAction;

    private readonly List<Button> _handButtons = new();

    // Estado de interaccion (mismo modelo que DuelScreen de MonoGame).
    private int _selectedHand = -1;
    private int _selectedZone = -1;
    private int _selectedAttacker = -1;
    private bool _fusionMode;
    private readonly List<int> _fusionSelected = new();
    private bool _ritualMode;
    private int _ritualFirst = -1;
    private string _statusMessage = "";

    private bool _targetMode;
    private bool _targetIsSetCard;
    private int _targetSourceIndex;
    private EffectTargetKind _pendingTargetKind;
    private int _graveyardCursor = -1;

    private bool _tributeMode;
    private bool _tributeIsSet;
    private readonly List<int> _selectedTributes = new();

    private readonly List<int> _selectedDiscards = new();

    private int _spellTrapCursor = -1;

    private double _aiTimer;
    private double _overTimer;
    private bool _overTriggered;
    private bool _overFlashDone;
    private DuelPhase _lastPhase;
    private int _lastActive;

    // ----------------------------------------------------- Mejoras visuales
    // (Epica V: paridad con las ultimas fases de MonoGame, adaptada a nodos
    // nativos de Godot -- Tween en vez de temporizadores/easing a mano,
    // CPUParticles2D en vez del sistema de particulas casero, y el offset de
    // la propia Control raiz en vez de una Camera2D: esta pantalla es toda UI
    // de Control, no hay un "mundo" 2D que filmar con una camara.)
    private AttackInfo? _lastObservedAttack;
    private readonly HashSet<Button> _flashingButtons = new();
    private ColorRect _screenFlash = null!;
    private Control _shakeRoot = null!;
    private Vector2? _shakeBasePosition;
    private int _lastHumanLp = -1;
    private int _lastCpuLp = -1;

    /// <summary>
    /// Ultima carta mostrada en cada boton de zona. Comparandola en cada
    /// refresco contra la que toca mostrar ahora se detectan, sin depender de
    /// eventos del motor, los volteos (misma instancia, otra cara) y las
    /// salidas del Campo (la instancia ya no esta) -- ver <see cref="TrackZoneCard"/>.
    /// </summary>
    private readonly record struct ShownCard(object Instance, bool FaceUp);
    private readonly Dictionary<Button, ShownCard> _shownCards = new();

    /// <summary>Botones a mitad de un volteo, con la cara que deben seguir mostrando hasta que la carta quede "de canto".</summary>
    private readonly Dictionary<Button, bool> _flipHeldFace = new();

    /// <summary>Causa de destruccion informada por el motor para la salida que se va a ver en ese boton en este frame (rotura en vez de disolucion).</summary>
    private readonly Dictionary<Button, DestructionCause> _pendingExitCause = new();

    /// <summary>Ultimo color de marco aplicado a cada boton (ver <see cref="ApplyCardFace"/>), para que la copia que se rompe/disuelve se vea igual.</summary>
    private readonly Dictionary<Button, Color> _frameColors = new();

    /// <summary>
    /// LP "mostrados" de un jugador: persiguen al valor real a velocidad
    /// constante (todo cambio tarda ~<see cref="LpCountDuration"/>, sea de 100
    /// o de 3000), como el contador de Forbidden Memories.
    /// </summary>
    private sealed class LpCounter
    {
        public float Shown = -1f;
        private int _target = -1;
        private float _speed;

        /// <summary>Avanza hacia <paramref name="target"/>; devuelve -1/0/+1 segun este bajando, quieto o subiendo.</summary>
        public int Step(int target, double delta)
        {
            if (Shown < 0f) { Shown = target; _target = target; }
            if (target != _target)
            {
                _target = target;
                _speed = Mathf.Max(Mathf.Abs(target - Shown) / LpCountDuration, 400f);
            }
            if (Mathf.IsEqualApprox(Shown, target)) { Shown = target; return 0; }
            int direction = target < Shown ? -1 : 1;
            Shown = Mathf.MoveToward(Shown, target, _speed * (float)delta);
            return direction;
        }
    }

    private const float LpCountDuration = 0.9f;
    private const double LpTickInterval = 0.05;
    private readonly LpCounter _humanLpCounter = new();
    private readonly LpCounter _cpuLpCounter = new();
    private int _humanLpTrend, _cpuLpTrend;
    private double _lpTickTimer;
    private readonly Dictionary<int, StyleBoxFlat> _lpFillStyles = new();

    /// <summary>Panel de detalle de la barra lateral, el boton bajo el cursor, y lo que cada boton de carta mostraria en el panel.</summary>
    private CardDetailPanel _detailPanel = null!;
    private Button? _hoverButton;
    private readonly Dictionary<Button, CardDetailPanel.Entry> _detailEntries = new();

    /// <summary>Tiempo que lleva un Monstruo propio recien Invocado sin Estrella Guardiana elegida: el selector se abre tras una pausa breve, para dejar aterrizar la animacion de invocacion.</summary>
    private double _starPromptTimer;
    private const double StarPromptDelay = 0.35;

    /// <summary>Banner de fase/turno en pantalla (se reemplaza si llega otro antes de que termine).</summary>
    private Control? _activeBanner;

    /// <summary>
    /// Tablero en perspectiva (Fase 3): las franjas de Zonas viven dentro de su
    /// SubViewport, asi que las posiciones de sus botones estan en el espacio
    /// del tablero PLANO. Efectos que se quedan en el tablero van a
    /// <see cref="PerspectiveFieldView.EffectsLayer"/> (se ven en perspectiva
    /// con el); los que van a la pantalla usan <see cref="PerspectiveFieldView.ProjectToScreen"/>.
    /// </summary>
    private PerspectiveFieldView _fieldView = null!;

    // ---------------------------------------------------------- Fase 4
    private MusicManager _music = null!;
    private OpponentProfile? _opponent;
    /// <summary>Estadisticas del jugador para la calificacion final (ver <see cref="DuelRank"/>).</summary>
    private readonly DuelStats _stats = new();
    /// <summary>Botones de Zona del jugador: una carta nueva en ellos cuenta como "carta usada".</summary>
    private readonly HashSet<Button> _humanZoneButtons = new();
    private PanelContainer _speechBubble = null!;
    private Label _speechText = null!;
    private Tween? _speechTween;
    private bool _cpuLowLpSaid;
    private const int CriticalLifePoints = 2000;

    /// <summary>Escena a pantalla completa en curso (batalla, fusion, ritual -- ver <see cref="DuelOverlay"/>), o null si no hay ninguna. Mientras exista, el tablero queda congelado.</summary>
    private DuelOverlay? _blockingOverlay;

    /// <summary>Capa de terreno (degradado + particulas tematicas) de cada franja de jugador, y el FieldType que muestra.</summary>
    private readonly Dictionary<PanelContainer, (string? FieldId, Control? Layer)> _terrainLayers = new();

    /// <summary>
    /// Falso mientras no haya un <see cref="DuelEngine"/> valido asignado.
    /// <c>ChangeSceneToFile</c> es diferido (recien surte efecto al final del
    /// frame), asi que esta misma escena sigue activa -y su <see cref="_Process"/>
    /// se sigue llamando- un instante despues de pedir la redireccion cuando
    /// no hay duelo pendiente; sin esta bandera, ese frame de mas intentaria
    /// usar <c>_engine</c> sin asignar.
    /// </summary>
    private bool _ready;

    public override void _Ready()
    {
        var root = GetNode<GameRoot>("/root/GameRoot");
        if (root.PendingEngine == null || root.PendingAi == null)
        {
            // No se llego aca desde Seleccion de Mazo (ej. se abrio esta escena
            // directamente en el editor) -- no hay duelo que mostrar. Godot no
            // permite cambiar de escena de forma sincronica desde _Ready() (el
            // arbol todavia esta ocupado agregando este mismo nodo) -- hace
            // falta CallDeferred, igual que ya usa GameRoot para su propia
            // transicion inicial.
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://Scenes/MainMenu/MainMenu.tscn");
            return;
        }
        _engine = root.PendingEngine;
        _ai = root.PendingAi;
        _audio = GetNode<AudioManager>("/root/AudioManager");
        _textures = new TextureCache(ProjectSettings.GlobalizePath("res://Data/Art"));
        root.PendingEngine = null;
        root.PendingAi = null;
        _ready = true;

        _lastPhase = _engine.Phase;
        _lastActive = _engine.ActiveIndex;
        _lastObservedAttack = _engine.State.LastAttack;
        _lastHumanLp = _engine.State.Players[HumanIndex].LifePoints;
        _lastCpuLp = _engine.State.Players[CpuIndex].LifePoints;

        _music = GetNode<MusicManager>("/root/MusicManager");
        _opponent = root.PendingOpponent;

        BuildUi();
        // Las barras de LP escalan contra los LP iniciales de cada uno (un jefe puede empezar con mas).
        _cpuLp.MaxValue = Math.Max(1, _engine.State.Players[CpuIndex].LifePoints);
        _playerLp.MaxValue = Math.Max(1, _engine.State.Players[HumanIndex].LifePoints);
        BuildOpponentCorner();
        RefreshAll();
        _music.Play("duel");

        // El layout de contenedores recien se resuelve despues del primer
        // frame: el banner necesita la posicion real del tablero.
        GetTree().CreateTimer(0.15).Timeout += ShowTurnBanner;
    }

    private void BuildUi()
    {
        _shakeRoot = GetNode<Control>("Root");
        _fieldView = GetNode<PerspectiveFieldView>("Root/Board/FieldView");

        _cpuName = GetNode<Label>("Root/Board/CpuInfo/CpuName");
        _cpuLp = GetNode<ProgressBar>("Root/Board/CpuInfo/CpuLp");
        _cpuLpLabel = BuildLpOverlayLabel(_cpuLp);
        _cpuCounts = GetNode<Label>("Root/Board/CpuInfo/CpuCounts");
        _playerName = GetNode<Label>("Root/Board/PlayerInfo/PlayerName");
        _playerLp = GetNode<ProgressBar>("Root/Board/PlayerInfo/PlayerLp");
        _playerLpLabel = BuildLpOverlayLabel(_playerLp);
        _phaseBanner = GetNode<Label>("Root/Board/FieldView/FieldViewport/FieldRoot/PhaseBanner");
        _statusLabel = GetNode<Label>("Root/Board/StatusLabel");
        _handRow = GetNode<HBoxContainer>("Root/Board/HandScroll/HandRow");
        _chainList = GetNode<ItemList>("Root/Sidebar/ChainList");

        var sidebar = GetNode<VBoxContainer>("Root/Sidebar");
        _detailPanel = new CardDetailPanel();
        _detailPanel.Setup(_textures);
        sidebar.AddChild(_detailPanel);
        sidebar.MoveChild(_detailPanel, 0);
        _logList = GetNode<ItemList>("Root/Sidebar/LogList");

        _cpuPanel = GetNode<PanelContainer>("Root/Board/FieldView/FieldViewport/FieldRoot/CpuPanel");
        _playerPanel = GetNode<PanelContainer>("Root/Board/FieldView/FieldViewport/FieldRoot/PlayerPanel");

        var cpuSpellTrapArc = GetNode<Control>("Root/Board/FieldView/FieldViewport/FieldRoot/CpuPanel/CpuFieldArea/CpuSpellTrapArc");
        _cpuSpellTrapButtons = BuildZoneArc(cpuSpellTrapArc, totalSlots: 6, startIndex: 0, buildCount: 5, ZoneButtonSize,
            i => OnSpellTrapZoneClicked(isHuman: false, i));
        _cpuFieldButton = BuildZoneArc(cpuSpellTrapArc, totalSlots: 6, startIndex: 5, buildCount: 1, ZoneButtonSize, null)[0];
        _cpuMonsterButtons = BuildZoneArc(GetNode<Control>("Root/Board/FieldView/FieldViewport/FieldRoot/CpuPanel/CpuFieldArea/CpuMonsterArc"), totalSlots: 5, startIndex: 0, buildCount: 5, ZoneButtonSize,
            i => OnMonsterZoneClicked(isHuman: false, i));

        _playerMonsterButtons = BuildZoneArc(GetNode<Control>("Root/Board/FieldView/FieldViewport/FieldRoot/PlayerPanel/PlayerFieldArea/PlayerMonsterArc"), totalSlots: 5, startIndex: 0, buildCount: 5, ZoneButtonSize,
            i => OnMonsterZoneClicked(isHuman: true, i));
        var playerSpellTrapArc = GetNode<Control>("Root/Board/FieldView/FieldViewport/FieldRoot/PlayerPanel/PlayerFieldArea/PlayerSpellTrapArc");
        _playerSpellTrapButtons = BuildZoneArc(playerSpellTrapArc, totalSlots: 6, startIndex: 0, buildCount: 5, ZoneButtonSize,
            i => OnSpellTrapZoneClicked(isHuman: true, i));
        _playerFieldButton = BuildZoneArc(playerSpellTrapArc, totalSlots: 6, startIndex: 5, buildCount: 1, ZoneButtonSize, null)[0];

        foreach (var b in _playerMonsterButtons.Concat(_playerSpellTrapButtons).Append(_playerFieldButton))
            _humanZoneButtons.Add(b);

        var actionsRow = GetNode<HBoxContainer>("Root/Board/ActionsRow");
        _phaseButton = NewActionButton(actionsRow, "SIGUIENTE FASE (ESPACIO)", () => { Apply(_engine.AdvancePhase()); ClearSelections(); });
        _endTurnButton = NewActionButton(actionsRow, "TERMINAR TURNO (ENTER)", () => { Apply(_engine.EndTurn()); ClearSelections(); });
        _ctx0 = NewActionButton(actionsRow, "", () => _ctx0Action?.Invoke());
        _ctx1 = NewActionButton(actionsRow, "", () => _ctx1Action?.Invoke());
        _directAttackButton = NewActionButton(actionsRow, "ATAQUE DIRECTO", () => _directAttackAction?.Invoke());
        _chainPassButton = NewActionButton(actionsRow, "PASAR / RESOLVER CADENA", () => _chainPassAction?.Invoke());
        _effectsButton = NewActionButton(actionsRow, "EFECTOS", OpenEffectsMenu);
        _cancelButton = NewActionButton(actionsRow, "CANCELAR (ESC)", () => _cancelAction?.Invoke());

        // Overlay de flash de pantalla (victoria/derrota): un ColorRect a
        // pantalla completa, agregado despues de "Root" para quedar dibujado
        // encima de todo el tablero. Empieza transparente e invisible.
        _screenFlash = new ColorRect
        {
            Color = new Color(1f, 1f, 1f, 0f),
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
            AnchorRight = 1f,
            AnchorBottom = 1f
        };
        AddChild(_screenFlash);

        _choicePanel = new EffectChoicePanel();
        _choicePanel.Setup(_textures, card =>
        {
            if (card != null) _detailPanel.ShowEntry(new CardDetailPanel.Entry(card, null, null, "Opcion a elegir"));
        });
        AddChild(_choicePanel);
    }

    /// <summary>Etiqueta "LP actual" superpuesta a una <see cref="ProgressBar"/> de vida: <c>ShowPercentage</c> solo sabe mostrar un porcentaje, nunca el valor real, asi que hace falta un Label propio encima.</summary>
    private static Label BuildLpOverlayLabel(ProgressBar bar)
    {
        var label = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AnchorRight = 1,
            AnchorBottom = 1
        };
        // El relleno de la barra es claro: fuerza texto oscuro para que siga
        // siendo legible sin depender del tema activo.
        label.AddThemeColorOverride("font_color", Colors.Black);
        label.AddThemeColorOverride("font_shadow_color", new Color(1, 1, 1, 0.6f));
        label.AddThemeConstantOverride("shadow_offset_x", 0);
        label.AddThemeConstantOverride("shadow_offset_y", 0);
        label.AddThemeConstantOverride("shadow_outline_size", 2);
        bar.AddChild(label);
        return label;
    }

    private static readonly Vector2 ZoneButtonSize = new(118, 112);
    private static readonly Vector2 HandButtonSize = new(128, 122);

    /// <summary>Ancho fijo del area de cada fila de Zonas (ver <c>custom_minimum_size</c> en Duel.tscn) -- se usa como constante conocida en vez de leer <c>area.Size</c> en vivo, que todavia no esta resuelto por el layout en el momento en que <see cref="BuildUi"/> corre.</summary>
    private const float BoardWidth = 760f;
    private const float ArcMaxAngleDeg = 9f;
    private const float ArcEdgeDrop = 12f;

    /// <summary>
    /// Construye <paramref name="buildCount"/> botones de carta distribuidos
    /// en un abanico/arco leve dentro de una fila de <paramref name="totalSlots"/>
    /// espacios (estilo Forbidden Memories: las Zonas no son una grilla
    /// plana, se curvan levemente y cada una mira un poco hacia el centro),
    /// empezando en el indice <paramref name="startIndex"/> del arco completo
    /// -- asi las 5 Zonas de Magia/Trampa y la de Campo (llamado aparte con
    /// <paramref name="startIndex"/> = 5) comparten el mismo arco de 6 y
    /// quedan alineadas, aunque se construyan en llamadas separadas.
    /// </summary>
    private Button[] BuildZoneArc(Control area, int totalSlots, int startIndex, int buildCount, Vector2 slotSize, Action<int>? onClick)
    {
        var buttons = new Button[buildCount];
        float slotWidth = BoardWidth / totalSlots;

        for (int k = 0; k < buildCount; k++)
        {
            int i = startIndex + k;
            int captured = k;
            var button = BuildCardButton(area, slotSize, onClick != null ? () => onClick(captured) : () => { });

            float t = totalSlots > 1 ? i / (float)(totalSlots - 1) * 2f - 1f : 0f;
            float angle = ArcMaxAngleDeg * t;
            float yOffset = ArcEdgeDrop * (t * t);
            float xCenter = slotWidth * (i + 0.5f);

            button.Position = new Vector2(xCenter - slotSize.X / 2f, yOffset);
            button.Size = slotSize;
            button.PivotOffset = slotSize / 2f;
            button.RotationDegrees = angle;

            _zoneBaseRotation[button] = angle;
            buttons[k] = button;
        }
        return buttons;
    }

    /// <summary>
    /// Construye un boton de carta con arte real + nombre + estadisticas,
    /// apilados verticalmente (nombre arriba, arte al medio ocupando la mayor
    /// parte del espacio, estadisticas abajo) -- la misma orientacion que una
    /// carta real, a diferencia del layout horizontal (icono chico + texto al
    /// costado) de la primera version. Todos los hijos decorativos llevan
    /// <c>MouseFilter = Ignore</c> a proposito -- en Godot el input de mouse se
    /// resuelve de hijo hacia padre, asi que sin esto consumirian el clic
    /// antes de que le llegue al propio <see cref="Button"/>.
    /// </summary>
    private Button BuildCardButton(Control container, Vector2 minSize, Action onClick)
    {
        var button = new Button { CustomMinimumSize = minSize, ClipText = true };
        button.Pressed += onClick;
        button.MouseEntered += () => _hoverButton = button;
        button.MouseExited += () => { if (_hoverButton == button) _hoverButton = null; };

        var content = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 4,
            OffsetTop = 4,
            OffsetRight = -4,
            OffsetBottom = -4
        };
        content.AddThemeConstantOverride("separation", 1);

        // Barra de nombre (nombre + insignia de Atributo), como la carta real.
        var nameBar = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, 17) };
        var nameRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        nameRow.AddThemeConstantOverride("separation", 1);
        var nameLabel = new Label
        {
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 10);
        var attribute = new TextureRect
        {
            CustomMinimumSize = new Vector2(13, 13),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore
        };
        nameRow.AddChild(nameLabel);
        nameRow.AddChild(attribute);
        nameBar.AddChild(nameRow);

        // Estrellas de Nivel alineadas a la derecha, como en la carta real.
        var starsRow = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.End,
            CustomMinimumSize = new Vector2(0, 9)
        };
        starsRow.AddThemeConstantOverride("separation", 1);

        var artFrame = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        var art = new TextureRect
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        artFrame.AddChild(art);
        // El PanelContainer apila a sus hijos en el mismo rect: el simbolo
        // queda encima del arte, alineado arriba a la izquierda.
        var guardian = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Visible = false
        };
        artFrame.AddChild(guardian);

        // Franja de pergamino con ATK/DEF (o el tipo de Magia/Trampa). Es un
        // RichTextLabel para poder colorear cada valor por separado (BBCode).
        var statsBar = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        var statsLabel = new RichTextLabel
        {
            MouseFilter = MouseFilterEnum.Ignore,
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.Off,
            CustomMinimumSize = new Vector2(0, 13),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        statsLabel.AddThemeFontOverride("normal_font", CardFrames.SerifBold);
        statsLabel.AddThemeFontSizeOverride("normal_font_size", 9);
        statsLabel.AddThemeColorOverride("default_color", new Color(CardFrames.InkHex));
        statsLabel.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        statsBar.AddChild(statsLabel);

        content.AddChild(nameBar);
        content.AddChild(starsRow);
        content.AddChild(artFrame);
        content.AddChild(statsBar);
        button.AddChild(content);

        var placeholder = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AnchorRight = 1,
            AnchorBottom = 1
        };
        placeholder.AddThemeFontSizeOverride("font_size", 11);
        placeholder.AddThemeColorOverride("font_color", new Color(0.55f, 0.57f, 0.62f));
        button.AddChild(placeholder);

        container.AddChild(button);

        _cardVisuals[button] = new CardVisual
        {
            NameBar = nameBar,
            Name = nameLabel,
            Attribute = attribute,
            Stars = starsRow,
            ArtFrame = artFrame,
            Art = art,
            StatsBar = statsBar,
            Stats = statsLabel,
            Placeholder = placeholder,
            Guardian = guardian
        };
        return button;
    }

    private static Button NewActionButton(Container container, string text, Action onClick)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 40) };
        button.Pressed += onClick;
        container.AddChild(button);
        return button;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_ready) return;
        if (@event.IsActionPressed("ui_cancel") && _engine.State.PendingDiscardCount == 0)
            ClearSelections();
    }

    // -------------------------------------------------------------- Proceso

    public override void _Process(double delta)
    {
        if (!_ready) return;
        // Una escena a pantalla completa (batalla, fusion, ritual) congela el
        // tablero: sin refresco, sin IA y sin drenar eventos/dano, que se
        // muestran recien al cerrarse (asi la carta destruida, el monstruo
        // fusionado o la barra de LP no se adelantan a la animacion).
        DetectAttackClash();
        if (_blockingOverlay != null) return;
        ProcessDuelEvents();
        if (_blockingOverlay != null) return;
        DetectDamage();
        UpdateLpCounters(delta);

        if (_engine.IsOver)
        {
            if (!_overTriggered)
            {
                if (!_overFlashDone)
                {
                    _overFlashDone = true;
                    bool humanWon = _engine.State.Winner == PlayerSide.Human;
                    FlashScreen(humanWon ? Colors.White : new Color(0.85f, 0.15f, 0.1f), 0.9f);
                    _audio.PlaySfx(humanWon ? "win" : "lose");
                    _music.Play(humanWon ? "victory" : "defeat");
                    if (_opponent != null) Say(humanWon ? _opponent.OnLose : _opponent.OnWin, 3.0);
                    _stats.Turns = _engine.State.TurnNumber;
                    _stats.RemainingLifePoints = _engine.State.Players[HumanIndex].LifePoints;
                }
                _overTimer += delta;
                if (_overTimer >= GameOverDelay)
                {
                    _overTriggered = true;
                    var root = GetNode<GameRoot>("/root/GameRoot");
                    root.PendingWinner = _engine.State.Winner;
                    root.LastDuelStats = _stats;
                    UiKit.GoTo(this, "res://Scenes/Result/Result.tscn");
                }
            }
            RefreshAll();
            return;
        }

        if (_engine.Phase != _lastPhase || _engine.ActiveIndex != _lastActive)
        {
            ClearSelections();
            bool turnChanged = _engine.ActiveIndex != _lastActive;
            _lastPhase = _engine.Phase;
            _lastActive = _engine.ActiveIndex;
            if (turnChanged) ShowTurnBanner();
            else ShowPhaseBannerFor(_engine.Phase);
        }

        if (CheckGuardianStarPrompt(delta))
        {
            RefreshAll();
            return;
        }

        // Una decision de un efecto que te toca a ti: el duelo espera tu respuesta.
        if (RefreshChoicePanel())
        {
            RefreshAll();
            return;
        }

        bool cpuTurnOrChainTurn = _engine.State.PendingChoice is { } pendingChoice
            ? pendingChoice.Chooser == PlayerSide.Cpu
            : _engine.State.Chain.Count > 0
                ? _engine.State.ChainPendingResponder == PlayerSide.Cpu
                : _engine.ActiveIndex == CpuIndex;

        if (cpuTurnOrChainTurn)
        {
            ClearSelections();
            _aiTimer += delta;
            if (_aiTimer >= AiStepDelay)
            {
                _aiTimer = 0;
                _ai.Step(_engine, CpuIndex);
            }
        }

        RefreshAll();
    }

    // ------------------------------------------------------------ Refrescar

    // -------------------------------------------------- Marco estilo carta
    // (paridad visual con Forbidden Memories): esquinas redondeadas + borde
    // de color segun Atributo/Magia/Trampa en vez del boton plano generico
    // de Godot. Se recalcula en cada refresh porque el color depende de que
    // carta ocupa el boton ahora mismo.

    private enum CardFace { Empty, FaceDown, FaceUp }

    private static void ShowGuardianBadge(CardVisual visual, GuardianStar star)
    {
        StarGlyphs.Style(visual.Guardian, star, 15);
        visual.Guardian.Visible = true;
    }

    private static readonly Color BoostGlow = new(0.45f, 1f, 0.55f, 0.85f);
    private static readonly Color WeakenGlow = new(1f, 0.35f, 0.3f, 0.85f);

    /// <summary>
    /// Marco clasico de carta (ver <see cref="CardFrames"/>): el color dice el
    /// tipo de carta, con barra de nombre, arte enmarcado y franja de
    /// pergamino. Boca abajo, el reverso ocupa toda la carta; vacia, la Zona
    /// es un hueco oscuro con su texto. El texto de cada parte lo pone quien
    /// llama -- aca solo se decide que se ve y con que estilo.
    /// </summary>
    private void ApplyCardFace(Button button, Card? card, CardFace face, Color? glow = null)
    {
        var visual = _cardVisuals[button];
        bool faceUp = face == CardFace.FaceUp && card != null;
        var frame = face switch
        {
            CardFace.Empty => CardFrames.Empty,
            CardFace.FaceDown => CardFrames.FaceDown,
            _ => card != null ? CardFrames.FrameColor(card) : CardFrames.Empty
        };
        _frameColors[button] = frame;

        var body = face == CardFace.Empty ? CardFrames.EmptySlot()
            : glow is { } glowColor ? CardFrames.GlowBody(frame, glowColor)
            : CardFrames.Body(frame);
        foreach (string state in new[] { "normal", "hover", "pressed", "disabled" })
            button.AddThemeStyleboxOverride(state, body);
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

        visual.NameBar.Visible = faceUp;
        visual.Stars.Visible = faceUp;
        visual.StatsBar.Visible = faceUp;
        visual.ArtFrame.Visible = face != CardFace.Empty;
        visual.Placeholder.Visible = face == CardFace.Empty;
        visual.Guardian.Visible = false;
        visual.ArtFrame.AddThemeStyleboxOverride("panel", CardFrames.ArtFrame(frame));

        if (!faceUp) return;
        visual.NameBar.AddThemeStyleboxOverride("panel", CardFrames.NameBar(frame));
        visual.StatsBar.AddThemeStyleboxOverride("panel", CardFrames.ParchmentBox());
        CardFrames.StyleName(visual.Name, card!);
        visual.Name.Text = card!.Name;
        visual.Attribute.Texture = card is MonsterCard monster ? _textures.AttributeIcon(monster.Attribute) : null;
        visual.Attribute.Visible = card is MonsterCard;
    }

    /// <summary>Puebla la fila de estrellas de Nivel (una por punto), o la limpia si <paramref name="level"/> es 0 -- equivalente Godot de <c>CardRenderer.DrawLevelStars</c> de MonoGame.</summary>
    private void RefreshLevelStars(HBoxContainer starsRow, int level)
    {
        foreach (var child in starsRow.GetChildren().ToList())
            child.QueueFree();
        if (level <= 0) return;

        var starTexture = _textures.LevelStar();
        for (int i = 0; i < level; i++)
        {
            Control star = starTexture != null
                ? new TextureRect
                {
                    Texture = starTexture,
                    CustomMinimumSize = new Vector2(8, 8),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
                }
                // Sin arte de estrella disponible: un punto dorado como respaldo, para que el Nivel nunca quede invisible.
                : new ColorRect { Color = new Color(1f, 0.85f, 0.3f), CustomMinimumSize = new Vector2(6, 6) };
            star.MouseFilter = MouseFilterEnum.Ignore;
            starsRow.AddChild(star);
        }
    }

    /// <summary>Color BBCode para un ATK/DEF: el de siempre si coincide con el valor impreso, o verde/rojo si un modificador (Equip, Campo) lo subio/bajo -- mismo criterio que <c>Theme.ModifiedStatColor</c> de MonoGame.</summary>
    private static string StatColorHex(int effective, int printed, string baseColorHex) =>
        effective > printed ? "#7cffa0" : effective < printed ? "#ff7c7c" : baseColorHex;

    /// <summary>
    /// Franja de tablero detras de las Zonas de un jugador: un lavado de
    /// color pulsante del <c>FieldType</c> activo (si tiene una Magia de
    /// Campo), o un panel neutro si no -- equivalente Godot de
    /// <c>CardRenderer.DrawFieldTheme</c> de MonoGame (sin imagen de fondo
    /// propia todavia, ya que el catalogo no trae ninguna).
    /// </summary>
    private void RefreshFieldTheme(PanelContainer panel, Player owner)
    {
        var fieldType = (owner.FieldZone?.Card as SpellCard)?.FieldType;
        var style = new StyleBoxFlat
        {
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10
        };

        if (fieldType != null)
        {
            var color = new Color(fieldType.Color);
            float pulse = 0.22f + 0.06f * Mathf.Sin((float)(Time.GetTicksMsec() / 1000.0) * 1.3f);
            style.BgColor = new Color(color.R, color.G, color.B, pulse);
            style.BorderColor = new Color(color.R, color.G, color.B, 0.55f);
            style.BorderWidthLeft = style.BorderWidthRight = style.BorderWidthTop = style.BorderWidthBottom = 2;
        }
        else
        {
            style.BgColor = new Color(0.1f, 0.11f, 0.14f, 0.5f);
        }

        panel.AddThemeStyleboxOverride("panel", style);
        RefreshTerrainLayer(panel, fieldType);
    }

    /// <summary>
    /// Fondo tematico del terreno activo detras de las Zonas: un degradado del
    /// color del FieldType y particulas segun el elemento (brasas que suben,
    /// burbujas, rafagas de viento, polvo, destellos, bruma). Se reconstruye
    /// solo cuando cambia el terreno, con un fundido.
    /// </summary>
    private void RefreshTerrainLayer(PanelContainer panel, FieldType? fieldType)
    {
        _terrainLayers.TryGetValue(panel, out var current);
        if (current.FieldId == fieldType?.Id) return;

        if (current.Layer is { } old && IsInstanceValid(old))
        {
            var fadeOut = old.CreateTween();
            fadeOut.TweenProperty(old, "modulate:a", 0f, 0.5);
            fadeOut.TweenCallback(Callable.From(old.QueueFree));
        }

        Control? layer = null;
        if (fieldType != null && panel.Size.X > 0)
        {
            layer = TerrainFx.BuildLayer(fieldType, panel.Size);
            panel.AddChild(layer);
            panel.MoveChild(layer, 0); // detras de las Zonas
            layer.Modulate = new Color(1, 1, 1, 0);
            layer.CreateTween().TweenProperty(layer, "modulate:a", 1f, 0.6);
        }
        _terrainLayers[panel] = (fieldType?.Id, layer);
    }

    private void RefreshAll()
    {
        var state = _engine.State;
        var human = state.Players[HumanIndex];
        var cpu = state.Players[CpuIndex];
        bool playerTurn = _engine.ActiveIndex == HumanIndex && !_engine.IsOver;

        _cpuName.Text = cpu.Name;
        RefreshLpBar(_cpuLp, _cpuLpLabel, _cpuLpCounter, _cpuLpTrend);
        _cpuCounts.Text = $"Mano {cpu.Hand.Count}  Mazo {cpu.Deck.Count}  Cem {cpu.Graveyard.Count}  Dest {cpu.Banished.Count}";
        _playerName.Text = $"{human.Name}   ·   Mazo {human.Deck.Count}  Cem {human.Graveyard.Count}  Dest {human.Banished.Count}";
        RefreshLpBar(_playerLp, _playerLpLabel, _humanLpCounter, _humanLpTrend);

        string turnLabel = _engine.IsOver ? "DUELO TERMINADO" : playerTurn ? "TU TURNO" : "TURNO DE LA CPU";
        _phaseBanner.Text = $"Turno {state.TurnNumber} -- {PhaseLabel(state.Phase)} -- {turnLabel}";

        RefreshMonsterRow(_cpuMonsterButtons, cpu, human, isHuman: false);
        RefreshMonsterRow(_playerMonsterButtons, human, human, isHuman: true);
        RefreshSpellTrapRow(_cpuSpellTrapButtons, cpu, revealFaceDown: false);
        RefreshSpellTrapRow(_playerSpellTrapButtons, human, revealFaceDown: true);
        RefreshField(_cpuFieldButton, cpu);
        RefreshField(_playerFieldButton, human);
        _pendingExitCause.Clear();
        RefreshFieldTheme(_cpuPanel, cpu);
        RefreshFieldTheme(_playerPanel, human);

        RebuildHandRow(human, state.PendingDiscardCount > 0);
        RefreshChainAndLog(state);
        RefreshActionButtons(state, human, cpu, playerTurn);
        RefreshDetailPanel();

        _statusLabel.Text = _statusMessage;
    }

    private void RefreshMonsterRow(Button[] buttons, Player owner, Player human, bool isHuman)
    {
        for (int z = 0; z < buttons.Length; z++)
        {
            var inst = owner.MonsterZones[z];
            var button = buttons[z];
            var visual = _cardVisuals[button];
            bool faceUp = TrackZoneCard(button, inst, inst?.IsFaceUp ?? false);
            if (inst == null)
            {
                _detailEntries.Remove(button);
                visual.Art.Texture = null;
                visual.Placeholder.Text = "(vacio)";
                button.Disabled = false;
                ApplyCardFace(button, null, CardFace.Empty);
            }
            else if (!faceUp)
            {
                visual.Art.Texture = _textures.CardArt("CardBack.jpg");
                // Tus propias cartas colocadas si las conoces; las del rival no.
                _detailEntries[button] = new CardDetailPanel.Entry(isHuman ? inst.Card : null, null, null,
                    isHuman ? "Colocado boca abajo" : "Monstruo boca abajo", isHuman ? inst.GuardianStar : null);
                ApplyCardFace(button, null, CardFace.FaceDown);
                // Tu propia estrella la conoces aunque este boca abajo; la del rival no.
                if (isHuman) ShowGuardianBadge(visual, inst.GuardianStar);
            }
            else
            {
                int atk = EffectiveStats.EffectiveAttack(inst, _engine.State, owner);
                int def = EffectiveStats.EffectiveDefense(inst, _engine.State, owner);
                _detailEntries[button] = new CardDetailPanel.Entry(inst.Card, atk, def,
                    inst.IsDefending ? "Posicion de Defensa" : "Posicion de Ataque", inst.GuardianStar);
                visual.Art.Texture = _textures.CardArt(inst.Card.Image);
                // Verde/rojo si un modificador (Equipo, Campo) cambio el valor
                // impreso; subrayado el valor en uso segun la posicion.
                visual.Stats.Text = CardFrames.StatsBbcode(atk, inst.Card.Attack, def, inst.Card.Defense, inst.IsDefending);
                RefreshLevelStars(visual.Stars, inst.Card.Level);
                // Aura verde/roja si un terreno o Equipo le cambio el ATK/DEF.
                int delta = (atk - inst.Card.Attack) + (def - inst.Card.Defense);
                Color? glow = delta > 0 ? BoostGlow : delta < 0 ? WeakenGlow : null;
                ApplyCardFace(button, inst.Card, CardFace.FaceUp, glow);
                ShowGuardianBadge(visual, inst.GuardianStar);
            }

            // Estilo Forbidden Memories: un monstruo en Posicion de Defensa se
            // ve rotado de costado (Colocado siempre esta en Defensa por
            // reglas, sin importar si ya se revelo), compuesto sobre el
            // angulo de abanico "de reposo" del arco (ver BuildZoneArc) en
            // vez de reemplazarlo -- asi el tablero mantiene su curva aunque
            // haya un monstruo en Defensa. Rotacion pura -- no toca Scale,
            // que sigue siendo del resorte exclusivo de los Tween de
            // pop/pulso/flash de la Epica V, asi que ninguno de los dos pisa
            // al otro.
            bool rotated = inst != null && (!faceUp || inst.IsDefending);
            float baseAngle = _zoneBaseRotation.TryGetValue(button, out var angle) ? angle : 0f;
            button.PivotOffset = button.Size / 2f;
            button.RotationDegrees = rotated ? baseAngle + 90f : baseAngle;

            bool selected = isHuman && (z == _selectedZone || z == _selectedAttacker || _selectedTributes.Contains(z));
            if (!_flashingButtons.Contains(button))
                button.Modulate = selected ? new Color(1f, 0.85f, 0.4f) : Colors.White;
        }
    }

    private void RefreshSpellTrapRow(Button[] buttons, Player owner, bool revealFaceDown)
    {
        for (int z = 0; z < buttons.Length; z++)
        {
            var instance = owner.SpellTrapZones[z];
            var button = buttons[z];
            var visual = _cardVisuals[button];
            bool faceUp = TrackZoneCard(button, instance, instance?.FaceUp ?? false);
            if (instance == null)
            {
                _detailEntries.Remove(button);
                visual.Art.Texture = null;
                visual.Placeholder.Text = "(vacio)";
                ApplyCardFace(button, null, CardFace.Empty);
            }
            else if (!faceUp && !revealFaceDown)
            {
                visual.Art.Texture = _textures.CardArt("CardBack.jpg");
                _detailEntries[button] = new CardDetailPanel.Entry(null, null, null, "Carta boca abajo");
                ApplyCardFace(button, null, CardFace.FaceDown);
            }
            else
            {
                _detailEntries[button] = new CardDetailPanel.Entry(instance.Card, null, null, faceUp ? "Activa" : "Colocada boca abajo");
                visual.Art.Texture = _textures.CardArt(instance.Card.Image);
                string kind = instance.Card.Kind == CardKind.Spell ? "MAGIA" : "TRAMPA";
                visual.Stats.Text = faceUp ? $"[{kind}] ACTIVA" : $"[{kind}] Colocada";
                RefreshLevelStars(visual.Stars, 0);
                ApplyCardFace(button, instance.Card, CardFace.FaceUp);
            }

            bool selected = revealFaceDown && z == _spellTrapCursor;
            if (!_flashingButtons.Contains(button))
                button.Modulate = selected ? new Color(1f, 0.85f, 0.4f) : Colors.White;
        }
    }

    private void RefreshField(Button button, Player owner)
    {
        var field = owner.FieldZone;
        var visual = _cardVisuals[button];
        TrackZoneCard(button, field, faceUp: true);
        if (field == null) _detailEntries.Remove(button);
        else _detailEntries[button] = new CardDetailPanel.Entry(field.Card, null, null, "Magia de Campo");
        if (field == null)
        {
            visual.Art.Texture = null;
            visual.Placeholder.Text = "Campo";
            ApplyCardFace(button, null, CardFace.Empty);
        }
        else
        {
            visual.Art.Texture = _textures.CardArt(field.Card.Image);
            visual.Stats.Text = "[MAGIA DE CAMPO]";
            RefreshLevelStars(visual.Stars, 0);
            ApplyCardFace(button, field.Card, CardFace.FaceUp);
        }

        // Terreno elemental: tine el boton de Campo con el color de catalogo
        // del FieldType activo (identifica de un vistazo que Campo domina,
        // sin depender solo del texto).
        if (_flashingButtons.Contains(button)) return;
        button.Modulate = field?.Card is SpellCard { FieldType: { } fieldType } ? new Color(fieldType.Color) : Colors.White;
    }

    /// <summary>
    /// Cuenta de cartas en mano observada la ultima vez que se reconstruyeron
    /// los botones -- ver la nota en <see cref="RebuildHandRow"/>.
    /// </summary>
    private int _lastHandCount = -1;

    /// <summary>
    /// Reconstruye la fila de mano SOLO cuando cambia la cantidad de cartas
    /// (robo, jugar, descartar) -- el resto de los frames reutiliza los
    /// mismos botones y solo refresca su arte/texto/seleccion, igual que
    /// <see cref="RefreshMonsterRow"/>. Reconstruir los 5 botones en CADA
    /// frame (como hacia antes) rompia el clic: <c>RefreshAll()</c> corre en
    /// <see cref="_Process"/>, asi que el boton de presion y el de soltada de
    /// un mismo clic casi nunca caian sobre la misma instancia -- Godot nunca
    /// llegaba a disparar <c>Pressed</c>.
    /// </summary>
    private void RebuildHandRow(Player human, bool discarding)
    {
        if (human.Hand.Count != _lastHandCount)
        {
            foreach (var child in _handRow.GetChildren().ToList())
            {
                if (child is Button oldButton)
                {
                    _cardVisuals.Remove(oldButton);
                    _detailEntries.Remove(oldButton);
                }
                child.QueueFree();
            }
            _handButtons.Clear();

            for (int i = 0; i < human.Hand.Count; i++)
            {
                int captured = i;
                var button = BuildCardButton(_handRow, HandButtonSize, () => OnHandCardClicked(captured));
                _handButtons.Add(button);
            }
            _lastHandCount = human.Hand.Count;
        }

        for (int i = 0; i < human.Hand.Count; i++)
        {
            var card = human.Hand[i];
            var button = _handButtons[i];
            var visual = _cardVisuals[button];

            visual.Art.Texture = _textures.CardArt(card.Image);
            _detailEntries[button] = new CardDetailPanel.Entry(card, null, null, "En tu mano");
            if (card is MonsterCard monster)
            {
                visual.Stats.Text = CardFrames.StatsBbcode(monster.Attack, monster.Attack, monster.Defense, monster.Defense);
                RefreshLevelStars(visual.Stars, monster.Level);
            }
            else
            {
                visual.Stats.Text = card.Kind == CardKind.Spell ? "[CARTA MAGICA]" : "[CARTA DE TRAMPA]";
                RefreshLevelStars(visual.Stars, 0);
            }
            ApplyCardFace(button, card, CardFace.FaceUp);

            bool selected = i == _selectedHand || _fusionSelected.Contains(i)
                || (discarding && _selectedDiscards.Contains(i)) || (_ritualMode && i == _ritualFirst);
            button.Modulate = selected ? new Color(1f, 0.85f, 0.4f) : Colors.White;
        }
    }

    private void RefreshChainAndLog(DuelState state)
    {
        _chainList.Clear();
        for (int i = state.Chain.Count - 1; i >= 0; i--)
        {
            var link = state.Chain[i];
            string cardName = link.CardIn(state)?.Name ?? "?";
            if (link.MonsterEffect is { } monsterEffect)
                cardName += $" (efecto {MonsterEffectCatalog.TypeLabel(monsterEffect.Effect.Type)})";
            string who = link.Controller == PlayerSide.Human ? "Vos" : "CPU";
            _chainList.AddItem($"[{i + 1}] {who}: {cardName}");
        }
        if (state.Chain.Count == 0)
            _chainList.AddItem("(sin cadena activa)");

        _logList.Clear();
        foreach (var entry in state.Log.Tail(60))
            _logList.AddItem(entry);
        if (_logList.ItemCount > 0)
            _logList.EnsureCurrentIsVisible();
    }

    private static string PhaseLabel(DuelPhase phase) => phase switch
    {
        DuelPhase.Draw => "Fase de Robo",
        DuelPhase.Standby => "Fase de Mantenimiento",
        DuelPhase.Main1 => "Main Phase 1",
        DuelPhase.Battle => "Battle Phase",
        DuelPhase.Main2 => "Main Phase 2",
        DuelPhase.End => "End Phase",
        _ => phase.ToString()
    };

    // ------------------------------------------------------- Zonas: clics

    private void OnMonsterZoneClicked(bool isHuman, int zone)
    {
        var state = _engine.State;
        var human = state.Players[HumanIndex];
        var cpu = state.Players[CpuIndex];
        if (state.PendingDiscardCount > 0 || state.PendingChoice != null) return;

        if (_targetMode)
        {
            if (_pendingTargetKind != EffectTargetKind.MonsterZone) return;
            var owner = isHuman ? human : cpu;
            if (owner.MonsterZones[zone] == null) return;
            ConfirmTarget(new EffectTarget { Side = isHuman ? PlayerSide.Human : PlayerSide.Cpu, ZoneIndex = zone });
            return;
        }

        if (_tributeMode)
        {
            if (!isHuman || human.MonsterZones[zone] == null) return;
            if (_selectedHand < 0 || _selectedHand >= human.Hand.Count || human.Hand[_selectedHand] is not MonsterCard monster) return;
            ToggleTribute(zone, monster.RequiredTributes);
            return;
        }

        if (_engine.Phase == DuelPhase.Battle)
        {
            if (isHuman)
            {
                var m = human.MonsterZones[zone];
                if (m is { Position: BattlePosition.Attack, HasAttackedThisTurn: false })
                {
                    _selectedAttacker = zone;
                    _spellTrapCursor = -1;
                    _statusMessage = "Elegi el objetivo del ataque.";
                }
                return;
            }

            if (_selectedAttacker < 0 || cpu.MonsterZones[zone] == null) return;
            Apply(_engine.DeclareAttack(_selectedAttacker, zone));
            _selectedAttacker = -1;
            return;
        }

        if (_engine.Phase is DuelPhase.Main1 or DuelPhase.Main2)
        {
            if (!isHuman || human.MonsterZones[zone] == null) return;
            _selectedZone = zone;
            _selectedHand = -1;
            _spellTrapCursor = -1;
            _fusionMode = false;
            _statusMessage = "";
        }
    }

    private void OnSpellTrapZoneClicked(bool isHuman, int zone)
    {
        var state = _engine.State;
        var human = state.Players[HumanIndex];
        if (state.PendingDiscardCount > 0 || !isHuman) return;
        if (_tributeMode || _fusionMode || _ritualMode) return;
        if (_engine.Phase is not (DuelPhase.Main1 or DuelPhase.Battle or DuelPhase.Main2)) return;
        if (human.SpellTrapZones[zone] == null) return;

        _spellTrapCursor = zone;
        _selectedHand = -1;
        _selectedZone = -1;
        _selectedAttacker = -1;
        _statusMessage = "";
    }

    private void OnHandCardClicked(int index)
    {
        var state = _engine.State;
        var human = state.Players[HumanIndex];
        if (index < 0 || index >= human.Hand.Count) return;

        if (state.PendingDiscardCount > 0)
        {
            int required = state.PendingDiscardCount;
            if (_selectedDiscards.Contains(index)) _selectedDiscards.Remove(index);
            else if (_selectedDiscards.Count < required) _selectedDiscards.Add(index);
            return;
        }

        if (_targetMode) return;

        if (_fusionMode)
        {
            if (human.Hand[index] is MonsterCard) ToggleFusionMaterial(index);
            return;
        }

        if (_ritualMode)
        {
            if (index != _ritualFirst)
            {
                Apply(_engine.RitualSummon(_ritualFirst, index));
                ClearSelections();
            }
            return;
        }

        if (_tributeMode) return;

        _selectedHand = index;
        _selectedZone = -1;
        _spellTrapCursor = -1;
        _statusMessage = "";
    }

    // ------------------------------------------------------------- Acciones

    private void BeginSummon(MonsterCard monster, bool isSet)
    {
        if (monster.RequiredTributes == 0)
        {
            var result = isSet ? _engine.SetMonster(_selectedHand) : _engine.NormalSummon(_selectedHand, BattlePosition.Attack);
            if (isSet && result.Success) _audio.PlaySfx("set");
            Apply(result, clearOnSuccess: true);
            return;
        }

        _tributeMode = true;
        _tributeIsSet = isSet;
        _selectedTributes.Clear();
        _statusMessage = $"Elegi {monster.RequiredTributes} sacrificio(s) en tu campo.";
    }

    private void ToggleTribute(int zone, int required)
    {
        if (_selectedTributes.Contains(zone)) _selectedTributes.Remove(zone);
        else if (_selectedTributes.Count < required) _selectedTributes.Add(zone);
    }

    private void ConfirmTribute()
    {
        var result = _tributeIsSet
            ? _engine.SetMonster(_selectedHand, _selectedTributes.ToArray())
            : _engine.NormalSummon(_selectedHand, BattlePosition.Attack, _selectedTributes.ToArray());
        if (_tributeIsSet && result.Success) _audio.PlaySfx("set");
        Apply(result, clearOnSuccess: true);
    }

    private void CancelTributeSelection()
    {
        _tributeMode = false;
        _selectedTributes.Clear();
        _statusMessage = "";
    }

    private void ToggleFusionMaterial(int handIndex)
    {
        if (_fusionSelected.Contains(handIndex)) _fusionSelected.Remove(handIndex);
        else _fusionSelected.Add(handIndex);
    }

    private void DoFuse()
    {
        var result = _fusionSelected.Count == 2
            ? _engine.Fuse(_fusionSelected[0], _fusionSelected[1])
            : _engine.FuseMany(_fusionSelected.ToArray());
        Apply(result, clearOnSuccess: true);
    }

    private void ClearFusion()
    {
        _fusionMode = false;
        _fusionSelected.Clear();
        _statusMessage = "";
    }

    private void ClearRitual()
    {
        _ritualMode = false;
        _ritualFirst = -1;
        _statusMessage = "";
    }

    private void BeginActivation(bool isSetCard, int sourceIndex, string effectId)
    {
        if (EffectDefinitionResolver.Get(effectId) is ITargetedEffectAction targeted)
        {
            _targetMode = true;
            _targetIsSetCard = isSetCard;
            _targetSourceIndex = sourceIndex;
            _pendingTargetKind = targeted.TargetKind;
            _graveyardCursor = -1;
            _statusMessage = targeted.TargetKind == EffectTargetKind.MonsterZone
                ? "Elegi 1 monstruo en el campo como objetivo."
                : "Elegi 1 monstruo de tu cementerio como objetivo.";
            return;
        }

        var result = isSetCard ? _engine.ActivateSetCard(sourceIndex) : _engine.ActivateSpell(sourceIndex);
        Apply(result, clearOnSuccess: true);
    }

    private void CycleGraveyardCursor()
    {
        var human = _engine.State.Players[HumanIndex];
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
        _graveyardCursor = -1;
    }

    private void ConfirmTarget(EffectTarget target)
    {
        var result = _targetIsSetCard ? _engine.ActivateSetCard(_targetSourceIndex, target) : _engine.ActivateSpell(_targetSourceIndex, target);
        Apply(result, clearOnSuccess: true);
        if (result.Success) CancelTargetSelection();
    }

    private void CancelTargetSelection()
    {
        _targetMode = false;
        _graveyardCursor = -1;
        _statusMessage = "";
    }

    private void Apply(ActionResult result, bool clearOnSuccess = false)
    {
        if (!result.Success)
        {
            _statusMessage = result.Message;
            _audio.PlaySfx("error");
            return;
        }
        _statusMessage = "";
        if (clearOnSuccess) ClearSelections();
    }

    private void ClearSelections()
    {
        _selectedHand = -1;
        _selectedZone = -1;
        _selectedAttacker = -1;
        _fusionMode = false;
        _fusionSelected.Clear();
        _ritualMode = false;
        _ritualFirst = -1;
        _targetMode = false;
        _graveyardCursor = -1;
        _tributeMode = false;
        _selectedTributes.Clear();
        _spellTrapCursor = -1;
        _statusMessage = "";
    }

    // ------------------------------------------------------ Botones de accion

    /// <summary>
    /// Calcula el estado final de los botones de accion en variables locales
    /// y recien los aplica UNA vez al final -- nunca resetea <c>Visible</c> a
    /// <c>false</c> "de paso" para un boton que va a seguir mostrandose. Un
    /// boton de Godot que pasa por <c>Visible = false</c> mientras el mouse
    /// lo tiene fisicamente presionado cancela el seguimiento interno del
    /// clic (el mismo mecanismo que oculta un tooltip/dropdown al perder
    /// foco); como este metodo corre en CADA fotograma via <c>_Process</c>,
    /// el patron anterior ("ocultar todos, despues volver a mostrar los que
    /// correspondan") apagaba y prendia _ctx0/_ctx1/_cancelButton en medio de
    /// cualquier clic que durara mas de un fotograma, y su senal <c>Pressed</c>
    /// nunca llegaba a dispararse -- confirmado con diagnostico en vivo: el
    /// clic caia exactamente sobre el boton correcto (mismo <c>MouseFilter</c>,
    /// mismo Rect) pero jamas se recibia. SIGUIENTE FASE/TERMINAR TURNO nunca
    /// tuvieron este problema porque su Visible se asigna en forma directa
    /// (sin ese paso intermedio).
    /// </summary>
    private void RefreshActionButtons(DuelState state, Player human, Player cpu, bool playerTurn)
    {
        bool ctx0Visible = false, ctx1Visible = false, cancelVisible = false, directAttackVisible = false, chainPassVisible = false;
        bool ctx0Enabled = true, ctx1Enabled = true;
        string ctx0Text = "", ctx1Text = "", cancelText = "CANCELAR (ESC)";
        Action? ctx0Action = null, ctx1Action = null, cancelAction = null, directAttackAction = null, chainPassAction = null;

        void ShowCtx0(string text, Action action, bool enabled = true) { ctx0Visible = true; ctx0Text = text; ctx0Enabled = enabled; ctx0Action = action; }
        void ShowCtx1(string text, Action action, bool enabled = true) { ctx1Visible = true; ctx1Text = text; ctx1Enabled = enabled; ctx1Action = action; }

        bool chainOpen = state.Chain.Count > 0;
        bool discarding = state.PendingDiscardCount > 0;
        bool choosing = state.PendingChoice != null || _effectsMenuOpen;

        _phaseButton.Visible = playerTurn && !chainOpen && !discarding && !choosing;
        _endTurnButton.Visible = playerTurn && !chainOpen && !discarding && !choosing;

        int effectCount = !choosing && (playerTurn || state.ChainPendingResponder == PlayerSide.Human)
            ? _engine.GetActivatableEffects(PlayerSide.Human).Count
            : 0;
        _effectsButton.Visible = effectCount > 0;
        _effectsButton.Text = $"EFECTOS ({effectCount})";
        if (choosing) playerTurn = false;

        if (playerTurn)
        {
            if (discarding)
            {
                int required = state.PendingDiscardCount;
                _statusMessage = $"Descarta {required} carta(s) para bajar al limite de mano.";
                ShowCtx0($"DESCARTAR ({_selectedDiscards.Count}/{required})", () =>
                {
                    Apply(_engine.DiscardForEndPhase(_selectedDiscards.ToArray()));
                    _selectedDiscards.Clear();
                }, enabled: _selectedDiscards.Count == required);
            }
            else
            {
                if (chainOpen)
                {
                    chainPassVisible = state.ChainPendingResponder == PlayerSide.Human;
                    chainPassAction = () => { if (state.ChainPendingResponder == PlayerSide.Human) Apply(_engine.PassPriority()); };
                }

                if (_targetMode)
                {
                    cancelVisible = true;
                    cancelAction = CancelTargetSelection;
                    if (_pendingTargetKind == EffectTargetKind.OwnGraveyard)
                    {
                        ShowCtx0("SIGUIENTE CEMENTERIO", CycleGraveyardCursor);
                        if (_graveyardCursor >= 0)
                            ShowCtx1("CONFIRMAR OBJETIVO", () => ConfirmTarget(new EffectTarget { Side = PlayerSide.Human, ZoneIndex = _graveyardCursor }));
                    }
                }
                else if (_engine.Phase is DuelPhase.Main1 or DuelPhase.Main2)
                {
                    if (_tributeMode)
                    {
                        var monster = _selectedHand >= 0 && _selectedHand < human.Hand.Count ? human.Hand[_selectedHand] as MonsterCard : null;
                        int required = monster?.RequiredTributes ?? 0;
                        _statusMessage = $"Elegi {required} sacrificio(s) en tu campo ({_selectedTributes.Count}/{required}).";
                        cancelVisible = true;
                        cancelAction = CancelTributeSelection;
                        if (_selectedTributes.Count == required)
                            ShowCtx0("CONFIRMAR SACRIFICIO", ConfirmTribute);
                    }
                    else if (_selectedHand >= 0 && _selectedHand < human.Hand.Count && human.Hand[_selectedHand] is MonsterCard monsterCard)
                    {
                        if (!_fusionMode)
                        {
                            string invocarLabel = monsterCard.RequiredTributes > 0 ? $"INVOCAR ({monsterCard.RequiredTributes} sacrif.)" : "INVOCAR";
                            ShowCtx0(invocarLabel, () => BeginSummon(monsterCard, isSet: false));
                            ShowCtx1("COLOCAR", () => BeginSummon(monsterCard, isSet: true));
                            // El tercer boton contextual (Fusionar) reusa el de Cancelar
                            // como boton "extra" cuando no hace falta cancelar nada mas.
                            cancelVisible = true;
                            cancelText = "FUSIONAR";
                            cancelAction = () => { _fusionMode = true; _fusionSelected.Clear(); _fusionSelected.Add(_selectedHand); _statusMessage = "Elegi los materiales de Fusion en tu mano."; };
                        }
                        else
                        {
                            cancelVisible = true;
                            cancelText = "CANCELAR (ESC)";
                            cancelAction = ClearFusion;
                            if (_fusionSelected.Count >= 2)
                                ShowCtx0($"FUSIONAR ({_fusionSelected.Count})", DoFuse);
                        }
                    }
                    else if (_selectedHand >= 0 && _selectedHand < human.Hand.Count && human.Hand[_selectedHand] is SpellCard { SubType: SpellSubType.Ritual })
                    {
                        if (!_ritualMode)
                        {
                            ShowCtx0("INVOCAR RITUAL", () => { _ritualMode = true; _ritualFirst = _selectedHand; _statusMessage = "Invocacion Ritual: elegi al Monstruo de Ritual en tu mano."; });
                        }
                        else
                        {
                            cancelVisible = true;
                            cancelAction = ClearRitual;
                        }
                    }
                    else if (_selectedHand >= 0 && _selectedHand < human.Hand.Count && human.Hand[_selectedHand] is SpellCard or TrapCard)
                    {
                        ShowCtx0("COLOCAR", () => Apply(_engine.SetSpellOrTrap(_selectedHand), clearOnSuccess: true));
                        if (human.Hand[_selectedHand] is SpellCard spellCard)
                            ShowCtx1("ACTIVAR", () => BeginActivation(isSetCard: false, _selectedHand, spellCard.EffectId));
                    }

                    if (_selectedZone >= 0 && _selectedZone < Player.MonsterZoneCount)
                    {
                        var m = human.MonsterZones[_selectedZone];
                        if (m is { Position: BattlePosition.DefenseFaceDown, SummonedThisTurn: false })
                            ShowCtx0("VOLTEAR", () => Apply(_engine.FlipSummon(_selectedZone), clearOnSuccess: true));
                        else if (m is { IsFaceUp: true })
                        {
                            if (m.IsDefending)
                                ShowCtx0("CAMBIAR A ATAQUE", () => Apply(_engine.ChangePosition(_selectedZone, BattlePosition.Attack), clearOnSuccess: true));
                            else
                                ShowCtx1("CAMBIAR A DEFENSA", () => Apply(_engine.ChangePosition(_selectedZone, BattlePosition.DefenseFaceUp), clearOnSuccess: true));
                        }
                    }

                    if (_spellTrapCursor >= 0 && _spellTrapCursor < Player.SpellTrapZoneCount)
                    {
                        var instance = human.SpellTrapZones[_spellTrapCursor];
                        if (instance != null && !instance.FaceUp)
                        {
                            string effectId = instance.Card switch { SpellCard s => s.EffectId, TrapCard t => t.EffectId, _ => "" };
                            ShowCtx1("ACTIVAR", () => BeginActivation(isSetCard: true, _spellTrapCursor, effectId));
                        }
                    }
                }
                else if (_engine.Phase == DuelPhase.Battle && _selectedAttacker >= 0 && cpu.MonsterCount == 0)
                {
                    directAttackVisible = true;
                    directAttackAction = () =>
                    {
                        Apply(_engine.DeclareAttack(_selectedAttacker, -1));
                        _selectedAttacker = -1;
                    };
                }
            }
        }

        _ctx0.Visible = ctx0Visible;
        if (ctx0Visible) { _ctx0.Text = ctx0Text; _ctx0.Disabled = !ctx0Enabled; }
        _ctx0Action = ctx0Action;

        _ctx1.Visible = ctx1Visible;
        if (ctx1Visible) { _ctx1.Text = ctx1Text; _ctx1.Disabled = !ctx1Enabled; }
        _ctx1Action = ctx1Action;

        _cancelButton.Visible = cancelVisible;
        _cancelButton.Text = cancelText;
        _cancelAction = cancelAction;

        _directAttackButton.Visible = directAttackVisible;
        _directAttackAction = directAttackAction;

        _chainPassButton.Visible = chainPassVisible;
        _chainPassAction = chainPassAction;
    }

    // ------------------------------------------------------ Mejoras visuales
    // (Epica V). DuelEngine ya encola eventos estructurados en
    // State.Events (ver DuelEvent.cs) para que la presentacion reaccione sin
    // inferir nada comparando estado -- este bloque los drena cada frame y
    // los traduce a Tween/CPUParticles2D nativos de Godot. La pantalla de
    // batalla es la unica reaccion que no viene de la cola: se detecta
    // comparando State.LastAttack (un slot unico que el motor sobreescribe
    // en cada ataque, con un Serial propio) contra el ultimo valor observado.

    private enum ZoneKind { Monster, SpellTrap, Field }

    private void ProcessDuelEvents()
    {
        // Si un evento abre una escena a pantalla completa, el resto espera en
        // la cola hasta que se cierre (el tablero queda congelado mientras tanto).
        var events = _engine.State.Events;
        while (events.Count > 0 && _blockingOverlay == null)
            HandleDuelEvent(events.Dequeue());
    }

    private void HandleDuelEvent(DuelEvent evt)
    {
        switch (evt)
        {
            case MonsterSummonedEvent summoned:
            {
                _audio.PlaySfx("summon");
                if (summoned.Side == PlayerSide.Cpu && _opponent != null && summoned.Card.Level >= 7)
                    Say(Opponents.Pick(_opponent.OnSummonStrong));
                var button = ZoneButtonFor(ZoneKind.Monster, summoned.Side, summoned.ZoneIndex);
                if (button == null) break;
                // Un Volteo no viene de la mano (el monstruo ya estaba en la
                // Zona boca abajo) -- solo Normal/Especial "vuelan" desde ahi.
                // Tampoco hace "pop": el volteo de TrackZoneCard ya anima la
                // escala, y dos Tween sobre "scale" se pisarian.
                if (summoned.Kind != SummonKind.Flip)
                {
                    FlyCardToZone(_textures.CardArt(summoned.Card.Image), CardFrames.FrameColor(summoned.Card), button, summoned.Side);
                    PopButton(button);
                }
                FlashButton(button, ColorForEvent(evt));
                // El circulo de invocacion se abre cuando la carta "aterriza".
                var flareColor = CardFrames.FrameColor(summoned.Card).Lerp(Colors.White, 0.35f);
                GetTree().CreateTimer(summoned.Kind == SummonKind.Flip ? 0.0 : 0.22).Timeout +=
                    () => CardFx.SummonFlare(_fieldView.EffectsLayer, ButtonCenter(button), button.Size, flareColor);
                break;
            }
            case FusionPerformedEvent fusion:
                if (fusion.Side == PlayerSide.Human)
                {
                    _stats.Fusions++;
                    // El resultado ya cuenta como carta nueva en la Zona; los materiales salen de la mano.
                    _stats.CardsUsed += fusion.Materials.Count - 1;
                }
                else if (_opponent != null) Say(Opponents.Pick(_opponent.OnSummonStrong));
                OpenSummonOverlay(SummonOverlay.SummonKind.Fusion, fusion.Materials, fusion.Result, fusion.Side, fusion.ZoneIndex);
                break;
            case RitualPerformedEvent ritual:
                if (ritual.Side == PlayerSide.Human) _stats.CardsUsed++; // la Magia de Ritual
                else if (_opponent != null) Say(Opponents.Pick(_opponent.OnSummonStrong));
                OpenSummonOverlay(SummonOverlay.SummonKind.Ritual, Array.Empty<MonsterCard>(), ritual.Result, ritual.Side, ritual.ZoneIndex);
                break;
            case MonsterDestroyedEvent destroyed:
            {
                if (destroyed.Side == PlayerSide.Cpu && destroyed.Cause != DestructionCause.Cost && _opponent != null)
                    Say(Opponents.Pick(_opponent.OnLoseMonster));
                if (destroyed.Cause == DestructionCause.Battle)
                {
                    _audio.PlaySfx("destroy_battle");
                    Shake(6f, 0.25f);
                }
                else if (destroyed.Cause == DestructionCause.Effect)
                {
                    _audio.PlaySfx("destroy_battle");
                }
                // La animacion la dispara TrackZoneCard en el refresco de este
                // mismo frame, al ver la Zona vacia; aca solo se le avisa que
                // fue una destruccion (rotura) y no un tributo (disolucion).
                var button = ZoneButtonFor(ZoneKind.Monster, destroyed.Side, destroyed.ZoneIndex);
                if (button != null)
                    _pendingExitCause[button] = destroyed.Cause;
                break;
            }
            case SpellTrapActivatedEvent activated:
            {
                _audio.PlaySfx("chain");
                if (activated.Side == PlayerSide.Human)
                {
                    if (activated.Card is SpellCard { SubType: SpellSubType.Equip }) _stats.EquipMagic++;
                    else if (activated.Card.Kind == CardKind.Spell) _stats.PureMagic++;
                    else _stats.TrapsActivated++;
                }
                var button = ZoneButtonFor(ZoneKind.SpellTrap, activated.Side, activated.ZoneIndex);
                if (button == null) break;
                FlashButton(button, ColorForEvent(evt));
                break;
            }
            case FieldChangedEvent fieldChanged:
            {
                var button = ZoneButtonFor(ZoneKind.Field, fieldChanged.Side, 0);
                if (button == null) break;
                PopButton(button);
                FlashButton(button, ColorForEvent(evt));
                // Nuevo terreno: banner con su nombre y que hace (el fondo
                // tematico lo arma RefreshFieldTheme al ver el cambio).
                var fieldType = (_engine.State.GetPlayer(fieldChanged.Side).FieldZone?.Card as SpellCard)?.FieldType;
                if (fieldType != null)
                    ShowBanner(fieldType.Name.ToUpperInvariant(), fieldType.Description, new Color(fieldType.Color).Lerp(Colors.White, 0.25f));
                break;
            }
            case MonsterEffectActivatedEvent effect:
            {
                _audio.PlaySfx("chain");
                if (effect.Zone == CardZone.MonsterZone && ZoneButtonFor(ZoneKind.Monster, effect.Side, effect.ZoneIndex) is { } effectButton)
                {
                    FlashButton(effectButton, ColorForEvent(evt));
                    PopButton(effectButton);
                }
                string who = effect.Side == PlayerSide.Human ? "Tu efecto" : "Efecto de la CPU";
                ShowBanner($"EFECTO: {effect.Card.Name.ToUpperInvariant()}",
                    $"{who}  ·  {MonsterEffectCatalog.TypeLabel(effect.EffectType)}  ·  desde {CardRef.ZoneName(effect.Zone)}",
                    new Color(0.75f, 0.55f, 1f));
                break;
            }
            case CardDiscardedEvent:
                _audio.PlaySfx("set");
                break;
            case StatModifierAppliedEvent statMod:
            {
                var button = ZoneButtonFor(ZoneKind.Monster, statMod.Side, statMod.ZoneIndex);
                if (button == null) break;
                FlashButton(button, ColorForEvent(evt));
                break;
            }
        }
    }

    private Button? ZoneButtonFor(ZoneKind kind, PlayerSide side, int index)
    {
        bool isHuman = side == PlayerSide.Human;
        return kind switch
        {
            ZoneKind.Monster => (isHuman ? _playerMonsterButtons : _cpuMonsterButtons).ElementAtOrDefault(index),
            ZoneKind.SpellTrap => (isHuman ? _playerSpellTrapButtons : _cpuSpellTrapButtons).ElementAtOrDefault(index),
            ZoneKind.Field => isHuman ? _playerFieldButton : _cpuFieldButton,
            _ => null
        };
    }

    /// <summary>Centro de un boton en SU lienzo: para las Zonas del tablero, el espacio del tablero plano (ver <see cref="_fieldView"/>).</summary>
    /// <remarks>Con la transformacion global y no con GlobalPosition: en un boton girado (Defensa, abanico) GlobalPosition es la esquina ya rotada, no la original.</remarks>
    private static Vector2 ButtonCenter(Control button) => button.GetGlobalTransform() * (button.Size / 2f);

    /// <summary>Centro de una Zona del tablero en la pantalla (ya proyectado en perspectiva).</summary>
    private Vector2 ZoneScreenCenter(Control zoneButton) => _fieldView.ProjectToScreen(ButtonCenter(zoneButton));

    // ------------------------------------------------- Efectos de Monstruo

    /// <summary>
    /// Muestra (o cierra) la ventana de la decision pendiente. Devuelve
    /// verdadero si hay una decision que le toca al jugador: mientras tanto
    /// el duelo (y la IA) esperan.
    /// </summary>
    private bool RefreshChoicePanel()
    {
        var choice = _engine.State.PendingChoice;
        if (choice != null && choice.Chooser == PlayerSide.Human)
        {
            _effectsMenuOpen = false;
            if (!ReferenceEquals(_choicePanel.ShownKey, choice)) ShowChoice(choice);
            return true;
        }

        if (_choicePanel.ShownKey is ChoiceRequest) _choicePanel.HidePanel();
        return false;
    }

    private void ShowChoice(ChoiceRequest choice)
    {
        ClearSelections();
        string title = choice.Source?.Name ?? "Efecto";
        switch (choice.Kind)
        {
            case ChoiceKind.YesNo:
                _choicePanel.ShowYesNo(choice, title, choice.Prompt, yes => AnswerChoice(() => _engine.AnswerYesNo(yes)));
                break;
            case ChoiceKind.SelectOption:
                _choicePanel.ShowOptions(choice, title, choice.Prompt, choice.Options, option => AnswerChoice(() => _engine.AnswerOption(option)));
                break;
            default:
                _choicePanel.ShowCards(choice, title, choice.Prompt, choice.Candidates, choice.Min, choice.Max,
                    indices => AnswerChoice(() => _engine.AnswerCards(indices)));
                break;
        }
    }

    private void AnswerChoice(Func<ActionResult> answer)
    {
        var result = answer();
        Apply(result);
        if (result.Success) _choicePanel.HidePanel();
    }

    /// <summary>Lista de efectos de Monstruo que puedes activar ahora (mano, Campo, Cementerio, desterradas).</summary>
    private void OpenEffectsMenu()
    {
        var effects = _engine.GetActivatableEffects(PlayerSide.Human);
        if (effects.Count == 0) return;
        ClearSelections();
        _effectsMenuOpen = true;
        _choicePanel.ShowOptions("effects-menu", "EFECTOS DE MONSTRUO", "Elige el efecto que quieres activar.",
            effects.Select(e => e.Label).ToList(),
            index =>
            {
                _effectsMenuOpen = false;
                _choicePanel.HidePanel();
                Apply(_engine.ActivateMonsterEffect(effects[index]), clearOnSuccess: true);
            },
            () =>
            {
                _effectsMenuOpen = false;
                _choicePanel.HidePanel();
            });
    }

    private static Color ColorForEvent(DuelEvent evt) => evt switch
    {
        MonsterSummonedEvent => new Color(0.55f, 0.85f, 1f),
        MonsterDestroyedEvent => new Color(1f, 0.35f, 0.3f),
        SpellTrapActivatedEvent => new Color(0.5f, 1f, 0.55f),
        MonsterEffectActivatedEvent => new Color(0.8f, 0.6f, 1f),
        FieldChangedEvent => new Color(0.9f, 0.75f, 0.4f),
        StatModifierAppliedEvent => new Color(1f, 1f, 0.6f),
        _ => Colors.White
    };

    // ------------------------------------------------- Rival: retrato y dialogo

    /// <summary>Retrato del rival junto a su nombre y LP, y el globo de dialogo (oculto hasta que hable).</summary>
    private void BuildOpponentCorner()
    {
        var cpuInfo = GetNode<HBoxContainer>("Root/Board/CpuInfo");
        if (_opponent != null)
        {
            var portrait = PortraitView.Create(_opponent.Id, _opponent.Color, _opponent.HairStyle, 44);
            cpuInfo.AddChild(portrait);
            cpuInfo.MoveChild(portrait, 0);
            _cpuName.AddThemeColorOverride("font_color", _opponent.Color.Lerp(Colors.White, 0.35f));
        }

        _speechBubble = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, Visible = false, ZIndex = 60 };
        _speechBubble.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.98f, 0.97f, 0.92f), _opponent?.Color ?? UiKit.Gold, 3, radius: 12));
        _speechText = new Label { MouseFilter = MouseFilterEnum.Ignore };
        _speechText.AddThemeFontSizeOverride("font_size", 15);
        _speechText.AddThemeColorOverride("font_color", new Color(0.1f, 0.08f, 0.15f));
        _speechBubble.AddChild(_speechText);
        cpuInfo.AddChild(_speechBubble);
    }

    /// <summary>El rival dice <paramref name="line"/> en su globo durante unos segundos (reemplaza lo anterior).</summary>
    private void Say(string line, double seconds = 2.6)
    {
        if (_opponent == null || string.IsNullOrEmpty(line)) return;
        _speechTween?.Kill();
        _speechText.Text = line;
        _speechBubble.Visible = true;
        _speechBubble.Modulate = new Color(1, 1, 1, 0);
        _speechBubble.PivotOffset = new Vector2(0, _speechBubble.Size.Y / 2f);
        _speechBubble.Scale = new Vector2(0.7f, 0.7f);
        _speechTween = CreateTween();
        _speechTween.TweenProperty(_speechBubble, "modulate:a", 1f, 0.15);
        _speechTween.Parallel().TweenProperty(_speechBubble, "scale", Vector2.One, 0.2).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _speechTween.TweenInterval(seconds);
        _speechTween.TweenProperty(_speechBubble, "modulate:a", 0f, 0.3);
        _speechTween.TweenCallback(Callable.From(() => _speechBubble.Visible = false));
    }

    // -------------------------------------------------- Estrella Guardiana

    /// <summary>
    /// Si hay un Monstruo tuyo recien Invocado/Colocado sin Estrella Guardiana
    /// elegida, abre el selector (tras <see cref="StarPromptDelay"/>).
    /// Devuelve verdadero si lo acaba de abrir.
    /// </summary>
    private bool CheckGuardianStarPrompt(double delta)
    {
        var human = _engine.State.Players[HumanIndex];
        int zone = Array.FindIndex(human.MonsterZones, m => m is { SummonedThisTurn: true, GuardianStarChosen: false });
        if (zone < 0)
        {
            _starPromptTimer = 0;
            return false;
        }

        _starPromptTimer += delta;
        if (_starPromptTimer < StarPromptDelay) return false;
        _starPromptTimer = 0;

        var card = human.MonsterZones[zone]!.Card;
        var picker = new GuardianStarPicker();
        picker.Setup(card, star => Apply(_engine.ChooseGuardianStar(PlayerSide.Human, zone, star)), _textures, _audio);
        OpenBlockingOverlay(picker);
        return true;
    }

    // ------------------------------------------------------ Contador de LP

    /// <summary>Avanza los contadores de LP hacia el valor real y hace sonar un "tic" por paso mientras alguno se mueve.</summary>
    private void UpdateLpCounters(double delta)
    {
        _humanLpTrend = _humanLpCounter.Step(_engine.State.Players[HumanIndex].LifePoints, delta);
        _cpuLpTrend = _cpuLpCounter.Step(_engine.State.Players[CpuIndex].LifePoints, delta);

        if (_humanLpTrend == 0 && _cpuLpTrend == 0)
        {
            _lpTickTimer = LpTickInterval;
            return;
        }
        _lpTickTimer += delta;
        if (_lpTickTimer >= LpTickInterval)
        {
            _lpTickTimer = 0;
            _audio.PlaySfx("lp_tick");
        }
    }

    /// <summary>Barra y numero de LP segun el valor mostrado: el numero se tine de rojo/verde mientras baja/sube, y el relleno pasa de verde a amarillo y a rojo segun lo que queda.</summary>
    private void RefreshLpBar(ProgressBar bar, Label label, LpCounter counter, int trend)
    {
        bar.Value = counter.Shown;
        label.Text = $"{Mathf.RoundToInt(counter.Shown)} LP";
        label.AddThemeColorOverride("font_color", trend < 0 ? new Color(0.75f, 0f, 0f) : trend > 0 ? new Color(0f, 0.5f, 0.1f) : Colors.Black);

        double ratio = bar.MaxValue > 0 ? counter.Shown / bar.MaxValue : 0;
        int bucket = ratio > 0.5 ? 2 : ratio > 0.25 ? 1 : 0;
        if (!_lpFillStyles.TryGetValue(bucket, out var fill))
        {
            var color = bucket switch
            {
                2 => new Color(0.45f, 0.85f, 0.5f),
                1 => new Color(0.95f, 0.8f, 0.3f),
                _ => new Color(0.95f, 0.4f, 0.35f)
            };
            fill = new StyleBoxFlat
            {
                BgColor = color,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4
            };
            _lpFillStyles[bucket] = fill;
        }
        bar.AddThemeStyleboxOverride("fill", fill);
    }

    // ---------------------------------------------------- Panel de detalle

    /// <summary>Muestra en el panel lateral la carta bajo el cursor; sin cursor sobre ninguna, el panel conserva la ultima.</summary>
    private void RefreshDetailPanel()
    {
        // Los botones de la mano se reconstruyen cuando cambia su cantidad:
        // el que estaba bajo el cursor puede ya no existir.
        if (_hoverButton != null && !IsInstanceValid(_hoverButton)) _hoverButton = null;
        if (_hoverButton != null && _detailEntries.TryGetValue(_hoverButton, out var entry))
            _detailPanel.ShowEntry(entry);
    }

    // ------------------------------------------------------ Banners de fase

    private static readonly Color HumanBannerColor = new(0.5f, 0.8f, 1f);
    private static readonly Color CpuBannerColor = new(1f, 0.5f, 0.45f);
    private static readonly Color BattleBannerColor = new(1f, 0.7f, 0.3f);

    private void ShowTurnBanner()
    {
        if (!_ready || _engine.IsOver) return;
        bool human = _engine.ActiveIndex == HumanIndex;
        ShowBanner(human ? "TU TURNO" : "TURNO DE LA CPU",
            $"Turno {_engine.State.TurnNumber}  ·  {PhaseLabel(_engine.Phase)}",
            human ? HumanBannerColor : CpuBannerColor);
    }

    /// <summary>Solo las fases donde se decide algo: Robo, Mantenimiento y Final pasan solas y llenarian la pantalla de banners.</summary>
    private void ShowPhaseBannerFor(DuelPhase phase)
    {
        if (phase is not (DuelPhase.Main1 or DuelPhase.Battle or DuelPhase.Main2)) return;
        bool human = _engine.ActiveIndex == HumanIndex;
        ShowBanner(PhaseLabel(phase).ToUpperInvariant(), human ? "Tu turno" : "Turno de la CPU",
            phase == DuelPhase.Battle ? BattleBannerColor : human ? HumanBannerColor : CpuBannerColor);
    }

    /// <summary>
    /// Franja que se abre sobre el centro del tablero, con el titulo entrando
    /// desde la izquierda; se sostiene un momento y se cierra. No bloquea
    /// clics, y si llega otro banner antes de que termine, lo reemplaza.
    /// </summary>
    private void ShowBanner(string title, string subtitle, Color accent)
    {
        _activeBanner?.QueueFree();

        var board = GetNode<Control>("Root/Board");
        const float height = 84f;
        float centerY = _fieldView.ProjectToScreen(ButtonCenter(_phaseBanner)).Y;
        var size = new Vector2(board.Size.X, height);
        var banner = new Control
        {
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 30,
            Position = new Vector2(board.GlobalPosition.X, centerY - height / 2f),
            Size = size,
            PivotOffset = size / 2f,
            Scale = new Vector2(1, 0)
        };
        banner.AddChild(new ColorRect { Color = new Color(0.03f, 0.04f, 0.1f, 0.88f), Size = size, MouseFilter = MouseFilterEnum.Ignore });
        foreach (float y in new[] { 0f, height - 3f })
            banner.AddChild(new ColorRect { Color = accent, Position = new Vector2(0, y), Size = new Vector2(size.X, 3), MouseFilter = MouseFilterEnum.Ignore });

        var text = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Size = size, Alignment = BoxContainer.AlignmentMode.Center };
        text.AddThemeConstantOverride("separation", -2);
        var titleLabel = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        titleLabel.AddThemeFontSizeOverride("font_size", 34);
        titleLabel.AddThemeColorOverride("font_color", accent.Lerp(Colors.White, 0.35f));
        titleLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        titleLabel.AddThemeConstantOverride("outline_size", 8);
        var subtitleLabel = new Label { Text = subtitle, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        subtitleLabel.AddThemeFontSizeOverride("font_size", 15);
        subtitleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
        text.AddChild(titleLabel);
        text.AddChild(subtitleLabel);
        text.Position = new Vector2(-size.X * 0.4f, 0);
        text.Modulate = new Color(1, 1, 1, 0);
        banner.AddChild(text);

        AddChild(banner);
        _activeBanner = banner;
        _audio.PlaySfx("banner");

        var tween = banner.CreateTween();
        tween.TweenProperty(banner, "scale:y", 1f, 0.14).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(text, "position:x", 0f, 0.28).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(text, "modulate:a", 1f, 0.15);
        tween.TweenInterval(0.75);
        tween.TweenProperty(text, "position:x", size.X * 0.4f, 0.2).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(text, "modulate:a", 0f, 0.2);
        tween.TweenProperty(banner, "scale:y", 0f, 0.12);
        tween.TweenCallback(Callable.From(() =>
        {
            if (_activeBanner == banner) _activeBanner = null;
            banner.QueueFree();
        }));
    }

    // --------------------------------------------- Volteo / salida en tablero

    /// <summary>
    /// Registra la carta que el boton va a mostrar en este refresco y anima
    /// la transicion respecto de la anterior: si es la misma instancia con
    /// otra cara, la carta se voltea; si la instancia cambio (o la Zona quedo
    /// vacia), la anterior se rompe o se disuelve. Se llama ANTES de pisar el
    /// arte del boton, asi la copia animada usa lo que el jugador estaba
    /// viendo. Devuelve la cara que hay que dibujar (durante la primera mitad
    /// de un volteo, la vieja).
    /// </summary>
    private bool TrackZoneCard(Button button, object? instance, bool faceUp)
    {
        bool isNew = instance != null && (!_shownCards.TryGetValue(button, out var previous) || !ReferenceEquals(previous.Instance, instance));
        if (isNew && _humanZoneButtons.Contains(button))
        {
            _stats.CardsUsed++;
            if (!faceUp) _stats.FaceDownPlays++;
        }

        if (_shownCards.TryGetValue(button, out var shown))
        {
            if (!ReferenceEquals(shown.Instance, instance))
            {
                _flipHeldFace.Remove(button);
                PlayZoneExit(button);
            }
            else if (shown.FaceUp != faceUp && !_flipHeldFace.ContainsKey(button))
            {
                BeginBoardFlip(button, shown.FaceUp);
            }
        }

        if (instance != null) _shownCards[button] = new ShownCard(instance, faceUp);
        else _shownCards.Remove(button);

        return _flipHeldFace.TryGetValue(button, out var held) ? held : faceUp;
    }

    /// <summary>La carta se cierra de canto (escala X a 0), cambia de cara en ese instante y se vuelve a abrir.</summary>
    private void BeginBoardFlip(Button button, bool oldFace)
    {
        _flipHeldFace[button] = oldFace;
        _audio.PlaySfx("set");
        button.PivotOffset = button.Size / 2f;
        var tween = button.CreateTween();
        tween.TweenProperty(button, "scale:x", 0f, 0.13).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(() => _flipHeldFace.Remove(button)));
        tween.TweenProperty(button, "scale:x", 1f, 0.13).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    /// <summary>Una carta deja la Zona: se rompe si el motor informo una destruccion (batalla/efecto), y se disuelve en cualquier otro caso (tributo, Magia ya usada, reemplazo de Campo).</summary>
    private void PlayZoneExit(Button button)
    {
        var visual = _cardVisuals[button];
        var frame = _frameColors.TryGetValue(button, out var color) ? color : CardFrames.Empty;
        var look = new CardFx.CardLook(visual.Art.Texture, ArtRectInButton(visual), frame, Border: 3f);

        var transform = button.GetGlobalTransform();
        var center = transform * (button.Size / 2f);

        bool destroyed = _pendingExitCause.TryGetValue(button, out var cause) && cause != DestructionCause.Cost;
        if (destroyed)
            CardFx.Shatter(_fieldView.EffectsLayer, center, button.Size, transform.Rotation, look, cols: 4, rows: 4, spread: 90f);
        else
            CardFx.Dissolve(_fieldView.EffectsLayer, center, button.Size, transform.Rotation, look);
    }

    /// <summary>Rect donde el boton dibuja el arte hoy (en coordenadas del boton): el <see cref="TextureRect"/> lo centra conservando proporcion, asi que se replica ese ajuste.</summary>
    private static Rect2 ArtRectInButton(CardVisual visual)
    {
        // Posicion local sumando la cadena de padres hasta el boton (el arte
        // esta dentro de su marco, dentro del VBox de contenido).
        var position = Vector2.Zero;
        for (Control? node = visual.Art; node != null && node is not Button; node = node.GetParent() as Control)
            position += node.Position;
        var rect = new Rect2(position, visual.Art.Size);
        var texture = visual.Art.Texture;
        if (texture == null || rect.Size.X <= 0 || rect.Size.Y <= 0) return rect;

        var textureSize = texture.GetSize();
        float scale = Mathf.Min(rect.Size.X / textureSize.X, rect.Size.Y / textureSize.Y);
        var drawn = textureSize * scale;
        return new Rect2(rect.Position + (rect.Size - drawn) / 2f, drawn);
    }

    private void FlashButton(Button button, Color color)
    {
        _flashingButtons.Add(button);
        button.Modulate = color;
        var tween = CreateTween();
        tween.TweenProperty(button, "modulate", Colors.White, 0.45)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenCallback(Callable.From(() => _flashingButtons.Remove(button)));
    }

    private static void PopButton(Button button)
    {
        button.PivotOffset = button.Size / 2f;
        button.Scale = new Vector2(0.35f, 0.35f);
        button.CreateTween()
            .TweenProperty(button, "scale", Vector2.One, 0.3)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }


    // ------------------------------------------------- Cartas "fantasma"
    // (paridad Forbidden Memories): copias visuales de vida corta, libres de
    // todo Container (agregadas directo a "this" con posicion absoluta), que
    // viajan de un punto a otro y se autodestruyen -- nunca tocan el boton
    // real de la zona (que sigue el modelo container-friendly de Scale/
    // Modulate/Rotation de la Epica V), asi que no hay conflicto entre ambos.

    private static readonly Vector2 GhostCardSize = new(80, 96);

    private Control SpawnGhostCard(Texture2D? art, Color frameColor, Vector2 centerPosition)
    {
        var ghost = new Panel
        {
            Size = GhostCardSize,
            PivotOffset = GhostCardSize / 2f,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 10
        };
        var style = new StyleBoxFlat
        {
            BgColor = frameColor,
            BorderColor = frameColor.Darkened(0.55f),
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            BorderWidthTop = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8
        };
        ghost.AddThemeStyleboxOverride("panel", style);

        var artRect = new TextureRect
        {
            Texture = art,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 4,
            OffsetTop = 4,
            OffsetRight = -4,
            OffsetBottom = -4,
            MouseFilter = MouseFilterEnum.Ignore
        };
        ghost.AddChild(artRect);

        AddChild(ghost);
        ghost.Position = centerPosition - GhostCardSize / 2f;
        return ghost;
    }

    /// <summary>Una carta "vuela" desde la mano de su duenio (la tuya abajo; la de la CPU, arriba junto a su contador) hasta la Zona donde acaba de entrar.</summary>
    private void FlyCardToZone(Texture2D? art, Color frameColor, Button toButton, PlayerSide side)
    {
        var fromCenter = side == PlayerSide.Human
            ? _handRow.GlobalPosition + _handRow.Size / 2f
            : _cpuCounts.GlobalPosition + _cpuCounts.Size / 2f;
        var ghost = SpawnGhostCard(art, frameColor, fromCenter);
        var targetPos = ZoneScreenCenter(toButton) - GhostCardSize / 2f;
        float targetScale = _fieldView.ScaleAt(ButtonCenter(toButton));

        var tween = CreateTween();
        tween.TweenProperty(ghost, "position", targetPos, 0.28)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(ghost, "scale", new Vector2(targetScale, targetScale), 0.28);
        tween.Parallel().TweenProperty(ghost, "modulate:a", 0.6, 0.28);
        tween.TweenCallback(Callable.From(() => ghost.QueueFree()));
    }

    private void Shake(float magnitude, float duration)
    {
        // La posicion de reposo real de "Root" no es (0,0) -- viene de sus
        // propios anchors/offsets en el .tscn -- asi que se ancla al primer
        // valor observado en vez de asumir el origen, y todo shake posterior
        // (incluso superpuesto) vibra alrededor de ese mismo punto de reposo.
        _shakeBasePosition ??= _shakeRoot.Position;
        var basePos = _shakeBasePosition.Value;

        var rng = Random.Shared;
        var tween = CreateTween();
        const int steps = 6;
        for (int i = 0; i < steps; i++)
        {
            var offset = new Vector2(
                (float)(rng.NextDouble() * 2 - 1) * magnitude,
                (float)(rng.NextDouble() * 2 - 1) * magnitude);
            tween.TweenProperty(_shakeRoot, "position", basePos + offset, duration / steps);
        }
        tween.TweenProperty(_shakeRoot, "position", basePos, duration / steps);
    }

    private void FlashScreen(Color color, float duration)
    {
        _screenFlash.Color = new Color(color.R, color.G, color.B, 0.65f);
        _screenFlash.Visible = true;
        var tween = CreateTween();
        tween.TweenProperty(_screenFlash, "color:a", 0f, duration)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenCallback(Callable.From(() => _screenFlash.Visible = false));
    }

    private void DetectAttackClash()
    {
        var attack = _engine.State.LastAttack;
        if (attack == null || attack == _lastObservedAttack) return;
        _lastObservedAttack = attack;
        BeginClash(attack.Value);
    }

    private void BeginClash(AttackInfo attack)
    {
        if (attack.AttackerSide == PlayerSide.Human)
        {
            if (attack.DefenderDestroyed || attack.DamageToDefender > 0) _stats.EffectiveAttacks++;
        }
        else
        {
            if (attack.DefenderCard != null && !attack.DefenderDestroyed) _stats.DefenseWins++;
            if (_opponent != null) Say(Opponents.Pick(_opponent.OnAttack));
        }

        var overlay = new BattleOverlay();
        overlay.Setup(attack, _textures, _audio);
        OpenBlockingOverlay(overlay);
    }

    private void OpenBlockingOverlay(DuelOverlay overlay, Action? onFinished = null)
    {
        overlay.Finished += () =>
        {
            _blockingOverlay = null;
            onFinished?.Invoke();
        };
        _blockingOverlay = overlay;
        AddChild(overlay);
    }

    /// <summary>Fusion/Ritual: la secuencia grande y, al cerrarse, la llegada a la Zona (pop, destello y circulo de invocacion).</summary>
    private void OpenSummonOverlay(SummonOverlay.SummonKind kind, IReadOnlyList<MonsterCard> materials, MonsterCard result, PlayerSide side, int zoneIndex)
    {
        var button = ZoneButtonFor(ZoneKind.Monster, side, zoneIndex);
        if (button == null) return;
        var overlay = new SummonOverlay();
        overlay.Setup(kind, materials, result, side == PlayerSide.Human,
            ZoneScreenCenter(button), button.Size * _fieldView.ScaleAt(ButtonCenter(button)), _textures, _audio);
        OpenBlockingOverlay(overlay, () =>
        {
            PopButton(button);
            FlashButton(button, CardFrames.FrameColor(result).Lerp(Colors.White, 0.5f));
            CardFx.SummonFlare(_fieldView.EffectsLayer, ButtonCenter(button), button.Size, CardFrames.FrameColor(result).Lerp(Colors.White, 0.35f));
        });
    }

    /// <summary>Sonido de dano por jugador (comparando LP contra el ultimo valor observado), igual que <c>DuelScreen.UpdateAnimations</c>.</summary>
    private void DetectDamage()
    {
        int human = _engine.State.Players[HumanIndex].LifePoints;
        int cpu = _engine.State.Players[CpuIndex].LifePoints;

        if (human < _lastHumanLp)
        {
            _audio.PlaySfx("damage");
            SpawnDamagePopup(_playerLp, _lastHumanLp - human);
        }
        if (cpu < _lastCpuLp)
        {
            _audio.PlaySfx("damage");
            SpawnDamagePopup(_cpuLp, _lastCpuLp - cpu);
        }

        _lastHumanLp = human;
        _lastCpuLp = cpu;

        if (!_engine.IsOver)
            _music.Play(human <= CriticalLifePoints ? "critical" : "duel");
        if (_opponent != null && !_cpuLowLpSaid && cpu > 0 && cpu <= CriticalLifePoints)
        {
            _cpuLowLpSaid = true;
            Say(_opponent.LowLifePoints);
        }
    }

    /// <summary>Numero de dano flotante sobre la barra de vida (sube y se desvanece), equivalente Godot de <c>DuelScreen.AddDamagePopup</c> de MonoGame.</summary>
    private void SpawnDamagePopup(ProgressBar bar, int amount)
    {
        var label = new Label
        {
            Text = $"-{amount}",
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 20
        };
        label.AddThemeColorOverride("font_color", new Color(1f, 0.35f, 0.3f));
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 5);
        label.AddThemeFontSizeOverride("font_size", 22);

        AddChild(label);
        var start = bar.GlobalPosition + new Vector2(bar.Size.X / 2f - 14, -6);
        label.Position = start;

        var tween = CreateTween();
        tween.TweenProperty(label, "position", start + new Vector2(0, -46), 0.9)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(label, "modulate:a", 0.0, 0.6).SetDelay(0.3);
        tween.TweenCallback(Callable.From(() => label.QueueFree()));
    }
}
