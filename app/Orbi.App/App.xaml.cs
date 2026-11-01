using System.Windows;
using System.Windows.Threading;
using Orbi.Services;
using Forms = System.Windows.Forms;

namespace Orbi;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _tray;
    private CommandBar? _bar;
    private SettingsWindow? _settings;
    private DispatcherTimer? _reminderTimer;
    private DispatcherTimer? _idleTimer;
    private DateTime _lastActivity = DateTime.Now;

    public static App Instance => (App)System.Windows.Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Uma instância só: se já existe, a segunda apenas abre a barra.
        if (!SingleInstance.TryAcquire())
        {
            SingleInstance.SignalExisting();
            Shutdown();
            return;
        }

        if (string.IsNullOrWhiteSpace(Store.Current.PairingCode))
        {
            Store.Current.PairingCode = PairingCodes.Generate();
            Store.Save();
        }

        BuildTray();
        RegisterHotkey();

        _reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _reminderTimer.Tick += (_, _) => CheckReminders();
        _reminderTimer.Start();

        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _idleTimer.Tick += (_, _) => CheckIdle();
        _idleTimer.Start();

        if (e.Args.Contains("--uninstall"))
        {
            // O desinstalador chama isto para encerrar o app limpo.
            Cleanup();
            Shutdown();
            return;
        }

        if (e.Args.Contains("--settings"))
        {
            ShowSettings();
            return;
        }

        var silent = e.Args.Contains("--tray");
        if (!silent)
        {
            ShowBar();
        }
    }

    /* ----------------------------- bandeja ----------------------------- */
    private void BuildTray()
    {
        _tray = new Forms.NotifyIcon
        {
            Icon = TrayIcon.Load(),
            Visible = true,
            Text = "Orbi — Alt+Space",
        };
        _tray.DoubleClick += (_, _) => ShowBar();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Abrir Orbi (Alt+Space)", null, (_, _) => ShowBar());
        menu.Items.Add("Ajustes…", null, (_, _) => ShowSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Bloquear o Windows", null, (_, _) => SystemActions.Lock());
        menu.Items.Add("Status do PC", null, (_, _) =>
        {
            var snapshot = SystemActions.Snapshot();
            Forms.MessageBox.Show(
                $"CPU: {snapshot.Cpu:0}%\nMemória: {snapshot.MemoryUsedGb:0.#} / {snapshot.MemoryTotalGb:0.#} GB\n" +
                $"Disco livre: {snapshot.DiskFreeGb:0.#} GB\n" +
                (snapshot.Battery is { } b ? $"Bateria: {b}%\n" : "") +
                $"Processo mais pesado: {snapshot.TopProcess ?? "—"}",
                "Orbi — status",
                Forms.MessageBoxButtons.OK,
                Forms.MessageBoxIcon.Information);
        });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => ExitApp());
        _tray.ContextMenuStrip = menu;
    }

    /* ----------------------------- atalho global ----------------------------- */
    private void RegisterHotkey()
    {
        HotKey.Register(handle => { }, () => ShowBar());
        // O hook precisa de uma janela viva; usamos a barra oculta como âncora.
        _bar = new CommandBar();
        HotKey.Attach(_bar);
        HotKey.TryRegister();
    }

    /* ----------------------------- janelas ----------------------------- */
    public void ShowBar()
    {
        _bar ??= new CommandBar();
        _bar.ShowBar();
    }

    public void ShowSettings(string? tab = null)
    {
        if (_settings is null)
        {
            _settings = new SettingsWindow();
            _settings.Closed += (_, _) => _settings = null;
        }
        _settings.Show();
        if (_settings.WindowState == WindowState.Minimized) _settings.WindowState = WindowState.Normal;
        _settings.Activate();
        if (tab is not null) _settings.SelectTab(tab);
    }

    public void ExitApp()
    {
        Cleanup();
        Shutdown();
    }

    private void Cleanup()
    {
        try
        {
            Store.Save();
            _tray?.Visible = false;
            _tray?.Dispose();
            HotKey.Detach();
            SingleInstance.Release();
        }
        catch
        {
            // encerrando de qualquer forma
        }
    }

    /* ----------------------------- lembretes ----------------------------- */
    private void CheckReminders()
    {
        var now = DateTime.Now;
        var due = Store.Current.Reminders
            .Where(reminder => !reminder.Done && reminder.DueAt <= now.AddMinutes(0.05))
            .ToList();

        foreach (var reminder in due)
        {
            reminder.Done = true;
            _tray?.ShowBalloonTip(8000, "Orbi", reminder.Text, Forms.ToolTipIcon.Info);
            System.Media.SystemSounds.Asterisk.Play();
        }

        if (due.Count > 0) Store.Save();
    }

    /* ----------------------------- bloqueio por inatividade ----------------------------- */
    private void CheckIdle()
    {
        if (!Store.Current.LockEnabled) return;

        var idle = IdleTime.Seconds();
        var limit = Math.Max(1, Store.Current.LockAfterMinutes) * 60;

        if (idle < limit)
        {
            _lastActivity = DateTime.Now.AddSeconds(-idle);
            return;
        }

        // Avisa uma vez e dá 15 s para cancelar.
        if (idle > limit && idle < limit + 15 && !_warned)
        {
            _warned = true;
            _tray?.ShowBalloonTip(6000, "Orbi",
                $"Vou bloquear o Windows em 15 s. Mexa o mouse para cancelar.",
                Forms.ToolTipIcon.Warning);
            return;
        }

        if (idle >= limit + 15)
        {
            _warned = false;
            SystemActions.Lock();
        }
    }

    private bool _warned;

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        Store.Save();
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Store.Save();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
