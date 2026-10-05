using Godot;
using GodotGame.Core.Battle;
using GodotGame.Core.Entities;
using GodotGame.Core.Rules;
using GodotGame.Graphics;

namespace GodotGame;

/// <summary>
/// Pantalla de batalla a pantalla completa (estilo Forbidden Memories): al
/// resolverse un ataque, las cartas involucradas aparecen en grande sobre una
/// franja oscura con sus valores de combate, el atacante embiste, y la
/// perdedora se rompe en pedazos. Es puramente visual: el motor ya resolvio la
/// batalla, todo sale del <see cref="AttackInfo"/> capturado en ese momento.
/// </summary>
public partial class BattleOverlay : DuelOverlay
{
    private static readonly Vector2 BigCardSize = BigCardFactory.CardSize;
    private const float CardGap = 130f;

    private static readonly Color AtkColor = new(1f, 0.66f, 0.3f);
    private static readonly Color DefColor = new(0.5f, 0.78f, 1f);
    private static readonly Color DamageColor = new(1f, 0.32f, 0.28f);

    /// <summary>Carta grande de la batalla: la vista de <see cref="BigCardFactory"/> mas el valor de combate que se muestra debajo.</summary>
    private sealed class BigCard
    {
        public BigCardFactory.View View = null!;
        public Label Value = null!;
        public bool IsHuman;
        public string StatName = "";
        public Label? Star;
        public Control Root => View.Root;
    }

    private AttackInfo _attack;

    public void Setup(AttackInfo attack, TextureCache textures, AudioManager audio)
    {
        _attack = attack;
        SetupBase(textures, audio);
    }

    protected override void Build()
    {
        AddBand();
        if (_attack.DefenderCard == null)
            PlayDirectAttack();
        else
            PlayMonsterBattle();
    }

    // ------------------------------------------------------------ Secuencias

    private void PlayMonsterBattle()
    {
        bool humanAttacks = _attack.AttackerSide == PlayerSide.Human;
        var attackerCard = _attack.AttackerCard;
        var defenderCard = _attack.DefenderCard!;
        bool defenderDefending = _attack.DefenderPosition is BattlePosition.DefenseFaceUp or BattlePosition.DefenseFaceDown;

        // El jugador siempre a la izquierda y la CPU a la derecha, ataque quien ataque.
        float cardY = Center.Y - BigCardSize.Y / 2f - 18f;
        var leftPos = new Vector2(Center.X - CardGap / 2f - BigCardSize.X, cardY);
        var rightPos = new Vector2(Center.X + CardGap / 2f, cardY);

        var attacker = BuildBigCard(attackerCard, humanAttacks, faceDown: false, "ATK",
            _attack.AttackerValue - _attack.AttackerStarBonus, AtkColor, _attack.AttackerStar);
        var defender = BuildBigCard(defenderCard, !humanAttacks, faceDown: _attack.DefenderWasFaceDown,
            defenderDefending ? "DEF" : "ATK", _attack.DefenderValue - _attack.DefenderStarBonus,
            defenderDefending ? DefColor : AtkColor, _attack.DefenderStar);

        var attackerPos = humanAttacks ? leftPos : rightPos;
        var defenderPos = humanAttacks ? rightPos : leftPos;
        SlideIn(attacker, attackerPos, delay: 0.08);
        SlideIn(defender, defenderPos, delay: 0.14);

        Timeline = CreateTween();
        Timeline.TweenInterval(0.55);

        if (_attack.DefenderWasFaceDown)
        {
            Timeline.TweenCallback(Callable.From(() =>
            {
                Flip(defender.View);
                if (defender.Star != null) defender.Star.Visible = true;
            }));
            Timeline.TweenInterval(0.32);
        }

        Timeline.TweenCallback(Callable.From(() => { PopValue(attacker); PopValue(defender); }));
        Timeline.TweenInterval(0.5);

        // Ventaja de Estrella Guardiana: el ganador suma +500 a la vista.
        if (_attack.AttackerStarBonus > 0 || _attack.DefenderStarBonus > 0)
        {
            bool attackerWins = _attack.AttackerStarBonus > 0;
            var winner = attackerWins ? attacker : defender;
            var loser = attackerWins ? defender : attacker;
            var winnerStar = attackerWins ? _attack.AttackerStar : _attack.DefenderStar!.Value;
            var loserStar = attackerWins ? _attack.DefenderStar!.Value : _attack.AttackerStar;
            int finalValue = attackerWins ? _attack.AttackerValue : _attack.DefenderValue;
            Timeline.TweenCallback(Callable.From(() => ShowStarAdvantage(winner, loser, winnerStar, loserStar, finalValue)));
            Timeline.TweenInterval(0.9);
        }

        // Embestida: el atacante se lanza contra el defensor y rebota.
        float direction = Mathf.Sign(defenderPos.X - attackerPos.X);
        var lungePos = attackerPos + new Vector2(direction * (CardGap + BigCardSize.X * 0.3f), -10f);
        Timeline.TweenCallback(Callable.From(() => Audio.PlaySfx("attack")));
        Timeline.TweenProperty(attacker.Root, "position", lungePos, 0.14)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        Timeline.Parallel().TweenProperty(attacker.Root, "rotation_degrees", direction * 8f, 0.14);

        var contact = new Vector2(Center.X, cardY + BigCardSize.Y / 2f);
        Timeline.TweenCallback(Callable.From(() => Impact(contact, new Color(1f, 0.8f, 0.35f))));
        Timeline.TweenProperty(attacker.Root, "position", attackerPos, 0.22)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        Timeline.Parallel().TweenProperty(attacker.Root, "rotation_degrees", 0f, 0.22);

        Timeline.TweenCallback(Callable.From(() => ShowOutcome(attacker, defender)));
        Timeline.TweenInterval(1.35);
        Timeline.TweenCallback(Callable.From(() => Finish()));
    }

