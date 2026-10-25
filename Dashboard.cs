using System.Drawing.Drawing2D;

namespace GlucoGeek;

internal sealed class Dashboard : Form
{
    public event Action? RefreshRequested;
    public event Action? SettingsRequested;
    private readonly Label headline = new() { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 34, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label status = new() { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(151, 169, 188), TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label details = new() { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11), Padding = new Padding(0, 12, 0, 0) };
    private readonly Label error = new() { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(255, 185, 113), AutoEllipsis = true };
    private readonly GlucoseChart chart = new() { Dock = DockStyle.Fill };
    private readonly Button refresh = new() { Text = "Refresh now", AutoSize = true };
    public Dashboard()
    {
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Text = "GlucoGeek"; ClientSize = new Size(580, 650); MinimumSize = new Size(550, 640);
        StartPosition = FormStartPosition.CenterScreen; BackColor = Color.FromArgb(16, 25, 36); ForeColor = Color.FromArgb(230, 238, 247);
        Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 8 };
        foreach (int h in new[] { 30, 82, 40, 210, 125, 50, 45 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "GLUCOGEEK   /   GLUCOSE", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(114, 210, 188) });
        layout.Controls.Add(headline); layout.Controls.Add(status); layout.Controls.Add(chart); layout.Controls.Add(details); layout.Controls.Add(error);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill };
        var settings = new Button { Text = "Settings…", AutoSize = true };
        foreach (var b in new[] { refresh, settings }) { b.BackColor = Color.FromArgb(38, 57, 76); b.ForeColor = ForeColor; b.FlatStyle = FlatStyle.Flat; buttons.Controls.Add(b); }
        refresh.Click += (_, _) => RefreshRequested?.Invoke(); settings.Click += (_, _) => SettingsRequested?.Invoke();
        layout.Controls.Add(buttons);
        layout.Controls.Add(new Label { Text = "Companion display only. Use your official app or meter for treatment decisions.", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8), ForeColor = Color.FromArgb(151, 169, 188) });
        Controls.Add(layout);
    }
    internal static Color ReadingColour(Snapshot s, Settings settings) => !settings.Colour ? Color.White : s.Current.Low || s.Current.Mgdl < s.Low ? Color.FromArgb(255, 112, 125) : s.Current.High || s.Current.Mgdl > s.High ? Color.FromArgb(255, 185, 85) : Color.FromArgb(114, 224, 188);
    public void UpdateView(Snapshot? snapshot, Settings settings, string message, bool busy, bool demo = false)
    {
        refresh.Enabled = !busy; refresh.Text = busy ? "Refreshing…" : "Refresh now";
        error.Text = message;
        chart.Data = snapshot; chart.Mmol = snapshot != null && settings.IsMmol(snapshot); chart.Invalidate();
        if (snapshot == null) { headline.Text = "No reading yet"; headline.ForeColor = ForeColor; status.Text = "Connect your glucose account in Settings."; details.Text = "Your current glucose and three-hour history will appear here."; return; }
        var r = snapshot.Current; bool mmol = settings.IsMmol(snapshot); string unit = mmol ? "mmol/L" : "mg/dL";
        bool stale = r.Stale(DateTimeOffset.UtcNow);
        headline.Text = Snapshot.Format(r.Mgdl, mmol) + (settings.ShowTrend ? " " + r.Arrow : "") + " " + unit;
        headline.ForeColor = stale || message.Length > 0 && !demo ? Color.Silver : ReadingColour(snapshot, settings);
        status.Text = $"{r.TrendText} · {Math.Max(0, (DateTimeOffset.UtcNow - r.At).TotalMinutes):F0} min old" + (stale ? " · STALE / CHECK TIMESTAMP" : "") + (message.Length > 0 && !demo ? " · LAST REFRESH FAILED" : "");
        var window = snapshot.Window;
        string stats = window.Count < 2 ? "Not enough history for statistics." : $"Last 3h: average {Snapshot.Format(window.Average(p => p.Mgdl), mmol)} {unit} · {100.0 * window.Count(p => p.Mgdl >= snapshot.Low && p.Mgdl <= snapshot.High) / window.Count:F0}% of readings in range";
        var prev = snapshot.Previous;
        string delta = prev == null ? "Change unavailable" : $"Change: {(r.Mgdl > prev.Mgdl ? "+" : "")}{Snapshot.Format(r.Mgdl - prev.Mgdl, mmol)} {unit} over {(r.At - prev.At).TotalMinutes:F0} min";
        string sensor = snapshot.Sensor is { } started && started <= DateTimeOffset.UtcNow ? $" · Sensor age {(DateTimeOffset.UtcNow - started).TotalDays:F1} days" : "";
        details.Text = $"{settings.Provider}: {snapshot.Patient}\n{delta}\n{stats}\nTarget {Snapshot.Format(snapshot.Low, mmol)}–{Snapshot.Format(snapshot.High, mmol)} {unit}{sensor}";
    }
}

internal sealed class GlucoseChart : Control
{
    public Snapshot? Data;
    public bool Mmol;
    public GlucoseChart() { DoubleBuffered = true; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(22, 34, 47));
        if (Data is not { } s) { g.DrawString("Three-hour history", Font, Brushes.Silver, 16, 16); return; }
        var history = s.Window; float left = 48, top = 16, width = Math.Max(1, Width - 64), height = Math.Max(1, Height - 48);
        double min = Math.Max(0, Math.Min(s.Low - 20, history.Min(r => r.Mgdl) - 15)), max = Math.Max(s.High + 20, history.Max(r => r.Mgdl) + 15);
        float Y(double v) => top + (float)((max - v) / (max - min)) * height;
        float X(DateTimeOffset t) => left + (float)((t - s.Current.At.AddHours(-3)).TotalHours / 3) * width;
        using var shade = new SolidBrush(Color.FromArgb(26, 69, 61));
        g.FillRectangle(shade, left, Y(s.High), width, Y(s.Low) - Y(s.High));
        using var line = new Pen(Color.FromArgb(105, 222, 187), 2);
        using var grid = new Pen(Color.FromArgb(54, 77, 88));
        foreach (var v in new[] { s.Low, s.High }) { g.DrawLine(grid, left, Y(v), left + width, Y(v)); g.DrawString(Snapshot.Format(v, Mmol), Font, Brushes.Silver, 2, Y(v) - 8); }
        for (int i = 1; i < history.Count; i++)
            if (history[i].At - history[i - 1].At <= TimeSpan.FromMinutes(20)) g.DrawLine(line, X(history[i - 1].At), Y(history[i - 1].Mgdl), X(history[i].At), Y(history[i].Mgdl));
        foreach (var r in history) g.FillEllipse(Brushes.Aquamarine, X(r.At) - 2, Y(r.Mgdl) - 2, 4, 4);
        g.DrawString(s.Current.At.AddHours(-3).ToLocalTime().ToString("HH:mm"), Font, Brushes.Silver, left, top + height + 7);
        g.DrawString(s.Current.At.ToLocalTime().ToString("HH:mm"), Font, Brushes.Silver, left + width - 42, top + height + 7);
    }
}
