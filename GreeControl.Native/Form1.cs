using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Text.Json;

namespace GreeControl.Native;

public sealed class Form1 : Form
{
    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private readonly WebView2 browser = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(18, 20, 27) };
    private readonly NotifyIcon trayIcon;
    private Process? bridge;
    private bool reallyExit;
    private readonly System.Windows.Forms.Timer assistantMonitor = new() { Interval = 30_000 };
    private readonly System.Windows.Forms.Timer windowMotion = new() { Interval = 15 };
    private readonly Stopwatch windowMotionWatch = new();
    private AssistantToastForm? assistantToast;
    private bool webPageReady;
    private string? pendingUpdateNotice;
    private bool openAssistantWhenReady;
    private bool exitAfterMotion;
    private WindowMotionState windowMotionState;
    private Point motionStart;
    private Point motionTarget;
    private Point motionAnchor;
    private double opacityStart;
    private double opacityTarget;
    private int motionDuration;

    private enum WindowMotionState { None, Opening, Closing }

    public Form1()
    {
        Text = "Gree Control";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        MaximizeBox = false;
        MinimizeBox = false;
        KeyPreview = true;
        BackColor = Color.FromArgb(18, 20, 27);
        var iconPath = Path.Combine(AppContext.BaseDirectory, "gree-control.ico");
        Icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;
        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Abrir Gree Control", null, (_, _) => RestoreFromTray());
        trayMenu.Items.Add("Sair", null, (_, _) => ExitFromTray());
        trayIcon = new NotifyIcon { Text = "Gree Control", Icon = Icon, ContextMenuStrip = trayMenu, Visible = true };
        trayIcon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) RestoreFromTray(); };
        Deactivate += (_, _) =>
        {
            BeginInvoke(() =>
            {
                // Keep the panel open when focus moves to its assistant bubble.
                if (Visible && !ContainsFocus && assistantToast?.ContainsFocus != true)
                    HideToTray();
            });
        };
        windowMotion.Tick += AnimateWindowFrame;
        assistantMonitor.Tick += (_, _) =>
        {
            if (browser.CoreWebView2 is not null)
                _ = browser.CoreWebView2.ExecuteScriptAsync("(async()=>{await window.greeActions?.refreshStatus?.();window.greeAssistantBackgroundCheck?.();window.greeAutomationTick?.()})()");
        };
        Controls.Add(browser);
        Load += LoadApp;
        FormClosed += (_, _) => { assistantMonitor.Stop(); assistantMonitor.Dispose(); windowMotion.Stop(); windowMotion.Dispose(); assistantToast?.Close(); trayIcon.Visible = false; trayIcon.Dispose(); StopBridge(); };
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) HideToTray(); };
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

    private void HideToTray()
    {
        if (!Visible) return;
        if (!exitAfterMotion) StartWindowMotion(opening: false);
    }

    private void RestoreFromTray()
    {
        var wasHidden = !Visible;
        WindowState = FormWindowState.Normal;
        exitAfterMotion = false;
        var anchor = GetWindowAnchor();
        if (wasHidden)
        {
            Location = new Point(anchor.X, anchor.Y + 48);
            Opacity = .08;
            Show();
            StartWindowMotion(opening: true, anchor);
        }
        else if (windowMotionState == WindowMotionState.Closing)
            StartWindowMotion(opening: true, anchor);
        BringToFront();
        Activate();
        browser.Focus();
    }

    private Point GetWindowAnchor()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? Screen.FromPoint(Cursor.Position).WorkingArea;
        return new Point(area.Right - Width, area.Bottom - Height);
    }

    private void StartWindowMotion(bool opening, Point? anchor = null)
    {
        windowMotion.Stop();
        motionAnchor = anchor ?? GetWindowAnchor();
        motionStart = Location;
        motionTarget = opening ? motionAnchor : new Point(motionAnchor.X, motionAnchor.Y + 32);
        opacityStart = Opacity;
        opacityTarget = opening ? 1 : .08;
        motionDuration = opening ? 280 : 190;
        windowMotionState = opening ? WindowMotionState.Opening : WindowMotionState.Closing;
        windowMotionWatch.Restart();
        windowMotion.Start();
    }

    private void AnimateWindowFrame(object? sender, EventArgs e)
    {
        if (windowMotionState == WindowMotionState.None) return;
        var progress = Math.Clamp(windowMotionWatch.Elapsed.TotalMilliseconds / motionDuration, 0, 1);
        var eased = windowMotionState == WindowMotionState.Opening
            ? 1 - Math.Pow(1 - progress, 3)
            : progress * progress * progress;
        Location = new Point(
            (int)Math.Round(motionStart.X + (motionTarget.X - motionStart.X) * eased),
            (int)Math.Round(motionStart.Y + (motionTarget.Y - motionStart.Y) * eased));
        Opacity = opacityStart + (opacityTarget - opacityStart) * eased;
        if (progress < 1) return;

        windowMotion.Stop();
        windowMotionWatch.Stop();
        var finishedState = windowMotionState;
        windowMotionState = WindowMotionState.None;
        if (finishedState == WindowMotionState.Opening)
        {
            Location = motionAnchor;
            Opacity = 1;
            return;
        }

        Hide();
        Location = motionAnchor;
        Opacity = 1;
        if (exitAfterMotion)
        {
            exitAfterMotion = false;
            reallyExit = true;
            trayIcon.Visible = false;
            Close();
        }
    }

    private void ExitFromTray()
    {
        if (!Visible)
        {
            reallyExit = true;
            trayIcon.Visible = false;
            Close();
            return;
        }

        exitAfterMotion = true;
        StartWindowMotion(opening: false);
    }

    protected override void OnShown(EventArgs e)
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? Screen.FromPoint(Cursor.Position).WorkingArea;
        Size = new Size(Math.Min(446, area.Width), Math.Min(760, area.Height));
        Location = new Point(area.Right - Width, area.Bottom - Height);
        base.OnShown(e);
        ApplyRoundedCorners();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (IsHandleCreated && Width > 0 && Height > 0)
            ApplyRoundedCorners();
    }

    private void ApplyRoundedCorners()
    {
        ApplyRoundedRegion(this, 38);
        // WebView2 paints through its own native child window. Clip that surface too,
        // otherwise its square corners can leak past the rounded Form region.
        if (browser.IsHandleCreated)
            ApplyRoundedRegion(browser, 38);
    }

    private static void ApplyRoundedRegion(Control control, int logicalRadius)
    {
        if (!control.IsHandleCreated || !GetWindowRect(control.Handle, out var rect))
            return;

        var physicalWidth = rect.Right - rect.Left;
        var physicalHeight = rect.Bottom - rect.Top;
        if (physicalWidth <= 0 || physicalHeight <= 0)
            return;

        var radius = Math.Max(18, (int)Math.Round((double)logicalRadius * control.DeviceDpi / 96d));
        var regionHandle = CreateRoundRectRgn(0, 0, physicalWidth, physicalHeight, radius * 2, radius * 2);
        if (regionHandle != IntPtr.Zero)
        {
            try
            {
                var roundedRegion = Region.FromHrgn(regionHandle);
                var previousRegion = control.Region;
                control.Region = roundedRegion;
                previousRegion?.Dispose();
            }
            finally
            {
                _ = DeleteObject(regionHandle);
            }
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

            await browser.EnsureCoreWebView2Async();
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
                if (text == "close-app") { Close(); return; }
                try
                {
                    using var payload = JsonDocument.Parse(text);
                    if (payload.RootElement.TryGetProperty("type", out var kind) && kind.GetString() == "assistant-toast")
                    {
                        var title = payload.RootElement.TryGetProperty("title", out var titleValue) ? titleValue.GetString() ?? "Gree Assistente" : "Gree Assistente";
                        var body = payload.RootElement.TryGetProperty("body", out var bodyValue) ? bodyValue.GetString() ?? "Há uma nova sugestão de conforto." : "Há uma nova sugestão de conforto.";
                        ShowAssistantToast(title, body);
                    }
                    else if (kind.GetString() == "connection-lost")
                        ShowAssistantToast("Gree desconectado", "A conexão caiu. Estou tentando reconectar pela rede local.");
                    else if (kind.GetString() == "connection-restored")
                        ShowAssistantToast("Gree reconectado", "O controle voltou a responder pela rede local.");
                    else if (kind.GetString() == "assistant-device-state")
                    {
                        var connected = payload.RootElement.TryGetProperty("connected", out var connectedValue) && connectedValue.GetBoolean();
                        if (connected)
                        {
                            var on = payload.RootElement.TryGetProperty("on", out var onValue) && onValue.GetBoolean();
                            var temp = payload.RootElement.TryGetProperty("temp", out var tempValue) ? tempValue.GetInt32() : 0;
                            var mode = payload.RootElement.TryGetProperty("mode", out var modeValue) ? modeValue.GetString() ?? "" : "";
                            if (assistantToast is not null) assistantToast.DeviceSummary = $"Gree {(on ? "ligado" : "desligado")} · {temp}°C · {mode}";
                        }
                    }
                }
                catch (JsonException) { }
            };
            browser.CoreWebView2.NavigationCompleted += (_, navigation) =>
            {
                webPageReady = navigation.IsSuccess;
                if (webPageReady && pendingUpdateNotice is not null)
                {
                    var message = pendingUpdateNotice;
                    pendingUpdateNotice = null;
                    ShowUpdateNotice(message);
                }
                if (webPageReady && openAssistantWhenReady) OpenAssistantPage();
            };
            browser.CoreWebView2.Navigate(url);
            assistantMonitor.Start();
            _ = ReleaseUpdater.CheckAndInstallAsync(ShowUpdateNotice, () =>
            {
                reallyExit = true;
                trayIcon.Visible = false;
                Application.Exit();
            });
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

    private void ShowUpdateNotice(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => ShowUpdateNotice(message));
            return;
        }
        if (!webPageReady || browser.CoreWebView2 is null)
        {
            pendingUpdateNotice = message;
            return;
        }
        var encodedMessage = JsonSerializer.Serialize(message);
        _ = browser.CoreWebView2.ExecuteScriptAsync($"window.greeUpdateNotice?.({encodedMessage})");
    }

    private void ShowAssistantToast(string title, string body)
    {
        assistantToast?.Close();
        assistantToast = new AssistantToastForm(title, body, () =>
        {
            OpenAssistantPage();
        }, OpenControlPage, ApplyAssistantRecommendation, ToggleGreePower);
        assistantToast.Show();
    }

    private void OpenAssistantPage()
    {
        RestoreFromTray();
        openAssistantWhenReady = true;
        if (!webPageReady || browser.CoreWebView2 is null) return;
        openAssistantWhenReady = false;
        _ = browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.nav button[data-view=assistant]')?.click();setTimeout(()=>{document.querySelector('#assistantChatForm')?.scrollIntoView({block:'end',behavior:'smooth'});document.querySelector('#assistantChatInput')?.focus()},220)");
    }

    private void OpenControlPage()
    {
        RestoreFromTray();
        openAssistantWhenReady = false;
        if (webPageReady && browser.CoreWebView2 is not null)
            _ = browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.nav button[data-view=control]')?.click()");
    }

    private void ApplyAssistantRecommendation() => _ = browser.CoreWebView2?.ExecuteScriptAsync("window.greeAssistantApplyFromBubble?.()");

    private void ToggleGreePower() => _ = browser.CoreWebView2?.ExecuteScriptAsync("window.greeActions?.togglePower?.()");

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
