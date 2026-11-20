using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using GameLocker.Common.Security;
using Microsoft.Win32;

namespace GameLocker.ConfigUI;

internal sealed class PrintScreenShortcut
{
    private const int ActivateApplication = 0x001C;
    private const int HotKey = 0x0312;
    private const int SnippingHotKey = 1;
    private const uint NoRepeat = 0x4000;
    private const string SnippingUri = "ms-screenclip:";
    private IntPtr _registeredWindow;

    public bool Handle(Message message)
    {
        if (message.Msg == ActivateApplication)
        {
            if (message.WParam == IntPtr.Zero)
                Release();
            else if (_registeredWindow == IntPtr.Zero && AclHelper.IsRunningAsAdmin() && UsesScreenSnipping() &&
                     RegisterHotKey(message.HWnd, SnippingHotKey, NoRepeat, (uint)Keys.PrintScreen))
                _registeredWindow = message.HWnd;
            return false;
        }

        if (message.Msg != HotKey || message.WParam != SnippingHotKey || message.HWnd != _registeredWindow)
            return false;

        GetWindowThreadProcessId(GetForegroundWindow(), out var foregroundProcess);
        if (foregroundProcess != Environment.ProcessId)
            return true;

        try
        {
            // Explorer opens the Windows overlay in the interactive desktop session.
            var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            using var process = Process.Start(new ProcessStartInfo(explorer, SnippingUri) { UseShellExecute = true });
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            Trace.TraceWarning("Could not open the Windows Snipping Tool: {0}", error.Message);
        }

        return true;
    }

    public void Release()
    {
        if (_registeredWindow == IntPtr.Zero)
            return;

        UnregisterHotKey(_registeredWindow, SnippingHotKey);
        _registeredWindow = IntPtr.Zero;
    }

    private static bool UsesScreenSnipping()
    {
        try
        {
            if (Registry.GetValue(@"HKEY_CLASSES_ROOT\ms-screenclip", "URL Protocol", null) is null)
                return false;

            var preference = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Keyboard",
                "PrintScreenKeyForSnippingEnabled", null);
            return preference is int configured ? configured == 1 : OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException)
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
