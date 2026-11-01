using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Orbi.Services;

namespace Orbi;

/// <summary>Uma linha de sugestão/resposta na barra.</summary>
public sealed class ResultRow
{
    public string Glyph { get; init; } = "\u2022";
    public string Text { get; init; } = "";
    public string? Detail { get; init; }
    public string Badge { get; init; } = "";
    public string BadgeBrush { get; init; } = "#1FFFFFFF";
    public string BadgeTextBrush { get; init; } = "#99F1EBDF";
    public string? Command { get; init; }
    public bool IsConfirm { get; init; }

    public Visibility DetailVisibility => string.IsNullOrWhiteSpace(Detail) ? Visibility.Collapsed : Visibility.Visible;
}

public partial class CommandBar : Window
{
    private readonly ObservableCollection<ResultRow> _rows = new();
    private readonly List<(string Role, string Text)> _history = new();
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(260) };
    private bool _thinking;

    /// <summary>Altura da barra fechada: só a linha de comando.</summary>
    private const double CompactHeight = 62;
    private const double ExpandedHeight = 380;

    private string Greeting
    {
        get
        {
            var first = string.IsNullOrWhiteSpace(Store.Current.UserName)
                ? ""
                : ", " + Store.Current.UserName.Split(' ')[0];
            return $"Oi{first}. Digite um comando, “/” para as ferramentas ou pergunte algo…";
        }
    }

    /// <summary>Só a linha de comando: sem resultados, sem rodapé.</summary>
    private void Collapse()
    {
        Results.Visibility = Visibility.Collapsed;
        FooterBar.Visibility = Visibility.Collapsed;
        Height = CompactHeight;
    }

    private void Expand()
    {
        Results.Visibility = Visibility.Visible;
        FooterBar.Visibility = Visibility.Visible;
        var area = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        var areaHeight = area.Height / dpi.DpiScaleY;
        Height = Math.Min(ExpandedHeight, Math.Max(CompactHeight + 80, areaHeight - 40));
        // Cresce para baixo: se não couber, sobe só o necessário para não sair da tela.
        var limite = area.Top / dpi.DpiScaleY + areaHeight - Height - 8;
        if (Top > limite) Top = Math.Max(area.Top / dpi.DpiScaleY + 8, limite);
    }

    public CommandBar()
    {
        InitializeComponent();
        ApplySpacing();
        RefreshAccountChip();
        Results.ItemsSource = _rows;
        _debounce.Tick += async (_, _) =>
        {
            _debounce.Stop();
            await AskAiAsync(Input.Text);
        };
        AiStatus.Text = Gemini.Mode;
        AiStatus.Foreground = Gemini.Available
            ? new SolidColorBrush(Color.FromRgb(0xC7, 0x45, 0x2A))
            : new SolidColorBrush(Color.FromArgb(0x80, 0xF1, 0xEB, 0xDF));
    }

    /* ----------------------------- espaçamento ----------------------------- */
    /// <summary>
    /// Todo o respiro fica em código (nada de Padding no XAML): evita
    /// ContentPresenter de ListBoxItem com padding duplicado e qualquer chance
    /// da janela abrir com área útil zero (tela "em branco").
    /// </summary>
    private void ApplySpacing()
    {
        Header.Padding = new Thickness(16, 14, 16, 14);
        FooterBar.Padding = new Thickness(16, 9, 16, 9);
        Root.Margin = new Thickness(1);
        Mark.Width = 28;
        Mark.Height = 28;
        Mark.Margin = new Thickness(0, 0, 12, 0);
        EscBadge.Padding = new Thickness(7, 4, 7, 4);
        Results.Padding = new Thickness(6, 4, 6, 4);
        Results.ItemContainerStyle = (Style)FindResource("ResultItem");
    }

    /* ----------------------------- conta ----------------------------- */
    /// <summary>O avatar mostra quem está logado (ou "O" quando não há conta).</summary>
    public void RefreshAccountChip()
    {
        var nome = string.IsNullOrWhiteSpace(Store.Current.UserName) ? "" : Store.Current.UserName.Trim();
        var primeiro = string.IsNullOrWhiteSpace(nome) ? "O" : nome[..1].ToUpperInvariant();
        AvatarInitial.Text = primeiro;
        Avatar.Background = Firebase.SignedIn
            ? new SolidColorBrush(Color.FromRgb(0x3B, 0x6E, 0x3F))
            : (Brush)new SolidColorBrush(Color.FromRgb(0xC7, 0x45, 0x2A));
        AccountButton.ToolTip = Firebase.SignedIn
            ? $"{nome} · {(Firebase.Email ?? "")}"
            : "Sem conta — clique para entrar";
    }

    /// <summary>Menu de conta: perfil, ajustes, ferramentas, atalhos e sair.</summary>
    private void AccountButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = AccountButton,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
            FontFamily = FontFamily,
        };

        void Item(string texto, Action acao, bool negrito = false)
        {
            var item = new MenuItem { Header = texto };
            if (negrito) item.FontWeight = FontWeights.SemiBold;
            item.Click += (_, _) => acao();
            menu.Items.Add(item);
        }

        if (Firebase.SignedIn)
        {
            var nome = string.IsNullOrWhiteSpace(Store.Current.UserName) ? "Sua conta" : Store.Current.UserName;
            var cabecalho = new MenuItem { Header = nome, IsEnabled = false };
            menu.Items.Add(cabecalho);
            if (!string.IsNullOrWhiteSpace(Firebase.Email))
            {
                menu.Items.Add(new MenuItem { Header = Firebase.Email, IsEnabled = false });
            }
            menu.Items.Add(new Separator());
            Item("Conta e sincronização…", () => App.Instance.ShowSettings("conta"));
        }
        else
        {
            Item("Entrar na conta…", () => App.Instance.ShowLogin(), negrito: true);
            Item("Criar conta no site", () => SystemActions.Open("https://orbi-app.web.app/criar"));
            menu.Items.Add(new Separator());
        }

        Item("IA e chave do Gemini…", () => App.Instance.ShowSettings("ia"));
        Item("Ajustes…", () => App.Instance.ShowSettings());
        Item("Ferramentas do Windows…", () => App.Instance.ShowSettings("ferramentas"));
        Item("Lembretes e notas…", () => App.Instance.ShowSettings("notas"));
        menu.Items.Add(new Separator());
        if (Firebase.SignedIn)
        {
            Item("Sair da conta", () =>
            {
                Firebase.SignOut();
                RefreshAccountChip();
                App.Instance.ShowBar();
            });
        }
        Item("Fechar a barra (Esc)", Hide);
        Item("Encerrar o Orbi", () => App.Instance.ExitApp());

        menu.IsOpen = true;
    }

    /* ----------------------------- abrir / fechar ----------------------------- */
    public void RefreshGreeting()
    {
        Hint.Text = Greeting;
        AiStatus.Text = Gemini.Mode;
    }

    /// <summary>
    /// Abre a barra. Se ela já esteve aberta antes, volta exatamente para onde
    /// estava e com o tamanho que você deixou (arrastou ou redimensionou).
    /// Sem posição salva, abre no centro, um pouco acima do meio.
    /// </summary>
    public void ShowBar()
    {
        // WorkingArea vem em pixels físicos; Left/Top/Width do WPF em DIPs.
        // Dividir pelo DPI da tela (150% em muitos notebooks) evita a janela
        // nascer fora da área visível.
        var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
        var area = screen.WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        var areaLeft = area.Left / dpi.DpiScaleX;
        var areaTop = area.Top / dpi.DpiScaleY;
        var areaWidth = area.Width / dpi.DpiScaleX;
        var areaHeight = area.Height / dpi.DpiScaleY;

        var salva = Store.Current;
        Width = Math.Clamp(salva.BarWidth > 200 ? salva.BarWidth : 660, 420, Math.Max(420, areaWidth - 16));
        Height = CompactHeight;

        if (salva.BarLeft is { } left && salva.BarTop is { } top)
        {
            Left = Math.Clamp(left, areaLeft + 4, Math.Max(areaLeft + 4, areaLeft + areaWidth - Width - 4));
            Top = Math.Clamp(top, areaTop + 4, Math.Max(areaTop + 4, areaTop + areaHeight - Height - 4));
        }
        else
        {
            Left = areaLeft + (areaWidth - Width) / 2;
            Top = areaTop + (areaHeight - Height) / 2 - areaHeight * 0.14;
        }

        // Confere o atalho global a cada abertura: se o handle da janela só
        // ficou pronto depois do Attach, ele é registrado agora.
        try { HotKey.TryRegister(); } catch { /* sem atalho ainda */ }

        if (!IsVisible) Show();

        Input.Text = "";
        _rows.Clear();
        Hint.Text = Greeting;
        Collapse();
        UpdateFooter();

        RefreshAccountChip();
        Topmost = true;
        Activate();
        Focus();
        Input.Focus();
        Keyboard.Focus(Input);

        // Primeira vez sem chave: a barra já explica, sem abrir janela.
        if (!Store.Current.ToldAboutCloud && !Gemini.HasKey)
        {
            Store.Current.ToldAboutCloud = true;
            Store.Save();
            Expand();
            Add(new ResultRow
            {
                Glyph = "\u2022",
                Text = "Cole a sua chave do Gemini para o Orbi pensar.",
                Detail = "Ajustes ▸ IA. A chave é gratuita e fica cifrada neste PC.",
                Command = "conta",
                Badge = "configurar",
                BadgeBrush = "#FFC7452A",
                BadgeTextBrush = "#FFFFFFFF",
            });
        }
    }

    /// <summary>Guarda posição e largura para a próxima vez que a barra abrir.</summary>
    private void RememberPlace()
    {
        if (WindowState != WindowState.Normal) return;
        if (double.IsNaN(Left) || double.IsNaN(Top) || Width < 100) return;
        var dados = Store.Current;
        dados.BarLeft = Left;
        dados.BarTop = Top;
        dados.BarWidth = Width;
        dados.BarHeight = CompactHeight;
        Store.Save();
    }

    /// <summary>Mostra o atalho que está realmente funcionando.</summary>
    private void UpdateFooter()
    {
        var keys = string.IsNullOrEmpty(HotKey.Combination) ? "use o ícone na bandeja" : HotKey.Combination;
        Footer.Text = $"{keys} abre · Enter executa · Esc esconde";
    }

    /// <summary>
    /// Fechar a janela (X, Alt+F4) só esconde — o app vive na bandeja.
    /// Só o menu "Sair" encerra de verdade.
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        var app = System.Windows.Application.Current as App;
        if (app is null || !app.ClosingForReal)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    public void HideBar()
    {
        RememberPlace();
        Hide();
    }

    /// <summary>Arrasta a janela pela faixa de entrada.</summary>
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); }
            catch { /* clique simples, sem arrasto */ }
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.Escape)
        {
            // Esc esconde a barra; o app continua vivo na bandeja.
            RememberPlace();
            Hide();
            e.Handled = true;
        }
    }

    /* ----------------------------- saudação ----------------------------- */
    private static string ShortcutHint() =>
        string.IsNullOrEmpty(HotKey.Combination)
            ? "Abra pelo ícone do Orbi na bandeja."
            : $"{HotKey.Combination} abre em qualquer programa.";

    private void Add(ResultRow row) => _rows.Add(row);

    /* ----------------------------- digitação ----------------------------- */
    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        Hint.Visibility = string.IsNullOrEmpty(Input.Text) ? Visibility.Visible : Visibility.Collapsed;
        var text = Input.Text.Trim();
        _rows.Clear();

        if (string.IsNullOrWhiteSpace(text))
        {
            _debounce.Stop();
            Collapse();
            return;
        }

        // "/" abre o catálogo: a pessoa vê as ferramentas em vez de decorar comandos.
        if (text == "/" )
        {
            _debounce.Stop();
            Expand();
            foreach (var group in Tools.Groups)
            {
                var sample = Tools.All.Where(tool => tool.Group == group).Take(3).Select(tool => tool.Name);
                Add(new ResultRow
                {
                    Glyph = "\u25A0",
                    Text = group,
                    Detail = string.Join(" · ", sample) + " …",
                    Badge = Tools.All.Count(tool => tool.Group == group).ToString(),
                    Command = "/" + group,
                });
            }
            Results.SelectedIndex = 0;
            UpdateFooter();
            return;
        }

        // "/grupo": lista as ferramentas daquele grupo. "/nome": executa direto.
        if (text.StartsWith("/", StringComparison.Ordinal) && text.Length > 1)
        {
            _debounce.Stop();
            Expand();
            var query = text[1..].Trim();
            var tool = Tools.Find(query);
            if (tool is not null)
            {
                Add(ToRow(OrbiReply.Local(Tools.Run(tool.Id), $"{tool.Group} · {tool.Name}")));
                Results.SelectedIndex = 0;
                UpdateFooter();
                return;
            }

            var found = Tools.Search(query);
            foreach (var item in found.Take(40))
            {
                Add(new ResultRow
                {
                    Glyph = "\u2192",
                    Text = item.Name,
                    Detail = item.Group,
                    Badge = "Enter",
                    Command = "/" + item.Id,
                });
            }
            if (found.Count == 0)
            {
                Add(new ResultRow { Glyph = "?", Text = $"Nenhuma ferramenta com “{query}”.", Detail = "Digite / para ver os grupos." });
            }
            Results.SelectedIndex = 0;
            UpdateFooter();
            return;
        }

        // Sugestão ao vivo: mostra o que vai acontecer antes de apertar Enter.
        var preview = Interpreter.Interpret(text);
        if (preview.Kind != ReplyKind.NeedsAi)
        {
            _debounce.Stop();
            Expand();
            Add(ToRow(preview));

            // 1º item selecionado: Enter executa sem precisar clicar.
            Results.SelectedIndex = 0;
            if (preview.Kind == ReplyKind.Confirm)
            {
                Add(new ResultRow
                {
                    Glyph = "\u2713",
                    Text = "Sim, executar",
                    Detail = "Enter confirma. Esc cancela.",
                    Badge = "confirmar",
                    BadgeBrush = "#FFC7452A",
                    BadgeTextBrush = "#FFFFFFFF",
                    Command = text,
                    IsConfirm = true,
                });
            }
            return;
        }

        if (!Gemini.Available)
        {
            Expand();
            Results.SelectedIndex = 0;
            Add(new ResultRow
            {
                Glyph = "?",
                Text = "Preciso da chave do Gemini para responder isso.",
                Detail = "Ajustes ▸ Modo Cloud. A chave é gratuita e fica cifrada neste PC.",
                Command = "conta",
                Badge = "configurar",
                BadgeBrush = "#FFC7452A",
                BadgeTextBrush = "#FFFFFFFF",
            });
            return;
        }

        Expand();
        Add(new ResultRow
        {
            Glyph = "?",
            Text = "Perguntar ao Gemini…",
            Detail = "Pausa na digitação para não gastar chamadas.",
            Badge = "IA",
            BadgeBrush = "#FFC7452A",
            BadgeTextBrush = "#FFFFFFFF",
        });
        _debounce.Stop();
        _debounce.Start();
    }

    private static ResultRow ToRow(OrbiReply reply) => new()
    {
        Glyph = reply.Kind switch
        {
            ReplyKind.Confirm => "\u26A0",
            ReplyKind.Private => "\u25A0",
            ReplyKind.NeedsAi => "?",
            _ => "\u2713",
        },
        Text = reply.Text,
        Detail = reply.Detail,
        Badge = reply.Kind switch
        {
            ReplyKind.Confirm => "confirmar",
            ReplyKind.Private => "privado",
            ReplyKind.NeedsAi => "IA",
            _ => "no seu PC",
        },
        BadgeBrush = reply.Kind is ReplyKind.Confirm or ReplyKind.NeedsAi ? "#FFC7452A" : "#1FFFFFFF",
        BadgeTextBrush = reply.Kind is ReplyKind.Confirm or ReplyKind.NeedsAi ? "#FFFFFFFF" : "#99F1EBDF",
    };

    /// <summary>
    /// Toda conversa vira memória: o pedido, o que o app fez e a resposta da IA.
    /// É isso que permite “o que eu tava fazendo ontem?” funcionar depois.
    /// </summary>
    private static void Remember(string pedido, OrbiReply reply)
    {
        try
        {
            var detalhe = string.IsNullOrWhiteSpace(reply.Detail) ? reply.Text : reply.Detail;
            Memory.Remember(pedido, "pedido", detalhe);
        }
        catch
        {
            // memória nunca pode atrapalhar a execução
        }
    }

    /* ----------------------------- IA ----------------------------- */
    /// <summary>
    /// Texto livre: o Orbi planeja e age. Cada passo aparece na barra, e no fim
    /// vem a resposta em uma frase.
    /// </summary>
    private async Task AskAiAsync(string prompt)
    {
        if (_thinking || string.IsNullOrWhiteSpace(prompt)) return;
        _thinking = true;
        Expand();
        Footer.Text = "pensando…";
        try
        {
            var (answer, steps) = await Agent.RunAsync(prompt, _history, Memory.TodaySummary());
            _rows.Clear();

            foreach (var passo in steps.TakeLast(6))
            {
                Add(new ResultRow
                {
                    Glyph = passo.Ok ? "\u2713" : "!",
                    Text = passo.Title,
                    Detail = passo.Detail,
                    Badge = passo.Ok ? "" : "erro",
                });
            }

            Add(new ResultRow
            {
                Glyph = "\u2726",
                Text = answer,
                Badge = Gemini.Available ? "IA" : "sem chave",
                BadgeBrush = Gemini.Available ? "#FFC7452A" : "#1FFFFFFF",
                BadgeTextBrush = Gemini.Available ? "#FFFFFFFF" : "#99F1EBDF",
            });

            if (Gemini.Available)
            {
                _history.Add(("user", prompt));
                _history.Add(("assistant", answer));
                if (_history.Count > 12) _history.RemoveRange(0, _history.Count - 12);
            }
        }
        finally
        {
            _thinking = false;
            UpdateFooter();
        }
    }

    /* ----------------------------- execução ----------------------------- */
    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Run();
        }
        else if (e.Key == Key.Down && _rows.Count > 0)
        {
            Results.SelectedIndex = Math.Min(_rows.Count - 1, Results.SelectedIndex + 1);
            e.Handled = true;
        }
        else if (e.Key == Key.Up && _rows.Count > 0)
        {
            Results.SelectedIndex = Math.Max(0, Results.SelectedIndex - 1);
            e.Handled = true;
        }
    }

    private void Input_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // F1 mostra a lista de comandos — a pessoa não precisa "adivinhar".
        if (e.Key == Key.F1)
        {
            Input.Text = "/";
            Input.CaretIndex = 1;
            e.Handled = true;
            return;
        }

        // Tab completa o primeiro item executável.
        if (e.Key == Key.Tab)
        {
            var first = _rows.FirstOrDefault(row => !string.IsNullOrWhiteSpace(row.Command));
            if (first?.Command is { } command)
            {
                Input.Text = command;
                Input.CaretIndex = Input.Text.Length;
                e.Handled = true;
            }
        }
    }

    private void Run()
    {
        var selected = Results.SelectedItem as ResultRow;
        var text = Input.Text.Trim();

        if (selected?.IsConfirm == true)
        {
            ExecuteSelected(text);
            return;
        }

        // Texto digitado sempre vence: Enter executa o que está na caixa.
        // A seleção só é usada quando a caixa está vazia (exemplos/recentes).
        if (string.IsNullOrWhiteSpace(text) && selected?.Command is { } picked)
        {
            ExecuteSelected(picked);
            return;
        }

        if (string.IsNullOrWhiteSpace(text)) return;
        ExecuteSelected(text);
    }

    /// <summary>
    /// Atalhos diretos de controle do PC, sem passar pela IA: são o que a pessoa
    /// pede com mais pressa. Devolve null quando não é um deles.
    /// </summary>
    private static ResultRow? DirectControl(string text)
    {
        var t = Interpreter.Simplify(text);

        var digitar = System.Text.RegularExpressions.Regex.Match(
            text, @"^(?:orbi[, ]+)?(?:digite|digitar|escreva|escrever)\s*:?\s+(?<t>.+)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (digitar.Success)
        {
            var conteudo = digitar.Groups["t"].Value.Trim().Trim('"');
            Control.Type(conteudo);
            return new ResultRow { Glyph = "\u2713", Text = "Digitei.", Detail = conteudo, Badge = "digitado" };
        }

        var clicar = System.Text.RegularExpressions.Regex.Match(
            t, @"^(?:clique|clicar|click)(?:\s+em)?\s+(?<p>.+)$");
        if (clicar.Success && Control.ParsePoint(clicar.Groups["p"].Value) is { } ponto)
        {
            Control.Click(ponto.X, ponto.Y);
            return new ResultRow { Glyph = "\u2713", Text = $"Cliquei em {ponto.X},{ponto.Y}.", Badge = "clique" };
        }

        var atalho = System.Text.RegularExpressions.Regex.Match(
            t, @"^(?:atalho|aperte|tecla|pressione|hotkey)\s+(?<k>[a-z0-9+ ]+)$");
        if (atalho.Success)
        {
            var combo = atalho.Groups["k"].Value.Trim();
            Control.Hotkey(combo);
            return new ResultRow { Glyph = "\u2713", Text = $"Atalho {combo} enviado.", Badge = "teclas" };
        }

        if (t is "ver a tela" or "ver tela" or "o que esta na tela" or "olhe a tela" or "olhar a tela")
        {
            return new ResultRow { Glyph = "\u25C9", Text = Control.DescribeScreen(), Badge = "tela" };
        }

        var focar = System.Text.RegularExpressions.Regex.Match(
            t, @"^(?:traga|trazer|focar|foca|ative|ativar)\s+(?:o |a |os |as )?(?<j>.+?)(?:\s+para a frente)?$");
        if (focar.Success)
        {
            var janela = focar.Groups["j"].Value.Trim();
            var ok = Control.Focus(janela);
            return new ResultRow
            {
                Glyph = ok ? "\u2713" : "!",
                Text = ok ? $"Trouxe {janela} para a frente." : $"Não achei uma janela de {janela}.",
                Badge = "janela",
            };
        }

        return null;
    }

    private void ExecuteSelected(string text)
    {
        Store.PushCommand(text);

        // "conta" abre o menu do botão de conta (sem digitar nada além disso).
        if (string.Equals(text.Trim(), "conta", StringComparison.OrdinalIgnoreCase))
        {
            Input.Text = "";
            AccountButton_Click(AccountButton, new RoutedEventArgs());
            return;
        }
        // controle direto tem prioridade: "digite:", "clique em 640,400",
        // "atalho ctrl+t", "traga o chrome", "ver a tela".
        // números soltos de "30" ou "50%" são volume, não clique: só trata
        // como clique quando vier a palavra.
        if (!System.Text.RegularExpressions.Regex.IsMatch(Interpreter.Simplify(text), @"^(clique|clicar|click)\b")
            && DirectControl(text) is { } direct)
        {
            _rows.Clear();
            Expand();
            Add(direct);
            Input.Text = "";
            Input.Focus();
            return;
        }

        var reply = Interpreter.Execute(text);
        Remember(text, reply);

        _rows.Clear();
        Expand();
        Add(ToRow(reply));

        if (reply.Kind == ReplyKind.Confirm)
        {
            Add(new ResultRow
            {
                Glyph = "\u2713",
                Text = "Sim, executar",
                Detail = "Enter confirma. Esc cancela.",
                Badge = "confirmar",
                BadgeBrush = "#FFC7452A",
                BadgeTextBrush = "#FFFFFFFF",
                Command = text,
                IsConfirm = true,
            });
        }

        Input.Text = "";
        Input.Focus();
    }

    private void Results_Click(object sender, MouseButtonEventArgs e)
    {
        if (Results.SelectedItem is not ResultRow row) return;
        if (row.Command is { } command)
        {
            Input.Text = command;
            ExecuteSelected(command);
        }
    }

    private void Results_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Results.SelectedItem is ResultRow { Command: { } command })
        {
            ExecuteSelected(command);
        }
    }
}
