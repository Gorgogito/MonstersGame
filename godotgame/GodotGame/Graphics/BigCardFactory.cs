using Godot;
using GodotGame.Core.Entities;

namespace GodotGame.Graphics;

/// <summary>
/// Carta grande con el marco clasico (ver <see cref="CardFrames"/>), la que
/// usan las pantallas de batalla y de fusion/ritual: barra de nombre con
/// Atributo, estrellas, arte enmarcado y pergamino con tipo y ATK/DEF, mas
/// un reverso para mostrarla boca abajo. Solo arma los nodos; quien la usa
/// la agrega donde quiera y la anima.
/// </summary>
public static class BigCardFactory
{
    public static readonly Vector2 CardSize = new(220, 310);

    /// <summary>Donde se dibuja el arte dentro de la carta (estirado a este rect, para que <see cref="CardFx.Shatter"/> pueda recortarlo exacto).</summary>
    public static readonly Rect2 ArtRect = new(20, 58, 180, 180);

    public sealed class View
    {
        /// <summary>Raiz de tamano <see cref="CardSize"/>, con pivote en el centro (para escalar/voltear).</summary>
        public Control Root = null!;
        public Control Front = null!;
        public Control Back = null!;
        public Texture2D? Art;
        public Color Frame;
    }

    public static View Build(TextureCache textures, MonsterCard card, bool faceDown)
    {
        var frame = CardFrames.FrameColor(card);
        var art = textures.CardArt(card.Image);

        var root = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Size = CardSize,
            PivotOffset = CardSize / 2f
        };

        var front = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = !faceDown };
        front.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        front.AddThemeStyleboxOverride("panel", CardStyle(frame));
        root.AddChild(front);

        // Barra de nombre con la insignia de Atributo, como la carta real.
        var nameBar = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = new Vector2(10, 8),
            Size = new Vector2(CardSize.X - 20, 28)
        };
        nameBar.AddThemeStyleboxOverride("panel", CardFrames.NameBar(frame));
        var nameRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var name = new Label
        {
            Text = card.Name,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        name.AddThemeFontSizeOverride("font_size", card.Name.Length > 18 ? 13 : 16);
        CardFrames.StyleName(name, card);
        nameRow.AddChild(name);
        var attributeIcon = textures.AttributeIcon(card.Attribute);
        if (attributeIcon != null)
        {
            nameRow.AddChild(new TextureRect
            {
                Texture = attributeIcon,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(22, 22),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
            });
        }
        nameBar.AddChild(nameRow);
        front.AddChild(nameBar);

        var stars = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.End,
            Position = new Vector2(10, 38),
            Size = new Vector2(CardSize.X - 20, 16)
        };
        stars.AddThemeConstantOverride("separation", 2);
        var starTexture = textures.LevelStar();
        for (int i = 0; i < card.Level; i++)
        {
            stars.AddChild(starTexture != null
                ? new TextureRect
                {
                    Texture = starTexture,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    CustomMinimumSize = new Vector2(14, 14),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
                }
                : new ColorRect { Color = new Color(1f, 0.85f, 0.3f), CustomMinimumSize = new Vector2(10, 10), MouseFilter = Control.MouseFilterEnum.Ignore });
        }
        front.AddChild(stars);

        front.AddChild(new ColorRect
        {
            Color = frame.Darkened(0.65f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = ArtRect.Position - new Vector2(3, 3),
            Size = ArtRect.Size + new Vector2(6, 6)
        });
        front.AddChild(new TextureRect
        {
            Texture = art,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Position = ArtRect.Position,
            Size = ArtRect.Size
        });

        // Franja de pergamino: tipo arriba a la izquierda, ATK/DEF abajo a la derecha.
        var parchment = new Panel
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = new Vector2(10, 245),
            Size = new Vector2(CardSize.X - 20, 55)
        };
        parchment.AddThemeStyleboxOverride("panel", CardFrames.ParchmentBox());
        var type = new Label
        {
            Text = $"[{card.Type}]",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = new Vector2(6, 2),
            Size = new Vector2(CardSize.X - 32, 20),
            ClipText = true
        };
        type.AddThemeFontOverride("font", CardFrames.SerifBold);
        type.AddThemeFontSizeOverride("font_size", 12);
        type.AddThemeColorOverride("font_color", new Color(CardFrames.InkHex));
        parchment.AddChild(type);
        var printed = new RichTextLabel
        {
            BbcodeEnabled = true,
            ScrollActive = false,
            Text = CardFrames.StatsBbcode(card.Attack, card.Attack, card.Defense, card.Defense),
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = new Vector2(4, 28),
            Size = new Vector2(CardSize.X - 30, 24)
        };
        printed.AddThemeFontOverride("normal_font", CardFrames.SerifBold);
        printed.AddThemeFontSizeOverride("normal_font_size", 15);
        printed.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        parchment.AddChild(printed);
        front.AddChild(parchment);

        var back = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = faceDown };
        back.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        back.AddThemeStyleboxOverride("panel", CardStyle(CardFrames.FaceDown));
        var backArt = new TextureRect
        {
            Texture = textures.CardArt("CardBack.jpg"),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale
        };
        backArt.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        backArt.OffsetLeft = backArt.OffsetTop = 6;
        backArt.OffsetRight = backArt.OffsetBottom = -6;
        back.AddChild(backArt);
        root.AddChild(back);

        return new View { Root = root, Front = front, Back = back, Art = art, Frame = frame };
    }

    /// <summary>Cuerpo de la carta grande: marco a pleno, borde oscuro grueso y sombra.</summary>
    public static StyleBoxFlat CardStyle(Color frame) => new()
    {
        BgColor = frame,
        BorderColor = frame.Darkened(0.55f),
        BorderWidthLeft = 5,
        BorderWidthRight = 5,
        BorderWidthTop = 5,
        BorderWidthBottom = 5,
        CornerRadiusTopLeft = 10,
        CornerRadiusTopRight = 10,
        CornerRadiusBottomLeft = 10,
        CornerRadiusBottomRight = 10,
        ShadowColor = new Color(0, 0, 0, 0.6f),
        ShadowSize = 10
    };
}
