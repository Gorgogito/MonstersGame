using MonstersGame.Core.Entities;
using MonstersGame.Data.Loaders;

namespace MonstersGame.CardEditor;

/// <summary>
/// Previsualizacion de la carta que se esta editando. No es el mismo
/// renderer que usa el juego (ese usa <c>SpriteBatch</c> de MonoGame, un
/// control WinForms no puede reutilizarlo), pero replica la misma paleta de
/// <c>Graphics/Theme.cs</c>, el mismo arte real (Bloque 11: imagen de carta,
/// insignia de Atributo, estrellas de Nivel) y la misma informacion que
/// <c>CardRenderer</c> muestra en partida, para que "como se ve aqui" y "como
/// se ve jugando" no se contradigan. Dibuja directamente los campos del
/// formulario (no una carta de dominio ya validada), asi que sigue mostrando
/// algo razonable mientras la carta todavia esta incompleta.
/// </summary>
public sealed class CardPreviewControl : Panel
{
    private static readonly Color Background = Color.FromArgb(18, 22, 34);
    private static readonly Color PanelBorder = Color.FromArgb(90, 110, 150);
    private static readonly Color SpellColor = Color.FromArgb(50, 110, 95);
    private static readonly Color TrapColor = Color.FromArgb(120, 55, 95);

    // Cuadros de nombre y descripcion (Bloque 14): fondo claro propio, a la manera
    // de una carta real, para que el texto negro resalte sobre el degradado de
    // color de la carta en vez de perderse en el.
    private static readonly Color BoxFill = Color.FromArgb(238, 231, 205);
    private static readonly Color BoxBorder = Color.FromArgb(70, 55, 30);
    private static readonly Color BoxText = Color.Black;
    private static readonly Color ArtMatte = Color.FromArgb(14, 16, 24);

    private CardDto _dto = new();

    /// <summary>Carpeta <c>Data/Art</c> (con subcarpetas Cards/Attributes/Level), o null si todavia no se conoce.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    [System.ComponentModel.Browsable(false)]
    public string? ArtRoot { get; set; }

    private readonly Dictionary<string, Image?> _imageCache = new(StringComparer.OrdinalIgnoreCase);

    public CardPreviewControl()
    {
        DoubleBuffered = true;
        BackColor = Background;
    }

    public void SetCard(CardDto dto)
    {
        _dto = dto;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var cardRect = new Rectangle(20, 20, Width - 40, Height - 40);
        var fill = FillColorFor(_dto);
        // Degradado vertical (mas claro arriba, mas oscuro abajo), igual
        // direccion que Primitives.GradientPanel en el juego.
        using (var fillBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
            cardRect, Lighten(fill, 0.16f), Darken(fill, 0.14f), System.Drawing.Drawing2D.LinearGradientMode.Vertical))
            g.FillRectangle(fillBrush, cardRect);
        using (var borderPen = new Pen(PanelBorder, 3))
            g.DrawRectangle(borderPen, cardRect);

        int pad = 16;
        bool monster = IsMonster(_dto);

        // Insignia de Atributo: reserva su sitio a la derecha del cuadro de
        // nombre pero se dibuja despues, ya que no se superpone con el.
        int nameWidth = cardRect.Width - pad * 2;
        Rectangle? badgeRect = null;
        if (monster)
        {
            const int badgeSize = 40;
            badgeRect = new Rectangle(cardRect.Right - pad - badgeSize, cardRect.Y + pad, badgeSize, badgeSize);
            nameWidth -= badgeSize + 8;
        }

        // Nombre dentro de su propio cuadro con fondo claro, para que el texto
        // resalte sobre el degradado de color de la carta.
        const int nameBoxHeight = 46;
        var nameBoxRect = new Rectangle(cardRect.X + pad, cardRect.Y + pad, nameWidth, nameBoxHeight);
        DrawBox(g, nameBoxRect);
        using (var nameFont = new Font("Segoe UI", 13, FontStyle.Bold))
        using (var nameBrush = new SolidBrush(BoxText))
            g.DrawString(string.IsNullOrWhiteSpace(_dto.Name) ? "(sin nombre)" : _dto.Name,
                nameFont, nameBrush, Rectangle.Inflate(nameBoxRect, -6, -4));

