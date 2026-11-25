using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Text.Json;

namespace GreeControl.Native;

public sealed class Form1 : Form
{
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
        ClientSize = new Size(446, 760);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(360, 440);
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
        Show();
        if (WindowState == FormWindowState.Minimized)
            WindowState = FormWindowState.Normal;
        Activate();
        browser.Focus();
    }

    private void ExitFromTray()
    {
        reallyExit = true;
        Close();
    }

    protected override void OnShown(EventArgs e)
    {
        var area = Screen.FromControl(this).WorkingArea;
        Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
        base.OnShown(e);
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
