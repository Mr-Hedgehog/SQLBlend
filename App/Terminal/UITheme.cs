using Terminal.Gui;

namespace SQLBlend.Terminal;

public static class UITheme
{
    public static void Initialize()
    {
        // Force dark theme by setting global colors before anything else is rendered
        var darkScheme = new ColorScheme
        {
            Normal = new global::Terminal.Gui.Attribute(Color.White, Color.Black),
            Focus = new global::Terminal.Gui.Attribute(Color.White, Color.Black),
            HotNormal = new global::Terminal.Gui.Attribute(Color.White, Color.Black),
            HotFocus = new global::Terminal.Gui.Attribute(Color.White, Color.Black),
            Disabled = new global::Terminal.Gui.Attribute(Color.White, Color.Black)
        };

        Colors.Base = darkScheme;
        Colors.Dialog = darkScheme;
        Colors.TopLevel = darkScheme;
        Colors.Menu = darkScheme;
    }

    private static ColorScheme GetDarkColorScheme()
    {
        return new ColorScheme
        {
            Normal = new global::Terminal.Gui.Attribute(Color.White, Color.Black),
            Focus = new global::Terminal.Gui.Attribute(Color.White, Color.DarkGray),
            HotNormal = new global::Terminal.Gui.Attribute(Color.White, Color.Black),
            HotFocus = new global::Terminal.Gui.Attribute(Color.DarkGray, Color.Black),
            Disabled = new global::Terminal.Gui.Attribute(Color.White, Color.Black)
        };
    }

    /// <summary>
    /// Applies a consistent dark theme to a Window
    /// </summary>
    public static void ApplyTheme(Window window)
    {
        if (window == null) return;
        window.ColorScheme = GetDarkColorScheme();
    }
}
