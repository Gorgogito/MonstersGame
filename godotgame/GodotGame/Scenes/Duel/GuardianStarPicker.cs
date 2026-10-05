using Godot;
using GodotGame.Core.Entities;
using GodotGame.Core.Rules;
using GodotGame.Graphics;
using System;

namespace GodotGame;

/// <summary>
/// Selector de Estrella Guardiana (Forbidden Memories): al Invocar o Colocar
/// un Monstruo, se muestra la carta en grande y sus dos estrellas, cada una
/// con a que estrella vence. Clic en una (o teclas 1/2, flechas + Enter) y se
/// cierra; Esc elige la primera. A diferencia de las otras escenas, no se
/// saltea con un clic cualquiera: hay que elegir.
/// </summary>
public partial class GuardianStarPicker : DuelOverlay
{
    private MonsterCard _card = null!;
    private Action<GuardianStar> _onChosen = null!;
    private readonly Button[] _buttons = new Button[2];
    private int _focused;
    private bool _chosen;

    public void Setup(MonsterCard card, Action<GuardianStar> onChosen, TextureCache textures, AudioManager audio)
    {
        _card = card;
        _onChosen = onChosen;
        SetupBase(textures, audio);
    }

    protected override void Build()
    {
        AddBand(420f);
        ShowCaption("ELIGE LA ESTRELLA GUARDIANA", new Color(1f, 0.9f, 0.6f), delay: 0.05);

        // Carta a la izquierda, un poco mas chica que en la batalla.
        var view = BigCardFactory.Build(Textures, _card, faceDown: false);
        view.Root.Scale = new Vector2(0.85f, 0.85f);
        view.Root.Position = new Vector2(Center.X - 330f, Center.Y - BigCardFactory.CardSize.Y / 2f);
        view.Root.Modulate = new Color(1, 1, 1, 0);
        Stage.AddChild(view.Root);
        CreateTween().TweenProperty(view.Root, "modulate:a", 1f, 0.2);

        var stars = new[] { _card.GuardianStar1, _card.GuardianStar2 };
        for (int i = 0; i < 2; i++)
        {
            var star = stars[i];
            var button = BuildStarButton(star, i + 1);
            button.Position = new Vector2(Center.X - 40f + i * 200f, Center.Y - 120f);
            int captured = i;
            button.Pressed += () => Choose(captured);
            button.MouseEntered += () => SetFocus(captured);
            Stage.AddChild(button);
            _buttons[i] = button;

            button.Scale = new Vector2(0.6f, 0.6f);
            button.Modulate = new Color(1, 1, 1, 0);
            var pop = CreateTween();
            pop.TweenInterval(0.1 + i * 0.08);
            pop.TweenProperty(button, "scale", Vector2.One, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            pop.Parallel().TweenProperty(button, "modulate:a", 1f, 0.15);
        }
        SetFocus(0);
    }

    private Button BuildStarButton(GuardianStar star, int number)
    {
        var size = new Vector2(180, 240);
        var tint = StarGlyphs.Tint(star);
        var button = new Button { Size = size, PivotOffset = size / 2f, FocusMode = FocusModeEnum.None };
        var normal = new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.08f, 0.16f, 0.95f),
            BorderColor = tint.Darkened(0.35f),
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            BorderWidthTop = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12
        };
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BorderColor = tint;
        hover.BgColor = new Color(0.12f, 0.13f, 0.24f, 0.98f);
        hover.ShadowColor = new Color(tint.R, tint.G, tint.B, 0.6f);
        hover.ShadowSize = 12;
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", hover);
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.SetMeta("hover_style", hover);
        button.SetMeta("normal_style", normal);

        var box = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Size = size,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        box.AddThemeConstantOverride("separation", 2);

        var symbol = StarGlyphs.MakeLabel(star, 84);
        var name = MakeLabel(GuardianStars.DisplayName(star).ToUpperInvariant(), 22, tint.Lerp(Colors.White, 0.4f), 5);
        var beats = GuardianStars.BeatsWhich(star);
        var hint = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            Text = $"vence a {StarGlyphs.Bbcode(beats)} {GuardianStars.DisplayName(beats)}"
        };
        hint.AddThemeFontOverride("normal_font", StarGlyphs.Font);
        hint.AddThemeFontSizeOverride("normal_font_size", 14);
        var key = MakeLabel($"[{number}]", 12, new Color(0.7f, 0.7f, 0.75f), 2);

        box.AddChild(symbol);
        box.AddChild(name);
        box.AddChild(hint);
        box.AddChild(key);
        button.AddChild(box);
        return button;
    }

    private void SetFocus(int index)
    {
        _focused = index;
        for (int i = 0; i < _buttons.Length; i++)
        {
            if (_buttons[i] == null) continue;
            var style = (StyleBoxFlat)_buttons[i].GetMeta(i == index ? "hover_style" : "normal_style");
            _buttons[i].AddThemeStyleboxOverride("normal", style);
        }
    }

    private void Choose(int index)
    {
        if (_chosen) return;
        _chosen = true;
        var star = index == 0 ? _card.GuardianStar1 : _card.GuardianStar2;
        Audio.PlaySfx("chain");
        _onChosen(star);

        var chosen = _buttons[index];
        var other = _buttons[1 - index];
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(chosen, "scale", new Vector2(1.15f, 1.15f), 0.15).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(other, "modulate:a", 0.2f, 0.15);
        tween.Chain().TweenInterval(0.25);
        tween.Chain().TweenCallback(Callable.From(() => Finish(0.18)));
    }

    // A diferencia de la base, un clic fuera de las estrellas no cierra nada.
    public override void _GuiInput(InputEvent @event) => AcceptEvent();

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true } key) return;
        GetViewport().SetInputAsHandled();
        if (key.Keycode is Key.Key1 or Key.Kp1) Choose(0);
        else if (key.Keycode is Key.Key2 or Key.Kp2) Choose(1);
        else if (@event.IsActionPressed("ui_left")) SetFocus(0);
        else if (@event.IsActionPressed("ui_right")) SetFocus(1);
        else if (@event.IsActionPressed("ui_accept") || @event.IsActionPressed("ui_select")) Choose(_focused);
        else if (@event.IsActionPressed("ui_cancel")) Choose(0);
    }
}
