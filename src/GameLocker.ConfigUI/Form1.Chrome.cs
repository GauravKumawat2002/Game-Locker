using System.Runtime.InteropServices;

namespace GameLocker.ConfigUI;

public partial class Form1
{
    private const int ResizeFrame = 0x00040000;
    private const int SystemMenu = 0x00080000;
    private const int MinimizeBoxStyle = 0x00020000;
    private const int MaximizeBoxStyle = 0x00010000;
    private const int CaptionHit = 2;
    private const int NonClientLeftButtonDown = 0x00A1;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.Style |= ResizeFrame | SystemMenu | MinimizeBoxStyle | MaximizeBoxStyle;
            return parameters;
        }
    }

    private void DragCaption(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;
        if (e.Clicks == 2)
        {
            ToggleMaximize();
            return;
        }

        ReleaseCapture();
        var screen = ((Control)sender!).PointToScreen(e.Location);
        var packed = (screen.X & 0xffff) | (screen.Y << 16);
        SendMessage(Handle, NonClientLeftButtonDown, CaptionHit, packed);
    }

    private void ToggleMaximize() => WindowState = WindowState == FormWindowState.Maximized
        ? FormWindowState.Normal : FormWindowState.Maximized;

    private bool HandleChrome(ref Message message)
    {
        const int calculateClient = 0x0083;
        const int hitTest = 0x0084;
        if (message.Msg == calculateClient)
        {
            if (IsZoomed(message.HWnd))
            {
                var area = Screen.FromHandle(message.HWnd).WorkingArea;
                Marshal.WriteInt32(message.LParam, 0, area.Left);
                Marshal.WriteInt32(message.LParam, 4, area.Top);
                Marshal.WriteInt32(message.LParam, 8, area.Right);
                Marshal.WriteInt32(message.LParam, 12, area.Bottom);
            }
            message.Result = 0;
            return true;
        }

        if (message.Msg != hitTest || WindowState != FormWindowState.Normal)
            return false;

        var packed = unchecked((int)message.LParam.ToInt64());
        var local = PointToClient(new Point((short)packed, (short)(packed >> 16)));
        var grip = LogicalToDeviceUnits(5);
        var horizontal = local.X < grip ? -1 : local.X >= ClientSize.Width - grip ? 1 : 0;
        var vertical = local.Y < grip ? -1 : local.Y >= ClientSize.Height - grip ? 1 : 0;
        var hit = (horizontal, vertical) switch
        {
            (-1, -1) => 13, (0, -1) => 12, (1, -1) => 14,
            (-1, 0) => 10, (1, 0) => 11,
            (-1, 1) => 16, (0, 1) => 15, (1, 1) => 17,
            _ => 0,
        };
        if (hit == 0)
            return false;

        message.Result = hit;
        return true;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(IntPtr handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);
}
