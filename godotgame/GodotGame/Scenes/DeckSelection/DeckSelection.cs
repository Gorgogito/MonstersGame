using Godot;
using GodotGame.Core.AI;
using GodotGame.Core.Battle;
using GodotGame.Core.Entities;
using GodotGame.Core.Rules;
using GodotGame.Data;
using GodotGame.Data.Loaders;

namespace GodotGame;

/// <summary>
/// Seleccion de mazo (equivalente Godot de <c>DeckSelectionScreen</c>). El
/// <see cref="ItemList"/> de Godot reemplaza la lista de mazos dibujada a
/// mano (hover/clic/teclado ya resueltos por el nodo); la previsualizacion de
/// cartas usa otro <see cref="ItemList"/> con scroll nativo en vez de la
/// paginacion manual (Izquierda/Derecha, rueda) que necesitaba MonoGame.
/// </summary>
public partial class DeckSelection : Control
{
    private GameRoot _root = null!;
    private System.Collections.Generic.List<DeckDefinition> _decks = null!;

    private ItemList _deckList = null!;
    private ItemList _cardList = null!;
    private Label _previewHeader = null!;
    private Label _statusLabel = null!;
    private Button _startButton = null!;

    public override void _Ready()
    {
        _root = GetNode<GameRoot>("/root/GameRoot");
        _decks = new System.Collections.Generic.List<DeckDefinition>(_root.Data.Decks);

        _deckList = GetNode<ItemList>("Main/Content/DeckList");
        _cardList = GetNode<ItemList>("Main/Content/PreviewPane/CardList");
        _previewHeader = GetNode<Label>("Main/Content/PreviewPane/PreviewHeader");
        _statusLabel = GetNode<Label>("Main/Bottom/StatusLabel");
        _startButton = GetNode<Button>("Main/Bottom/StartButton");

        foreach (var deck in _decks)
            _deckList.AddItem($"{deck.Name.ToUpperInvariant()}  ({deck.Count} cartas)");

        if (_decks.Count == 0)
        {
            _statusLabel.Text = "No se encontraron mazos.";
            _startButton.Disabled = true;
            return;
        }

        _deckList.Select(0);
        ShowPreview(0);
        _deckList.GrabFocus();

        if (_root.PendingOpponent is { } opponent)
            _statusLabel.Text = $"Rival: {opponent.Name}, {opponent.Title}. Elige tu mazo.";
        GetNode<MusicManager>("/root/MusicManager").Play("menu");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel")) OnBackPressed();
    }

    private void OnDeckSelected(long index) => ShowPreview((int)index);

    private void OnDeckActivated(long index) => StartDuel((int)index);

    private void ShowPreview(int deckIndex)
    {
        _cardList.Clear();
        var deck = _decks[deckIndex];

        var grouped = new System.Collections.Generic.List<(Card Card, int Count)>();
        var indexById = new System.Collections.Generic.Dictionary<int, int>();
        foreach (int id in deck.CardIds)
        {
            var card = _root.Data.Cards.Get(id);
            if (card == null) continue;

            if (indexById.TryGetValue(id, out int idx))
                grouped[idx] = (grouped[idx].Card, grouped[idx].Count + 1);
            else
            {
                indexById[id] = grouped.Count;
                grouped.Add((card, 1));
            }
        }

        _previewHeader.Text = $"{deck.Name.ToUpperInvariant()} -- {grouped.Count} cartas distintas / {deck.Count} total";

        foreach (var (card, count) in grouped)
            _cardList.AddItem(DescribeRow(card, count));
    }

    private static string DescribeRow(Card card, int count)
    {
        string namePart = count > 1 ? $"{card.Name} x{count}" : card.Name;
        if (card is MonsterCard monster)
            return $"{namePart}  --  Nv{monster.Level}  ATK {monster.Attack}  DEF {monster.Defense}  ({monster.Attribute})";

        string kindLabel = card.Kind == CardKind.Trap ? "TRAMPA" : "MAGIA";
        return $"{namePart}  --  {kindLabel}";
    }

    private void OnBackPressed() => Graphics.UiKit.GoTo(this, _root.PendingOpponent != null
        ? "res://Scenes/OpponentSelect/OpponentSelect.tscn"
        : "res://Scenes/MainMenu/MainMenu.tscn");

    private void OnStartPressed()
    {
        int selected = _deckList.GetSelectedItems().Length > 0 ? _deckList.GetSelectedItems()[0] : 0;
        StartDuel(selected);
    }

    private void StartDuel(int playerDeckIndex)
    {
        var playerDef = _decks[playerDeckIndex];
        var opponent = _root.PendingOpponent;
        var cpuDef = opponent != null ? _root.DeckByName(opponent.DeckName) ?? ChooseCpuDeck(playerDef) : ChooseCpuDeck(playerDef);

        var human = new Player(PlayerSide.Human, "JUGADOR");
        var cpu = new Player(PlayerSide.Cpu, opponent?.Name.ToUpperInvariant() ?? "CPU");

        human.Deck.AddRange(_root.Data.BuildDeck(playerDef));
        cpu.Deck.AddRange(_root.Data.BuildDeck(cpuDef));
        GameData.Shuffle(human.Deck, _root.Random);
        GameData.Shuffle(cpu.Deck, _root.Random);

        var engine = new DuelEngine(new DuelConfig(), _root.Data.Fusions);
        var ai = new BasicCpuAI(_root.Data.Fusions);

        int firstPlayer = _root.Random.Next(2);
        engine.StartDuel(human, cpu, firstPlayer);
        // Los jefes pueden empezar con mas LP que el jugador.
        if (opponent != null) cpu.LifePoints = opponent.StartingLifePoints;

        _root.PendingEngine = engine;
        _root.PendingAi = ai;
        _root.PendingPlayerDeck = playerDef;
        // Con rival elegido, primero la presentacion "VS"; sin el (camino viejo), directo al duelo.
        Graphics.UiKit.GoTo(this, opponent != null ? "res://Scenes/VsIntro/VsIntro.tscn" : "res://Scenes/Duel/Duel.tscn");
    }

    private DeckDefinition ChooseCpuDeck(DeckDefinition playerDeck)
    {
        foreach (var deck in _decks)
            if (deck.Name.ToUpperInvariant().Contains("CPU"))
                return deck;

        var different = new System.Collections.Generic.List<DeckDefinition>();
        foreach (var deck in _decks)
            if (!ReferenceEquals(deck, playerDeck)) different.Add(deck);

        if (different.Count > 0) return different[_root.Random.Next(different.Count)];
        return playerDeck;
    }
}
