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
        Diagnostics.Install();
        // Uma instância só: se já existe, a segunda apenas abre a barra.
        if (!SingleInstance.TryAcquire())
        {
            SingleInstance.SignalExisting();
            Shutdown();
            return;
        }

        try
        {
            BuildTray();
            RegisterHotkey();
            Sync.Start();
        }
        catch (Exception error)
        {
            // Sem bandeja/atalho o app ainda deve abrir: registra e segue.
            Diagnostics.Report(error, "Startup", fatal: false);
        }

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
        if (silent)
        {
            // Iniciado com o Windows: fica na bandeja, com um aviso de que está vivo.
            _tray?.ShowBalloonTip(5000, "Orbi está pronto",
                "Aperte Alt+Space para abrir. Clique com o botão direito aqui para ajustes.",
                Forms.ToolTipIcon.Info);
            return;
        }

        ShowBar();

        // Primeiro uso sem conta: pergunta DEPOIS que a barra já apareceu,
        // para o app nunca parecer "travado".
        if (!Firebase.SignedIn && !Store.Current.AskedAccount)
        {
            Store.Current.AskedAccount = true;
            Store.Save();
            Dispatcher.BeginInvoke(new Action(AskAccount), DispatcherPriority.ApplicationIdle);
        }
    }

    /// <summary>Pergunta uma vez se a pessoa quer vincular a conta.</summary>
    private void AskAccount()
    {
        var answer = System.Windows.MessageBox.Show(
            "Quer entrar na conta do Orbi neste PC?\n\n" +
            "Sincroniza apelidos, atalhos de texto, rotinas e preferências entre o site e o app. " +
            "Notas, área de transferência e a chave do Gemini continuam só neste computador.\n\n" +
            "Você pode fazer isso depois em Ajustes ▸ Conta.",
            "Orbi — conta",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer == MessageBoxResult.Yes) ShowLogin();
    }

    /* ----------------------------- bandeja ----------------------------- */
    private void BuildTray()
    {
        _tray = new Forms.NotifyIcon
        {
            Icon = TrayIcon.Load(),
            Visible = true,
            Text = "Orbi — clique para abrir",
        };
        _tray.DoubleClick += (_, _) => ShowBar();

        var menu = new Forms.ContextMenuStrip();
        menu.Opening += (_, _) =>
        {
            _tray!.Text = string.IsNullOrEmpty(HotKey.Combination)
                ? "Orbi — clique para abrir"
                : $"Orbi — {HotKey.Combination}";
            if (menu.Items.Count > 0)
            {
                menu.Items[0].Text = string.IsNullOrEmpty(HotKey.Combination)
                    ? "Abrir o Orbi"
                    : $"Abrir o Orbi ({HotKey.Combination})";
            }
        };
        menu.Items.Add("Abrir o Orbi", null, (_, _) => ShowBar());
        menu.Items.Add("Ajustes…", null, (_, _) => ShowSettings());
        menu.Items.Add(Firebase.SignedIn ? "Conta e sincronização…" : "Entrar na conta…", null, (_, _) => ShowAccount());
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
    /// <summary>
    /// Registra Alt+Space. O hotkey do Win32 precisa de um HWND: criamos a barra
    /// (ela ainda não aparece) e reagimos ao evento SourceInitialized, que é
    /// quando a janela realmente passa a existir. Antes, TryRegister() era
    /// chamado antes do handle existir e o atalho nunca era registrado.
    /// </summary>
    private void RegisterHotkey()
    {
        _bar ??= new CommandBar();
        HotKey.Attach(_bar, () => ShowBar());
    }

    /* ----------------------------- janelas ----------------------------- */
    public void ShowBar()
    {
        _bar ??= new CommandBar();
        if (_bar.WindowState == WindowState.Minimized) _bar.WindowState = WindowState.Normal;
        _bar.ShowBar();
    }

    public void ShowLogin()
    {
        var login = new LoginWindow { Owner = null };
        var ok = login.ShowDialog();
        if (ok == true)
        {
            _bar?.RefreshGreeting();
            _settings?.Refresh();
        }
    }

    public void ShowAccount()
    {
        if (!Firebase.SignedIn)
        {
            ShowLogin();
            return;
        }
        ShowSettings("conta");
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

    /// <summary>Fechar a barra não pode matar o app: ela é a janela principal.</summary>
    public bool ClosingForReal { get; private set; }

    public void ExitApp()
    {
        ClosingForReal = true;
        Cleanup();
        Shutdown();
    }

    private void Cleanup()
    {
        try
        {
            Store.Save();
            if (_tray is not null)
            {
                _tray.Visible = false;
                _tray.Dispose();
            }
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
