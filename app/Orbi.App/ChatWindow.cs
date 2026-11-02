using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Orbi.Services;

namespace Orbi;

/// <summary>
/// O chat do Orbi: uma conversa de verdade com a IA.
///
/// - a pergunta aparece na hora (balão da pessoa);
/// - embaixo, os passos do agente entram um a um, animados, com ícone;
/// - a resposta é escrita em streaming, letra por letra, no balão do Orbi;
/// - o cabeçalho mostra o motor: modelo, quantas ferramentas ele enxerga e se a
///   chave está configurada;
/// - Enter envia; Shift+Enter quebra linha; Esc fecha; o botão quadrado para o
///   agente no meio do caminho.
/// </summary>
public sealed class ChatWindow : Window
{
    private readonly StackPanel _conversa = new() { Margin = new Thickness(18, 8, 18, 8) };
    private readonly ScrollViewer _rolagem = new();
    private readonly TextBox _entrada = new();
    private readonly TextBlock _dica = new();
    private readonly Border _caixaEntrada = new();
    private readonly Button _enviar = new();
    private readonly Path _enviarIcone;
    private readonly TextBlock _motor = new();
    private readonly Border _seloMotor = new();
    private readonly StackPanel _sugestoes = new();
    private CancellationTokenSource? _cancelamento;
    private bool _ocupado;
    private bool _rolarSozinho = true;

