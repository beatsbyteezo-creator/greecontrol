using System.Drawing.Drawing2D;

namespace GreeControl.Native;

internal sealed class AssistantToastForm : Form
{
    private readonly System.Windows.Forms.Timer motion = new() { Interval = 16 };
    private readonly Action activate;
    private readonly Label heading;
    private readonly Label body;
    private readonly Button chatButton;
    private readonly AssistantAvatar avatar;
    private readonly Action openControl;
    private readonly Action applyRecommendation;
    private readonly Action togglePower;
    private readonly ToolStripMenuItem deviceSummaryItem;
    private int targetY;
    private int phase;
    private int visibleTicks;
    private bool collapsed;
    private bool pointerDown;
    private bool dragged;
    private Point pointerOrigin;
    private Point bubbleOrigin;

    public AssistantToastForm(string title, string message, Action activate, Action openControl, Action applyRecommendation, Action togglePower)
    {
        this.activate = activate;
        this.openControl = openControl;
        this.applyRecommendation = applyRecommendation;
        this.togglePower = togglePower;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(370, 104);
        BackColor = Color.FromArgb(1, 2, 3);
        TransparencyKey = BackColor;
        DoubleBuffered = true;
        Opacity = 0;

        avatar = new AssistantAvatar { Location = new Point(13, 19), Size = new Size(62, 62) };
        heading = new Label { Text = title, Location = new Point(88, 17), Size = new Size(260, 22), ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold), BackColor = Color.Transparent };
        body = new Label { Text = message, Location = new Point(88, 42), Size = new Size(176, 45), ForeColor = Color.FromArgb(213, 223, 239), Font = new Font("Segoe UI", 9), BackColor = Color.Transparent };
        chatButton = new Button { Text = "Conversar", Location = new Point(268, 64), Size = new Size(83, 26), ForeColor = Color.White, BackColor = Color.FromArgb(58, 112, 191), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8, FontStyle.Bold), Cursor = Cursors.Hand };
        chatButton.FlatAppearance.BorderSize = 0;
        chatButton.Region = new Region(new Rectangle(0, 0, chatButton.Width, chatButton.Height));
        Controls.AddRange([avatar, heading, body, chatButton]);
        Click += OnActivate;
        avatar.MouseDown += BeginBubbleDrag;
        avatar.MouseMove += MoveBubbleDrag;
        avatar.MouseUp += EndBubbleDrag;
        heading.Click += OnActivate;
        body.Click += OnActivate;
        chatButton.Click += OnActivate;
        chatButton.BringToFront();
        ContextMenuStrip = new ContextMenuStrip();
        deviceSummaryItem = new ToolStripMenuItem("Estado do Gree: aguardando") { Enabled = false };
        ContextMenuStrip.Items.Add(deviceSummaryItem);
        ContextMenuStrip.Items.Add(new ToolStripSeparator());
        ContextMenuStrip.Items.Add("Conversar com o assistente", null, (_, _) => OnActivate(this, EventArgs.Empty));
        ContextMenuStrip.Items.Add("Abrir controle do Gree", null, (_, _) => this.openControl());
        ContextMenuStrip.Items.Add("Aplicar recomendação", null, (_, _) => this.applyRecommendation());
        ContextMenuStrip.Items.Add("Ligar / desligar", null, (_, _) => this.togglePower());
        ContextMenuStrip.Items.Add("Ocultar assistente", null, (_, _) => Close());
        avatar.ContextMenuStrip = ContextMenuStrip;
        motion.Tick += MoveToast;
        Shown += (_, _) =>
        {
            var area = Screen.FromPoint(Cursor.Position).WorkingArea;
            targetY = area.Top + 14;
            Location = new Point(area.Right - Width - 18, area.Top - Height - 8);
            motion.Start();
        };
        FormClosed += (_, _) => motion.Dispose();
    }

    public string DeviceSummary
    {
        set
        {
            if (IsDisposed) return;
            deviceSummaryItem.Text = value;
            body.Text = value;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (collapsed) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var radius = collapsed ? 40 : 22;
        using var path = RoundedRectangle(new Rectangle(1, 1, Width - 3, Height - 3), radius);
        using var fill = new LinearGradientBrush(ClientRectangle, Color.FromArgb(244, 42, 49, 64), Color.FromArgb(247, 22, 27, 38), 110f);
        using var edge = new Pen(Color.FromArgb(72, 224, 237, 255), 1);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(edge, path);
    }

    private void MoveToast(object? sender, EventArgs e)
    {
        if (phase == 0)
        {
            var delta = targetY - Top;
            Top += Math.Clamp(delta / 8, 3, 10);
            Opacity = Math.Min(1, Opacity + .08);
            if (Top >= targetY) { Top = targetY; phase = 1; }
            return;
        }
        if (phase == 1 && ++visibleTicks < 360) return;
        CollapseToBubble();
    }

    private void OnActivate(object? sender, EventArgs e)
    {
        if (phase == 0) { Top = targetY; Opacity = 1; phase = 1; }
        CollapseToBubble();
        activate();
    }

    private void CollapseToBubble()
    {
        if (collapsed) return;
        collapsed = true;
        heading.Visible = body.Visible = chatButton.Visible = false;
        Size = new Size(64, 64);
        avatar.Location = Point.Empty;
        avatar.Size = new Size(64, 64);
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Left = area.Right - Width - 18;
        Invalidate();
        motion.Stop();
    }

    private void BeginBubbleDrag(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        pointerDown = true;
        dragged = false;
        pointerOrigin = Cursor.Position;
        bubbleOrigin = Location;
    }

    private void MoveBubbleDrag(object? sender, MouseEventArgs e)
    {
        if (!pointerDown || !collapsed) return;
        var pointer = Cursor.Position;
        var dx = pointer.X - pointerOrigin.X;
        var dy = pointer.Y - pointerOrigin.Y;
        if (!dragged && Math.Abs(dx) + Math.Abs(dy) < 6) return;
        dragged = true;
        var area = Screen.FromPoint(pointer).WorkingArea;
        Left = Math.Clamp(bubbleOrigin.X + dx, area.Left, area.Right - Width);
        Top = Math.Clamp(bubbleOrigin.Y + dy, area.Top, area.Bottom - Height);
    }

    private void EndBubbleDrag(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !pointerDown) return;
        pointerDown = false;
        if (!dragged) OnActivate(sender, EventArgs.Empty);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class AssistantAvatar : Control
{
    private readonly System.Windows.Forms.Timer floatMotion = new() { Interval = 38 };
    private double phase;

    public AssistantAvatar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        floatMotion.Tick += (_, _) => { phase += .075; Invalidate(); };
        VisibleChanged += (_, _) => { if (Visible) floatMotion.Start(); else floatMotion.Stop(); };
        Disposed += (_, _) => floatMotion.Dispose();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = Math.Min(Width, Height) / 64f;
        g.TranslateTransform((Width - 64 * scale) / 2, (Height - 64 * scale) / 2);
        g.ScaleTransform(scale, scale);
        var bob = (float)Math.Sin(phase) * 3.4f;
        using var shadow = new SolidBrush(Color.FromArgb(52, 78, 53, 156)); g.FillEllipse(shadow, 8, 55 - bob * .4f, 48, 7);
        var shiver = (float)Math.Sin(phase * 3.2) * 1.15f;
        g.TranslateTransform(shiver, -bob);
        using var coat = new SolidBrush(Color.FromArgb(255, 63, 111, 166)); g.FillEllipse(coat, 9, 48, 46, 25);
        using var scarf = new SolidBrush(Color.FromArgb(255, 145, 211, 247)); g.FillRectangle(scarf, 22, 47, 20, 11);
        Point[] scarfTail = [new(25, 53), new(21, 62), new(30, 59), new(34, 53)]; g.FillPolygon(scarf, scarfTail);
        using var hair = new SolidBrush(Color.FromArgb(255, 43, 34, 72)); g.FillEllipse(hair, 9, 5, 46, 49);
        using var face = new SolidBrush(Color.FromArgb(255, 236, 203, 187));
        using (var shape = new GraphicsPath())
        {
            shape.AddBezier(15, 20, 16, 12, 23, 10, 32, 10);
            shape.AddBezier(32, 10, 43, 10, 49, 16, 49, 22);
            shape.AddLine(49, 22, 47, 39);
            shape.AddBezier(47, 39, 45, 47, 39, 52, 32, 54);
            shape.AddBezier(32, 54, 24, 52, 18, 47, 16, 39);
            shape.AddLine(16, 39, 15, 20);
            shape.CloseFigure(); g.FillPath(face, shape);
        }
        using var fringe = new GraphicsPath();
        fringe.AddBezier(12, 29, 8, 16, 15, 7, 27, 7);
        fringe.AddBezier(27, 7, 37, 1, 53, 9, 52, 23);
        fringe.AddLine(52, 23, 46, 21);
        fringe.AddBezier(46, 21, 37, 18, 27, 22, 18, 20);
        fringe.CloseFigure(); g.FillPath(hair, fringe);
        using var ear = new SolidBrush(Color.FromArgb(255, 236, 203, 187)); g.FillEllipse(ear, 11, 29, 8, 13); g.FillEllipse(ear, 45, 29, 8, 13);
        using var brow = new Pen(Color.FromArgb(255, 42, 38, 54), 2.7f); brow.StartCap = LineCap.Round; brow.EndCap = LineCap.Round;
        g.DrawLine(brow, 19, 26, 27, 25); g.DrawLine(brow, 37, 25, 45, 26);
        using var frame = new Pen(Color.FromArgb(255, 38, 42, 59), 2.7f); g.DrawEllipse(frame, 16, 29, 13, 12); g.DrawEllipse(frame, 35, 29, 13, 12); g.DrawLine(frame, 29, 34, 35, 34);
        using var eye = new Pen(Color.FromArgb(255, 42, 48, 62), 1.7f); eye.StartCap = LineCap.Round; eye.EndCap = LineCap.Round; g.DrawLine(eye, 21, 35, 25, 35); g.DrawLine(eye, 39, 35, 43, 35);
        using var coldCheek = new SolidBrush(Color.FromArgb(125, 119, 187, 228)); g.FillEllipse(coldCheek, 19, 39, 7, 4); g.FillEllipse(coldCheek, 38, 39, 7, 4);
        using var nose = new SolidBrush(Color.FromArgb(255, 203, 126, 133)); g.FillEllipse(nose, 30, 38, 4, 4);
        using var teeth = new SolidBrush(Color.FromArgb(255, 239, 246, 251)); Point[] chatteringTeeth = [new(27, 47), new(32, 45), new(37, 47), new(34, 50), new(30, 49)]; g.FillPolygon(teeth, chatteringTeeth);
        using var mouth = new Pen(Color.FromArgb(255, 96, 91, 112), 1.4f); g.DrawLine(mouth, 27, 47, 37, 47);
        using var frost = new Pen(Color.FromArgb(220, 156, 222, 255), 1.7f); frost.StartCap = LineCap.Round; frost.EndCap = LineCap.Round;
        g.DrawLine(frost, 55, 29, 55, 37); g.DrawLine(frost, 52, 31, 58, 35); g.DrawLine(frost, 58, 31, 52, 35);
        g.ResetTransform();
    }
}
