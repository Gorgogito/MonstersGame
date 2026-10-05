using Godot;
using GodotGame.Core.Entities;
using GodotGame.Data.Loaders;

namespace GodotGame.Editor.Controls;

/// <summary>
/// Previsualizacion de la carta que se esta editando. Adaptacion Godot de
/// <c>CardPreviewControl</c> de MonstersGame.CardEditor: mismo diseno visual
/// (cuadro de nombre, insignia de Atributo, estrellas de Nivel, arte, cuadro
/// de descripcion con ATK/DEF), dibujado con <c>_Draw()</c> nativo de Godot
/// en vez de <c>System.Drawing</c>/<c>OnPaint</c>. Dibuja directamente los
/// campos del formulario (no una carta de dominio ya validada), asi que
/// sigue mostrando algo razonable mientras la carta todavia esta incompleta.
/// </summary>
public sealed partial class CardPreview : Control
{
    private static readonly Color PanelBorder = new(90 / 255f, 110 / 255f, 150 / 255f);
    private static readonly Color SpellColor = new(50 / 255f, 110 / 255f, 95 / 255f);
    private static readonly Color TrapColor = new(120 / 255f, 55 / 255f, 95 / 255f);
    private static readonly Color BoxFill = new(238 / 255f, 231 / 255f, 205 / 255f);
    private static readonly Color BoxBorder = new(70 / 255f, 55 / 255f, 30 / 255f);
    private static readonly Color BoxText = Colors.Black;
    private static readonly Color ArtMatte = new(14 / 255f, 16 / 255f, 24 / 255f);

    private CardDto _dto = new();
    private Font _font = null!;

    /// <summary>Carpeta <c>Data/Art</c> (con subcarpetas Cards/Attributes/Level), o null si todavia no se conoce.</summary>
    public string? ArtRoot { get; set; }

    private readonly Dictionary<string, Texture2D?> _imageCache = new(StringComparer.OrdinalIgnoreCase);

    public override void _Ready()
    {
        _font = ThemeDB.FallbackFont;
        Resized += QueueRedraw;
    }

    public void SetCard(CardDto dto)
    {
        _dto = dto;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var cardRect = new Rect2(20, 20, Size.X - 40, Size.Y - 40);
        var fill = FillColorFor(_dto);
        DrawVerticalGradient(cardRect, Lighten(fill, 0.16f), Darken(fill, 0.14f));
        DrawRect(cardRect, PanelBorder, filled: false, width: 3);

        const int pad = 16;
        bool monster = IsMonster(_dto);

        float nameWidth = cardRect.Size.X - pad * 2;
        Rect2? badgeRect = null;
        if (monster)
        {
            const int badgeSize = 40;
            badgeRect = new Rect2(cardRect.End.X - pad - badgeSize, cardRect.Position.Y + pad, badgeSize, badgeSize);
            nameWidth -= badgeSize + 8;
        }

        const int nameBoxHeight = 46;
        var nameBoxRect = new Rect2(cardRect.Position.X + pad, cardRect.Position.Y + pad, nameWidth, nameBoxHeight);
        DrawBox(nameBoxRect);
        DrawString(_font, nameBoxRect.Position + new Vector2(6, nameBoxHeight * 0.65f),
            string.IsNullOrWhiteSpace(_dto.Name) ? "(sin nombre)" : _dto.Name, HorizontalAlignment.Left, nameWidth - 12, 17, BoxText);

        if (monster && badgeRect.HasValue)
        {
            var attributeArt = LoadImage("Attributes", AttributeFileName(CardDtoMapper.ParseEnum(_dto.Attribute, MonsterAttribute.Dark)));
            if (attributeArt != null) DrawTextureRect(attributeArt, badgeRect.Value, false);
        }

        float y = nameBoxRect.End.Y + 8;
        if (monster)
        {
            DrawLevelStars(_dto.Level, cardRect.Position.X + pad, y, cardRect.Size.X - pad * 2);
            y += 24;
        }

        float descBoxHeight = monster ? 118 : 96;
        var descBoxRect = new Rect2(cardRect.Position.X + pad, cardRect.End.Y - pad - descBoxHeight,
            cardRect.Size.X - pad * 2, descBoxHeight);
        DrawBox(descBoxRect);
        DrawDescriptionBox(descBoxRect, monster);

        var artFrameRect = new Rect2(cardRect.Position.X + pad, y, cardRect.Size.X - pad * 2, descBoxRect.Position.Y - 8 - y);
        if (artFrameRect.Size.Y > 10)
        {
            DrawRect(artFrameRect, ArtMatte, filled: true);
            DrawRect(artFrameRect, BoxBorder, filled: false, width: 2);

            var art = LoadImage("Cards", _dto.Image);
            if (art != null)
            {
                var artRect = FitSquare(art, artFrameRect.Position.X + 2, artFrameRect.Position.Y + 2,
                    artFrameRect.Size.X - 4, artFrameRect.Size.Y - 4);
                DrawTextureRect(art, artRect, false);
            }
        }
    }

