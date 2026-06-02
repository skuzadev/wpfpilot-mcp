using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;

namespace WpfPilot.Mcp.Server.Services;

/// <summary>
/// DPI-aware screenshot service. Uses PrintWindow with PW_RENDERFULLCONTENT
/// which renders the window at its native scale instead of doing a screen
/// BitBlt (which fails on HiDPI and mixed-DPI multi-monitor setups).
/// </summary>
public sealed class ScreenshotService
{
    private readonly SessionManager _session;

    private const int PW_RENDERFULLCONTENT = 0x00000002;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, int nFlags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    public ScreenshotService(SessionManager session)
    {
        _session = session;
    }

    public byte[] CaptureWindow()
    {
        if (!_session.IsAttached || _session.ActiveWindow is null)
            throw new InvalidOperationException("No window attached.");

        var window = _session.ActiveWindow;
        var handle = window.Properties.NativeWindowHandle.ValueOrDefault;
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException("Window has no native handle.");

        if (!GetWindowRect(handle, out var rect))
            throw new InvalidOperationException("Could not get window bounds.");

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("Window has no visible bounds (is it minimized?).");

        return CaptureViaPrintWindow(handle, width, height);
    }

    public byte[] CaptureElement(AutomationElement element)
    {
        var bounds = element.BoundingRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new InvalidOperationException("Element has no visible bounds.");

        var handle = element.Properties.NativeWindowHandle.ValueOrDefault;
        if (handle == IntPtr.Zero)
        {
            // Fallback: screen grab the element's bounding box
            using var bitmap = new Bitmap((int)bounds.Width, (int)bounds.Height);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen((int)bounds.X, (int)bounds.Y, 0, 0, new Size((int)bounds.Width, (int)bounds.Height));
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }

        return CaptureViaPrintWindow(handle, (int)bounds.Width, (int)bounds.Height);
    }

    public Rectangle GetWindowBounds()
    {
        if (!_session.IsAttached || _session.ActiveWindow is null)
            throw new InvalidOperationException("No window attached.");

        var bounds = _session.ActiveWindow.BoundingRectangle;
        return new Rectangle((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height);
    }

    private static byte[] CaptureViaPrintWindow(IntPtr hwnd, int width, int height)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        var hdc = graphics.GetHdc();
        try
        {
            if (!PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT))
            {
                // Fallback to client rect print
                PrintWindow(hwnd, hdc, 0);
            }
        }
        finally
        {
            graphics.ReleaseHdc(hdc);
        }
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
