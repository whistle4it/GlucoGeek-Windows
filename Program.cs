using System.Runtime.InteropServices;

namespace Librewin;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--preview-tray"))
        {
            TrayArtwork.Preview(args.Last()); return 0;
        }
        if (args.Contains("--smoke-test"))
        {
            using var context = new TrayApp(new Settings(), true, true);
            using var stop = new System.Windows.Forms.Timer { Interval = 500 };
            stop.Tick += (_, _) => context.ExitThread(); stop.Start();
            Application.Run(context);
            File.WriteAllText(args.Last(), "PASS tray creation, render, message loop and shutdown"); return 0;
        }
        if (args.Contains("--self-test")) return SelfTests.Run(args.Last());
        if (args.Contains("--preview-settings"))
        {
            using var form = new SettingsDialog(new Settings());
            form.Show(); Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            bitmap.Save(args.Last()); return 0;
        }
        if (args.Contains("--preview"))
        {
            using var form = new Dashboard();
            form.UpdateView(SelfTests.Demo(), new Settings(), "DEMO DATA — not a live reading", false, true);
            form.Show();
            Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            bitmap.Save(args.Last());
            return 0;
        }
        using var mutex = new Mutex(true, @"Local\Librewin", out bool first);
        if (!first) { MessageBox.Show("Librewin is already running. Click its system tray icon.", "Librewin"); return 0; }
        try { Application.Run(new TrayApp(Settings.Load(), args.Contains("--background"))); }
        catch (Exception ex) { MessageBox.Show("Librewin could not start: " + ex.Message, "Librewin", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
        return 0;
    }
}

internal sealed class TrayApp : ApplicationContext
{
    private Settings settings;
    private LibreLinkClient client;
    private readonly NotifyIcon tray = new() { Visible = true, Text = "Librewin" };
    private Icon? currentIcon;
    private readonly Dashboard dashboard = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 15000 };
    private readonly CancellationTokenSource shutdown = new();
    private Snapshot? snapshot;
    private string error = "";
    private DateTimeOffset next = DateTimeOffset.MinValue;
    private bool busy, stopping;
    public TrayApp(Settings settings, bool background, bool smoke = false)
    {
        this.settings = settings;
        client = new(settings);
        // Establish a WinForms synchronization context before any asynchronous work.
        _ = dashboard.Handle;
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Librewin", null, (_, _) => Open());
        menu.Items.Add("Refresh now", null, async (_, _) => await Refresh());
        menu.Items.Add("Settings…", null, async (_, _) => await Configure());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit Librewin", null, (_, _) => ExitThread());
        tray.ContextMenuStrip = menu;
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) Open(); };
        dashboard.RefreshRequested += async () => await Refresh();
        dashboard.SettingsRequested += async () => await Configure();
        dashboard.FormClosing += (_, e) => { if (!stopping) { e.Cancel = true; dashboard.Hide(); } };
        timer.Tick += async (_, _) => { Render(); if (DateTimeOffset.UtcNow >= next) await Refresh(); };
        Render();
        timer.Start();
        dashboard.BeginInvoke(async () =>
        {
            if (smoke) return;
            if (!background || settings.PasswordProtected.Length == 0) Open();
            if (settings.PasswordProtected.Length == 0) await Configure();
            else await Refresh();
        });
    }
    private void Open() { dashboard.Show(); dashboard.Activate(); Render(); }
    private async Task Configure()
    {
        if (busy) { Open(); return; }
        using var dialog = new SettingsDialog(settings);
        if (dialog.ShowDialog(dashboard) != DialogResult.OK) return;
        settings = dialog.Result;
        client.Dispose();
        client = new(settings);
        snapshot = null; // Never leave another account's reading on screen.
        error = "";
        await Refresh();
    }
    private async Task Refresh()
    {
        if (busy || stopping) return;
        busy = true;
        Render();
        try { snapshot = await client.Fetch(shutdown.Token); error = ""; }
        catch (OperationCanceledException) { if (!stopping) error = "Request timed out. Check your connection and retry."; }
        catch (Exception ex) { error = ex is SessionExpired ? "Session expired. Reconnect in Settings." : ex.Message; }
        finally
        {
            busy = false;
            next = DateTimeOffset.UtcNow.AddMinutes(new[] { 1, 2, 5, 10 }.Contains(settings.Minutes) ? settings.Minutes : 1);
            if (!stopping) Render();
        }
    }
    private void Render()
    {
        dashboard.UpdateView(snapshot, settings, error, busy);
        bool stale = snapshot?.Current.Stale(DateTimeOffset.UtcNow) ?? true;
        string value = snapshot == null ? "--" : Snapshot.Format(snapshot.Current.Mgdl, settings.IsMmol(snapshot));
        // The tray always communicates range and trend; display preferences apply to the dashboard.
        Color colour = snapshot == null ? Color.Gray : TrayArtwork.RangeColour(snapshot);
        using var bitmap = TrayArtwork.Draw(snapshot?.Current.Trend ?? 0, colour,
            error.Length > 0 ? "error" : snapshot == null ? "unknown" : stale ? "stale" : "live");
        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            var icon = (Icon)temporary.Clone();
            tray.Icon = icon;
            currentIcon?.Dispose();
            currentIcon = icon;
        }
        finally { DestroyIcon(handle); }
        string tooltip = "Librewin: " + value + (snapshot == null ? "" : " " + (settings.IsMmol(snapshot) ? "mmol/L" : "mg/dL") + (settings.ShowTrend ? " " + snapshot.Current.Arrow : "")) + (error.Length > 0 ? " — refresh failed" : stale ? " — stale / unavailable" : "");
        tray.Text = tooltip[..Math.Min(63, tooltip.Length)];
    }
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    protected override void ExitThreadCore()
    {
        stopping = true;
        shutdown.Cancel();
        timer.Stop(); timer.Dispose(); tray.Visible = false; tray.Dispose(); currentIcon?.Dispose();
        dashboard.Dispose(); client.Dispose();
        base.ExitThreadCore();
    }
}
