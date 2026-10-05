using Godot;
using GodotGame.Core.Entities;
using GodotGame.Graphics;

namespace GodotGame;

/// <summary>
/// Panel de detalle de carta de la barra lateral (como el recuadro de
/// informacion de Forbidden Memories): la ultima carta sobre la que paso el
/// cursor, con arte grande, Atributo, Nivel, tipo, ATK/DEF (efectivos si esta
/// en el Campo) y descripcion. Una carta boca abajo del rival se muestra
/// como reverso, sin revelar nada.
/// </summary>
public partial class CardDetailPanel : PanelContainer
{
    /// <summary>
    /// Lo que se muestra: la carta (null = boca abajo y oculta), sus ATK/DEF
    /// efectivos si esta en el Campo (null = mostrar los impresos), y una
    /// nota de donde/como esta ("En tu mano", "Posicion de Defensa"...).
    /// </summary>
    public readonly record struct Entry(Card? Card, int? Attack, int? Defense, string Note, GuardianStar? ActiveStar = null);


    private TextureCache _textures = null!;
    private Label _name = null!, _type = null!, _note = null!;
    private TextureRect _art = null!, _attribute = null!;
    private HBoxContainer _stars = null!;
    private RichTextLabel _stats = null!, _description = null!, _guardians = null!;
    private Entry? _shown;

    public void Setup(TextureCache textures) => _textures = textures;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ApplyFrame(new Color(0.3f, 0.3f, 0.35f));

        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 3);
        AddChild(box);

        var header = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _name = new Label
        {
            Text = "Pasa el cursor sobre una carta",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _name.AddThemeFontSizeOverride("font_size", 16);
        _name.AddThemeFontOverride("font", CardFrames.SerifBold);
        _name.AddThemeColorOverride("font_outline_color", Colors.Black);
        _name.AddThemeConstantOverride("outline_size", 4);
        _attribute = new TextureRect
        {
            CustomMinimumSize = new Vector2(24, 24),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            MouseFilter = MouseFilterEnum.Ignore
        };
        header.AddChild(_name);
        header.AddChild(_attribute);
        box.AddChild(header);

        _stars = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, 14) };
        _stars.AddThemeConstantOverride("separation", 2);
        box.AddChild(_stars);

        _art = new TextureRect
        {
            CustomMinimumSize = new Vector2(0, 170),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        box.AddChild(_art);

        _type = new Label { MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center };
        _type.AddThemeFontSizeOverride("font_size", 12);
        _type.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f));
        box.AddChild(_type);

        _stats = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _stats.AddThemeFontSizeOverride("normal_font_size", 15);
        box.AddChild(_stats);

        // Estrellas Guardianas: las dos de la carta, la que esta en uso resaltada.
        _guardians = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _guardians.AddThemeFontOverride("normal_font", StarGlyphs.Font);
        _guardians.AddThemeFontSizeOverride("normal_font_size", 13);
        box.AddChild(_guardians);

        _note = new Label { MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center };
        _note.AddThemeFontSizeOverride("font_size", 11);
        _note.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.55f));
        box.AddChild(_note);

        _description = new RichTextLabel
        {
            CustomMinimumSize = new Vector2(0, 74),
            ScrollActive = true,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _description.AddThemeFontSizeOverride("normal_font_size", 12);
        box.AddChild(_description);
    }

    /// <summary>Muestra <paramref name="entry"/>; no reconstruye nada si ya es lo que esta en pantalla (se llama en cada frame).</summary>
    public void ShowEntry(Entry entry)
    {
        if (_shown == entry) return;
        _shown = entry;

        var card = entry.Card;
        _note.Text = entry.Note;
        foreach (var child in _stars.GetChildren())
            child.QueueFree();

        if (card == null)
        {
            ApplyFrame(CardFrames.FaceDown);
            _name.Text = "???";
            _attribute.Texture = null;
            _art.Texture = _textures.CardArt("CardBack.jpg");
            _type.Text = "";
            _stats.Text = "";
            _guardians.Text = "";
            _description.Text = "Carta boca abajo: no se sabe que es hasta que se revele.";
            return;
        }

        ApplyFrame(CardFrames.FrameColor(card));
        _name.Text = card.Name;
        _art.Texture = _textures.CardArt(card.Image);
        _description.Text = string.IsNullOrWhiteSpace(card.Description) ? "(sin descripcion)" : card.Description;

        if (card is MonsterCard monster)
        {
            _attribute.Texture = _textures.AttributeIcon(monster.Attribute);
            _type.Text = $"[{monster.Type}]  Nivel {monster.Level}";
            AddStars(monster.Level);
            int atk = entry.Attack ?? monster.Attack;
            int def = entry.Defense ?? monster.Defense;
            _stats.Text = $"[color={StatHex(atk, monster.Attack, "#ffb478")}]ATK {atk}[/color]   [color={StatHex(def, monster.Defense, "#96c8ff")}]DEF {def}[/color]";
            _guardians.Text = $"{StarText(monster.GuardianStar1, entry.ActiveStar)}   {StarText(monster.GuardianStar2, entry.ActiveStar)}";
        }
        else
        {
            _attribute.Texture = null;
            _type.Text = card.Kind == CardKind.Spell ? "[CARTA MAGICA]" : "[CARTA DE TRAMPA]";
            _stats.Text = "";
            _guardians.Text = "";
        }
    }

    /// <summary>Simbolo y nombre de una estrella; la que esta en uso va subrayada y la otra atenuada (si se sabe cual esta en uso).</summary>
    private static string StarText(GuardianStar star, GuardianStar? active)
    {
        string text = $"{StarGlyphs.Bbcode(star)} {GodotGame.Core.Rules.GuardianStars.DisplayName(star)}";
        if (active == null) return text;
        return active == star ? $"[u]{text}[/u]" : $"[color=#ffffff66]{text}[/color]";
    }

    private void AddStars(int level)
    {
        var star = _textures.LevelStar();
        for (int i = 0; i < level; i++)
        {
            _stars.AddChild(star != null
                ? new TextureRect
                {
                    Texture = star,
                    CustomMinimumSize = new Vector2(14, 14),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = MouseFilterEnum.Ignore
                }
                : new ColorRect { Color = new Color(1f, 0.85f, 0.3f), CustomMinimumSize = new Vector2(10, 10), MouseFilter = MouseFilterEnum.Ignore });
        }
    }

    /// <summary>Verde/rojo si un modificador subio/bajo el valor impreso, el color de siempre si no.</summary>
    private static string StatHex(int effective, int printed, string baseHex) =>
        effective > printed ? "#7cffa0" : effective < printed ? "#ff7c7c" : baseHex;

    private void ApplyFrame(Color frame)
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = frame.Lerp(Colors.Black, 0.6f),
            BorderColor = frame.Lerp(Colors.White, 0.2f),
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            BorderWidthTop = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 6,
            ContentMarginBottom = 6
        });
    }
}