        if (monster && badgeRect.HasValue)
        {
            var attributeArt = LoadImage("Attributes", AttributeFileName(CardDtoMapper.ParseEnum(_dto.Attribute, MonsterAttribute.Dark)));
            if (attributeArt != null)
                g.DrawImage(attributeArt, badgeRect.Value);
        }

        int y = nameBoxRect.Bottom + 8;
        if (monster)
        {
            DrawLevelStars(g, _dto.Level, cardRect.X + pad, y, cardRect.Width - pad * 2);
            y += 24;
        }

        // Cuadro de descripcion, pegado al pie de la carta: titulo (Atributo/Tipo
        // o Magia/Trampa — SubType), cuerpo con la descripcion y, en Monstruos,
        // el pie con ATK/DEF.
        int descBoxHeight = monster ? 118 : 96;
        var descBoxRect = new Rectangle(cardRect.X + pad, cardRect.Bottom - pad - descBoxHeight,
            cardRect.Width - pad * 2, descBoxHeight);
        DrawBox(g, descBoxRect);
        DrawDescriptionBox(g, descBoxRect, monster);

        // Arte de la carta, con su propio marco, en el espacio libre entre el
        // nombre y el cuadro de descripcion.
        var artFrameRect = new Rectangle(cardRect.X + pad, y, cardRect.Width - pad * 2, descBoxRect.Y - 8 - y);
        if (artFrameRect.Height > 10)
        {
            using (var matteBrush = new SolidBrush(ArtMatte))
                g.FillRectangle(matteBrush, artFrameRect);
            using (var artPen = new Pen(BoxBorder, 2))
                g.DrawRectangle(artPen, artFrameRect);

            var art = LoadImage("Cards", _dto.Image);
            if (art != null)
            {
                var artRect = FitSquare(art, artFrameRect.X + 2, artFrameRect.Y + 2,
                    artFrameRect.Width - 4, artFrameRect.Height - 4);
                g.DrawImage(art, artRect);
            }
        }
    }

    /// <summary>Fondo claro + borde de un cuadro de la carta (nombre o descripcion).</summary>
    private static void DrawBox(Graphics g, Rectangle box)
    {
        using (var fillBrush = new SolidBrush(BoxFill))
            g.FillRectangle(fillBrush, box);
        using (var borderPen = new Pen(BoxBorder, 2))
            g.DrawRectangle(borderPen, box);
    }

    /// <summary>Titulo [Atributo/Tipo o Magia-Trampa/SubType], cuerpo con la descripcion y, en Monstruos, pie ATK/DEF — todo en negro, dentro del cuadro de descripcion.</summary>
    private void DrawDescriptionBox(Graphics g, Rectangle box, bool monster)
    {
        var inner = Rectangle.Inflate(box, -8, -6);
        using var textBrush = new SolidBrush(BoxText);
        using var titleFont = new Font("Segoe UI", 9, FontStyle.Bold);
        using var bodyFont = new Font("Segoe UI", 9);
        using var footerFont = new Font("Segoe UI", 10, FontStyle.Bold);

        string title = monster
            ? $"[{_dto.Attribute} / {_dto.Type}]"
            : $"[{(_dto.Kind.Equals("Trap", StringComparison.OrdinalIgnoreCase) ? "Trampa" : "Magia")} / {_dto.SubType}]";
        g.DrawString(title, titleFont, textBrush, inner.X, inner.Y);
        int titleHeight = (int)g.MeasureString(title, titleFont).Height;

        string? footer = monster ? $"ATK {_dto.Attack} / DEF {_dto.Defense}" : null;
        int footerHeight = footer != null ? (int)g.MeasureString(footer, footerFont).Height : 0;
        int footerGap = footer != null ? 4 : 0;

        var descRect = new Rectangle(inner.X, inner.Y + titleHeight + 4, inner.Width,
            Math.Max(0, inner.Height - titleHeight - 4 - footerHeight - footerGap));
        string description = string.IsNullOrWhiteSpace(_dto.Description) ? "(sin descripcion)" : _dto.Description;
        g.DrawString(description, bodyFont, textBrush, descRect);

        if (footer != null)
            g.DrawString(footer, footerFont, textBrush, inner.X, inner.Bottom - footerHeight);
    }

    /// <summary>Rectangulo centrado, tan grande como entre sin deformar la imagen, dentro del area disponible.</summary>
    private static Rectangle FitSquare(Image image, int x, int y, int maxWidth, int maxHeight)
    {
        float scale = Math.Min(maxWidth / (float)image.Width, maxHeight / (float)image.Height);
        int w = (int)(image.Width * scale);
        int h = (int)(image.Height * scale);
        return new Rectangle(x + (maxWidth - w) / 2, y + (maxHeight - h) / 2, w, h);
    }

    /// <summary>Una estrella por punto de Nivel (Bloque 11); sin arte disponible, no dibuja nada (el numero ya se ve en el titulo del cuadro de descripcion).</summary>
    private void DrawLevelStars(Graphics g, int level, int x, int y, int maxWidth)
    {
        var star = LoadImage("Level", "level.png");
        if (star == null || level <= 0) return;

        int size = Math.Clamp(maxWidth / Math.Max(level, 1), 10, 22);
        for (int i = 0; i < level; i++)
            g.DrawImage(star, new Rectangle(x + i * size, y, size, size));
    }

    private Image? LoadImage(string subfolder, string? fileName)
    {
        if (string.IsNullOrEmpty(ArtRoot) || string.IsNullOrWhiteSpace(fileName)) return null;
        string path = Path.Combine(ArtRoot, subfolder, fileName);

        if (_imageCache.TryGetValue(path, out var cached)) return cached;

        Image? image = null;
        if (File.Exists(path))
        {
            try { image = Image.FromFile(path); }
            catch { image = null; } // archivo presente pero ilegible: se degrada sin arte, no interrumpe la edicion
        }

        _imageCache[path] = image;
        return image;
    }

    private static string AttributeFileName(MonsterAttribute attribute) => attribute switch
    {
        MonsterAttribute.Dark => "Oscuridad.png",
        MonsterAttribute.Light => "Luz.png",
        MonsterAttribute.Earth => "Tierra.png",
        MonsterAttribute.Fire => "Fuego.png",
        MonsterAttribute.Water => "Agua.png",
        MonsterAttribute.Wind => "Viento.png",
        MonsterAttribute.Divine => "Divinidad.png",
        _ => "Oscuridad.png"
    };

    private static bool IsMonster(CardDto dto) => dto.Kind.Equals("Monster", StringComparison.OrdinalIgnoreCase);

    private static Color FillColorFor(CardDto dto)
    {
        if (!IsMonster(dto))
            return dto.Kind.Equals("Trap", StringComparison.OrdinalIgnoreCase) ? TrapColor : SpellColor;

        var attribute = CardDtoMapper.ParseEnum(dto.Attribute, MonsterAttribute.Dark);
        return attribute switch
        {
            MonsterAttribute.Dark => Color.FromArgb(70, 55, 95),
            MonsterAttribute.Light => Color.FromArgb(150, 140, 90),
            MonsterAttribute.Earth => Color.FromArgb(95, 80, 55),
            MonsterAttribute.Fire => Color.FromArgb(140, 60, 50),
            MonsterAttribute.Water => Color.FromArgb(50, 90, 130),
            MonsterAttribute.Wind => Color.FromArgb(60, 110, 80),
            MonsterAttribute.Divine => Color.FromArgb(150, 120, 60),
            _ => Color.FromArgb(70, 70, 80)
        };
    }

    /// <summary>Aclara un color hacia blanco, igual que <c>Graphics/Theme.Lighten</c> en el juego (misma direccion de degradado).</summary>
    private static Color Lighten(Color color, float amount) => Blend(color, Color.White, amount);

    /// <summary>Oscurece un color hacia negro, igual que <c>Graphics/Theme.Darken</c> en el juego.</summary>
    private static Color Darken(Color color, float amount) => Blend(color, Color.Black, amount);

    private static Color Blend(Color from, Color to, float amount) => Color.FromArgb(
        (int)(from.R + (to.R - from.R) * amount),
        (int)(from.G + (to.G - from.G) * amount),
        (int)(from.B + (to.B - from.B) * amount));
}
