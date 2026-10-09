using Godot;
using GodotGame.Core.Effects.Monster;
using GodotGame.Core.Entities;
using GodotGame.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GodotGame;

/// <summary>
/// Ventana modal del duelo para las decisiones de los efectos de Monstruo:
/// Si/No ("¿Activas el efecto de X?"), una opcion de una lista ("¿A que
/// Campo?", "¿Que efecto activas?") o elegir cartas (objetivos, que
/// descartar, que buscar en el Deck). Se arma por codigo con el estilo de
/// <see cref="UiKit"/> y bloquea los clics al tablero mientras esta abierta.
/// </summary>
public partial class EffectChoicePanel : Control
{
    private TextureCache _textures = null!;
    private Action<Card?>? _onHover;
    private PanelContainer _window = null!;
    private VBoxContainer _body = null!;

    /// <summary>Lo que se esta mostrando ahora (para no reconstruir la ventana en cada frame).</summary>
    public object? ShownKey { get; private set; }

    public void Setup(TextureCache textures, Action<Card?> onHover)
    {
        _textures = textures;
        _onHover = onHover;

        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 40;
        Visible = false;

        AddChild(new ColorRect { Color = new Color(0, 0, 0, 0.45f), MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 });

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 };
        AddChild(center);
        _window = new PanelContainer { CustomMinimumSize = new Vector2(720, 0) };
        _window.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.05f, 0.06f, 0.13f, 0.97f), UiKit.Gold, 3, glow: true, radius: 12));
        center.AddChild(_window);

        _body = new VBoxContainer();
        _body.AddThemeConstantOverride("separation", 10);
        _window.AddChild(_body);
    }

    public void HidePanel()
    {
        Visible = false;
        ShownKey = null;
    }

    private void Begin(object key, string title, string prompt)
    {
        ShownKey = key;
        foreach (var child in _body.GetChildren()) child.QueueFree();
        _body.AddChild(UiKit.Title(title, 26));
        var promptLabel = UiKit.Text(prompt, 17);
        promptLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        promptLabel.HorizontalAlignment = HorizontalAlignment.Center;
        promptLabel.CustomMinimumSize = new Vector2(680, 0);
        _body.AddChild(promptLabel);
        Visible = true;

        _window.Scale = new Vector2(0.92f, 0.92f);
        _window.PivotOffset = _window.Size / 2f;
        CreateTween().TweenProperty(_window, "scale", Vector2.One, 0.14).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    private HBoxContainer ButtonRow()
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 14);
        _body.AddChild(row);
        return row;
    }

    public void ShowYesNo(object key, string title, string prompt, Action<bool> onAnswer)
    {
        Begin(key, title, prompt);
        var row = ButtonRow();
        row.AddChild(UiKit.MenuButton("SÍ", () => onAnswer(true), 180f));
        row.AddChild(UiKit.MenuButton("NO", () => onAnswer(false), 180f));
    }

    /// <summary>Una lista de opciones; <paramref name="onCancel"/> null = hay que elegir.</summary>
    public void ShowOptions(object key, string title, string prompt, IReadOnlyList<string> options, Action<int> onPick, Action? onCancel = null)
    {
        Begin(key, title, prompt);
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 6);
        for (int i = 0; i < options.Count; i++)
        {
            int captured = i;
            var button = UiKit.MenuButton(options[i], () => onPick(captured), 680f);
            button.AddThemeFontSizeOverride("font_size", 16);
            button.ClipText = true;
            button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            button.TooltipText = options[i];
            list.AddChild(button);
        }

        if (options.Count > 6)
        {
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(700, 380), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            scroll.AddChild(list);
            _body.AddChild(scroll);
        }
        else _body.AddChild(list);

        if (onCancel != null)
            ButtonRow().AddChild(UiKit.MenuButton("CANCELAR", onCancel, 200f));
    }

    /// <summary>Elegir entre <paramref name="min"/> y <paramref name="max"/> cartas.</summary>
    public void ShowCards(object key, string title, string prompt, IReadOnlyList<CardRef> candidates, int min, int max, Action<int[]> onConfirm)
    {
        Begin(key, title, prompt);
        var selected = new List<int>();

        var tiles = new HBoxContainer();
        tiles.AddThemeConstantOverride("separation", 8);
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(Math.Min(940, Math.Max(680, candidates.Count * 142)), 232),
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        scroll.AddChild(tiles);
        _body.AddChild(scroll);

        var footer = ButtonRow();
        var counter = UiKit.Text("", 16);
        var confirm = UiKit.MenuButton("CONFIRMAR", () => onConfirm(selected.ToArray()), 220f);
        footer.AddChild(counter);
        footer.AddChild(confirm);

        void Refresh()
        {
            counter.Text = min == max ? $"Elegidas {selected.Count} / {max}" : $"Elegidas {selected.Count} (entre {min} y {max})";
            confirm.Disabled = selected.Count < min || selected.Count > max;
            for (int i = 0; i < tiles.GetChildCount(); i++)
                if (tiles.GetChild(i) is Button tile)
                    tile.Modulate = selected.Contains(i) ? new Color(1f, 0.85f, 0.4f) : Colors.White;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            int captured = i;
            var candidate = candidates[i];
            bool hidden = candidate.Side == PlayerSide.Cpu && candidate.Zone is CardZone.Hand or CardZone.Deck;
            var tile = BuildTile(candidate, hidden);
            tile.Pressed += () =>
            {
                if (selected.Contains(captured)) selected.Remove(captured);
                else if (max == 1) { selected.Clear(); selected.Add(captured); }
                else if (selected.Count < max) selected.Add(captured);
                Refresh();
            };
            if (!hidden) tile.MouseEntered += () => _onHover?.Invoke(candidate.Card);
            tiles.AddChild(tile);
        }
        Refresh();
    }

    private Button BuildTile(CardRef candidate, bool hidden)
    {
        var tile = new Button { CustomMinimumSize = new Vector2(134, 218), FocusMode = FocusModeEnum.None };
        tile.AddThemeStyleboxOverride("normal", UiKit.Box(new Color(0.08f, 0.09f, 0.18f), hidden ? new Color(0.4f, 0.4f, 0.45f) : CardFrames.FrameColor(candidate.Card), 2, radius: 6));
        tile.AddThemeStyleboxOverride("hover", UiKit.Box(new Color(0.14f, 0.12f, 0.26f), UiKit.Gold, 3, glow: true, radius: 6));
        tile.AddThemeStyleboxOverride("pressed", UiKit.Box(new Color(0.2f, 0.15f, 0.3f), UiKit.Gold, 3, radius: 6));

        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1, OffsetLeft = 6, OffsetTop = 6, OffsetRight = -6, OffsetBottom = -6 };
        column.AddThemeConstantOverride("separation", 2);
        column.AddChild(new TextureRect
        {
            Texture = _textures.CardArt(hidden ? "CardBack.jpg" : candidate.Card.Image),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(0, 112),
            MouseFilter = MouseFilterEnum.Ignore
        });

        var name = UiKit.Text(hidden ? "Carta oculta" : candidate.Card.Name, 11);
        name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.CustomMinimumSize = new Vector2(120, 0);
        column.AddChild(name);

        string zone = candidate.Zone switch
        {
            CardZone.MonsterZone => "Campo",
            CardZone.SpellTrapZone or CardZone.FieldZone => "Campo (M/T)",
            _ => CardRef.ZoneName(candidate.Zone)
        };
        string where = $"{zone} · {(candidate.Side == PlayerSide.Human ? "tuya" : "rival")}";
        if (!hidden && candidate.Card is MonsterCard monster) where = $"ATK {monster.Attack} / DEF {monster.Defense}\n{where}";
        var info = UiKit.Text(where, 10, new Color(0.7f, 0.72f, 0.8f));
        info.HorizontalAlignment = HorizontalAlignment.Center;
        info.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        info.CustomMinimumSize = new Vector2(120, 0);
        column.AddChild(info);

        tile.AddChild(column);
        return tile;
    }
}