    private void PlayDirectAttack()
    {
        bool humanAttacks = _attack.AttackerSide == PlayerSide.Human;
        var attacker = BuildBigCard(_attack.AttackerCard, humanAttacks, faceDown: false, "ATK", _attack.AttackerValue, AtkColor,
            _attack.AttackerStar);
        var restPos = new Vector2(Center.X - BigCardSize.X / 2f, Center.Y - BigCardSize.Y / 2f - 18f);
        SlideIn(attacker, restPos, delay: 0.08);

        ShowCaption("¡ATAQUE DIRECTO!", Colors.White, delay: 0.25);

        Timeline = CreateTween();
        Timeline.TweenInterval(0.5);
        Timeline.TweenCallback(Callable.From(() => PopValue(attacker)));
        Timeline.TweenInterval(0.45);

        // El rival esta "arriba" para el jugador y "abajo" para la CPU, como en el tablero.
        float direction = humanAttacks ? -1f : 1f;
        var lungePos = restPos + new Vector2(0, direction * 150f);
        Timeline.TweenCallback(Callable.From(() => Audio.PlaySfx("attack")));
        Timeline.TweenProperty(attacker.Root, "position", lungePos, 0.15)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        Timeline.Parallel().TweenProperty(attacker.Root, "scale", new Vector2(1.18f, 1.18f), 0.15);

        var hitPoint = new Vector2(Center.X, Center.Y + direction * 200f);
        Timeline.TweenCallback(Callable.From(() =>
        {
            Impact(hitPoint, new Color(1f, 0.45f, 0.3f));
            // Debajo de la franja: arriba ya esta el titulo "¡ATAQUE DIRECTO!".
            ShowDamage(_attack.DamageToDefender, new Vector2(Center.X, Center.Y + 315f),
                humanAttacks ? "CPU" : "JUGADOR");
        }));
        Timeline.TweenProperty(attacker.Root, "position", restPos, 0.25)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        Timeline.Parallel().TweenProperty(attacker.Root, "scale", Vector2.One, 0.25);

        Timeline.TweenInterval(1.2);
        Timeline.TweenCallback(Callable.From(() => Finish()));
    }