    public ChatWindow()
    {
        Title = "Orbi";
        Width = 760;
        Height = 700;
        MinWidth = 520;
        MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = ChatKit.FundoBrush;
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");

        var raiz = new Grid();
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        raiz.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        raiz.Children.Add(Cabecalho());

        _rolagem.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _rolagem.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _rolagem.Content = _conversa;
        _rolagem.Padding = new Thickness(0, 6, 0, 6);
        _rolagem.ScrollChanged += (_, e) =>
        {
            // se a pessoa rolou para cima, não puxa de volta na cara dela
            if (e.ExtentHeightChange == 0) _rolarSozinho = _rolagem.VerticalOffset >= _rolagem.ScrollableHeight - 40;
        };
        Grid.SetRow(_rolagem, 1);
        raiz.Children.Add(_rolagem);

        _enviarIcone = ChatKit.Icone("send", 16, Brushes.White, 1.6);
        var rodape = Rodape();
        Grid.SetRow(rodape, 2);
        raiz.Children.Add(rodape);

        Content = raiz;
        Loaded += (_, _) =>
        {
            BoasVindas();
            _entrada.Focus();
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                if (_ocupado) Cancelar();
                else Close();
            }
        };
    }

    /// <summary>Para o autoteste: a janela montou e há balões na conversa?</summary>
    public bool Pronto => IsLoaded && _conversa is not null && _entrada is not null;

    public int Baloes => _conversa.Children.Count;

    /// <summary>
    /// Manda uma frase como se tivesse sido digitada (a barra usa isso: o texto
    /// que a pessoa escreveu abre o chat já enviado).
    /// </summary>
    public void Perguntar(string texto)
    {
        if (!IsLoaded)
        {
            Loaded += (_, _) =>
            {
                _entrada.Text = texto;
                Enviar();
            };
            return;
        }
        _entrada.Text = texto;
        Enviar();
    }

    /* ------------------------------ cabeçalho ------------------------------ */

    private FrameworkElement Cabecalho()
    {
        var grade = new Grid { Margin = new Thickness(20, 14, 16, 12) };
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var marca = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(10),
            Background = ChatKit.SinalBrush,
            Margin = new Thickness(0, 0, 12, 0),
            Child = ChatKit.Icone("spark", 18, Brushes.White, 1.7),
        };
        ChatKit.Respirar(marca, 0.92, 1, 2200);
        grade.Children.Add(marca);

        var titulos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titulos.Children.Add(new TextBlock
        {
            Text = "Orbi",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = ChatKit.TintaBrush,
        });
        _motor.FontFamily = new FontFamily("Cascadia Mono, Consolas");
        _motor.FontSize = 10.5;
        _motor.Foreground = ChatKit.QuenteBrush;
        titulos.Children.Add(_motor);
        Grid.SetColumn(titulos, 1);
        grade.Children.Add(titulos);

        var acoes = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var limpar = BotaoIcone("plus", "Conversa nova", () => { Agent.LimparHistorico(); NovaConversa(); });
        var ajustes = BotaoIcone("sliders", "Ajustes da IA", () =>
        {
            Close();
            App.Instance.OpenPanel("ia");
        });
        var fechar = BotaoIcone("stop", "Fechar (Esc)", Close);
        acoes.Children.Add(limpar);
        acoes.Children.Add(ajustes);
        acoes.Children.Add(fechar);
        Grid.SetColumn(acoes, 2);
        grade.Children.Add(acoes);

        AtualizarMotor();

        var borda = new Border
        {
            Child = grade,
            Background = ChatKit.PapelBrush,
            BorderBrush = ChatKit.LinhaBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        return borda;
    }

    private void AtualizarMotor()
    {
        _motor.Text = Gemini.Available
            ? $"{Gemini.Mode} · {ToolSchema.Total} funções · memória {Memory.All.Count}"
            : "sem chave — abra os ajustes da IA";
        _motor.Foreground = Gemini.Available ? ChatKit.QuenteBrush : ChatKit.SinalBrush;
    }

    private static Button BotaoIcone(string icone, string dica, Action acao)
    {
        var botao = new Button
        {
            Content = ChatKit.Icone(icone, 14, ChatKit.QuenteBrush, 1.6),
            Width = 30,
            Height = 30,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = dica,
            Margin = new Thickness(4, 0, 0, 0),
            Template = TemplateRedondo(),
        };
        botao.Click += (_, _) => acao();
        return botao;
    }

    /* ------------------------------ rodapé ------------------------------ */

    private FrameworkElement Rodape()
    {
        _entrada.AcceptsReturn = false;
        _entrada.TextWrapping = TextWrapping.Wrap;
        _entrada.MinHeight = 24;
        _entrada.MaxHeight = 120;
        _entrada.BorderThickness = new Thickness(0);
        _entrada.Background = Brushes.Transparent;
        _entrada.FontSize = 14;
        _entrada.Foreground = ChatKit.TintaBrush;
        _entrada.CaretBrush = ChatKit.SinalBrush;
        _entrada.VerticalContentAlignment = VerticalAlignment.Center;
        _entrada.Padding = new Thickness(0);
        _entrada.KeyDown += Entrada_KeyDown;
        _entrada.TextChanged += (_, _) => AjustarAlturaEntrada();

        _dica.Text = "Pergunte ou peça algo no PC";
        _dica.FontSize = 14;
        _dica.Foreground = new SolidColorBrush(Color.FromArgb(0x66, ChatKit.Quente.R, ChatKit.Quente.G, ChatKit.Quente.B));
        _dica.IsHitTestVisible = false;
        _dica.VerticalAlignment = VerticalAlignment.Center;

        var pilha = new Grid();
        pilha.Children.Add(_dica);
        pilha.Children.Add(_entrada);

        _enviar.Content = _enviarIcone;
        _enviar.Width = 34;
        _enviar.Height = 34;
        _enviar.Background = ChatKit.SinalBrush;
        _enviar.BorderThickness = new Thickness(0);
        _enviar.Cursor = Cursors.Hand;
        _enviar.ToolTip = "Enviar (Enter)";
        _enviar.Click += (_, _) => Enviar();
        _enviar.Template = TemplateRedondo();
        _enviar.Margin = new Thickness(10, 0, 0, 0);

        var linha = new Grid();
        linha.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        linha.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        linha.Children.Add(pilha);
        Grid.SetColumn(_enviar, 1);
        linha.Children.Add(_enviar);

        _caixaEntrada.Child = linha;
        _caixaEntrada.Background = ChatKit.PapelBrush;
        _caixaEntrada.BorderBrush = ChatKit.LinhaBrush;
        _caixaEntrada.BorderThickness = new Thickness(1);
        _caixaEntrada.CornerRadius = new CornerRadius(18);
        _caixaEntrada.Padding = new Thickness(16, 10, 8, 10);
        _caixaEntrada.Margin = new Thickness(18, 0, 18, 0);

        _sugestoes.Orientation = Orientation.Horizontal;
        _sugestoes.Margin = new Thickness(18, 8, 18, 4);

        var rodape = new StackPanel { Margin = new Thickness(0, 6, 0, 14) };
        rodape.Children.Add(_caixaEntrada);

        var dica = new TextBlock
        {
            Text = "Enter envia · Shift+Enter quebra linha · Esc fecha",
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 10,
            Foreground = ChatKit.QuenteBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0),
        };
        rodape.Children.Add(dica);
        return rodape;
    }

    /// <summary>Botão redondo sem o cromo padrão do Windows.</summary>
    private static ControlTemplate TemplateRedondo()
    {
        var borda = new FrameworkElementFactory(typeof(Border));
        borda.SetValue(Border.CornerRadiusProperty, new CornerRadius(17));
        borda.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        var conteudo = new FrameworkElementFactory(typeof(ContentPresenter));
        conteudo.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        conteudo.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        borda.AppendChild(conteudo);
        return new ControlTemplate(typeof(Button)) { VisualTree = borda };
    }

    private void AjustarAlturaEntrada()
    {
        _dica.Visibility = string.IsNullOrEmpty(_entrada.Text) ? Visibility.Visible : Visibility.Collapsed;
        _entrada.Height = double.NaN;
        _entrada.Measure(new Size(_entrada.ActualWidth <= 0 ? 400 : _entrada.ActualWidth, double.PositiveInfinity));
        var altura = Math.Min(120, Math.Max(24, _entrada.DesiredSize.Height));
        _entrada.Height = altura;
    }

    /* ------------------------------ conversa ------------------------------ */

    private void BoasVindas()
    {
        var total = Tools.All.Count;
        var bemVindo = new StackPanel();
        bemVindo.Children.Add(new TextBlock
        {
            Text = "Oi. Eu sou o Orbi.",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = ChatKit.TintaBrush,
        });
        bemVindo.Children.Add(new TextBlock
        {
            Text = Gemini.Available
                ? $"Falo com você e uso {total} ferramentas do Windows por você — abro programas, mexo em " +
                  "arquivos, vejo a tela, clico, digito e pesquiso na web. Peça em uma frase."
                : "Preciso da sua chave do Google AI Studio para pensar. Abra Ajustes ▸ IA e cole a chave — " +
                  $"é gratuita. As {total} ferramentas da barra continuam funcionando sem ela.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13.5,
            LineHeight = 19,
            Foreground = ChatKit.QuenteBrush,
            Margin = new Thickness(0, 6, 0, 0),
        });

        if (!Gemini.Available)
        {
            var abrir = new Button
            {
                Content = "Abrir os ajustes da IA",
                Background = ChatKit.SinalBrush,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand,
            };
            abrir.Click += (_, _) => { Close(); App.Instance.OpenPanel("ia"); };
            bemVindo.Children.Add(abrir);
        }

        var balao = ChatKit.BalaoOrbi(bemVindo);
        _conversa.Children.Add(balao);
        ChatKit.Entrar(balao);

        Sugestoes();
    }

    /// <summary>Pílulas de sugestão: um toque para começar bem.</summary>
    private void Sugestoes()
    {
        _sugestoes.Children.Clear();
        var ideias = new (string Texto, string Icone)[]
        {
            ("o que está aberto agora?", "window"),
            ("organizar os arquivos de Downloads", "folder"),
            ("quanto está o dólar hoje?", "globe"),
            ("resumir o que está na tela", "eye"),
        };
        foreach (var (texto, icone) in ideias)
        {
            var conteudo = new StackPanel { Orientation = Orientation.Horizontal };
            conteudo.Children.Add(ChatKit.Icone(icone, 12, ChatKit.QuenteBrush, 1.5));
            conteudo.Children.Add(new TextBlock
            {
                Text = texto,
                FontSize = 12,
                Foreground = ChatKit.QuenteBrush,
                Margin = new Thickness(6, 0, 0, 0),
            });
            var pilula = new Button
            {
                Content = conteudo,
                Background = ChatKit.PapelBrush,
                BorderBrush = ChatKit.LinhaBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand,
            };
            pilula.Click += (_, _) =>
            {
                _entrada.Text = texto;
                Enviar();
            };
            _sugestoes.Children.Add(pilula);
        }

        // um pai só: já removida da conversa anterior, entra na atual
        if (_sugestoes.Parent is System.Windows.Controls.Panel antigo) antigo.Children.Remove(_sugestoes);
        _conversa.Children.Add(_sugestoes);
        ChatKit.Entrar(_sugestoes, 120);
    }

    private void NovaConversa()
    {
        _conversa.Children.Clear();
        _sugestoes.Children.Clear();
        AtualizarMotor();
        BoasVindas();
    }

    private void RolarParaFim()
    {
        // depois que o layout mede o balão novo, senão o fim ainda é o antigo
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_rolarSozinho) _rolagem.ScrollToEnd();
        }), DispatcherPriority.Background);
    }

    /* ------------------------------ envio ------------------------------ */

    private void Entrada_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            Enviar();
        }
    }

    private async void Enviar()
    {
        var texto = _entrada.Text.Trim();
        if (texto.Length == 0 || _ocupado) return;

        _entrada.Text = "";
        _sugestoes.Visibility = Visibility.Collapsed;
        Ocupado(true);

        var balaoPessoa = ChatKit.BalaoPessoa(texto);
        _conversa.Children.Add(balaoPessoa);
        ChatKit.Entrar(balaoPessoa, 0, 180);
        RolarParaFim();

        // Balão do Orbi que já nasce com os pontinhos: é ali que a resposta cresce.
        var corpo = new StackPanel();
        var passos = new StackPanel { Margin = new Thickness(-14, 0, -14, 0) };
        var textoResposta = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13.5,
            LineHeight = 20,
            Foreground = ChatKit.TintaBrush,
        };
        var pensando = ChatKit.Pontos();
        var rotulo = new TextBlock
        {
            Text = "pensando",
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 10,
            Foreground = ChatKit.QuenteBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        var estado = new StackPanel { Orientation = Orientation.Horizontal };
        estado.Children.Add(pensando);
        estado.Children.Add(rotulo);

        corpo.Children.Add(estado);
        corpo.Children.Add(passos);
        textoResposta.Visibility = Visibility.Collapsed;
        corpo.Children.Add(textoResposta);

        var balaoOrbi = ChatKit.BalaoOrbi(corpo);
        _conversa.Children.Add(balaoOrbi);
        ChatKit.Entrar(balaoOrbi, 80);
        RolarParaFim();

        // Atualizações chegam de outra thread: sempre pelo dispatcher.
        void NaTela(Action acao) => Dispatcher.Invoke(acao);

        var acumulado = new StringBuilder();
        var ultimoPintar = DateTime.MinValue;

        // o indicador sai na primeira letra que chega
        void MostrarParcial(string parcial)
        {
            if (estado.Visibility == Visibility.Visible) estado.Visibility = Visibility.Collapsed;
            textoResposta.Visibility = Visibility.Visible;
            textoResposta.Text = parcial;
            RolarParaFim();
        }

        var eventos = new AgentEvents
        {
            Pensando = _ => NaTela(() =>
            {
                // o modelo está reunindo antes de escrever: mantém o indicador vivo
                if (estado.Visibility != Visibility.Visible) return;
                rotulo.Text = "pensando";
            }),

            Escrevendo = trecho => NaTela(() =>
            {
                acumulado.Append(trecho);
                // repinta no máximo a cada 60 ms: streaming sem travar a interface
                if ((DateTime.UtcNow - ultimoPintar).TotalMilliseconds < 60) return;
                ultimoPintar = DateTime.UtcNow;
                MostrarParcial(acumulado.ToString());
            }),

            Passo = passo => NaTela(() =>
            {
                var cartao = ChatKit.CartaoPasso(passo.Icone ?? "check", passo.Title, passo.Detail, passo.Ok);
                passos.Children.Add(cartao);
                ChatKit.Entrar(cartao, 0, 200);
                if (!passo.Ok) ChatKit.Pulsar(cartao, 1.02);
                RolarParaFim();
            }),
        };

        _cancelamento = new CancellationTokenSource();
        try
        {
            var (resposta, listaPassos, ok) = await Agent.RunAsync(texto, eventos, _cancelamento.Token);

            NaTela(() =>
            {
                estado.Visibility = Visibility.Collapsed;
                textoResposta.Text = "";
                // texto final entra com um fade suave, palavra por palavra seria lento demais aqui
                var final = new TextBlock
                {
                    Text = resposta,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13.5,
                    LineHeight = 20,
                    Foreground = ok ? ChatKit.TintaBrush : ChatKit.SinalBrush,
                };
                corpo.Children.Add(final);
                ChatKit.Entrar(final, 0, 260);
                // um traço curto na cor do Orbi marca o fim da resposta
                corpo.Children.Add(ChatKit.Faisca());

                if (listaPassos.Count > 0)
                {
                    var resumo = new TextBlock
                    {
                        Text = $"{listaPassos.Count} passo{(listaPassos.Count > 1 ? "s" : "")}",
                        FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                        FontSize = 9.5,
                        Foreground = ChatKit.QuenteBrush,
                        Margin = new Thickness(0, 6, 0, 0),
                    };
                    corpo.Children.Add(resumo);
                }

                AtualizarMotor();
                RolarParaFim();
            });
        }
        catch (Exception error)
        {
            Log.Error("chat", error);
            NaTela(() =>
            {
                estado.Visibility = Visibility.Collapsed;
                corpo.Children.Add(new TextBlock
                {
                    Text = "Alguma coisa falhou aqui: " + error.Message,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13,
                    Foreground = ChatKit.SinalBrush,
                });
            });
        }
        finally
        {
            Ocupado(false);
            _cancelamento?.Dispose();
            _cancelamento = null;
        }
    }

    private void Ocupado(bool ocupado)
    {
        _ocupado = ocupado;
        _enviar.Background = ocupado
            ? new SolidColorBrush(Color.FromArgb(0x33, ChatKit.Sinal.R, ChatKit.Sinal.G, ChatKit.Sinal.B))
            : ChatKit.SinalBrush;
        _enviarIcone.Data = ChatKit.Geometria(ocupado ? "stop" : "send");
        if (!ocupado) _entrada.Focus();
    }

    private void Cancelar()
    {
        _cancelamento?.Cancel();
        _ocupado = false;
        Ocupado(false);
    }
}
