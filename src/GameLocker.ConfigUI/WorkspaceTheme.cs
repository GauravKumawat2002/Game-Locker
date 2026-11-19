using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;

namespace GameLocker.ConfigUI;

internal enum WorkspaceThemeMode { System, Dark, Light }

internal sealed record WorkspaceTheme(
    Color Canvas, Color Surface, Color Inset, Color Raised, Color Hover, Color Line, Color ControlLine,
    Color Text, Color Muted, Color Disabled, Color Pink, Color OnPink, Color Yellow, Color OnYellow,
    Color Selected, Color OnSelected, Color Success, Color Warn, Color Error, bool IsDark = false,
    bool HighContrast = false)
{
    private static Color Rgb(int value) => Color.FromArgb(unchecked((int)0xFF000000) | value);

    private static readonly WorkspaceTheme Dark = new(
        Canvas: Rgb(0x090E1E), Surface: Rgb(0x11182C), Inset: Rgb(0x0C1224), Raised: Rgb(0x1A2238),
        Hover: Rgb(0x2B3551), Line: Rgb(0x343F5F), ControlLine: Rgb(0x7784A4),
        Text: Rgb(0xF6F7FF), Muted: Rgb(0xBEC7E2), Disabled: Rgb(0x919CB9),
        Pink: Rgb(0xFF3B96), OnPink: Rgb(0x24091A), Yellow: Rgb(0xEDFA3F), OnYellow: Rgb(0x171B25),
        Selected: Rgb(0x512242), OnSelected: Rgb(0xFFE3F1), Success: Rgb(0xEDFA78),
        Warn: Rgb(0xFFD394), Error: Rgb(0xFF99BD), IsDark: true);

    private static readonly WorkspaceTheme Light = new(
        Canvas: Rgb(0xF7F7FB), Surface: Rgb(0xEEF1F8), Inset: Rgb(0xFCFCFF), Raised: Rgb(0xE2E7F4),
        Hover: Rgb(0xD7DEF0), Line: Rgb(0xBAC5DB), ControlLine: Rgb(0x68748E),
        Text: Rgb(0x121B38), Muted: Rgb(0x485672), Disabled: Rgb(0x596581),
        Pink: Rgb(0xA60C55), OnPink: Color.White, Yellow: Rgb(0xEDFA3F), OnYellow: Rgb(0x171B25),
        Selected: Rgb(0xF2CCE0), OnSelected: Rgb(0x671336), Success: Rgb(0x3B5307),
        Warn: Rgb(0x72440A), Error: Rgb(0x9D1640));

    public static readonly Font Body = new("Segoe UI", 9.75F);
    public static readonly Font Small = new("Segoe UI", 9F);
    public static readonly Font Strong = new("Segoe UI", 9.75F, FontStyle.Bold);
    public static readonly Font Transition = new("Segoe UI", 11F, FontStyle.Bold);
    public static readonly Font Section = new("Segoe UI", 16F, FontStyle.Bold);
    public static readonly Font Schedule = new("Segoe UI", 22F, FontStyle.Bold);
    public static readonly Font Brand = new("Segoe UI", 24F, FontStyle.Bold);
    public static readonly Font Clock = new("Consolas", 23F, FontStyle.Bold);
    public static readonly Font PreciseClock = new("Consolas", 16F, FontStyle.Bold);
    public static readonly Font Data = new("Consolas", 10F, FontStyle.Bold);
    public static readonly Font Metadata = new("Consolas", 9F);

    public bool UseLightLogos => Raised.GetBrightness() < 0.5F;

    public static WorkspaceTheme For(Control control) => control.FindForm() switch
    {
        Form1 form => form.Theme,
        WorkspaceDialog dialog => dialog.Theme,
        _ => Light,
    };

    public static WorkspaceTheme Resolve(WorkspaceThemeMode mode)
    {
        if (SystemInformation.HighContrast)
        {
            return new WorkspaceTheme(
                Canvas: SystemColors.Window, Surface: SystemColors.Window, Inset: SystemColors.Window,
                Raised: SystemColors.Control, Hover: SystemColors.Window, Line: SystemColors.WindowText,
                ControlLine: SystemColors.WindowText, Text: SystemColors.WindowText, Muted: SystemColors.WindowText,
                Disabled: SystemColors.GrayText, Pink: SystemColors.Highlight, OnPink: SystemColors.HighlightText,
                Yellow: SystemColors.Highlight, OnYellow: SystemColors.HighlightText,
                Selected: SystemColors.Highlight, OnSelected: SystemColors.HighlightText,
                Success: SystemColors.WindowText, Warn: SystemColors.WindowText, Error: SystemColors.WindowText,
                HighContrast: true);
        }

        return mode switch
        {
            WorkspaceThemeMode.Dark => Dark,
            WorkspaceThemeMode.Light => Light,
            _ => WindowsUsesDarkApps() ? Dark : Light,
        };
    }

    private static bool WindowsUsesDarkApps()
    {
        try
        {
            using var personalize = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return personalize?.GetValue("AppsUseLightTheme") is 0;
        }
        catch (Exception error) when (error is UnauthorizedAccessException or SecurityException or IOException)
        {
            return false;
        }
    }

    // WM_SYSCOLORCHANGE and WM_SETTINGCHANGE carry contrast and appearance changes for open windows.
    public static bool IsAppearanceMessage(int message) => message is 0x0015 or 0x001A;

    public void ApplyTitleBar(Form form)
    {
        if (!form.IsHandleCreated)
            return;

        var handle = form.Handle;
        var dark = IsDark ? 1 : 0;
        if (DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int)) != 0)
            _ = DwmSetWindowAttribute(handle, 19, ref dark, sizeof(int));

        // Windows 11 accepts explicit caption and text colors for manual theme overrides.
        // The default value returns both colors to Windows when contrast themes take over.
        var caption = HighContrast ? -1 : ColorTranslator.ToWin32(Canvas);
        var text = HighContrast ? -1 : ColorTranslator.ToWin32(Text);
        _ = DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 36, ref text, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
}
