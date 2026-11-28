using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace GreeControl.Native;

internal sealed class FrostedBackdrop : IDisposable
{
    private const int Popup = unchecked((int)0x80000000);
    private const int PassiveToolWindow = 0x080000A0; // NOACTIVATE | TOOLWINDOW | TRANSPARENT
    private const uint PlaceWithoutActivation = 0x0010 | 0x0040; // NOACTIVATE | SHOWWINDOW
    private const int AccentPolicyAttribute = 19;
    private const int AcrylicBlur = 4;
    private const int HideWindow = 0;

    private readonly HwndSource surface;
    private int width;
    private int height;
    private int cornerDiameter;

    private FrostedBackdrop()
    {
        surface = new HwndSource(new HwndSourceParameters("Gree Control backdrop")
        {
            WindowStyle = Popup,
            ExtendedWindowStyle = PassiveToolWindow,
            Width = 1,
            Height = 1
        });
        surface.CompositionTarget!.BackgroundColor = Colors.Transparent;
        surface.RootVisual = new System.Windows.Controls.Border { Background = System.Windows.Media.Brushes.Transparent };
        surface.AddHook(PassThroughHitTest);
    }

    public static FrostedBackdrop? TryCreate()
    {
        if (SystemParameters.HighContrast || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            return null;
        if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "EnableTransparency", 1) is 0)
            return null;

        FrostedBackdrop? backdrop = null;
        try
        {
            backdrop = new FrostedBackdrop();
            // The compositor samples the desktop behind this surface. The WPF window above it carries the controls.
            if (ApplyAccent(backdrop.surface.Handle))
                return backdrop;
        }
        catch (Exception error) when (error is EntryPointNotFoundException or Win32Exception) { }

        backdrop?.Dispose();
        return null;
    }

    public void Follow(IntPtr panel, int diameter)
    {
        if (!GetWindowRect(panel, out var rect))
            return;

        var newWidth = rect.Right - rect.Left;
        var newHeight = rect.Bottom - rect.Top;
        if (newWidth <= 0 || newHeight <= 0)
            return;
        if (newWidth != width || newHeight != height || diameter != cornerDiameter)
        {
            var region = CreateRoundRectRgn(0, 0, newWidth + 1, newHeight + 1, diameter, diameter);
            if (region != IntPtr.Zero && SetWindowRgn(surface.Handle, region, true) == 0)
                _ = DeleteObject(region);
            (width, height, cornerDiameter) = (newWidth, newHeight, diameter);
        }

        _ = SetWindowPos(surface.Handle, panel, rect.Left, rect.Top, width, height, PlaceWithoutActivation);
    }

    public void Hide() => _ = ShowWindow(surface.Handle, HideWindow);

    public void Dispose() => surface.Dispose();

    private static IntPtr PassThroughHitTest(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        handled = message == 0x0084; // WM_NCHITTEST
        return handled ? new IntPtr(-1) : IntPtr.Zero; // HTTRANSPARENT
    }

    private static bool ApplyAccent(IntPtr hwnd)
    {
        var accent = new AccentPolicy { State = AcrylicBlur, Color = 0x30251C12 };
        var size = Marshal.SizeOf<AccentPolicy>();
        var payload = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, payload, false);
            var attribute = new CompositionAttribute { Attribute = AccentPolicyAttribute, Data = payload, Size = (nuint)size };
            return SetWindowCompositionAttribute(hwnd, ref attribute);
        }
        finally { Marshal.FreeHGlobal(payload); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int State;
        public int Flags;
        public uint Color;
        public int Animation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CompositionAttribute
    {
        public int Attribute;
        public IntPtr Data;
        public nuint Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowCompositionAttribute(IntPtr window, ref CompositionAttribute attribute);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out WindowRect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr window, IntPtr region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr handle);
}
