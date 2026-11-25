using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Text;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Text.Json;

namespace GreeControl.Native;

public sealed class Form1 : Form
{
    private const uint GetDesktopWallpaper = 0x0073;

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint length, StringBuilder value, uint flags);

    private readonly WebView2 browser = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(18, 20, 27) };
    private readonly NotifyIcon trayIcon;
    private Process? bridge;
    private bool reallyExit;
    private readonly System.Windows.Forms.Timer assistantMonitor = new() { Interval = 30_000 };
    private bool webPageReady;
    private string? pendingUpdateNotice;
    private string? pendingUpdateAction;
    private bool pendingUpdatePersistent;

    public Form1()
    {
        Text = "Gree Control";
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(18, 20, 27);
        var iconPath = Path.Combine(AppContext.BaseDirectory, "gree-control.ico");
        Icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;
        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Abrir Gree Control", null, (_, _) => RestoreFromTray());
        trayMenu.Items.Add("Sair", null, (_, _) => ExitFromTray());
        trayIcon = new NotifyIcon { Text = "Gree Control", Icon = Icon, ContextMenuStrip = trayMenu, Visible = true };
        trayIcon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) RestoreFromTray(); };
        assistantMonitor.Tick += (_, _) =>
        {
            if (webPageReady && browser.CoreWebView2 is not null)
                _ = browser.CoreWebView2.ExecuteScriptAsync("(async()=>{await window.greeActions?.refreshStatus?.();window.greeAssistantBackgroundCheck?.();window.greeAutomationTick?.()})()");
        };
        Controls.Add(browser);
        Load += (_, _) => PositionAboveClock();
        Load += LoadApp;
        FormClosed += (_, _) =>
        {
            assistantMonitor.Dispose();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            trayMenu.Dispose();
            StopBridge();
        };
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!reallyExit && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        base.OnFormClosing(e);
    }

    private void HideToTray() => Hide();

    private void RestoreFromTray()
    {
        PositionAboveClock();
        Show();
        Activate();
        browser.Focus();
    }

    private void ExitFromTray()
    {
        reallyExit = true;
        Close();
    }

    private void PositionAboveClock()
    {
        var area = (Screen.PrimaryScreen ?? Screen.FromControl(this)).WorkingArea;
        var inset = ScaleLogicalPixels(8);
        var width = Math.Min(ScaleLogicalPixels(446), Math.Max(1, area.Width - inset * 2));
        var height = Math.Min(ScaleLogicalPixels(760), Math.Max(1, area.Height - inset * 2));
        SetBounds(area.Right - width - inset, area.Bottom - height - inset, width, height);
    }

    private int ScaleLogicalPixels(int pixels) => (int)Math.Round(pixels * DeviceDpi / 96.0);

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (ClientSize.Width == 0 || ClientSize.Height == 0)
            return;

        var diameter = Math.Min(ScaleLogicalPixels(56), Math.Min(ClientSize.Width, ClientSize.Height));
        var right = ClientSize.Width - diameter;
        var bottom = ClientSize.Height - diameter;
        using var outline = new GraphicsPath();
        outline.AddArc(0, 0, diameter, diameter, 180, 90);
        outline.AddArc(right, 0, diameter, diameter, 270, 90);
        outline.AddArc(right, bottom, diameter, diameter, 0, 90);
        outline.AddArc(0, bottom, diameter, diameter, 90, 90);
        outline.CloseFigure();

        var oldRegion = Region;
        Region = new Region(outline);
        oldRegion?.Dispose();
    }

    protected override void WndProc(ref Message message)
    {
        const int DisplayChange = 0x007E;
        const int SettingChange = 0x001A;
        base.WndProc(ref message);
        if (Visible && message.Msg is DisplayChange or SettingChange)
            PositionAboveClock();
    }

    private string? CreateWallpaperBackdrop()
    {
        if (SystemInformation.HighContrast)
            return null;

        var wallpaper = new StringBuilder(32768);
        _ = SystemParametersInfo(GetDesktopWallpaper, (uint)wallpaper.Capacity, wallpaper, 0);
        var cachedWallpaper = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Themes", "TranscodedWallpaper");
        var path = File.Exists(wallpaper.ToString()) ? wallpaper.ToString() : cachedWallpaper;
        if (!File.Exists(path))
            return null;

        try
        {
            using var image = Image.FromFile(path);
            var screen = (Screen.PrimaryScreen ?? Screen.FromControl(this)).Bounds;
            var scale = Math.Max((double)screen.Width / image.Width, (double)screen.Height / image.Height);
            var imageLeft = screen.Left + (screen.Width - image.Width * scale) / 2;
            var imageTop = screen.Top + (screen.Height - image.Height * scale) / 2;
            var source = new RectangleF((float)((Left - imageLeft) / scale), (float)((Top - imageTop) / scale),
                (float)(Width / scale), (float)(Height / scale));

            // Keep only the part of the wallpaper behind the panel in the WebView.
            using var crop = new Bitmap(Width, Height);
            using (var graphics = Graphics.FromImage(crop))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(image, new Rectangle(0, 0, crop.Width, crop.Height), source, GraphicsUnit.Pixel);
            }

            using var buffer = new MemoryStream();
            crop.Save(buffer, ImageFormat.Jpeg);
            return $"data:image/jpeg;base64,{Convert.ToBase64String(buffer.ToArray())}";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or ExternalException or OutOfMemoryException)
        {
            return null;
        }
    }

    private async void LoadApp(object? sender, EventArgs e)
    {
        try
        {
            var root = AppContext.BaseDirectory;
            var nodeExecutable = Path.Combine(root, "node.exe");
            var webViewProfile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GreeControl", "WebView2");
            try { Directory.CreateDirectory(webViewProfile); }
            catch { webViewProfile = Path.Combine(Path.GetTempPath(), "GreeControl", "WebView2"); Directory.CreateDirectory(webViewProfile); }
            browser.CreationProperties = new CoreWebView2CreationProperties { UserDataFolder = webViewProfile };
            bridge = new Process
            {
                StartInfo = new ProcessStartInfo(File.Exists(nodeExecutable) ? nodeExecutable : "node", "\"" + Path.Combine(root, "servidor.cjs") + "\"")
                {
                    WorkingDirectory = root,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };
            bridge.Start();
            var readUrl = bridge.StandardOutput.ReadLineAsync();
            var completed = await Task.WhenAny(readUrl, Task.Delay(TimeSpan.FromSeconds(12)));
            if (completed != readUrl) throw new InvalidOperationException("A ponte local demorou para iniciar.");
            var url = await readUrl;
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("http://127.0.0.1:", StringComparison.Ordinal))
                throw new InvalidOperationException("Não foi possível iniciar a ponte local do Gree.");

            // Keep draining both streams so the bridge cannot block on a full pipe.
            bridge.ErrorDataReceived += (_, _) => { };
            bridge.BeginErrorReadLine();
            _ = DrainBridgeOutputAsync(bridge.StandardOutput);

            var origin = JsonSerializer.Serialize(new Uri(url).GetLeftPart(UriPartial.Authority));
            var backdrop = JsonSerializer.Serialize(CreateWallpaperBackdrop());
            await browser.EnsureCoreWebView2Async();
            await browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync($$"""
                if (window === window.top && location.origin === {{origin}}) document.addEventListener('DOMContentLoaded', () => {
                    const root = document.documentElement;
                    root.setAttribute('data-native-panel', '');
                    const wallpaper = {{backdrop}};
                    if (wallpaper) {
                        root.style.setProperty('--desktop-wallpaper', 'url("' + wallpaper + '")');
                        root.setAttribute('data-wallpaper-glass', '');
                    }
                }, { once: true });
                """);
            browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            browser.CoreWebView2.Settings.IsZoomControlEnabled = false;
            browser.CoreWebView2.PermissionRequested += (_, request) =>
            {
                if (request.PermissionKind != Microsoft.Web.WebView2.Core.CoreWebView2PermissionKind.Geolocation)
                    return;

                var origin = Uri.TryCreate(request.Uri, UriKind.Absolute, out var permissionUri)
                    ? permissionUri
                    : null;
                if (origin is null || (origin.Host != "127.0.0.1" && origin.Host != "localhost"))
                {
                    request.State = Microsoft.Web.WebView2.Core.CoreWebView2PermissionState.Deny;
                    return;
                }

                // The app serves only from its own loopback bridge. Grant location there
                // automatically so local weather and Gree Assistente work without a dialog.
                request.State = Microsoft.Web.WebView2.Core.CoreWebView2PermissionState.Allow;
            };
            browser.CoreWebView2.WebMessageReceived += (_, message) =>
            {
                var text = message.TryGetWebMessageAsString();
                if (text == "hide-panel")
                {
                    HideToTray();
                    return;
                }

                if (text == "install-update")
                {
                    _ = ReleaseUpdater.InstallPending(ShowUpdateNotice, () =>
                    {
                        reallyExit = true;
                        trayIcon.Visible = false;
                        Application.Exit();
                    });
                    return;
                }
            };
            browser.CoreWebView2.NavigationCompleted += (_, navigation) =>
            {
                webPageReady = navigation.IsSuccess;
                if (webPageReady && pendingUpdateNotice is not null)
                {
                    var message = pendingUpdateNotice;
                    var action = pendingUpdateAction;
                    var persistent = pendingUpdatePersistent;
                    pendingUpdateNotice = null;
                    pendingUpdateAction = null;
                    ShowUpdateNotice(message, action, persistent);
                }
            };
            browser.CoreWebView2.Navigate(url);
            assistantMonitor.Start();
            _ = ReleaseUpdater.CheckForUpdateAsync(ShowUpdateNotice);
        }
        catch (Exception ex)
        {
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "GreeControl-startup.log"), $"{DateTime.Now:O}{Environment.NewLine}{ex}{Environment.NewLine}"); } catch { }
            MessageBox.Show(this, ex.Message, "Gree Control", MessageBoxButtons.OK, MessageBoxIcon.Error);
            reallyExit = true;
            trayIcon.Visible = false;
            Close();
        }
    }

    private void ShowUpdateNotice(string message, string? actionLabel = null, bool persistent = false)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => ShowUpdateNotice(message, actionLabel, persistent));
            return;
        }
        if (!webPageReady || browser.CoreWebView2 is null)
        {
            pendingUpdateNotice = message;
            pendingUpdateAction = actionLabel;
            pendingUpdatePersistent = persistent;
            return;
        }
        var encodedMessage = JsonSerializer.Serialize(message);
        var encodedAction = JsonSerializer.Serialize(actionLabel);
        _ = browser.CoreWebView2.ExecuteScriptAsync($"window.greeUpdateNotice?.({encodedMessage},{encodedAction},{persistent.ToString().ToLowerInvariant()})");
    }

    private static async Task DrainBridgeOutputAsync(StreamReader output)
    {
        try
        {
            while (await output.ReadLineAsync() is not null) { }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    private void StopBridge()
    {
        try
        {
            if (bridge is { HasExited: false }) bridge.Kill(entireProcessTree: true);
            bridge?.Dispose();
        }
        catch { }
    }
}
