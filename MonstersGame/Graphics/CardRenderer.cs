using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonstersGame.Core.Entities;

namespace MonstersGame.Graphics;

/// <summary>
/// Dibuja cartas como placeholders (rectangulos coloreados con texto). Centraliza
/// la representacion visual de una carta para que las pantallas no la dupliquen.
/// </summary>
public sealed class CardRenderer
{
    private readonly Primitives _primitives;
    private readonly BitmapFont _font;
    private readonly IconAtlas _icons;
    private readonly TextureCache _textures;

    public CardRenderer(Primitives primitives, BitmapFont font, IconAtlas icons, TextureCache textures)
    {
        _primitives = primitives;
        _font = font;
        _icons = icons;
        _textures = textures;
    }

    /// <summary>
    /// Dibuja una carta de monstruo boca arriba dentro del rectangulo. En
    /// Posicion de Defensa (Bloque 17) se ve rotada 90 grados y un poco mas
    /// chica para no invadir las zonas vecinas — igual que en Yu-Gi-Oh!
    /// Forbidden Memories, donde la orientacion misma de la carta comunica
    /// Ataque/Defensa. <paramref name="scale"/> &lt; 1 encoge la carta
    /// alrededor de su centro (usado para el efecto de destruccion del
    /// choque de batalla); no tiene relacion con la Posicion.
    /// </summary>
    public void DrawMonster(SpriteBatch sb, MonsterCard card, Rectangle rect,
        bool highlighted = false, BattlePosition? position = null, float scale = 1f)
    {
        bool rotate = position == BattlePosition.DefenseFaceUp;
        if (rotate || scale < 0.999f)
        {
            DrawTransformed(sb, rect, rotate, scale,
                () => DrawMonsterContent(sb, card, rect, highlighted, position));
            return;
        }
        DrawMonsterContent(sb, card, rect, highlighted, position);
    }

    private void DrawMonsterContent(SpriteBatch sb, MonsterCard card, Rectangle rect,
        bool highlighted, BattlePosition? position)
    {
        var fill = Theme.AttributeColor(card.Attribute);
        var border = highlighted ? Theme.Highlight : Theme.PanelBorder;
        _primitives.GradientPanel(sb, rect, Theme.Lighten(fill, 0.16f), Theme.Darken(fill, 0.14f), border, highlighted ? 3 : 2);

        int pad = 5;
        const int badgeSize = 14;
        int badgeReserve = badgeSize + 3;

        // Nombre (2 lineas: hay que dejar sitio para el arte de la carta),
        // dejando ademas espacio para la insignia de Atributo arriba a la derecha.
        DrawWrapped(sb, card.Name.ToUpperInvariant(), rect.X + pad, rect.Y + pad,
            rect.Width - pad * 2 - badgeReserve, Theme.TextPrimary, 1, maxLines: 2);

        var attributeArt = _textures.AttributeIcon(card.Attribute);
        var badgeRect = new Rectangle(rect.Right - pad - badgeSize, rect.Y + pad, badgeSize, badgeSize);
        if (attributeArt != null)
            sb.Draw(attributeArt, badgeRect, Color.White);
        else
            _icons.Draw(sb, IconAtlas.ForAttribute(card.Attribute), new Vector2(badgeRect.X, badgeRect.Y - 1), Theme.TextPrimary, 2);

        int levelY = rect.Y + pad + 18;
        DrawLevelStars(sb, card.Level, rect.X + pad, levelY, rect.Width - pad * 2);

        // Arte de la carta (miniatura), si el catalogo tiene una imagen asociada
        // a esta carta — si no, el fondo con degradado por Atributo ya dibujado
        // arriba sigue siendo un placeholder razonable por si solo.
        int artY = levelY + 10;
        int artBottom = rect.Bottom - 26;
        if (artBottom > artY)
        {
            var art = _textures.CardArt(card);
            if (art != null)
                sb.Draw(art, new Rectangle(rect.X + pad, artY, rect.Width - pad * 2, artBottom - artY), Color.White);
        }

        // ATK / DEF.
        _font.Draw(sb, $"A{card.Attack}", new Vector2(rect.X + pad, rect.Bottom - 22),
            new Color(255, 180, 120), 1);
        _font.Draw(sb, $"D{card.Defense}", new Vector2(rect.X + pad, rect.Bottom - 12),
            new Color(150, 200, 255), 1);

        // Indicador de posicion de batalla.
        if (position.HasValue)
        {
            string tag = position.Value == BattlePosition.Attack ? "ATK" : "DEF";
            var tagColor = position.Value == BattlePosition.Attack ? Theme.Danger : Theme.Good;
            int w = _font.Measure(tag, 1);
            _font.Draw(sb, tag, new Vector2(rect.Right - pad - w, rect.Bottom - 12), tagColor, 1);
        }
    }

