using Godot;
using GodotGame.Game;
using GodotGame.Graphics;

namespace GodotGame;

/// <summary>
/// Seleccion de rival. En la campana es una escalera: cada rival se
/// desbloquea al vencer al anterior, y los bloqueados se ven como silueta. En
/// duelo libre estan todos disponibles. Al elegir, se pasa a Seleccion de Mazo.
/// </summary>
public partial class OpponentSelect : Control
{
    private GameRoot _root = null!;

    public override void _Ready()
    {
        _root = GetNode<GameRoot>("/root/GameRoot");
        GetNode<MusicManager>("/root/MusicManager").Play("menu");
        bool campaign = _root.Mode == GameRoot.PlayMode.Campaign;

        AddChild(new MenuBackground());

        var layout = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        layout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        layout.AddThemeConstantOverride("separation", 18);
        AddChild(layout);

        layout.AddChild(UiKit.Title(campaign ? "CAMPAÑA" : "DUELO LIBRE", 52));
        var hint = UiKit.Text(campaign
            ? "Vence a cada rival para desafiar al siguiente."
            : "Elige a cualquier rival.", 18);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        layout.AddChild(hint);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 16);
        layout.AddChild(row);

        Button? firstAvailable = null;
        for (int i = 0; i < Opponents.Roster.Count; i++)
        {
            var opponent = Opponents.Roster[i];
            bool unlocked = !campaign || _root.Save.IsUnlocked(opponent);
            var card = BuildOpponentCard(opponent, i + 1, unlocked, _root.Save.HasBeaten(opponent.Id));
            row.AddChild(card);
            if (unlocked && (firstAvailable == null || (campaign && !_root.Save.HasBeaten(opponent.Id)))) firstAvailable = card;

            card.Modulate = new Color(1, 1, 1, 0);
            var tween = CreateTween();
            tween.TweenInterval(0.05 + i * 0.07);
            tween.TweenProperty(card, "modulate:a", 1f, 0.3);
        }

        var back = new CenterContainer();
        back.AddChild(UiKit.MenuButton("VOLVER", GoBack, 220));
        layout.AddChild(back);

        firstAvailable?.CallDeferred(Control.MethodName.GrabFocus);
    }

    private Button BuildOpponentCard(OpponentProfile opponent, int number, bool unlocked, bool beaten)
    {
        var button = new Button { CustomMinimumSize = new Vector2(220, 360), Disabled = !unlocked };
        var border = beaten ? new Color(0.45f, 0.9f, 0.5f) : opponent.Color;
        button.AddThemeStyleboxOverride("normal", UiKit.Box(UiKit.Panel, border.Darkened(0.35f), 2, radius: 14));
        button.AddThemeStyleboxOverride("hover", UiKit.Box(new Color(0.1f, 0.1f, 0.2f, 0.95f), border, 3, glow: true, radius: 14));
        button.AddThemeStyleboxOverride("focus", UiKit.Box(new Color(0.1f, 0.1f, 0.2f, 0.95f), border, 3, glow: true, radius: 14));
        button.AddThemeStyleboxOverride("pressed", UiKit.Box(new Color(0.15f, 0.12f, 0.25f, 0.95f), border, 3, radius: 14));
        button.AddThemeStyleboxOverride("disabled", UiKit.Box(new Color(0.04f, 0.04f, 0.06f, 0.85f), new Color(0.25f, 0.25f, 0.3f), 2, radius: 14));
        button.Pressed += () => Choose(opponent);

        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        box.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        box.AddThemeConstantOverride("separation", 6);
        button.AddChild(box);

        box.AddChild(Centered(UiKit.Text($"RIVAL {number}", 13, new Color(0.7f, 0.7f, 0.8f))));

        var portrait = PortraitView.Create(opponent.Id, opponent.Color, opponent.HairStyle, 150);
        if (!unlocked) portrait.Modulate = new Color(0, 0, 0, 0.85f); // silueta
        var portraitHolder = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        portraitHolder.AddChild(portrait);
        box.AddChild(portraitHolder);

        box.AddChild(Centered(UiKit.Title(unlocked ? opponent.Name.ToUpperInvariant() : "???", 26, unlocked ? opponent.Color.Lerp(Colors.White, 0.4f) : new Color(0.5f, 0.5f, 0.55f))));
        box.AddChild(Centered(UiKit.Text(unlocked ? opponent.Title : "Bloqueado", 14)));
        if (unlocked)
        {
            box.AddChild(Centered(UiKit.Text($"Mazo: {opponent.DeckName}", 12, new Color(0.7f, 0.75f, 0.85f))));
            if (opponent.StartingLifePoints != 8000)
                box.AddChild(Centered(UiKit.Text($"{opponent.StartingLifePoints} LP", 13, new Color(1f, 0.6f, 0.5f))));
        }
        if (beaten)
            box.AddChild(Centered(UiKit.Text("VENCIDO", 15, new Color(0.5f, 1f, 0.55f))));
        return button;
    }

    private static Label Centered(Label label)
    {
        label.HorizontalAlignment = HorizontalAlignment.Center;
        return label;
    }

    private void Choose(OpponentProfile opponent)
    {
        GetNode<AudioManager>("/root/AudioManager").PlaySfx("chain");
        _root.PendingOpponent = opponent;
        UiKit.GoTo(this, "res://Scenes/DeckSelection/DeckSelection.tscn");
    }

    private void GoBack() => UiKit.GoTo(this, "res://Scenes/MainMenu/MainMenu.tscn");

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel")) GoBack();
    }
}
