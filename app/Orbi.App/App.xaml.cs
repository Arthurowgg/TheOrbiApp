using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

    public bool BarCreated => _bar is not null;
    public bool TrayVisible => _tray?.Visible == true;

    protected override void OnStartup(StartupEventArgs e)
    {
        // --console: log de cada etapa no terminal (para descobrir onde para).
        var console = e.Args.Contains("--console") || e.Args.Contains("--console-keep");
        if (console && !ConsoleWindow.Attach())
        {
            // Sem console: pelo menos o log em arquivo continua.
            ConsoleLog.EnableConsole(false);
        }

        ConsoleLog.EnableConsole(console);
        ConsoleLog.Reset();
        ConsoleLog.Step("Orbi iniciando", string.Join(" ", e.Args));
        ConsoleLog.Step("Versão", typeof(App).Assembly.GetName().Version?.ToString() ?? "?");

        base.OnStartup(e);
        Diagnostics.Install();
        ConsoleLog.Step("Manifesto e tema carregados");
        // Uma instância só: se já existe, a segunda apenas abre a barra.
        if (!SingleInstance.TryAcquire())
        {
            ConsoleLog.Step("Já existe uma instância — pedindo para mostrar a barra");
            SingleInstance.SignalExisting(settings: e.Args.Contains("--settings"));
            Shutdown();
            return;
        }
        ConsoleLog.Step("Instância única adquirida");

        // --safe: só a interface. Sem bandeja, sem atalho, sem sincronização,
        // sem leitura de conta — o caminho mais curto entre clicar e ver algo.
        if (e.Args.Contains("--safe"))
        {
            ConsoleLog.Step("Modo --safe: sem bandeja, sem atalho, sem sincronização");
            ShowBar();
            return;
        }

        try
        {
            BuildTray();
            ConsoleLog.Step("Bandeja criada", TrayVisible ? "visível" : "não visível");
            RegisterHotkey();
            ConsoleLog.Step("Atalho global", string.IsNullOrEmpty(HotKey.Combination) ? "NÃO registrado" : HotKey.Combination);
            SingleInstance.Listen(ShowBar, () => ShowSettings());
        }
        catch (Exception error)
        {
            Diagnostics.Report(error, "Bandeja/atalho", fatal: false);
        }

        // Sincronização: se o Firebase estiver inacessível (sem internet, regra
        // errada, projeto fora do ar), o app segue 100% local.
        try
        {
            Sync.Start();
        }
        catch (Exception error)
        {
            // Sem sincronização o app continua funcionando com os dados locais.
            Diagnostics.Report(error, "Sincronização", fatal: false);
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

        if (e.Args.Contains("--selftest"))
        {
            ConsoleLog.Step("Rodando autoteste das janelas");
            Smoke.Run(() =>
            {
                _bar ??= CreateBar();
                return _bar;
            });
            ConsoleLog.Step("Autoteste concluído", Smoke.ReportPath);

            // mantém o relatório na tela por 20s e encerra
            var aviso = new Window
            {
                Title = "Orbi — autoteste",
                Width = 760,
                Height = 560,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush(Color.FromRgb(0xF2, 0xED, 0xE3)),
            };
            var texto = new TextBlock
            {
                Text = File.Exists(Smoke.ReportPath) ? File.ReadAllText(Smoke.ReportPath) : "sem relatório",
                FontFamily = new FontFamily("Consolas, Cascadia Mono"),
                FontSize = 12,
                Margin = new Thickness(20),
                TextWrapping = TextWrapping.Wrap,
            };
            aviso.Content = new ScrollViewer { Content = texto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            aviso.Show();
            return;
        }

        if (e.Args.Contains("--diag"))
        {
            ConsoleLog.Step("Abrindo diagnóstico");
            ShowDiagnostics();
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
                "Aperte {HotKey.Combination} para abrir. Clique com o botão direito aqui para ajustes.",
                Forms.ToolTipIcon.Info);
            return;
        }

        ShowBar();

        ConsoleLog.Step("Inicialização concluída", "app pronto");
        ConsoleLog.Step("Se o terminal fechar, rode: Orbi.exe --console-keep");

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
        menu.Items.Add("Diagnóstico (por que não abre?)", null, (_, _) => ShowDiagnostics());
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
    /// Registra o atalho global (padrão Alt+Shift+O). O hotkey do Win32 precisa de um HWND: criamos a barra
    /// (ela ainda não aparece) e reagimos ao evento SourceInitialized, que é
    /// quando a janela realmente passa a existir. Antes, TryRegister() era
    /// chamado antes do handle existir e o atalho nunca era registrado.
    /// </summary>
    private void RegisterHotkey()
    {
        try
        {
            _bar = CreateBar();
            HotKey.Preferred = Store.Current.Hotkey;
            if (_bar is not null) HotKey.Attach(_bar, () => ShowBar());
        }
        catch (Exception error)
        {
            Diagnostics.Report(error, "Barra (hotkey)", fatal: false);
        }
    }

    /// <summary>Cria a barra; se o XAML/tema falhar, devolve null em vez de derrubar o app.</summary>
    private CommandBar? CreateBar()
    {
        try
        {
            ConsoleLog.Step("Criando a barra (XAML)");
            var bar = new CommandBar();
            ConsoleLog.Step("Barra criada");
            return bar;
        }
        catch (Exception error)
        {
            ConsoleLog.Fail("criar a barra", error);
            Diagnostics.Report(error, "Barra", fatal: false);
            return null;
        }
    }

    /* ----------------------------- janelas ----------------------------- */
    public void ShowBar()
    {
        try
        {
            _bar ??= CreateBar();

            if (_bar is null)
            {
                ConsoleLog.Step("Barra nula — abrindo janela de emergência");
                EmergencyWindow();
                return;
            }

            if (_bar.WindowState == WindowState.Minimized) _bar.WindowState = WindowState.Normal;
            ConsoleLog.Step("Exibindo barra");
            _bar.ShowBar();
            ConsoleLog.Step("Barra visível", $"visível={_bar.IsVisible} · {_bar.Left:0},{_bar.Top:0} · {_bar.Width:0}×{_bar.Height:0}");
        }
        catch (Exception error)
        {
            ConsoleLog.Fail("mostrar barra", error);
            Diagnostics.Report(error, "Mostrar barra", fatal: false);
            EmergencyWindow();
        }
    }

    private bool _emergencyShown;

    /// <summary>
    /// Último recurso: uma janela WPF montada só em código (sem XAML, sem tema).
    /// Se a interface principal falhar, o app ainda abre e diz o que aconteceu.
    /// </summary>
    private void EmergencyWindow()
    {
        if (_emergencyShown) return;
        _emergencyShown = true;

        var window = new Window
        {
            Title = "Orbi — modo de emergência",
            Width = 560,
            Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF2, 0xED, 0xE3)),
        };

        var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "O Orbi abriu no modo de emergência.",
            FontSize = 20,
            FontWeight = System.Windows.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10),
        });
        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "A interface normal não conseguiu carregar. Seus dados não foram afetados.\n\n" +
                   $"Detalhes em:\n{Diagnostics.LogPath}\n\n" +
                   "Envie esse arquivo para o suporte — ele diz exatamente o que falhou.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
        });

        var openLog = new System.Windows.Controls.Button
        {
            Content = "Abrir a pasta do log",
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        openLog.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", Store.Root) { UseShellExecute = true }); }
            catch { /* ignorar */ }
        };
        panel.Children.Add(openLog);

        var quit = new System.Windows.Controls.Button
        {
            Content = "Fechar o Orbi",
            Padding = new Thickness(12, 8, 12, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        quit.Click += (_, _) => ExitApp();
        panel.Children.Add(quit);

        window.Content = panel;
        window.Closed += (_, _) => _emergencyShown = false;
        window.Show();
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

    public void ShowDiagnostics()
    {
        // Sem Dono e sem Modal: fecha e o app continua vivo na bandeja.
        new DiagnosticoWindow().Show();
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
        if (Environment.GetCommandLineArgs().Contains("--console-keep"))
        {
            ConsoleLog.Step("Pressione ENTER para fechar o terminal");
            try { Console.ReadLine(); } catch { }
        }
        Store.Save();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