    private void ShowOutcome(BigCard attacker, BigCard defender)
    {
        var a = _attack;
        string caption;
        Color captionColor = Colors.White;

        if (a.AttackerDestroyed && a.DefenderDestroyed) { caption = "¡AMBOS DESTRUIDOS!"; captionColor = DamageColor; }
        else if (a.DefenderDestroyed) caption = "¡DESTRUIDO!";
        else if (a.AttackerDestroyed) { caption = "¡CONTRAATAQUE!"; captionColor = DamageColor; }
        else if (a.DamageToAttacker > 0) { caption = "¡BLOQUEADO!"; captionColor = DefColor; }
        else caption = "EMPATE";
        ShowCaption(caption, captionColor, delay: 0);

        if (a.DefenderDestroyed) Shatter(defender);
        if (a.AttackerDestroyed) Shatter(attacker);
        if (a.AttackerDestroyed || a.DefenderDestroyed) Audio.PlaySfx("destroy_battle");

        // Defensa que aguanta: el atacante sale despedido hacia atras y vuelve.
        if (!a.AttackerDestroyed && !a.DefenderDestroyed)
        {
            float away = Mathf.Sign(attacker.Root.Position.X - defender.Root.Position.X);
            var rest = attacker.Root.Position;
            var knock = CreateTween();
            knock.TweenProperty(attacker.Root, "position", rest + new Vector2(away * 36f, 0), 0.1)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            knock.TweenProperty(attacker.Root, "position", rest, 0.25)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        }

        // Sobre el arte de la carta (o donde estaba, si se rompio).
        if (a.DamageToDefender > 0)
            ShowDamage(a.DamageToDefender, CardCenter(defender) - new Vector2(0, 20f), null);
        if (a.DamageToAttacker > 0)
            ShowDamage(a.DamageToAttacker, CardCenter(attacker) - new Vector2(0, 20f), null);
    }

    // --------------------------------------------------------------- Piezas

    private BigCard BuildBigCard(MonsterCard card, bool isHuman, bool faceDown, string statName, int statValue, Color statColor,
        GuardianStar? star = null)
    {
        var view = BigCardFactory.Build(Textures, card, faceDown);

        var side = MakeLabel(isHuman ? "JUGADOR" : "CPU", 16, new Color(0.9f, 0.85f, 0.7f), 4);
        side.Position = new Vector2(0, -28);
        side.Size = new Vector2(BigCardSize.X, 24);
        view.Root.AddChild(side);

        // Estrella Guardiana en uso, sobre la esquina superior de la carta.
        Label? starLabel = null;
        if (star is { } guardian)
        {
            starLabel = StarGlyphs.MakeLabel(guardian, 30);
            starLabel.Position = new Vector2(BigCardSize.X - 26, -34);
            starLabel.Size = new Vector2(40, 40);
            starLabel.PivotOffset = starLabel.Size / 2f;
            starLabel.Visible = !faceDown; // boca abajo no se revela hasta voltearse
            view.Root.AddChild(starLabel);
        }

        // El valor de combate va fuera de la carta, debajo: es el numero que decide la batalla.
        var value = MakeLabel($"{statName} {statValue}", 34, statColor, 9);
        value.Size = new Vector2(BigCardSize.X + 80, 46);
        value.PivotOffset = value.Size / 2f;
        value.Modulate = new Color(1, 1, 1, 0);
        Stage.AddChild(view.Root);
        Stage.AddChild(value);

        return new BigCard { View = view, Value = value, IsHuman = isHuman, StatName = statName, Star = starLabel };
    }

    private static Vector2 CardCenter(BigCard card) => card.Root.Position + BigCardSize / 2f;

    // ----------------------------------------------------------- Animaciones

