using Godot;
using GodotGame.Game;
using GodotGame.Graphics;

namespace GodotGame;

/// <summary>
/// Presentacion previa al duelo: los dos retratos entran desde los costados,
/// aparece un "VS" con destello y el rival dice su frase en un cuadro de
/// dialogo que se escribe letra por letra. Clic/Enter completa el texto o,
/// si ya termino, arranca el duelo con un "¡DUELO!".
/// </summary>
public partial class VsIntro : Control
{
    private const double CharsPerSecond = 40;

    private GameRoot _root = null!;
    private OpponentProfile _opponent = null!;
    private Label _dialogue = null!;
    private Tween? _typing;
    private bool _leaving;

    public override void _Ready()
    {
        _root = GetNode<GameRoot>("/root/GameRoot");
        if (_root.PendingOpponent == null || _root.PendingEngine == null)
        {
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://Scenes/MainMenu/MainMenu.tscn");
            return;
        }
        _opponent = _root.PendingOpponent;
        GetNode<MusicManager>("/root/MusicManager").Play("duel");

        AddChild(new MenuBackground());
        var size = GetViewportRect().Size;
        var center = size / 2f;

        var band = new ColorRect { Color = new Color(0.03f, 0.03f, 0.08f, 0.85f), Position = new Vector2(0, center.Y - 230), Size = new Vector2(size.X, 400), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(band);

        var player = BuildSide(PortraitView.Create("player", UiKit.Gold, 5, 260), "TÚ", "Duelista", UiKit.Gold);
        var rival = BuildSide(PortraitView.Create(_opponent.Id, _opponent.Color, _opponent.HairStyle, 260),
            _opponent.Name.ToUpperInvariant(), _opponent.Title, _opponent.Color.Lerp(Colors.White, 0.3f));
        AddChild(player);
        AddChild(rival);
        var playerRest = new Vector2(center.X - 420, center.Y - 210);
        var rivalRest = new Vector2(center.X + 140, center.Y - 210);
        player.Position = new Vector2(-400, playerRest.Y);
        rival.Position = new Vector2(size.X + 100, rivalRest.Y);

        var vs = UiKit.Title("VS", 110, new Color(1f, 0.85f, 0.4f));
        vs.Size = new Vector2(240, 140);
        vs.Position = new Vector2(center.X - 120, center.Y - 110);
        vs.PivotOffset = vs.Size / 2f;
        vs.Scale = Vector2.Zero;
        AddChild(vs);

        // Cuadro de dialogo del rival.
        var box = UiKit.Frame(_opponent.Color);
        box.Position = new Vector2(center.X - 420, center.Y + 200);
        box.Size = new Vector2(840, 130);
        box.CustomMinimumSize = box.Size;
        var content = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        content.AddChild(UiKit.Text(_opponent.Name, 18, _opponent.Color.Lerp(Colors.White, 0.4f)));
        _dialogue = UiKit.Text("", 22);
        _dialogue.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _dialogue.CustomMinimumSize = new Vector2(800, 60);
        content.AddChild(_dialogue);
        var hint = UiKit.Text("Clic o Enter para continuar", 12, new Color(0.6f, 0.6f, 0.7f));
        hint.HorizontalAlignment = HorizontalAlignment.Right;
        content.AddChild(hint);
        box.AddChild(content);
        box.Modulate = new Color(1, 1, 1, 0);
        AddChild(box);

        var audio = GetNode<AudioManager>("/root/AudioManager");
        var tween = CreateTween();
        tween.TweenProperty(player, "position", playerRest, 0.45).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(rival, "position", rivalRest, 0.45).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenCallback(Callable.From(() => { audio.PlaySfx("impact"); Flash(0.7f); }));
        tween.TweenProperty(vs, "scale", Vector2.One, 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(box, "modulate:a", 1f, 0.2);
        tween.TweenCallback(Callable.From(() => TypeLine(_opponent.Intro)));
    }

    private static Control BuildSide(PortraitView portrait, string name, string subtitle, Color color)
    {
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(280, 0) };
        var holder = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        holder.AddChild(portrait);
        box.AddChild(holder);
        var title = UiKit.Title(name, 36, color);
        box.AddChild(title);
        var sub = UiKit.Text(subtitle, 16);
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(sub);
        return box;
    }

    private void TypeLine(string text)
    {
        _dialogue.Text = text;
        _dialogue.VisibleCharacters = 0;
        _typing = CreateTween();
        _typing.TweenProperty(_dialogue, "visible_characters", text.Length, text.Length / CharsPerSecond);
    }

    private void Flash(float alpha)
    {
        var flash = new ColorRect { Color = new Color(1, 1, 1, alpha), MouseFilter = MouseFilterEnum.Ignore };
        flash.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(flash);
        var tween = flash.CreateTween();
        tween.TweenProperty(flash, "color:a", 0f, 0.35);
        tween.TweenCallback(Callable.From(flash.QueueFree));
    }

    private void Advance()
    {
        if (_leaving) return;
        if (_typing != null && _typing.IsRunning())
        {
            _typing.Kill();
            _dialogue.VisibleCharacters = -1;
            return;
        }
        if (_dialogue.Text.Length == 0) return; // todavia entrando

        _leaving = true;
        GetNode<AudioManager>("/root/AudioManager").PlaySfx("banner");
        var duel = UiKit.Title("¡DUELO!", 120, new Color(1f, 0.9f, 0.5f));
        duel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        duel.VerticalAlignment = VerticalAlignment.Center;
        duel.PivotOffset = GetViewportRect().Size / 2f;
        duel.Scale = new Vector2(2.2f, 2.2f);
        duel.Modulate = new Color(1, 1, 1, 0);
        AddChild(duel);
        Flash(0.8f);
        var tween = CreateTween();
        tween.TweenProperty(duel, "scale", Vector2.One, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(duel, "modulate:a", 1f, 0.15);
        tween.TweenInterval(0.6);
        tween.TweenCallback(Callable.From(() => UiKit.GoTo(this, "res://Scenes/Duel/Duel.tscn")));
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true }) Advance();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_accept") || @event.IsActionPressed("ui_select")) Advance();
    }
}
