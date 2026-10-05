using Godot;
using GodotGame.Core.Entities;
using GodotGame.Game;
using GodotGame.Graphics;
using System.Collections.Generic;

namespace GodotGame;

/// <summary>
/// Resultado del duelo, al estilo Forbidden Memories: las estadisticas del
/// jugador aparecen una por una con sus puntos, se estampa el rango (S-POW ...
/// S-TEC) y, si ganaste, una carta de recompensa se revela girando (mejor
/// rango, mejor carta). En la campana, ademas, se registra el rival vencido
/// y se anuncia el siguiente. Todo se guarda en <see cref="SaveData"/>.
/// </summary>
public partial class Result : Control
{
    private GameRoot _root = null!;
    private AudioManager _audio = null!;

    public override void _Ready()
    {
        _root = GetNode<GameRoot>("/root/GameRoot");
        _audio = GetNode<AudioManager>("/root/AudioManager");
        var winner = _root.PendingWinner;
        bool won = winner == PlayerSide.Human;
        var stats = _root.LastDuelStats ?? new DuelStats();
        var opponent = _root.PendingOpponent;

        AddChild(new MenuBackground());

        var (title, color) = winner switch
        {
            PlayerSide.Human => ("VICTORIA", new Color(0.45f, 0.9f, 0.55f)),
            PlayerSide.Cpu => ("DERROTA", new Color(0.95f, 0.35f, 0.35f)),
            _ => ("EMPATE", new Color(1f, 0.82f, 0.31f))
        };

        var main = new VBoxContainer();
        main.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        main.OffsetLeft = 40; main.OffsetRight = -40; main.OffsetTop = 24; main.OffsetBottom = -24;
        main.AddThemeConstantOverride("separation", 14);
        AddChild(main);

        main.AddChild(UiKit.Title(title, 64, color));
        if (opponent != null)
        {
            var vs = UiKit.Text($"contra {opponent.Name}, {opponent.Title}", 18);
            vs.HorizontalAlignment = HorizontalAlignment.Center;
            main.AddChild(vs);
        }

        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        columns.AddThemeConstantOverride("separation", 40);
        main.AddChild(columns);

        // ------------------------------------------------ Progreso y recompensa
        var save = _root.Save;
        if (_root.PendingPlayerDeck != null)
            foreach (int id in _root.PendingPlayerDeck.CardIds)
                if (!save.Collection.ContainsKey(id)) save.AddCard(id, 1);

        string rank = DuelRank.RankFor(DuelRank.Points(stats));
        Card? reward = null;
        bool newCard = false;
        string? progressMessage = null;
        if (won)
        {
            save.Wins++;
            reward = PickReward(opponent, DuelRank.Tier(rank));
            if (reward != null)
            {
                newCard = !save.Collection.ContainsKey(reward.Id);
                save.AddCard(reward.Id);
            }
            if (opponent != null && _root.Mode == GameRoot.PlayMode.Campaign && !save.HasBeaten(opponent.Id))
            {
                save.BeatenOpponents.Add(opponent.Id);
                int index = Opponents.Roster.ToList().IndexOf(opponent);
                progressMessage = index + 1 < Opponents.Roster.Count
                    ? $"¡Nuevo rival desbloqueado: {Opponents.Roster[index + 1].Name}!"
                    : "¡CAMPAÑA COMPLETADA! Eres el nuevo campeón.";
            }
        }
        else if (winner == PlayerSide.Cpu) save.Losses++;
        save.Save();

        columns.AddChild(BuildStatsPanel(stats, rank, won));
        columns.AddChild(BuildRewardPanel(reward, newCard, won));

        if (progressMessage != null)
        {
            var progress = UiKit.Title(progressMessage, 26, new Color(1f, 0.85f, 0.4f));
            main.AddChild(progress);
            progress.Modulate = new Color(1, 1, 1, 0);
            var tween = CreateTween();
            tween.TweenInterval(3.2);
            tween.TweenProperty(progress, "modulate:a", 1f, 0.4);
        }

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 16);
        var next = UiKit.MenuButton(opponent != null ? "CONTINUAR" : "NUEVO DUELO",
            () => UiKit.GoTo(this, opponent != null ? "res://Scenes/OpponentSelect/OpponentSelect.tscn" : "res://Scenes/DeckSelection/DeckSelection.tscn"), 240);
        buttons.AddChild(next);
        if (opponent != null)
            buttons.AddChild(UiKit.MenuButton("REVANCHA", () => UiKit.GoTo(this, "res://Scenes/DeckSelection/DeckSelection.tscn"), 240));
        buttons.AddChild(UiKit.MenuButton("MENÚ", () => UiKit.GoTo(this, "res://Scenes/MainMenu/MainMenu.tscn"), 240));
        main.AddChild(buttons);
        next.CallDeferred(Control.MethodName.GrabFocus);
    }

    // ------------------------------------------------------------ Estadisticas

    private Control BuildStatsPanel(DuelStats stats, string rank, bool won)
    {
        var frame = UiKit.Frame();
        frame.CustomMinimumSize = new Vector2(470, 0);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        frame.AddChild(box);

        var header = UiKit.Title("CALIFICACIÓN", 24);
        box.AddChild(header);

        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 24);
        grid.AddThemeConstantOverride("v_separation", 3);
        box.AddChild(grid);

        var lines = DuelRank.Breakdown(stats);
        double delay = 0.4;
        foreach (var line in lines)
        {
            var label = UiKit.Text(line.Label, 17);
            label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var value = UiKit.Text(line.Value, 17, new Color(0.95f, 0.95f, 1f));
            value.HorizontalAlignment = HorizontalAlignment.Right;
            var points = UiKit.Text(line.Points == 0 ? "±0" : line.Points > 0 ? $"+{line.Points}" : line.Points.ToString(), 17,
                line.Points > 0 ? new Color(0.5f, 1f, 0.55f) : line.Points < 0 ? new Color(1f, 0.5f, 0.45f) : new Color(0.65f, 0.65f, 0.7f));
            points.HorizontalAlignment = HorizontalAlignment.Right;
            points.CustomMinimumSize = new Vector2(48, 0);
            foreach (var cell in new[] { label, value, points })
            {
                cell.Modulate = new Color(1, 1, 1, 0);
                grid.AddChild(cell);
            }
            var tween = CreateTween();
            tween.TweenInterval(delay);
            tween.TweenCallback(Callable.From(() => _audio.PlaySfx("lp_tick")));
            foreach (var cell in new[] { label, value, points })
                tween.Parallel().TweenProperty(cell, "modulate:a", 1f, 0.15);
            delay += 0.18;
        }

        int total = DuelRank.Points(stats);
        var totalLabel = UiKit.Text($"Total: {total} puntos (base {DuelRank.BasePoints})", 18, UiKit.Gold);
        totalLabel.HorizontalAlignment = HorizontalAlignment.Right;
        totalLabel.Modulate = new Color(1, 1, 1, 0);
        box.AddChild(totalLabel);

        // El rango se estampa al final (solo cuenta si ganaste, como en FM).
        var stamp = UiKit.Title(won ? rank : "—", 80, won ? (rank.EndsWith("POW") ? new Color(1f, 0.55f, 0.35f) : new Color(0.5f, 0.75f, 1f)) : new Color(0.5f, 0.5f, 0.55f));
        stamp.CustomMinimumSize = new Vector2(0, 100);
        stamp.Modulate = new Color(1, 1, 1, 0);
        box.AddChild(stamp);
        var rankHint = UiKit.Text(won ? (rank.EndsWith("POW") ? "Ganaste con fuerza" : "Ganaste con técnica") : "Sin rango: no ganaste", 15);
        rankHint.HorizontalAlignment = HorizontalAlignment.Center;
        rankHint.Modulate = new Color(1, 1, 1, 0);
        box.AddChild(rankHint);

        var finish = CreateTween();
        finish.TweenInterval(delay + 0.2);
        finish.TweenProperty(totalLabel, "modulate:a", 1f, 0.2);
        finish.TweenCallback(Callable.From(() =>
        {
            stamp.PivotOffset = stamp.Size / 2f;
            stamp.Scale = new Vector2(2.5f, 2.5f);
            _audio.PlaySfx("impact");
        }));
        finish.TweenProperty(stamp, "modulate:a", 1f, 0.12);
        finish.Parallel().TweenProperty(stamp, "scale", Vector2.One, 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        finish.TweenProperty(rankHint, "modulate:a", 1f, 0.2);
        return frame;
    }

    // -------------------------------------------------------------- Recompensa

    private Control BuildRewardPanel(Card? reward, bool newCard, bool won)
    {
        var frame = UiKit.Frame();
        frame.CustomMinimumSize = new Vector2(360, 0);
        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        box.AddThemeConstantOverride("separation", 8);
        frame.AddChild(box);
        box.AddChild(UiKit.Title("RECOMPENSA", 24));

        if (!won || reward == null)
        {
            var none = UiKit.Text(won ? "No hay cartas para ganar." : "Gana el duelo para obtener una carta.", 17);
            none.HorizontalAlignment = HorizontalAlignment.Center;
            box.AddChild(none);
            return frame;
        }

        var textures = new TextureCache(ProjectSettings.GlobalizePath("res://Data/Art"));
        var holder = new Control { CustomMinimumSize = BigCardFactory.CardSize, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        Control cardRoot;
        Control front, back;
        if (reward is MonsterCard monster)
        {
            var view = BigCardFactory.Build(textures, monster, faceDown: true);
            cardRoot = view.Root; front = view.Front; back = view.Back;
        }
        else
        {
            // Magia/Trampa: la carta grande es de monstruo; se muestra su arte con el marco de su tipo.
            cardRoot = new Panel { Size = BigCardFactory.CardSize, PivotOffset = BigCardFactory.CardSize / 2f };
            cardRoot.AddThemeStyleboxOverride("panel", BigCardFactory.CardStyle(CardFrames.FrameColor(reward)));
            front = new TextureRect
            {
                Texture = textures.CardArt(reward.Image),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Position = new Vector2(14, 14),
                Size = BigCardFactory.CardSize - new Vector2(28, 28),
                Visible = false
            };
            back = new TextureRect
            {
                Texture = textures.CardArt("CardBack.jpg"),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                Position = new Vector2(6, 6),
                Size = BigCardFactory.CardSize - new Vector2(12, 12)
            };
            cardRoot.AddChild(front);
            cardRoot.AddChild(back);
        }
        cardRoot.PivotOffset = BigCardFactory.CardSize / 2f;
        holder.AddChild(cardRoot);
        box.AddChild(holder);

        var name = UiKit.Title(reward.Name, 22, new Color(1f, 0.9f, 0.6f));
        name.Modulate = new Color(1, 1, 1, 0);
        box.AddChild(name);
        var tag = UiKit.Text(newCard ? "¡NUEVA CARTA!" : "Otra copia para tu colección", 16, newCard ? new Color(0.5f, 1f, 0.55f) : new Color(0.75f, 0.75f, 0.8f));
        tag.HorizontalAlignment = HorizontalAlignment.Center;
        tag.Modulate = new Color(1, 1, 1, 0);
        box.AddChild(tag);

        // Gira varias veces y se revela.
        var spin = CreateTween();
        spin.TweenInterval(2.6);
        for (int i = 0; i < 3; i++)
        {
            spin.TweenProperty(cardRoot, "scale:x", 0f, 0.09);
            spin.TweenProperty(cardRoot, "scale:x", 1f, 0.09);
        }
        spin.TweenProperty(cardRoot, "scale:x", 0f, 0.12);
        spin.TweenCallback(Callable.From(() =>
        {
            back.Visible = false;
            front.Visible = true;
            _audio.PlaySfx("summon");
        }));
        spin.TweenProperty(cardRoot, "scale:x", 1f, 0.15);
        spin.TweenProperty(name, "modulate:a", 1f, 0.2);
        spin.TweenProperty(tag, "modulate:a", 1f, 0.2);
        return frame;
    }

    /// <summary>
    /// Carta de recompensa del mazo del rival (o de todo el catalogo si no hay
    /// rival): ordenadas de mejor a peor, un rango alto elige entre las mejores
    /// y uno bajo entre las mas modestas.
    /// </summary>
    private Card? PickReward(OpponentProfile? opponent, int tier)
    {
        IEnumerable<Card> source = opponent != null && _root.DeckByName(opponent.DeckName) is { } deck
            ? deck.CardIds.Distinct().Select(id => _root.Data.Cards.Get(id)).Where(c => c != null)!
            : _root.Data.Cards.AllCards;
        var pool = source.OrderByDescending(c => c is MonsterCard m ? m.Attack + m.Defense / 2 : 1500).ToList();
        if (pool.Count == 0) return null;

        (double from, double to) = tier switch
        {
            4 => (0.0, 0.25),
            3 => (0.0, 0.45),
            2 => (0.15, 0.65),
            1 => (0.3, 0.85),
            _ => (0.4, 1.0)
        };
        int start = (int)(pool.Count * from);
        int end = Mathf.Max(start + 1, (int)(pool.Count * to));
        return pool[System.Random.Shared.Next(start, Mathf.Min(end, pool.Count))];
    }
}
