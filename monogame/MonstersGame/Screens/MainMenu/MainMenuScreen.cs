using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonstersGame.Graphics;
using MonstersGame.Screens.DeckSelection;

namespace MonstersGame.Screens.MainMenu;

/// <summary>
/// Menu principal. Opciones: Nuevo Duelo y Salir (segun lo solicitado).
/// </summary>
public sealed class MainMenuScreen : Screen
{
    private readonly string[] _options = { "NUEVO DUELO", "SALIR" };
    private int _selected;

    public MainMenuScreen(GameContext ctx) : base(ctx) { }

    public override void Update(GameTime gameTime)
    {
        var input = Ctx.Input;

        if (input.KeyPressed(Keys.Down)) _selected = (_selected + 1) % _options.Length;
        if (input.KeyPressed(Keys.Up)) _selected = (_selected - 1 + _options.Length) % _options.Length;
        if (input.KeyPressed(Keys.Enter)) Activate(_selected);

        // Soporte de raton: hover selecciona, clic activa.
        for (int i = 0; i < _options.Length; i++)
        {
            var rect = OptionRect(i);
            if (input.IsHovering(rect)) _selected = i;
            if (input.ClickedIn(rect)) Activate(i);
        }
    }

    private void Activate(int option)
    {
        if (option == 0)
            Ctx.Screens.Set(new DeckSelectionScreen(Ctx));
        else
            Ctx.RequestExit();
    }

    public override void Draw(GameTime gameTime)
    {
        var sb = Ctx.SpriteBatch;
        int cx = Ctx.ScreenWidth / 2;

        Ctx.Font.DrawCentered(sb, "MONSTERS GAME", cx, 110, Theme.Highlight, 6);
        Ctx.Font.DrawCentered(sb, "DUELO DE MONSTRUOS", cx, 180, Theme.TextDim, 2);

        for (int i = 0; i < _options.Length; i++)
        {
            var rect = OptionRect(i);
            bool sel = i == _selected;
            Ctx.Primitives.Panel(sb, rect,
                sel ? new Color(70, 90, 130) : new Color(40, 48, 70),
                sel ? Theme.Highlight : Theme.PanelBorder, sel ? 3 : 2);
            Ctx.Font.DrawCentered(sb, _options[i], rect.Center.X,
                rect.Center.Y - Ctx.Font.LineHeight(3) / 2,
                sel ? Theme.TextPrimary : Theme.TextDim, 3);
        }

        Ctx.Font.DrawCentered(sb, "FLECHAS O RATON PARA NAVEGAR  -  ENTER PARA ELEGIR",
            cx, Ctx.ScreenHeight - 60, Theme.TextDim, 1);
    }

    private Rectangle OptionRect(int index)
    {
        int w = 360, h = 64, gap = 24;
        int cx = Ctx.ScreenWidth / 2;
        int startY = 300;
        return new Rectangle(cx - w / 2, startY + index * (h + gap), w, h);
    }
}
