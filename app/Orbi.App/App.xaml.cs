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
    /// <summary>
    /// Entrada explícita: sem App.g.cs, sem StartupUri, sem LoadComponent.
    /// Um App.xaml quebrado derrubava o processo antes de qualquer log.
    /// </summary>
    [STAThread]
    public static void Main()
    {
        try
        {
            var app = new App();
            if (Environment.GetCommandLineArgs().Any(arg => arg == "--self-test"))
            {
                app._selfTest = true;
            }
            app.Run();
        }
        catch (Exception error)
        {
            Log.Error("entrada do app", error);
            System.Windows.MessageBox.Show(
                "O Orbi não conseguiu iniciar.\n\n" + error.Message +
                "\n\nDetalhes em " + Log.Path,
                "Orbi");
        }
    }

    private Forms.NotifyIcon? _tray;
    private CommandBar? _bar;
    private Window? _panel;
    private string? _panelName;
    private bool _barShouldReturn;
    private bool _selfTest;
    private DispatcherTimer? _reminderTimer;
    private DispatcherTimer? _idleTimer;
    private DateTime _lastActivity = DateTime.Now;

    public static App Instance => (App)System.Windows.Application.Current;

    /// <summary>Última exceção de pintura/interação (o autoteste falha se houver).</summary>
    public static string? UltimoErroDeInterface { get; set; }

    public bool BarCreated => _bar is not null;
    public bool TrayVisible => _tray?.Visible == true;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Exceção de interface não pode derrubar o app sem deixar rastro.
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("interface", args.Exception);
            UltimoErroDeInterface = args.Exception.GetType().Name + ": " + args.Exception.Message;
            var nota = Path.Combine(Store.Root, "autoteste.log");
            try
            {
                File.AppendAllText(nota, "exceção de interface: " + args.Exception.GetType().Name +
                                          ": " + args.Exception.Message + Environment.NewLine);
            }
            catch { }
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error("app", args.ExceptionObject as Exception ?? new Exception("erro desconhecido"));

        base.OnStartup(e);
        Log.Add("Orbi iniciando", typeof(App).Assembly.GetName().Version?.ToString() ?? "?");

        // O tema fora do App.xaml: se um recurso faltar, o app abre mesmo assim.
        try
        {
            Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Orbi;component/Theme.xaml", UriKind.Absolute),
            });
        }
        catch (Exception error)
        {
            Log.Error("tema", error);
        }

        // Uma instância só: a segunda apenas mostra a barra.
        if (!SingleInstance.TryAcquire())
        {
            SingleInstance.SignalExisting(settings: e.Args.Contains("--settings"));
            Shutdown();
            return;
        }

        try
        {
            SingleInstance.Listen(
                () => Dispatcher.BeginInvoke(new Action(ShowBar)),
                () => Dispatcher.BeginInvoke(new Action(() => ShowSettings())));
        }
        catch (Exception error)
        {
            Log.Error("instância única", error);
        }

        try
        {
            Sync.Start();
        }
        catch (Exception error)
        {
            Log.Error("sincronização", error);
        }

        try
        {
            BuildTray();
            RegisterHotkey();
            Log.Add("bandeja e atalho", HotKey.Combination);
        }
        catch (Exception error)
        {
            Log.Error("bandeja/atalho", error);
        }

        _reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _reminderTimer.Tick += (_, _) => CheckReminders();
        _reminderTimer.Start();

        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _idleTimer.Tick += (_, _) => CheckIdle();
        _idleTimer.Start();

        if (e.Args.Contains("--uninstall"))
        {
            Cleanup();
            Shutdown();
            return;
        }

        if (e.Args.Contains("--settings"))
        {
            ShowSettings();
            return;
        }

        // Autoteste do CI: abre barra e painéis, confere e sai.
        if (_selfTest || e.Args.Contains("--self-test"))
        {
            RunSelfTest();
            return;
        }

        // Iniciado com o Windows: fica quieto na bandeja.
        if (e.Args.Contains("--tray")) return;

        ShowBar();
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
        menu.Items.Add("Conversar com o Orbi", null, (_, _) => OpenChat());
        menu.Items.Add("Ajustes…", null, (_, _) => ShowSettings());
        menu.Items.Add("Conta…", null, (_, _) => ShowAccount());
        menu.Items.Add("Ferramentas…", null, (_, _) => ShowSettings("ferramentas"));
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
            Log.Error("barra (atalho)", error);
        }
    }

    /// <summary>Cria a barra; se o XAML/tema falhar, devolve null em vez de derrubar o app.</summary>
    private CommandBar? CreateBar()
    {
        try
        {
            return new CommandBar();
        }
        catch (Exception error)
        {
            Log.Error("criar a barra", error);
            return null;
        }
    }

    /* ----------------------------- janelas ----------------------------- */
    /// <summary>
    /// Esconde a barra porque o Orbi vai mexer na tela (ver, clicar, digitar).
    /// Nesse caso ela volta sozinha depois.
    /// </summary>
    public void HideBarForAction()
    {
        _bar?.HideBar();
        _barShouldReturn = true;
    }

    /// <summary>Esconde a barra em definitivo até você chamar de novo.</summary>
    public void HideBar()
    {
        _barShouldReturn = false;
        _bar?.HideBar();
    }

    /// <summary>
    /// Autoteste do CI: abre a barra, o menu e cada painel, confere que eles
    /// existem de verdade e fecha. Sem isso, um painel quebrado só apareceria
    /// na mão de quem usa.
    /// </summary>
    public void RunSelfTest()
    {
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            var linhas = new List<string>();
            void Nota(string texto)
            {
                linhas.Add(texto);
                try
                {
                    File.AppendAllText(Path.Combine(Store.Root, "autoteste.log"), texto + Environment.NewLine);
                }
                catch { }
            }

            var ok = true;
            try
            {
                ShowBar();
                ok &= _bar is { IsVisible: true };
                Nota($"barra visível={_bar?.IsVisible} largura={_bar?.Width:0} altura={_bar?.Height:0}");

                // A barra não pode nascer no meio da tela.
                if (_bar is not null)
                {
                    var area = System.Windows.Forms.Screen.FromPoint(
                        System.Windows.Forms.Cursor.Position).WorkingArea;
                    var dpi = VisualTreeHelper.GetDpi(_bar);
                    var alturaTela = area.Height / dpi.DpiScaleY;
                    var noTopo = _bar.Top < alturaTela / 3;
                    ok &= noTopo;
                    Nota($"fora do meio: topo={_bar.Top:0} de {alturaTela:0} = {noTopo}");
                }

                // A versão que o app instalado exibe é a mesma da release.
                Nota($"versão instalada={Update.Instalada}");
                ok &= Update.Instalada != "0.0.0";

                // Comparação de versões: só conta como nova quando é maior de verdade.
                var comparacoes = new (string Nova, string Atual, bool Esperado)[]
                {
                    ("1.2.0", "1.1.2", true),
                    ("1.2.0", "1.2.0", false),
                    ("1.1.2", "1.2.0", false),
                    ("1.2.10", "1.2.9", true),
                    ("2.0.0", "1.9.9", true),
                };
                foreach (var (nova, atual, esperado) in comparacoes)
                {
                    var deu = Update.MaisNova(nova, atual);
                    ok &= deu == esperado;
                    if (deu != esperado) Nota($"comparação errada: {nova} > {atual} = {deu}");
                }
                Nota($"comparação de versões: {comparacoes.Length} casos ok={ok}");

                // Tela cheia de verdade: cobre a tela e volta exatamente como estava.
                if (_bar is not null)
                {
                    var antesLargura = _bar.Width;
                    var antesAltura = _bar.Height;
                    _bar.AlternarTelaCheia();
                    await Task.Delay(400);
                    var tela = System.Windows.Forms.Screen.FromPoint(
                        System.Windows.Forms.Cursor.Position).Bounds;
                    var dpiCheia = VisualTreeHelper.GetDpi(_bar);
                    var cobre = _bar.Width / dpiCheia.DpiScaleX >= tela.Width - 8 &&
                                _bar.Height / dpiCheia.DpiScaleY >= tela.Height - 8;
                    ok &= cobre && _bar.EmTelaCheia;
                    Nota($"tela cheia: {_bar.Width:0}x{_bar.Height:0} de {tela.Width}x{tela.Height} = {cobre}");

                    _bar.AlternarTelaCheia();
                    await Task.Delay(400);
                    var voltou = Math.Abs(_bar.Width - antesLargura) < 2 && Math.Abs(_bar.Height - antesAltura) < 2;
                    ok &= voltou && !_bar.EmTelaCheia;
                    Nota($"voltou do tela cheia: {_bar.Width:0}x{_bar.Height:0} (antes {antesLargura:0}x{antesAltura:0}) = {voltou}");
                }

                _bar?.OpenMenuForTest();
                ok &= _bar?.MenuOpen == true;
                Nota($"menu aberto={_bar?.MenuOpen}");

                // com conta vinculada, o painel de perfil precisa mostrar o e-mail
                Nota($"conta: vinculada={Firebase.SignedIn} sincronização={Sync.LastStatus}");

                foreach (var painel in new[] { "perfil", "ia", "ferramentas", "ajustes" })
                {
                    OpenPanel(painel);
                    await Task.Delay(250);
                    var janela = _panel;
                    var visivel = janela is { IsVisible: true } && janela.ActualWidth > 300 && janela.ActualHeight > 250;
                    ok &= visivel;
                    Nota($"painel {painel}: visível={janela?.IsVisible} tamanho={janela?.ActualWidth:0}x{janela?.ActualHeight:0}");
                }

                // O catálogo novo tem que executar de verdade, não só compilar.
                var casos = new (string Id, string Dado, string Esperado)[]
                {
                    ("somar", "2+2", "4"),
                    ("calcular-uma-porcentagem", "20% de 250", "50"),
                    ("converter-celsius-para-fahrenheit", "100", "212"),
                    ("calcular-o-imc", "80 1,80", "24,7"),
                    ("deixar-em-maiusculas", "orbi", "ORBI"),
                    ("inverter-o-texto", "abc", "cba"),
                    ("contar-as-letras", "orbi", "4"),
                    ("copiar-para-a-area-de-transferencia", "teste orbi", "teste orbi"),
                    ("ver-o-que-esta-copiado", "", "teste orbi"),
                    ("contar-as-palavras-do-texto-copiado", "", "2"),
                };
                foreach (var (id, dado, esperado) in casos)
                {
                    var saida = Tools.Run(id, dado);
                    var casou = saida.Contains(esperado, StringComparison.OrdinalIgnoreCase);
                    ok &= casou;
                    Nota($"ferramenta {id}({dado}) => {saida}{(casou ? "" : "  [esperado: " + esperado + "]")}");
                }

                // A IA não pode receber o catálogo inteiro: o pedido escolhe as ferramentas.
                var enxuto = Tools.ForAi("converter 30 graus para fahrenheit");
                ok &= enxuto.Contains("converter-celsius-para-fahrenheit") && enxuto.Length < Tools.CatalogoCompleto().Length;
                Nota($"catálogo para a IA: {enxuto.Split('\n').Length} de {Tools.All.Count} linhas");

                // A página de conta: sem conta ela é o formulário; com conta, o painel.
                OpenPanel("perfil");
                await Task.Delay(300);
                if (_panel is ProfileWindow conta)
                {
                    ok &= conta.FormularioVisivel && !conta.PainelDaContaVisivel;
                    Nota($"conta: formulário visível={conta.FormularioVisivel} " +
                         $"google disponível={conta.GoogleDisponivel} " +
                         $"senha acompanha={conta.SenhaAcompanha()}");
                }
                else
                {
                    ok = false;
                    Nota("conta: a página não abriu");
                }

                // Entrar não abre uma segunda janela: o painel de conta é o formulário.
                ShowLogin();
                await Task.Delay(250);
                // A barra e os painéis já criados contam como janelas; o que não pode
                // acontecer é uma janela de login por cima da página de conta.
                var janelas = System.Windows.Application.Current.Windows.Count;
                var extra = System.Windows.Application.Current.Windows
                    .OfType<Window>().Count(janela => janela is not CommandBar && janela != _panel && janela.IsVisible);
                ok &= _panel is ProfileWindow && extra == 0;
                Nota($"entrar abre a própria conta: {_panel is ProfileWindow} janelas={janelas} extras={extra}");

                // O identificador do Google é aceito, validado e guardado.
                var idAntes = Store.Current.GoogleClientId;
                GoogleLogin.ClientId = "123-orbi.apps.googleusercontent.com";
                var guardou = Store.Current.GoogleClientId.Contains("apps.googleusercontent.com");
                var recusa = !GoogleLogin.IdValido("abc123");
                GoogleLogin.ClientId = idAntes;
                // O Google pelo Firebase (o caminho do site) precisa só do WebView2.
                var temWebView = GoogleLogin.Disponivel || !string.IsNullOrWhiteSpace(idAntes);
                ok &= guardou && recusa;
                Nota($"google: identificador guardado={guardou} recusa inválido={recusa} " +
                     $"caminho do firebase={temWebView}");

                // O motor da IA: as 598 ferramentas viram funções declaradas, com o
                // controle do PC por cima, e os nomes voltam para o id certo.
                // Teto de 128 por pedido: o pacote é escolhido pelo pedido e o catálogo
                // inteiro continua alcançável por pc_buscar_no_catalogo + pc_usar_ferramenta.
                var todas = ToolSchema.Todos();
                var nomesTodos = todas.Select(item => item!["name"]!.GetValue<string>()).ToHashSet();
                var unicos = nomesTodos.Count == todas.Count;

                var pacote = ToolSchema.Declaracoes("abrir o chrome e pesquisar no google");
                var nomesPacote = pacote.Select(item => item!["name"]!.GetValue<string>()).ToList();
                var dentroDoTeto = nomesPacote.Count <= ToolSchema.MaxFuncoes;
                var temControle = ToolSchema.Reservados.All(nomesPacote.Contains);
                var semDuplicata = nomesPacote.Count == nomesPacote.Distinct().Count();
                var trouxeCerta = ToolSchema.Escolher("abrir o chrome e pesquisar no google")
                    .Any(item => item.Id.Contains("chrome", StringComparison.OrdinalIgnoreCase));
                var resolve = ToolSchema.Resolver("abrir_o_chrome") == "abrir-o-chrome" &&
                              ToolSchema.Resolver("pc_ver_a_tela") == "pc_ver_a_tela" &&
                              ToolSchema.Resolver("somar") == "somar";
                // ...e qualquer ferramenta do catálogo volta resolvida pelo nome da função
                var resolveTudo = Tools.All.Count(item => ToolSchema.Resolver(
                    string.Join("_", item.Id.Split('-', '.'))) == item.Id) >= Tools.All.Count - 2;
                ok &= resolve && resolveTudo && unicos && dentroDoTeto &&
                      temControle && semDuplicata && trouxeCerta;
                Nota($"funções: pacote={nomesPacote.Count} (teto {ToolSchema.MaxFuncoes}) " +
                     $"catálogo={todas.Count} únicos={unicos} controle={temControle} " +
                     $"relevante={trouxeCerta} resolve={resolve}/{resolveTudo}");

                // O chat abre, monta a conversa e desenha os balões.
                UltimoErroDeInterface = null;
                try
                {
                    // primeira as peças, uma por uma: se algo estourar, o log diz qual
                    foreach (var (nome, criar) in new (string, Func<object>)[]
                    {
                        ("icone", () => ChatKit.Icone("spark", 16)),
                        ("balaoPessoa", () => ChatKit.BalaoPessoa("teste")),
                        ("balaoOrbi", () => ChatKit.BalaoOrbi(new TextBlock { Text = "oi" })),
                        ("cartaoPasso", () => ChatKit.CartaoPasso("check", "t", "d", true)),
                        ("pontos", () => ChatKit.Pontos()),
                    })
                    {
                        try { _ = criar(); }
                        catch (Exception peca) { Nota($"chat: peça {nome} falhou: {peca.GetType().Name}: {peca.Message}"); ok = false; }
                    }

                    Nota("chat: criando a janela");
                    var chat = new ChatWindow();
                    Nota("chat: janela criada");
                    chat.Show();
                    Nota("chat: janela mostrada");
                    await Task.Delay(600);
                    var montou = chat.Pronto && chat.Baloes >= 1;
                    Nota($"chat: montou={montou} balões={chat.Baloes} chave={Gemini.HasKey} " +
                         $"erros de interface={UltimoErroDeInterface ?? "nenhum"}");
                    ok &= UltimoErroDeInterface is null;
                    chat.Close();
                    await Task.Delay(200);
                    ok &= montou;
                }
                catch (Exception error)
                {
                    ok = false;
                    Nota("chat falhou: " + error.GetType().Name + ": " + error.Message);
                }

                // Com a chave do CI, a IA de verdade: uma pergunta e uma ação no PC.
                // Sem chave, o teste segue (e o log diz que ficou de fora).
                if (Gemini.HasKey)
                {
                    var (resposta, passosIA, okIA) = await Agent.RunAsync(
                        "Quanto é 12 vezes 8? Responda em uma frase curta.");
                    var respondeu = okIA && resposta.Contains("96");
                    ok &= respondeu;
                    Nota($"ia: resposta=\"{Cortar(resposta)}\" passos={passosIA.Count} ok={respondeu}");

                    var (acao, passosAcao, _) = await Agent.RunAsync(
                        "Quanto é 37 mais 5? Use a ferramenta de somar e depois me diga só o resultado.");
                    var usouFerramenta = passosAcao.Count > 0 && acao.Contains("42");
                    ok &= usouFerramenta;
                    Nota($"ia: ação=\"{Cortar(acao)}\" passos={passosAcao.Count} " +
                         $"ferramenta={(passosAcao.FirstOrDefault()?.Title ?? "nenhuma")} ok={usouFerramenta}");
                }
                else
                {
                    Nota("ia: sem chave neste build — a chamada real não foi testada");
                }

                // abrir a barra de novo tem que fechar o painel: um na frente por vez
                ShowBar();
                await Task.Delay(200);
                ok &= _panel is null;
                Nota($"um por vez: painel fechado={_panel is null}");
            }
            catch (Exception error)
            {
                ok = false;
                Nota("falhou: " + error.Message);
            }

            Nota(ok ? "ok" : "falhou");
            Environment.ExitCode = ok ? 0 : 1;
            ExitApp();
            // Garante o código de saída mesmo se algum handle segurar o processo.
            Environment.Exit(ok ? 0 : 1);
        }));
    }

    /// <summary>Trecho curto para caber no log do autoteste.</summary>
    private static string Cortar(string texto)
    {
        var limpo = texto.Replace("\r", " ").Replace("\n", " ").Trim();
        return limpo.Length > 140 ? limpo[..140] + "…" : limpo;
    }

    /// <summary>A barra volta (usado depois de salvar a chave, fechar um painel…).</summary>
    public void ReturnBar()
    {
        if (_panel is not null)
        {
            ClosePanels();
        }
        _barShouldReturn = false;
        ShowBar();
    }

    public void ShowBar()
    {
        try
        {
            _bar ??= CreateBar();
            if (_bar is null)
            {
                EmergencyWindow();
                return;
            }
            // Uma coisa na frente por vez: se um painel está aberto, a barra o
            // substitui em vez de aparecer por cima.
            _barShouldReturn = false;
            ClosePanels();
            if (_bar.WindowState == WindowState.Minimized) _bar.WindowState = WindowState.Normal;
            _bar.ShowBar();
        }
        catch (Exception error)
        {
            Log.Error("mostrar barra", error);
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
        try
        {
            ProntoWindow.Show(primeiraVez: false);
            return;
        }
        catch (Exception error)
        {
            Log.Error("janela de emergência", error);
        }

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
                   $"Detalhes em:\n{Log.Path}",
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

    /// <summary>Entra na conta (a mesma do site) ou mostra o perfil já vinculado.</summary>
    public void ShowAccount()
    {
        if (!Firebase.SignedIn)
        {
            ShowLogin();
            return;
        }
        OpenPanel("perfil");
    }

    /// <summary>
    /// Abre o chat do Orbi — a conversa de verdade com a IA, com os passos do
    /// agente aparecendo em tempo real. Uma janela só: se já está aberta, traz
    /// para a frente em vez de abrir outra.
    /// </summary>
    public void OpenChat(string? primeiraMensagem = null)
    {
        if (_chat is not { IsLoaded: true })
        {
            _chat = new ChatWindow();
            _chat.Closed += (_, _) => _chat = null;
            _chat.Show();
        }
        else
        {
            _chat.Activate();
        }
        if (!string.IsNullOrWhiteSpace(primeiraMensagem)) _chat.Perguntar(primeiraMensagem!);
    }

    private ChatWindow? _chat;

    /// <summary>A conta mudou (entrou, saiu, salvou perfil): a barra reflete na hora.</summary>
    public void RefreshAccount()
    {
        _bar?.RefreshAccountChip();
        if (_panel is ProfileWindow conta) conta.Refresh();
    }

    /// <summary>
    /// Entrar acontece dentro da página de conta: o painel já abre com o
    /// formulário, sem uma segunda janela por cima.
    /// </summary>
    public void ShowLogin() => OpenPanel("perfil");

    public void ShowSettings(string? tab = null) => OpenPanel(Mapear(tab));

    private static string Mapear(string? tab) => tab switch
    {
        null or "" or "geral" or "ajustes" => "ajustes",
        "ia" => "ia",
        "conta" or "perfil" => "perfil",
        "ferramentas" => "ferramentas",
        "memoria" or "lembretes" or "notas" or "atalhos" => "ajustes",
        _ => "ajustes",
    };

    /// <summary>
    /// Um painel por vez. Abrir outro fecha o anterior antes: nada de janelas
    /// empilhadas na frente da pessoa.
    /// </summary>
    public void OpenPanel(string qual)
    {
        if (_panel is not null)
        {
            var anterior = _panel;
            _panel = null;
            _panelName = null;
            try { anterior.Close(); } catch { }
        }

        Window painel = qual switch
        {
            "ia" => new IaWindow(),
            "ferramentas" => new ToolsWindow(),
            "perfil" => new ProfileWindow(),
            _ => new SettingsWindow(),
        };

        _panel = painel;
        _panelName = qual;
        painel.Closed += (_, _) =>
        {
            if (ReferenceEquals(_panel, painel))
            {
                _panel = null;
                _panelName = null;
            }
        };
        painel.Show();
        painel.Activate();
    }

    /// <summary>A barra ficou escondida porque um painel abriu (não é caso de voltar sozinha).</summary>
    public void NoteBarHiddenByPanel() => _barShouldReturn = false;

    public bool BarShouldReturn => _barShouldReturn;

    /// <summary>Depois de salvar/tirar a chave, a barra atualiza o estado e volta.</summary>
    public void RefreshAfterKey()
    {
        _bar?.RefreshGreeting();
        _bar?.RefreshAccountChip();
    }

    /// <summary>Fecha o painel que estiver aberto (a barra voltou a ser a única na frente).</summary>
    public void ClosePanels()
    {
        var painel = _panel;
        if (painel is null) return;
        _panel = null;
        _panelName = null;
        try { painel.Close(); } catch { }
    }

    /// <summary>O painel aberto respondeu? A barra não abre por cima dele.</summary>
    public bool PanelOpen => _panel is not null;

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