    /// <summary>Dibuja la fila de estrellas de Nivel (una por punto de Nivel), o el texto "NVx" si falta el arte de la estrella.</summary>
    private void DrawLevelStars(SpriteBatch sb, int level, int x, int y, int maxWidth)
    {
        var star = _textures.LevelStar();
        if (star == null || level <= 0)
        {
            _font.Draw(sb, $"NV{level}", new Vector2(x, y), Theme.TextDim, 1);
            return;
        }

        int size = Math.Clamp(maxWidth / level, 4, 10);
        for (int i = 0; i < level; i++)
            sb.Draw(star, new Rectangle(x + i * size, y, size, size), Color.White);
    }

    /// <summary>Dibuja una Carta Magica o de Trampa boca arriba dentro del rectangulo.</summary>
    public void DrawSpellTrap(SpriteBatch sb, Card card, Rectangle rect, bool highlighted = false)
    {
        var fill = card.Kind == CardKind.Spell ? Theme.SpellColor : Theme.TrapColor;
        var border = highlighted ? Theme.Highlight : Theme.PanelBorder;
        _primitives.GradientPanel(sb, rect, Theme.Lighten(fill, 0.16f), Theme.Darken(fill, 0.14f), border, highlighted ? 3 : 2);

        int pad = 5;
        int iconScale = 2;
        var icon = card.Kind == CardKind.Spell ? IconKind.Spell : IconKind.Trap;
        int iconReserve = _icons.PixelSize(iconScale) + 3;

        DrawWrapped(sb, card.Name.ToUpperInvariant(), rect.X + pad, rect.Y + pad,
            rect.Width - pad * 2 - iconReserve, Theme.TextPrimary, 1, maxLines: 2);
        _icons.Draw(sb, icon, new Vector2(rect.Right - pad - _icons.PixelSize(iconScale), rect.Y + pad - 1), Theme.TextPrimary, iconScale);

        int artY = rect.Y + pad + 18;
        int artBottom = rect.Bottom - 16;
        if (artBottom > artY)
        {
            var art = _textures.CardArt(card);
            if (art != null)
                sb.Draw(art, new Rectangle(rect.X + pad, artY, rect.Width - pad * 2, artBottom - artY), Color.White);
        }

        _font.Draw(sb, SubTypeTag(card), new Vector2(rect.X + pad, rect.Bottom - 14), Theme.TextDim, 1);
    }

    private static string SubTypeTag(Card card) => card switch
    {
        SpellCard { SubType: SpellSubType.Normal } => "MAGIA",
        SpellCard { SubType: SpellSubType.Continuous } => "MAGIA CONT",
        SpellCard { SubType: SpellSubType.Equip } => "MAGIA EQUIP",
        SpellCard { SubType: SpellSubType.Field } => "MAGIA CAMPO",
        SpellCard { SubType: SpellSubType.QuickPlay } => "MAGIA VELOZ",
        SpellCard { SubType: SpellSubType.Ritual } => "MAGIA RITUAL",
        TrapCard { SubType: TrapSubType.Normal } => "TRAMPA",
        TrapCard { SubType: TrapSubType.Continuous } => "TRAMPA CONT",
        TrapCard { SubType: TrapSubType.Counter } => "TRAMPA CONTRA",
        _ => card.Kind.ToString().ToUpperInvariant()
    };

