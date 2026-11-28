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
    private const uint NoActivate = 0x0010;
    private const uint NoZOrder = 0x0004;
    private const uint NoSize = 0x0001;
    private const uint Show = 0x0040;
    private const int AccentPolicyAttribute = 19;
    private const int AcrylicBlur = 4;
    private const int HideWindow = 0;
    private const int TransitionsForcedDisabled = 3;

    private readonly HwndSource surface;
    private int width;
    private int height;
    private int cornerDiameter;
    private bool visible;

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
        DisableSystemTransitions(surface.Handle);
    }

    public static void DisableSystemTransitions(IntPtr handle)
    {
        var disabled = 1;
        _ = DwmSetWindowAttribute(handle, TransitionsForcedDisabled, ref disabled, sizeof(int));
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

    public void Present(IntPtr panel, int left, int top, int diameter, int inset)
    {
        if (!GetWindowRect(panel, out var rect))
            return;

        var newWidth = rect.Right - rect.Left - 2 * inset;
        var newHeight = rect.Bottom - rect.Top - 2 * inset;
        var newDiameter = Math.Min(diameter - 2 * inset, Math.Min(newWidth, newHeight));
        if (newWidth <= 0 || newHeight <= 0 || newDiameter <= 0)
            return;

        var flags = NoActivate | NoZOrder;
        if (!visible || newWidth != width || newHeight != height || newDiameter != cornerDiameter)
        {
            if (!SetWindowPos(surface.Handle, panel, left + inset, top + inset, newWidth, newHeight, NoActivate))
                return;
            var region = CreateRoundRectRgn(0, 0, newWidth, newHeight, newDiameter, newDiameter);
            if (region != IntPtr.Zero && SetWindowRgn(surface.Handle, region, true) == 0)
                _ = DeleteObject(region);
            (width, height, cornerDiameter) = (newWidth, newHeight, newDiameter);
            if (!visible)
                flags |= Show;
        }

        // Move both surfaces in one batch so the acrylic tracks the panel without reordering it.
        var batch = BeginDeferWindowPos(2);
        if (batch != IntPtr.Zero)
            batch = DeferWindowPos(batch, panel, IntPtr.Zero, left, top, 0, 0, NoActivate | NoZOrder | NoSize);
        if (batch != IntPtr.Zero)
            batch = DeferWindowPos(batch, surface.Handle, IntPtr.Zero, left + inset, top + inset, 0, 0, flags | NoSize);
        if (batch != IntPtr.Zero && EndDeferWindowPos(batch))
            visible = true;
    }

    public void Hide()
    {
        if (!visible)
            return;
        visible = false;
        _ = ShowWindow(surface.Handle, HideWindow);
    }

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

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

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
    private static extern IntPtr BeginDeferWindowPos(int count);

    [DllImport("user32.dll")]
    private static extern IntPtr DeferWindowPos(IntPtr batch, IntPtr window, IntPtr after,
        int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EndDeferWindowPos(IntPtr batch);

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
