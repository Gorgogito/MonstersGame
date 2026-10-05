using Godot;
using GodotGame.Graphics;

namespace GodotGame;

/// <summary>
/// Menu principal: fondo animado, titulo y las entradas del juego
/// (Campana, Duelo libre, Biblioteca, Salir). Botones nativos de Godot, asi
/// que foco, hover y teclado (flechas + Enter) ya los resuelve el motor.
/// </summary>
public partial class MainMenu : Control
{
    public override void _Ready()
    {
        GetNode<MusicManager>("/root/MusicManager").Play("menu");
        var root = GetNode<GameRoot>("/root/GameRoot");

        AddChild(new MenuBackground());

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        box.AddThemeConstantOverride("separation", 14);
        center.AddChild(box);

        var title = UiKit.Title("MONSTERS GAME", 72);
        var subtitle = UiKit.Title("Duelo de Monstruos", 24, new Color(0.85f, 0.85f, 0.95f));
        box.AddChild(title);
        box.AddChild(subtitle);
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 30) });

        int beaten = root.Save.BeatenOpponents.Count;
        var campaign = UiKit.MenuButton($"CAMPAÑA  ({beaten}/{Game.Opponents.Roster.Count})", () => Open(root, GameRoot.PlayMode.Campaign));
        var free = UiKit.MenuButton("DUELO LIBRE", () => Open(root, GameRoot.PlayMode.Free));
        var library = UiKit.MenuButton("BIBLIOTECA", () => UiKit.GoTo(this, "res://Scenes/Library/Library.tscn"));
        var quit = UiKit.MenuButton("SALIR", () => GetTree().Quit());
        foreach (var button in new[] { campaign, free, library, quit })
        {
            var holder = new CenterContainer();
            holder.AddChild(button);
            box.AddChild(holder);
        }

        // Entrada: el titulo baja y los botones aparecen escalonados.
        title.PivotOffset = new Vector2(0, 0);
        box.Modulate = new Color(1, 1, 1, 0);
        var tween = CreateTween();
        tween.TweenProperty(box, "modulate:a", 1f, 0.6);

        campaign.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void Open(GameRoot root, GameRoot.PlayMode mode)
    {
        root.Mode = mode;
        UiKit.GoTo(this, "res://Scenes/OpponentSelect/OpponentSelect.tscn");
    }
}
