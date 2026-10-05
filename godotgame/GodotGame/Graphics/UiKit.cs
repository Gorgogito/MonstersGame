using Godot;
using System;

namespace GodotGame.Graphics;

/// <summary>Piezas de interfaz con el estilo comun de los menus (dorado sobre azul noche).</summary>
public static class UiKit
{
    public static readonly Color Gold = new(0.95f, 0.8f, 0.45f);
    public static readonly Color Panel = new(0.05f, 0.06f, 0.13f, 0.88f);

    /// <summary>Boton grande de menu: fondo translucido, filo dorado, se ilumina con el cursor/foco.</summary>
    public static Button MenuButton(string text, Action onPressed, float width = 340f)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(width, 54) };
        button.AddThemeFontSizeOverride("font_size", 22);
        button.AddThemeFontOverride("font", CardFrames.SerifBold);
        button.AddThemeColorOverride("font_color", Gold);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_focus_color", Colors.White);
        button.AddThemeColorOverride("font_disabled_color", new Color(0.5f, 0.5f, 0.55f));
        button.AddThemeStyleboxOverride("normal", Box(Panel, Gold.Darkened(0.45f), 2));
        button.AddThemeStyleboxOverride("hover", Box(new Color(0.12f, 0.1f, 0.22f, 0.95f), Gold, 3, glow: true));
        button.AddThemeStyleboxOverride("focus", Box(new Color(0.12f, 0.1f, 0.22f, 0.95f), Gold, 3, glow: true));
        button.AddThemeStyleboxOverride("pressed", Box(new Color(0.2f, 0.15f, 0.3f, 0.95f), Gold, 3));
        button.AddThemeStyleboxOverride("disabled", Box(new Color(0.06f, 0.06f, 0.08f, 0.7f), new Color(0.3f, 0.3f, 0.35f), 2));
        button.Pressed += onPressed;
        return button;
    }

    public static StyleBoxFlat Box(Color bg, Color border, int borderWidth, bool glow = false, int radius = 10) => new()
    {
        BgColor = bg,
        BorderColor = border,
        BorderWidthLeft = borderWidth,
        BorderWidthRight = borderWidth,
        BorderWidthTop = borderWidth,
        BorderWidthBottom = borderWidth,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
        ShadowColor = glow ? new Color(border.R, border.G, border.B, 0.45f) : new Color(0, 0, 0, 0.4f),
        ShadowSize = glow ? 10 : 4,
        ContentMarginLeft = 12,
        ContentMarginRight = 12,
        ContentMarginTop = 6,
        ContentMarginBottom = 6
    };

    public static Label Title(string text, int size, Color? color = null)
    {
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", CardFrames.SerifBold);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Gold);
        label.AddThemeColorOverride("font_outline_color", new Color(0.1f, 0.05f, 0f));
        label.AddThemeConstantOverride("outline_size", Math.Max(4, size / 6));
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.6f));
        label.AddThemeConstantOverride("shadow_offset_y", 3);
        return label;
    }

    public static Label Text(string text, int size, Color? color = null)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? new Color(0.88f, 0.88f, 0.92f));
        return label;
    }

    /// <summary>Panel translucido con filo dorado, para agrupar contenido.</summary>
    public static PanelContainer Frame(Color? border = null)
    {
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", Box(Panel, border ?? Gold.Darkened(0.4f), 2));
        return panel;
    }

    /// <summary>Cambio de escena con fundido a negro.</summary>
    public static void GoTo(Node from, string scenePath)
    {
        var fade = new ColorRect { Color = new Color(0, 0, 0, 0), MouseFilter = Control.MouseFilterEnum.Stop, ZIndex = 100 };
        fade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        from.GetTree().Root.AddChild(fade);
        var tween = fade.CreateTween();
        tween.TweenProperty(fade, "color:a", 1f, 0.25);
        tween.TweenCallback(Callable.From(() => from.GetTree().ChangeSceneToFile(scenePath)));
        tween.TweenProperty(fade, "color:a", 0f, 0.25);
        tween.TweenCallback(Callable.From(fade.QueueFree));
    }
}
