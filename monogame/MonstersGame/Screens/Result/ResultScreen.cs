using Microsoft.Xna.Framework;
using MonstersGame.Core.Entities;
using MonstersGame.Graphics;
using MonstersGame.Screens.DeckSelection;
using MonstersGame.Screens.MainMenu;

namespace MonstersGame.Screens.Result;

/// <summary>
/// Pantalla de resultado: muestra Victoria, Derrota o Empate y permite jugar de
/// nuevo o volver al menu principal.
/// </summary>
public sealed class ResultScreen : Screen
{
    private readonly PlayerSide? _winner;

    public ResultScreen(GameContext ctx, PlayerSide? winner) : base(ctx)
    {
        _winner = winner;
    }

    public override void Update(GameTime gameTime)
    {
        if (Widgets.Clicked(Ctx, ButtonRect(0)))
            Ctx.Screens.Set(new DeckSelectionScreen(Ctx));
        if (Widgets.Clicked(Ctx, ButtonRect(1)))
            Ctx.Screens.Set(new MainMenuScreen(Ctx));
    }

    public override void Draw(GameTime gameTime)
    {
        var sb = Ctx.SpriteBatch;
        int cx = Ctx.ScreenWidth / 2;

        (string title, Color color) = _winner switch
        {
            PlayerSide.Human => ("VICTORIA", Theme.Good),
            PlayerSide.Cpu => ("DERROTA", Theme.Danger),
            _ => ("EMPATE", Theme.Highlight)
        };

        Ctx.Font.DrawCentered(sb, title, cx, 180, color, 7);
        Ctx.Font.DrawCentered(sb,
            _winner == PlayerSide.Human ? "HAS REDUCIDO LOS LP DE LA CPU A 0"
            : _winner == PlayerSide.Cpu ? "TUS LP HAN LLEGADO A 0"
            : "AMBOS DUELISTAS CAYERON A LA VEZ",
            cx, 280, Theme.TextDim, 2);

        Widgets.DrawButton(Ctx, ButtonRect(0), "NUEVO DUELO");
        Widgets.DrawButton(Ctx, ButtonRect(1), "MENU PRINCIPAL");
    }

    private Rectangle ButtonRect(int index)
    {
        int w = 300, h = 60, gap = 24;
        int cx = Ctx.ScreenWidth / 2;
        int startY = 400;
        return new Rectangle(cx - w / 2, startY + index * (h + gap), w, h);
    }
}
