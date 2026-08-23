using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonstersGame.Core.AI;
using MonstersGame.Core.Battle;
using MonstersGame.Core.Entities;
using MonstersGame.Core.Rules;
using MonstersGame.Data;
using MonstersGame.Data.Loaders;
using MonstersGame.Graphics;
using MonstersGame.Screens.Duel;
using MonstersGame.Screens.MainMenu;

namespace MonstersGame.Screens.DeckSelection;

/// <summary>
/// Permite al jugador elegir su mazo, con una previsualizacion de sus cartas
/// (al estilo de la lista de mazo de Yu-Gi-Oh! Forbidden Memories) a medida
/// que pasa el mouse por cada opcion. La CPU recibe automaticamente un mazo.
/// Tras confirmar, prepara el duelo y abre la pantalla de Duelo.
/// </summary>
public sealed class DeckSelectionScreen : Screen
{
    private const int RowsPerPage = 10;
    private const int RowHeight = 34;

    private readonly List<DeckDefinition> _decks;
    private int _selected;

    // Cache de la agrupacion de cartas del mazo actualmente previsualizado
    // (una fila por carta distinta, con cuantas copias hay) — se recalcula
    // solo cuando cambia el mazo seleccionado, no en cada fotograma.
    private int _previewDeckIndex = -1;
    private List<(Card Card, int Count)> _previewCards = new();
    private int _page;

    public DeckSelectionScreen(GameContext ctx) : base(ctx)
    {
        _decks = Ctx.Data.Decks.ToList();
    }

    public override void Update(GameTime gameTime)
    {
        var input = Ctx.Input;

        if (input.KeyPressed(Keys.Escape))
        {
            Ctx.Screens.Set(new MainMenuScreen(Ctx));
            return;
        }

        if (_decks.Count == 0) return;

        if (input.KeyPressed(Keys.Down)) _selected = (_selected + 1) % _decks.Count;
        if (input.KeyPressed(Keys.Up)) _selected = (_selected - 1 + _decks.Count) % _decks.Count;
        if (input.KeyPressed(Keys.Enter)) StartDuel(_selected);

        for (int i = 0; i < _decks.Count; i++)
        {
            var rect = DeckRect(i);
            if (input.IsHovering(rect)) _selected = i;
            if (input.ClickedIn(rect)) StartDuel(i);
        }

        EnsurePreviewFor(_selected);

        int maxPage = Math.Max(0, (_previewCards.Count - 1) / RowsPerPage);
        if (input.KeyPressed(Keys.Left)) _page = Math.Max(0, _page - 1);
        if (input.KeyPressed(Keys.Right)) _page = Math.Min(maxPage, _page + 1);
        if (Widgets.Clicked(Ctx, PrevPageRect)) _page = Math.Max(0, _page - 1);
        if (Widgets.Clicked(Ctx, NextPageRect)) _page = Math.Min(maxPage, _page + 1);
        if (input.IsHovering(CardListRect) && input.ScrollDelta != 0)
            _page = Math.Clamp(_page - Math.Sign(input.ScrollDelta), 0, maxPage);
    }

    /// <summary>Recalcula la lista agrupada (carta distinta + cantidad de copias) solo si cambio el mazo previsualizado.</summary>
    private void EnsurePreviewFor(int deckIndex)
    {
        if (deckIndex == _previewDeckIndex) return;
        _previewDeckIndex = deckIndex;
        _page = 0;

        var grouped = new List<(Card Card, int Count)>();
        var indexById = new Dictionary<int, int>();
        foreach (int id in _decks[deckIndex].CardIds)
        {
            var card = Ctx.Data.Cards.Get(id);
            if (card == null) continue;

            if (indexById.TryGetValue(id, out int idx))
                grouped[idx] = (grouped[idx].Card, grouped[idx].Count + 1);
            else
            {
                indexById[id] = grouped.Count;
                grouped.Add((card, 1));
            }
        }
        _previewCards = grouped;
    }