    private void SlideIn(BigCard card, Vector2 restPos, double delay)
    {
        // Cada carta entra desde el lado de su duenio (jugador por la izquierda, CPU por la derecha).
        float startX = card.IsHuman ? -BigCardSize.X - 40f : ViewportSize.X + 40f;
        card.Root.Position = new Vector2(startX, restPos.Y);
        card.Value.Position = new Vector2(restPos.X - 40f, restPos.Y + BigCardSize.Y + 8f);

        var tween = CreateTween();
        tween.TweenInterval(delay);
        tween.TweenProperty(card.Root, "position", restPos, 0.36)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    private void PopValue(BigCard card)
    {
        card.Value.Scale = new Vector2(1.7f, 1.7f);
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(card.Value, "modulate:a", 1f, 0.12);
        tween.TweenProperty(card.Value, "scale", Vector2.One, 0.22)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    /// <summary>
    /// La estrella ganadora late, aparece "☉ vence a ☽" entre las cartas y un
    /// "+500" sube desde el valor de combate, que pasa a mostrar el total.
    /// </summary>
    private void ShowStarAdvantage(BigCard winner, BigCard loser, GuardianStar winnerStar, GuardianStar loserStar, int finalValue)
    {
        Audio.PlaySfx("chain");

        if (winner.Star != null)
        {
            var pulse = CreateTween();
            pulse.TweenProperty(winner.Star, "scale", new Vector2(1.8f, 1.8f), 0.15).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            pulse.TweenProperty(winner.Star, "scale", Vector2.One, 0.25);
        }
        if (loser.Star != null)
            CreateTween().TweenProperty(loser.Star, "modulate:a", 0.35f, 0.3);

        var versus = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.Off,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            Text = $"{StarGlyphs.Bbcode(winnerStar)} vence a {StarGlyphs.Bbcode(loserStar)}",
            Size = new Vector2(240, 40),
            Position = new Vector2(Center.X - 120f, Center.Y - 70f),
            Modulate = new Color(1, 1, 1, 0)
        };
        versus.AddThemeFontOverride("normal_font", StarGlyphs.Font);
        versus.AddThemeFontSizeOverride("normal_font_size", 20);
        versus.AddThemeConstantOverride("outline_size", 6);
        versus.AddThemeColorOverride("font_outline_color", Colors.Black);
        Stage.AddChild(versus);
        var show = CreateTween();
        show.TweenProperty(versus, "modulate:a", 1f, 0.15);
        show.TweenInterval(0.6);
        show.TweenProperty(versus, "modulate:a", 0f, 0.3);

        var plus = MakeLabel($"+{GuardianStars.Bonus}", 30, new Color(0.5f, 1f, 0.55f), 8);
        plus.Size = new Vector2(120, 40);
        plus.Position = winner.Value.Position + new Vector2(winner.Value.Size.X / 2f - 60f, 0);
        Stage.AddChild(plus);
        var rise = CreateTween();
        rise.TweenProperty(plus, "position:y", plus.Position.Y - 46f, 0.6).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        rise.Parallel().TweenProperty(plus, "modulate:a", 0f, 0.3).SetDelay(0.4);

        var update = CreateTween();
        update.TweenInterval(0.3);
        update.TweenCallback(Callable.From(() =>
        {
            winner.Value.Text = $"{winner.StatName} {finalValue}";
            winner.Value.Scale = new Vector2(1.4f, 1.4f);
            CreateTween().TweenProperty(winner.Value, "scale", Vector2.One, 0.2).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }));
    }

    private void Impact(Vector2 point, Color color)
    {
        Audio.PlaySfx("impact");
        Flash(0.75f, 0.3);
        CardFx.Burst(Stage, point, color, amount: 40, speed: 380f);
        Shake(12f, 0.3f);
    }

    private void ShowDamage(int amount, Vector2 center, string? target)
    {
        if (amount <= 0) return;
        var label = MakeLabel(target != null ? $"-{amount}  {target}" : $"-{amount}", 44, DamageColor, 10);
        label.Size = new Vector2(360, 56);
        label.PivotOffset = label.Size / 2f;
        label.Position = center - label.Size / 2f;
        label.Scale = new Vector2(0.3f, 0.3f);
        Stage.AddChild(label);

        var tween = CreateTween();
        tween.TweenProperty(label, "scale", Vector2.One, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(label, "position:y", label.Position.Y - 40f, 0.9)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        Audio.PlaySfx("damage");
    }

    /// <summary>La carta grande se rompe en fragmentos (ver <see cref="CardFx.Shatter"/>); el arte se dibuja estirado a <see cref="BigCardFactory.ArtRect"/>, asi que el recorte es exacto.</summary>
    private void Shatter(BigCard card)
    {
        card.Root.Visible = false;
        CreateTween().TweenProperty(card.Value, "modulate:a", 0f, 0.3);
        CardFx.Shatter(Stage, CardCenter(card), BigCardSize, 0f,
            new CardFx.CardLook(card.View.Art, BigCardFactory.ArtRect, card.View.Frame, Border: 5f), cols: 5, rows: 7, spread: 250f, zIndex: 0);
    }
}