    /// <summary>
    /// Dibuja el reverso de una carta (boca abajo), con la imagen real de
    /// <c>CardBack.jpg</c> si esta disponible (Bloque 17) o el placeholder de
    /// siempre si no. Una carta Colocada (<paramref name="defense"/> = true,
    /// el caso de siempre en el campo) esta en Posicion de Defensa y por eso
    /// se ve rotada 90 grados, igual que un Monstruo boca arriba en Defensa
    /// (ver <see cref="DrawMonster"/>) — sin objetivo/mano no aplica
    /// (<paramref name="defense"/> = false).
    /// </summary>
    public void DrawFaceDown(SpriteBatch sb, Rectangle rect, bool highlighted = false, bool defense = true, float scale = 1f)
    {
        if (defense || scale < 0.999f)
        {
            DrawTransformed(sb, rect, defense, scale, () => DrawFaceDownContent(sb, rect, highlighted));
            return;
        }
        DrawFaceDownContent(sb, rect, highlighted);
    }

    private void DrawFaceDownContent(SpriteBatch sb, Rectangle rect, bool highlighted)
    {
        var border = highlighted ? Theme.Highlight : Theme.PanelBorder;
        var back = _textures.CardBack();
        if (back != null)
        {
            sb.Draw(back, rect, Color.White);
            _primitives.DrawBorder(sb, rect, highlighted ? 3 : 2, border);
        }
        else
        {
            _primitives.GradientPanel(sb, rect, Theme.Lighten(Theme.CardBack, 0.12f), Theme.Darken(Theme.CardBack, 0.16f), border, highlighted ? 3 : 2);
            _font.DrawCentered(sb, "?", rect.Center.X, rect.Center.Y - 6, Theme.TextDim, 3);
        }
    }

    /// <summary>Dibuja una zona vacia.</summary>
    public void DrawEmptyZone(SpriteBatch sb, Rectangle rect, bool highlighted = false)
    {
        var border = highlighted ? Theme.Highlight : new Color(60, 70, 95);
        _primitives.DrawBorder(sb, rect, highlighted ? 3 : 1, border);
    }

    /// <summary>
    /// Dibuja una carta en una Zona de Magia/Trampa (formato compacto, mas bajo
    /// que una carta de Monstruo). Si esta boca abajo y <paramref name="revealFaceDown"/>
    /// es verdadero, se muestra igual el nombre real en vez del reverso generico:
    /// aplica solo a las zonas del propio jugador humano, que siempre sabe que
    /// Coloco (a diferencia de las de la CPU, cuya identidad boca abajo es
    /// informacion que el jugador no deberia poder ver).
    /// </summary>
    public void DrawSpellTrapZoneCard(SpriteBatch sb, SpellTrapInstance? instance, Rectangle rect,
        bool revealFaceDown, bool highlighted = false)
    {
        if (instance == null)
        {
            DrawEmptyZone(sb, rect, highlighted);
            return;
        }

        if (!instance.FaceUp && !revealFaceDown)
        {
            var backBorder = highlighted ? Theme.Highlight : Theme.PanelBorder;
            _primitives.Panel(sb, rect, Theme.CardBack, backBorder, highlighted ? 3 : 2);
            _font.DrawCentered(sb, "?", rect.Center.X, rect.Center.Y - 4, Theme.TextDim, 2);
            return;
        }

        var fill = instance.Card.Kind == CardKind.Spell ? Theme.SpellColor : Theme.TrapColor;
        var border = highlighted ? Theme.Highlight : Theme.PanelBorder;
        _primitives.GradientPanel(sb, rect, Theme.Lighten(fill, 0.16f), Theme.Darken(fill, 0.14f), border, highlighted ? 3 : 2);

        int pad = 4;
        var icon = instance.Card.Kind == CardKind.Spell ? IconKind.Spell : IconKind.Trap;
        DrawWrapped(sb, instance.Card.Name.ToUpperInvariant(), rect.X + pad, rect.Y + pad,
            rect.Width - pad * 2 - (_icons.PixelSize(1) + 2), Theme.TextPrimary, 1, maxLines: 2);
        _icons.Draw(sb, icon, new Vector2(rect.Right - pad - _icons.PixelSize(1), rect.Y + pad), Theme.TextPrimary, 1);

        string tag = instance.FaceUp ? "ACTIVA" : "COLOCADA";
        _font.Draw(sb, tag, new Vector2(rect.X + pad, rect.Bottom - 9), Theme.TextDim, 1);
    }