    private void StartDuel(int playerDeckIndex)
    {
        var playerDef = _decks[playerDeckIndex];
        var cpuDef = ChooseCpuDeck(playerDef);

        var human = new Player(PlayerSide.Human, "JUGADOR");
        var cpu = new Player(PlayerSide.Cpu, "CPU");

        human.Deck.AddRange(Ctx.Data.BuildDeck(playerDef));
        cpu.Deck.AddRange(Ctx.Data.BuildDeck(cpuDef));
        GameData.Shuffle(human.Deck, Ctx.Random);
        GameData.Shuffle(cpu.Deck, Ctx.Random);

        var engine = new DuelEngine(new DuelConfig(), Ctx.Data.Fusions);
        var ai = new BasicCpuAI(Ctx.Data.Fusions);

        int firstPlayer = Ctx.Random.Next(2); // 0 = humano, 1 = CPU
        engine.StartDuel(human, cpu, firstPlayer);

        Ctx.Screens.Set(new DuelScreen(Ctx, engine, ai));
    }

    /// <summary>Elige el mazo de la CPU: prefiere el marcado para CPU; si no, otro distinto.</summary>
    private DeckDefinition ChooseCpuDeck(DeckDefinition playerDeck)
    {
        var cpuMarked = _decks.FirstOrDefault(d => d.Name.ToUpperInvariant().Contains("CPU"));
        if (cpuMarked != null) return cpuMarked;

        var different = _decks.Where(d => !ReferenceEquals(d, playerDeck)).ToList();
        if (different.Count > 0) return different[Ctx.Random.Next(different.Count)];

        return playerDeck;
    }

    // ------------------------------------------------------------------- Draw

    public override void Draw(GameTime gameTime)
    {
        var sb = Ctx.SpriteBatch;
        int cx = Ctx.ScreenWidth / 2;

        Ctx.Font.DrawCentered(sb, "SELECCION DE MAZO", cx, 30, Theme.Highlight, 3);
        Ctx.Font.DrawCentered(sb, "ELIGE TU MAZO. LA CPU RECIBIRA UNO AUTOMATICAMENTE.",
            cx, 62, Theme.TextDim, 1);

        if (_decks.Count == 0)
        {
            Ctx.Font.DrawCentered(sb, "NO SE ENCONTRARON MAZOS", cx, 300, Theme.Danger, 2);
            return;
        }

        for (int i = 0; i < _decks.Count; i++)
        {
            var rect = DeckRect(i);
            bool sel = i == _selected;
            Ctx.Primitives.Panel(sb, rect,
                sel ? new Color(60, 80, 120) : new Color(38, 46, 66),
                sel ? Theme.Highlight : Theme.PanelBorder, sel ? 3 : 2);

            Ctx.Font.Draw(sb, _decks[i].Name.ToUpperInvariant(),
                new Vector2(rect.X + 14, rect.Y + 14), Theme.TextPrimary, 2);
            Ctx.Font.Draw(sb, $"{_decks[i].Count} CARTAS",
                new Vector2(rect.X + 14, rect.Y + 40), Theme.TextDim, 1);
        }

        DrawCardListPanel();

        Ctx.Font.DrawCentered(sb, "ENTER O CLIC: COMENZAR   ESC: VOLVER",
            cx, Ctx.ScreenHeight - 24, Theme.TextDim, 1);
    }

    /// <summary>
    /// Lista de cartas del mazo previsualizado (una fila por carta distinta,
    /// con "xN" si hay copias), al estilo de la lista de mazo de Forbidden
    /// Memories: miniatura, nombre, Nivel/Tipo de carta, ATK/DEF e insignia
    /// de Atributo o de Magia/Trampa. Pagina con las flechas o la rueda del mouse.
    /// </summary>
    private void DrawCardListPanel()
    {
        var sb = Ctx.SpriteBatch;
        var panel = CardListRect;
        Ctx.Primitives.Panel(sb, panel, new Color(24, 28, 42), Theme.PanelBorder, 2);

        var deck = _decks[_selected];
        Ctx.Font.Draw(sb, $"{deck.Name.ToUpperInvariant()} — {_previewCards.Count} CARTAS DISTINTAS / {deck.Count} TOTAL",
            new Vector2(panel.X + 12, panel.Y + 10), Theme.TextPrimary, 1);

        if (_previewCards.Count == 0)
        {
            Ctx.Font.Draw(sb, "MAZO VACIO O CARTAS SIN CATALOGAR", new Vector2(panel.X + 12, panel.Y + 34), Theme.TextDim, 1);
            return;
        }

        int firstRow = _page * RowsPerPage;
        int rowY = panel.Y + 34;
        for (int i = firstRow; i < Math.Min(firstRow + RowsPerPage, _previewCards.Count); i++)
        {
            DrawCardRow(new Rectangle(panel.X + 8, rowY, panel.Width - 16, RowHeight - 4), _previewCards[i]);
            rowY += RowHeight;
        }

        int maxPage = Math.Max(0, (_previewCards.Count - 1) / RowsPerPage);
        if (maxPage > 0)
        {
            Widgets.DrawButton(Ctx, PrevPageRect, "< ANTERIOR", _page > 0);
            Widgets.DrawButton(Ctx, NextPageRect, "SIGUIENTE >", _page < maxPage);
            Ctx.Font.DrawCentered(sb, $"PAGINA {_page + 1}/{maxPage + 1}", panel.Center.X, panel.Bottom - 22, Theme.TextDim, 1);
        }
    }

