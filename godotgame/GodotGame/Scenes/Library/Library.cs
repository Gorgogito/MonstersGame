using Godot;
using GodotGame.Core.Entities;
using GodotGame.Graphics;

namespace GodotGame;

/// <summary>
/// Biblioteca: todo el catalogo en una grilla, con las cartas obtenidas a la
/// vista y las que faltan como reverso "???", mas un contador "X / N" como
/// el de Forbidden Memories. Al pasar el cursor, el panel de la derecha
/// muestra el detalle (y cuantas copias tienes).
/// </summary>
public partial class Library : Control
{
    private GameRoot _root = null!;
    private TextureCache _textures = null!;
    private CardDetailPanel _detail = null!;
    private GridContainer _grid = null!;
    private bool _onlyOwned;

    public override void _Ready()
    {
        _root = GetNode<GameRoot>("/root/GameRoot");
        _textures = new TextureCache(ProjectSettings.GlobalizePath("res://Data/Art"));
        GetNode<MusicManager>("/root/MusicManager").Play("menu");

        AddChild(new MenuBackground());

        var main = new VBoxContainer();
        main.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        main.OffsetLeft = 24; main.OffsetRight = -24; main.OffsetTop = 16; main.OffsetBottom = -16;
        main.AddThemeConstantOverride("separation", 10);
        AddChild(main);

        var cards = _root.Data.Cards.AllCards.OrderBy(c => c.Id).ToList();
        int owned = cards.Count(c => _root.Save.Collection.ContainsKey(c.Id));

        var header = new HBoxContainer();
        var title = UiKit.Title("BIBLIOTECA", 46);
        title.HorizontalAlignment = HorizontalAlignment.Left;
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);
        var counter = UiKit.Title($"{owned} / {cards.Count}", 34, new Color(0.85f, 0.9f, 1f));
        counter.VerticalAlignment = VerticalAlignment.Center;
        header.AddChild(counter);
        main.AddChild(header);

        var toolbar = new HBoxContainer();
        toolbar.AddThemeConstantOverride("separation", 12);
        var toggle = UiKit.MenuButton("MOSTRAR: TODAS", () => { }, 260);
        toggle.Pressed += () =>
        {
            _onlyOwned = !_onlyOwned;
            toggle.Text = _onlyOwned ? "MOSTRAR: OBTENIDAS" : "MOSTRAR: TODAS";
            Populate(cards);
        };
        toolbar.AddChild(toggle);
        toolbar.AddChild(UiKit.MenuButton("VOLVER", GoBack, 200));
        main.AddChild(toolbar);

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 16);
        main.AddChild(body);

        var scroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _grid = new GridContainer { Columns = 7 };
        _grid.AddThemeConstantOverride("h_separation", 8);
        _grid.AddThemeConstantOverride("v_separation", 8);
        scroll.AddChild(_grid);
        body.AddChild(scroll);

        _detail = new CardDetailPanel { CustomMinimumSize = new Vector2(300, 0), SizeFlagsVertical = SizeFlags.ShrinkBegin };
        _detail.Setup(_textures);
        body.AddChild(_detail);

        Populate(cards);
    }

    private void Populate(List<Card> cards)
    {
        foreach (var child in _grid.GetChildren()) child.QueueFree();
        foreach (var card in cards)
        {
            int copies = _root.Save.Collection.GetValueOrDefault(card.Id);
            if (_onlyOwned && copies == 0) continue;
            _grid.AddChild(BuildTile(card, copies));
        }
    }

    private Button BuildTile(Card card, int copies)
    {
        bool owned = copies > 0;
        var frame = owned ? CardFrames.FrameColor(card) : CardFrames.FaceDown;
        var button = new Button { CustomMinimumSize = new Vector2(104, 146), FocusMode = FocusModeEnum.All };
        foreach (string state in new[] { "normal", "pressed", "disabled" })
            button.AddThemeStyleboxOverride(state, CardFrames.Body(frame));
        var hover = (StyleBoxFlat)CardFrames.Body(frame).Duplicate();
        hover.ShadowColor = new Color(1f, 0.85f, 0.4f, 0.7f);
        hover.ShadowSize = 8;
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("focus", hover);

        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        box.OffsetLeft = 4; box.OffsetTop = 4; box.OffsetRight = -4; box.OffsetBottom = -4;
        button.AddChild(box);

        var name = new Label
        {
            Text = owned ? card.Name : $"#{card.Id:000}  ???",
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        name.AddThemeFontSizeOverride("font_size", 10);
        if (owned) CardFrames.StyleName(name, card);
        else name.AddThemeColorOverride("font_color", new Color(0.8f, 0.75f, 0.65f));
        box.AddChild(name);

        var art = new TextureRect
        {
            Texture = owned ? _textures.CardArt(card.Image) : _textures.CardArt("CardBack.jpg"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = owned ? TextureRect.StretchModeEnum.KeepAspectCentered : TextureRect.StretchModeEnum.Scale,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = owned ? Colors.White : new Color(0.55f, 0.55f, 0.55f)
        };
        box.AddChild(art);

        if (copies > 1)
        {
            var count = UiKit.Text($"x{copies}", 11, UiKit.Gold);
            count.HorizontalAlignment = HorizontalAlignment.Right;
            box.AddChild(count);
        }

        void ShowDetail() => _detail.ShowEntry(owned
            ? new CardDetailPanel.Entry(card, null, null, copies == 1 ? "1 copia" : $"{copies} copias")
            : new CardDetailPanel.Entry(null, null, null, "Aún no la tienes"));
        button.MouseEntered += ShowDetail;
        button.FocusEntered += ShowDetail;
        return button;
    }

    private void GoBack() => UiKit.GoTo(this, "res://Scenes/MainMenu/MainMenu.tscn");

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel")) GoBack();
    }
}