    private void DrawVerticalGradient(Rect2 rect, Color top, Color bottom)
    {
        const int strips = 24;
        float stripHeight = rect.Size.Y / strips;
        for (int i = 0; i < strips; i++)
        {
            float t = i / (float)(strips - 1);
            var stripRect = new Rect2(rect.Position.X, rect.Position.Y + i * stripHeight, rect.Size.X, stripHeight + 1);
            DrawRect(stripRect, top.Lerp(bottom, t), filled: true);
        }
    }

    private void DrawBox(Rect2 box)
    {
        DrawRect(box, BoxFill, filled: true);
        DrawRect(box, BoxBorder, filled: false, width: 2);
    }

    private void DrawDescriptionBox(Rect2 box, bool monster)
    {
        var inner = box.Grow(-8);

        string title = monster
            ? $"[{_dto.Attribute} / {_dto.Type}]"
            : $"[{(_dto.Kind.Equals("Trap", StringComparison.OrdinalIgnoreCase) ? "Trampa" : "Magia")} / {_dto.SubType}]";
        DrawString(_font, inner.Position + new Vector2(0, 14), title, HorizontalAlignment.Left, inner.Size.X, 13, BoxText);

        string? footer = monster ? $"ATK {_dto.Attack} / DEF {_dto.Defense}" : null;

        string description = string.IsNullOrWhiteSpace(_dto.Description) ? "(sin descripcion)" : _dto.Description;
        var descRect = new Rect2(inner.Position.X, inner.Position.Y + 22, inner.Size.X, inner.Size.Y - 22 - (footer != null ? 22 : 0));
        DrawMultilineWrappedString(description, descRect, 12);

        if (footer != null)
            DrawString(_font, new Vector2(inner.Position.X, inner.End.Y - 4), footer, HorizontalAlignment.Left, inner.Size.X, 14, BoxText);
    }

    /// <summary>Envoltorio de texto simple por ancho disponible, linea por linea (Godot's DrawString no envuelve solo, hace falta partirlo a mano).</summary>
    private void DrawMultilineWrappedString(string text, Rect2 area, int fontSize)
    {
        var words = text.Split(' ');
        var lines = new List<string>();
        string current = "";
        foreach (var word in words)
        {
            string candidate = current.Length == 0 ? word : current + " " + word;
            if (_font.GetStringSize(candidate, HorizontalAlignment.Left, -1, fontSize).X > area.Size.X && current.Length > 0)
            {
                lines.Add(current);
                current = word;
            }
            else current = candidate;
        }
        if (current.Length > 0) lines.Add(current);

        float lineHeight = fontSize + 4;
        int maxLines = Math.Max(1, (int)(area.Size.Y / lineHeight));
        for (int i = 0; i < Math.Min(lines.Count, maxLines); i++)
            DrawString(_font, area.Position + new Vector2(0, (i + 1) * lineHeight - 4), lines[i], HorizontalAlignment.Left, area.Size.X, fontSize, BoxText);
    }

    private static Rect2 FitSquare(Texture2D image, float x, float y, float maxWidth, float maxHeight)
    {
        var size = image.GetSize();
        float scale = Math.Min(maxWidth / size.X, maxHeight / size.Y);
        float w = size.X * scale, h = size.Y * scale;
        return new Rect2(x + (maxWidth - w) / 2f, y + (maxHeight - h) / 2f, w, h);
    }

    private void DrawLevelStars(int level, float x, float y, float maxWidth)
    {
        var star = LoadImage("Level", "level.png");
        if (star == null || level <= 0) return;

        float size = Math.Clamp(maxWidth / Math.Max(level, 1), 10, 22);
        for (int i = 0; i < level; i++)
            DrawTextureRect(star, new Rect2(x + i * size, y, size, size), false);
    }

    private Texture2D? LoadImage(string subfolder, string? fileName)
    {
        if (string.IsNullOrEmpty(ArtRoot) || string.IsNullOrWhiteSpace(fileName)) return null;
        string path = Path.Combine(ArtRoot, subfolder, fileName);

        if (_imageCache.TryGetValue(path, out var cached)) return cached;

        Texture2D? texture = null;
        if (File.Exists(path))
        {
            var image = new Image();
            var err = image.Load(path);
            if (err == Error.Ok) texture = ImageTexture.CreateFromImage(image);
        }

        _imageCache[path] = texture;
        return texture;
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
            MonsterAttribute.Dark => new Color(70 / 255f, 55 / 255f, 95 / 255f),
            MonsterAttribute.Light => new Color(150 / 255f, 140 / 255f, 90 / 255f),
            MonsterAttribute.Earth => new Color(95 / 255f, 80 / 255f, 55 / 255f),
            MonsterAttribute.Fire => new Color(140 / 255f, 60 / 255f, 50 / 255f),
            MonsterAttribute.Water => new Color(50 / 255f, 90 / 255f, 130 / 255f),
            MonsterAttribute.Wind => new Color(60 / 255f, 110 / 255f, 80 / 255f),
            MonsterAttribute.Divine => new Color(150 / 255f, 120 / 255f, 60 / 255f),
            _ => new Color(70 / 255f, 70 / 255f, 80 / 255f)
        };
    }

    private static Color Lighten(Color color, float amount) => color.Lerp(Colors.White, amount);
    private static Color Darken(Color color, float amount) => color.Lerp(Colors.Black, amount);
}