    private void DrawCardRow(Rectangle row, (Card Card, int Count) entry)
    {
        var sb = Ctx.SpriteBatch;
        var card = entry.Card;

        var thumbRect = new Rectangle(row.X, row.Y, RowHeight - 4, RowHeight - 4);
        var art = Ctx.Textures.CardArt(card);
        if (art != null)
            sb.Draw(art, thumbRect, Color.White);
        else
            Ctx.Primitives.DrawBorder(sb, thumbRect, 1, Theme.PanelBorder);

        int x = thumbRect.Right + 8;
        string name = entry.Count > 1 ? $"{card.Name.ToUpperInvariant()} x{entry.Count}" : card.Name.ToUpperInvariant();
        Ctx.Font.Draw(sb, Truncate(name, 330), new Vector2(x, row.Y + 6), Theme.TextPrimary, 1);

        int statsX = row.X + 380;
        if (card is MonsterCard monster)
        {
            Ctx.Font.Draw(sb, $"NV{monster.Level}", new Vector2(statsX, row.Y + 6), Theme.TextDim, 1);
            Ctx.Font.Draw(sb, $"A{monster.Attack}", new Vector2(statsX + 60, row.Y + 6), new Color(255, 180, 120), 1);
            Ctx.Font.Draw(sb, $"D{monster.Defense}", new Vector2(statsX + 130, row.Y + 6), new Color(150, 200, 255), 1);

            var badgeRect = new Rectangle(row.Right - 22, row.Y + 3, 18, 18);
            var attributeArt = Ctx.Textures.AttributeIcon(monster.Attribute);
            if (attributeArt != null)
                sb.Draw(attributeArt, badgeRect, Color.White);
            else
                Ctx.Icons.Draw(sb, IconAtlas.ForAttribute(monster.Attribute), new Vector2(badgeRect.X, badgeRect.Y), Theme.TextPrimary, 2);
        }
        else
        {
            string kindLabel = card.Kind == CardKind.Trap ? "TRAMPA" : "MAGIA";
            Ctx.Font.Draw(sb, kindLabel, new Vector2(statsX, row.Y + 6), Theme.TextDim, 1);
            var icon = card.Kind == CardKind.Trap ? IconKind.Trap : IconKind.Spell;
            Ctx.Icons.Draw(sb, icon, new Vector2(row.Right - 22, row.Y + 3), Theme.TextPrimary, 2);
        }
    }

    private string Truncate(string text, int maxWidth)
    {
        if (Ctx.Font.Measure(text, 1) <= maxWidth) return text;
        while (text.Length > 1 && Ctx.Font.Measure(text + "...", 1) > maxWidth)
            text = text[..^1];
        return text + "...";
    }

    // --------------------------------------------------------------- Geometria

    private Rectangle DeckRect(int index)
    {
        int w = 380, h = 70, gap = 14;
        return new Rectangle(40, 150 + index * (h + gap), w, h);
    }

    private Rectangle CardListRect => new(440, 150, Ctx.ScreenWidth - 480, 490);
    private Rectangle PrevPageRect => new(CardListRect.X + 12, CardListRect.Bottom - 34, 150, 26);
    private Rectangle NextPageRect => new(CardListRect.Right - 162, CardListRect.Bottom - 34, 150, 26);
}