    /// <summary>Dibuja la Zona del Campo (siempre boca arriba mientras esta activa).</summary>
    public void DrawFieldZone(SpriteBatch sb, SpellTrapInstance? field, Rectangle rect)
    {
        if (field == null)
        {
            DrawEmptyZone(sb, rect);
            _font.DrawCentered(sb, "CAMPO", rect.Center.X, rect.Bottom - 11, new Color(60, 70, 95), 1);
            return;
        }

        _primitives.GradientPanel(sb, rect, Theme.Lighten(Theme.SpellColor, 0.16f), Theme.Darken(Theme.SpellColor, 0.14f), Theme.Highlight, 2);
        int pad = 4;
        DrawWrapped(sb, field.Card.Name.ToUpperInvariant(), rect.X + pad, rect.Y + pad,
            rect.Width - pad * 2 - (_icons.PixelSize(1) + 2), Theme.TextPrimary, 1, maxLines: 2);
        _icons.Draw(sb, IconKind.Spell, new Vector2(rect.Right - pad - _icons.PixelSize(1), rect.Y + pad), Theme.TextPrimary, 1);
        _font.Draw(sb, "CAMPO", new Vector2(rect.X + pad, rect.Bottom - 9), Theme.TextDim, 1);
    }

    /// <summary>Dibuja texto envuelto por palabras dentro de un ancho dado.</summary>
    private void DrawWrapped(SpriteBatch sb, string text, int x, int y, int maxWidth,
        Color color, int scale, int maxLines)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = "";
        int lineY = y;
        int lines = 0;

        foreach (var word in words)
        {
            string candidate = line.Length == 0 ? word : line + " " + word;
            if (_font.Measure(candidate, scale) > maxWidth && line.Length > 0)
            {
                _font.Draw(sb, line, new Vector2(x, lineY), color, scale);
                lineY += _font.LineHeight(scale) + 2;
                line = word;
                if (++lines >= maxLines - 1) break;
            }
            else
            {
                line = candidate;
            }
        }

        if (lines < maxLines && line.Length > 0)
            _font.Draw(sb, line, new Vector2(x, lineY), color, scale);
    }

    /// <summary>
    /// Ejecuta <paramref name="draw"/> (que dibuja normalmente, en las
    /// coordenadas sin rotar de <paramref name="rect"/>) rotado 90 grados
    /// y/o encogido, alrededor del centro de <paramref name="rect"/>. Rotar
    /// asi -con una matriz de transformacion aplicada a todo un sub-batch de
    /// <see cref="SpriteBatch"/>- evita tener que rotar cada trazo del
    /// contenido por separado (panel, texto, arte, insignias). Cuando
    /// <paramref name="rotate"/> ademas se escala para que la carta rotada
    /// quepa dentro del ancho original de <paramref name="rect"/> sin invadir
    /// las zonas vecinas (una carta vertical rotada 90 grados mide, sin
    /// encoger, tanto de ancho como alta era, y viceversa).
    /// </summary>
    private static void DrawTransformed(SpriteBatch sb, Rectangle rect, bool rotate, float scale, Action draw)
    {
        float fitScale = rotate && rect.Height > 0 ? (float)rect.Width / rect.Height : 1f;
        float totalScale = MathHelper.Clamp(scale, 0f, 1f) * fitScale;
        if (totalScale <= 0.001f) return;

        var center = new Vector2(rect.Center.X, rect.Center.Y);
        var transform =
            Matrix.CreateTranslation(-center.X, -center.Y, 0f) *
            Matrix.CreateScale(totalScale) *
            Matrix.CreateRotationZ(rotate ? MathHelper.PiOver2 : 0f) *
            Matrix.CreateTranslation(center.X, center.Y, 0f);

        sb.End();
        sb.Begin(samplerState: SamplerState.PointClamp, transformMatrix: transform);
        draw();
        sb.End();
        sb.Begin(samplerState: SamplerState.PointClamp);
    }
}
