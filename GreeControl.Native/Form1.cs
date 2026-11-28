using System.IO;
using System.Diagnostics;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Text.Json;

namespace GreeControl.Native;

public sealed class Form1 : Window
{
    private const double PanelOpacity = 0.87;
    private readonly DispatcherTimer panelAnimation = new() { Interval = TimeSpan.FromMilliseconds(15) };
    private readonly Stopwatch transitionClock = new();
    private Rect restingBounds = Rect.Empty;
    private bool panelRequested = true;
    private bool panelReady;
    private bool exitRequested;
    private double progress;
    private double transitionStart;
    private double transitionTarget;
    private Action? afterHide;

    private readonly WebView2CompositionControl browser = new() { DefaultBackgroundColor = System.Drawing.Color.FromArgb(25, 35, 45) };
    private readonly Grid panel = new() { Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(25, 35, 45)), Opacity = 0 };
    private readonly Forms.NotifyIcon trayIcon;
    private Process? bridge;
    private bool reallyExit;
    private bool closed;
    private readonly DispatcherTimer assistantMonitor = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly DispatcherTimer updateMonitor = new() { Interval = TimeSpan.FromMinutes(15) };
    private bool checkingUpdate;
    private bool webPageReady;
    private string? pendingUpdateNotice;
    private string? pendingUpdateAction;
    private bool pendingUpdatePersistent;

    public Form1()
    {
        Title = "Gree Control";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        PositionAboveClock();

        var iconPath = Path.Combine(AppContext.BaseDirectory, "gree-control.ico");
        var icon = File.Exists(iconPath) ? new System.Drawing.Icon(iconPath) : null;
        var trayMenu = new Forms.ContextMenuStrip();
        trayMenu.Items.Add("Abrir Gree Control", null, (_, _) => RestoreFromTray());
        trayMenu.Items.Add("Sair", null, (_, _) => ExitFromTray());
        trayIcon = new Forms.NotifyIcon
        {
            Text = "Gree Control",
            Icon = icon ?? System.Drawing.SystemIcons.Application,
            ContextMenuStrip = trayMenu,
            Visible = true
        };
        trayIcon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) RestoreFromTray(); };
        assistantMonitor.Tick += (_, _) =>
        {
            if (webPageReady && browser.CoreWebView2 is not null)
                _ = browser.CoreWebView2.ExecuteScriptAsync("(async()=>{await window.greeActions?.refreshStatus?.();window.greeAssistantBackgroundCheck?.();window.greeAutomationTick?.()})()");
        };
        updateMonitor.Tick += async (_, _) => await CheckForUpdatesAsync();
        panel.Children.Add(browser);
        Content = panel;
        panel.SizeChanged += (_, e) =>
        {
            var radius = Math.Min(24, Math.Min(e.NewSize.Width, e.NewSize.Height) / 2);
            panel.Clip = new RectangleGeometry(new Rect(e.NewSize), radius, radius);
        };
        panelAnimation.Tick += (_, _) => AdvanceAnimation();
        Loaded += LoadApp;
        Closed += (_, _) =>
        {
            closed = true;
            panelAnimation.Stop();
            assistantMonitor.Stop();
            updateMonitor.Stop();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            trayMenu.Dispose();
            icon?.Dispose();
            browser.Dispose();
            StopBridge();
        };
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!reallyExit)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        if (!exitRequested && panelRequested)
            HideToTray();
    }

    private void HideToTray()
    {
        panelRequested = false;
        AnimatePanel(0);
    }

    private void RestoreFromTray()
    {
        if (exitRequested)
            return;

        panelRequested = true;
        PositionAboveClock();
        Show();
        Activate();
        browser.Focus();
        if (panelReady)
            AnimatePanel(1);
    }

    private void ExitFromTray()
    {
        CloseAfterAnimation(() =>
        {
            reallyExit = true;
            Close();
        });
    }

    private void CloseAfterAnimation(Action onHidden)
    {
        if (exitRequested)
            return;

        exitRequested = true;
        afterHide = onHidden;
        HideToTray();
    }

    private void AnimatePanel(double target)
    {
        if (panelAnimation.IsEnabled && transitionTarget == target)
            return;

        transitionStart = progress;
        transitionTarget = target;
        transitionClock.Restart();
        if (progress != target)
        {
            panelAnimation.Start();
            return;
        }

        FinishAnimation();
    }

    private void AdvanceAnimation()
    {
        var duration = transitionTarget == 1 ? 210 : 155;
        var fraction = Math.Min(1, transitionClock.Elapsed.TotalMilliseconds / duration);
        var eased = transitionTarget == 1 ? 1 - Math.Pow(1 - fraction, 3) : fraction * fraction;
        progress = transitionStart + (transitionTarget - transitionStart) * eased;
        ApplyPanelFrame();
        if (fraction >= 1)
            FinishAnimation();
    }

    private void FinishAnimation()
    {
        panelAnimation.Stop();
        progress = transitionTarget;
        ApplyPanelFrame();
        if (progress != 0)
            return;

        Hide();
        var callback = afterHide;
        afterHide = null;
        callback?.Invoke();
    }

    private void ApplyPanelFrame()
    {
        var opacity = SystemParameters.HighContrast ? 1 : PanelOpacity;
        panel.Opacity = progress * opacity;
        Left = restingBounds.Left;
        Top = restingBounds.Top + (1 - progress) * 14;
    }

    private void PositionAboveClock()
    {
        var area = SystemParameters.WorkArea;
        const double inset = 8;
        Width = Math.Min(446, Math.Max(1, area.Width - inset * 2));
        Height = Math.Min(760, Math.Max(1, area.Height - inset * 2));
        restingBounds = new Rect(area.Right - Width - inset, area.Bottom - Height - inset, Width, Height);
        ApplyPanelFrame();
    }

    private async void LoadApp(object sender, RoutedEventArgs e)
    {
        Loaded -= LoadApp;
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
            await browser.EnsureCoreWebView2Async();
            await browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync($$"""
                if (window === window.top && location.origin === {{origin}}) document.addEventListener('DOMContentLoaded', () => {
                    const root = document.documentElement;
                    root.setAttribute('data-native-panel', '');
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
                    CloseAfterAnimation(() =>
                    {
                        if (ReleaseUpdater.InstallPending(ShowUpdateNotice, () =>
                        {
                            reallyExit = true;
                            Close();
                        }))
                            return;

                        exitRequested = false;
                        RestoreFromTray();
                    });
                    return;
                }
            };
            browser.CoreWebView2.NavigationCompleted += (_, navigation) =>
            {
                if (!panelReady)
                {
                    panelReady = true;
                    if (panelRequested)
                        AnimatePanel(1);
                }
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
            updateMonitor.Start();
            _ = CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "GreeControl-startup.log"), $"{DateTime.Now:O}{Environment.NewLine}{ex}{Environment.NewLine}"); } catch { }
            if (closed) return;
            System.Windows.MessageBox.Show(ex.Message, "Gree Control", MessageBoxButton.OK, MessageBoxImage.Error);
            reallyExit = true;
            trayIcon.Visible = false;
            Close();
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        if (checkingUpdate || closed)
            return;

        checkingUpdate = true;
        try
        {
            await ReleaseUpdater.CheckForUpdateAsync((message, action, persistent) =>
            {
                if (!closed)
                    ShowUpdateNotice(message, action, persistent);
            });
        }
        finally
        {
            checkingUpdate = false;
        }
    }

    private void ShowUpdateNotice(string message, string? actionLabel = null, bool persistent = false)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => ShowUpdateNotice(message, actionLabel, persistent));
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
