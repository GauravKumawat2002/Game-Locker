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
        Canvas: Rgb(0x090F22), Surface: Rgb(0x0E172B), Inset: Rgb(0x10182D), Raised: Rgb(0x222C43),
        Hover: Rgb(0x28334A), Line: Rgb(0x3A4A66), ControlLine: Rgb(0xCFD5E3),
        Text: Rgb(0xFAF9FC), Muted: Rgb(0xE2E5EE), Disabled: Rgb(0xA1ABC2),
        Pink: Rgb(0xEB1969), OnPink: Color.White, Yellow: Rgb(0xEDFA39), OnYellow: Rgb(0x080F27),
        Selected: Rgb(0x591539), OnSelected: Color.White, Success: Rgb(0xEDFA39),
        Warn: Rgb(0xFFD394), Error: Rgb(0xFF99BD), IsDark: true);

    private static readonly WorkspaceTheme Light = new(
        Canvas: Rgb(0xF6F5F3), Surface: Rgb(0xF0F2F7), Inset: Rgb(0xF5F5F7), Raised: Rgb(0xD9DFEF),
        Hover: Rgb(0xE1E5F0), Line: Rgb(0xCBD4E4), ControlLine: Rgb(0x152249),
        Text: Rgb(0x081337), Muted: Rgb(0x253352), Disabled: Rgb(0x586784),
        Pink: Rgb(0xA81154), OnPink: Color.White, Yellow: Rgb(0xECFA39), OnYellow: Rgb(0x081337),
        Selected: Rgb(0xE5AACB), OnSelected: Rgb(0x101432), Success: Rgb(0x3B5307),
        Warn: Rgb(0x72440A), Error: Rgb(0x9D1640));

    public static readonly Font Body = new("Segoe UI", 9.75F);
    public static readonly Font Small = new("Segoe UI", 9F);
    public static readonly Font Strong = new("Segoe UI", 9.75F, FontStyle.Bold);
    public static readonly Font Transition = new("Segoe UI", 15F, FontStyle.Bold);
    public static readonly Font Section = new("Segoe UI Black", 19F, FontStyle.Bold);
    public static readonly Font Schedule = new("Segoe UI Black", 24F, FontStyle.Bold);
    public static readonly Font Brand = new("Segoe UI Black", 25F, FontStyle.Bold);
    public static readonly Font Subtitle = new("Segoe UI", 17F, FontStyle.Regular);
    public static readonly Font Day = new("Segoe UI", 14F, FontStyle.Bold);
    public static readonly Font Link = new("Segoe UI", 12F, FontStyle.Bold);
    public static readonly Font Clock = new("Consolas", 31F, FontStyle.Bold);
    public static readonly Font PreciseClock = new("Consolas", 12F, FontStyle.Bold);
    public static readonly Font Data = new("Consolas", 10F, FontStyle.Bold);
    public static readonly Font Metadata = new("Consolas", 9F);
    public static readonly Font Ruler = new("Consolas", 11F, FontStyle.Bold);

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
        if (form is Form1)
        {
            var round = 2;
            _ = DwmSetWindowAttribute(handle, 33, ref round, sizeof(int));
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
}
