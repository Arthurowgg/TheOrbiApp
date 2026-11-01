using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Orbi.Setup;

/// <summary>
/// A janela inteira do instalador: bem-vindo → removendo versão antiga →
/// instalando (com progresso e log ao vivo) → pronto.
/// </summary>
public class SetupWindow : Window
{
    private readonly string[] _args;
    private readonly Grid _raiz = new();
    private readonly StackPanel _conteudo = new();
    private readonly ProgressBar _barra = new();
    private readonly TextBlock _status = new();
    private readonly Button _principal = new();
    private readonly Button _secundario = new();

    private enum Etapa { BoasVindas, Removendo, Instalando, Pronto, Erro }
    private Etapa _etapa = Etapa.BoasVindas;
    private string _erro = "";

    public SetupWindow(string[] args)
    {
        _args = args;
        Title = "Orbi — instalação";
        Width = 760;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(App.Paper);
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");

        if (_args.Contains("--uninstall"))
        {
            Desinstalar();
            return;
        }

        Montar();
        MostrarBoasVindas();
    }

    /* ------------------------------ layout ------------------------------ */
    private void Montar()
    {
        _raiz.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(232) });
        _raiz.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _raiz.Children.Add(Lateral());

        var direita = new Grid { Margin = new Thickness(30, 28, 30, 24) };
        direita.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        direita.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        direita.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var titulo = new StackPanel();
        titulo.Children.Add(new TextBlock
        {
            Text = "ORBI",
            FontFamily = new FontFamily("Bahnschrift SemiBold, Segoe UI Semibold"),
            FontSize = 30,
            Foreground = new SolidColorBrush(App.Ink),
        });
        titulo.Children.Add(_status);
        _status.FontSize = 13.5;
        _status.Margin = new Thickness(0, 6, 0, 0);
        _status.TextWrapping = TextWrapping.Wrap;
        _status.Foreground = new SolidColorBrush(App.Warm);
        Grid.SetRow(titulo, 0);
        direita.Children.Add(titulo);

        var meio = new Grid { Margin = new Thickness(0, 22, 0, 18) };
        meio.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        meio.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _barra.Height = 6;
        _barra.Minimum = 0;
        _barra.Maximum = 1;
        _barra.Foreground = new SolidColorBrush(App.Signal);
        _barra.Background = new SolidColorBrush(Color.FromRgb(0xE2, 0xDA, 0xCC));
        Grid.SetRow(_barra, 0);
        meio.Children.Add(_barra);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 16, 0, 0),
            Content = _conteudo,
        };
        Grid.SetRow(scroll, 1);
        meio.Children.Add(scroll);

        Grid.SetRow(meio, 1);
        direita.Children.Add(meio);

        var botoes = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        EstiloBotao(_secundario, primario: false);
        EstiloBotao(_principal, primario: true);
        _secundario.Margin = new Thickness(0, 0, 8, 0);
        _principal.Click += (_, _) => OnPrincipal();
        _secundario.Click += (_, _) => OnSecundario();
        botoes.Children.Add(_secundario);
        botoes.Children.Add(_principal);
        Grid.SetRow(botoes, 2);
        direita.Children.Add(botoes);

        Grid.SetColumn(direita, 1);
        _raiz.Children.Add(direita);
        Content = _raiz;
    }

    private UIElement Lateral()
    {
        var fundo = new Grid { Background = new SolidColorBrush(App.Ink) };

        // grade fina
        var linhas = new Canvas();
        for (var x = 0; x < 232; x += 20)
        {
            linhas.Children.Add(new Line
            {
                X1 = x, X2 = x, Y1 = 0, Y2 = 520,
                Stroke = new SolidColorBrush(Color.FromRgb(0x1C, 0x16, 0x12)), StrokeThickness = 1,
            });
        }
        fundo.Children.Add(linhas);

        var conteudo = new StackPanel { Margin = new Thickness(24, 26, 20, 24) };

        conteudo.Children.Add(new Border
        {
            Width = 34, Height = 34,
            Background = new SolidColorBrush(App.Signal),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new Ellipse
            {
                Width = 10, Height = 10,
                Fill = new SolidColorBrush(App.Ink),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        });

        conteudo.Children.Add(new TextBlock
        {
            Text = "Você pede.\nO PC faz.",
            FontFamily = new FontFamily("Bahnschrift SemiBold, Segoe UI Semibold"),
            FontSize = 26,
            Foreground = new SolidColorBrush(App.Paper),
            Margin = new Thickness(0, 22, 0, 0),
            LineHeight = 30,
        });

        conteudo.Children.Add(new TextBlock
        {
            Text = $"versão {Installer.Versao}",
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x8C, 0x7C)),
            Margin = new Thickness(0, 10, 0, 0),
        });

        var imagem = Logo();
        if (imagem is not null)
        {
            imagem.Margin = new Thickness(0, 26, 0, 0);
            conteudo.Children.Add(imagem);
        }

        conteudo.Children.Add(new TextBlock
        {
            Text = "Alt+Shift+O\nabre a barra em\nqualquer programa",
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x8C, 0x7C)),
            Margin = new Thickness(0, 26, 0, 0),
            LineHeight = 17,
        });

        fundo.Children.Add(conteudo);
        return fundo;
    }

    private static Image? Logo()
    {
        try
        {
            var assembly = typeof(SetupWindow).Assembly;
            var nome = assembly.GetManifestResourceNames()
                .FirstOrDefault(item => item.EndsWith("orbi.png", StringComparison.OrdinalIgnoreCase));
            if (nome is null) return null;
            using var stream = assembly.GetManifestResourceStream(nome)!;
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            return new Image { Source = bitmap, Width = 128, HorizontalAlignment = HorizontalAlignment.Left };
        }
        catch
        {
            return null;
        }
    }

    private static void EstiloBotao(Button botao, bool primario)
    {
        botao.Padding = new Thickness(20, 10, 20, 10);
        botao.FontSize = 13.5;
        botao.FontWeight = FontWeights.SemiBold;
        botao.Cursor = System.Windows.Input.Cursors.Hand;
        botao.BorderThickness = primario ? new Thickness(0) : new Thickness(1);
        botao.Background = new SolidColorBrush(primario ? App.Ink : Colors.Transparent);
        botao.Foreground = new SolidColorBrush(primario ? App.Paper : App.Ink);
        botao.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD0, 0xC6, 0xB5));
    }

    /* ------------------------------ etapas ------------------------------ */
    private void MostrarBoasVindas()
    {
        _etapa = Etapa.BoasVindas;
        _status.Text = "Pronto para instalar. Nada de administrador, nada de cadastro.";
        _barra.Visibility = Visibility.Collapsed;
        _conteudo.Children.Clear();

        var instalada = Paths.InstalledVersion;
        var linhas = new List<(string Rotulo, string Valor)>
        {
            ("Instala em", Paths.Target),
            ("Versão", Installer.Versao),
            ("Tamanho", Tamanho()),
            ("Conta", "opcional — a mesma do site"),
        };
        if (instalada is not null)
        {
            linhas.Insert(0, ("Versão instalada", $"{instalada} — será substituída"));
        }

        foreach (var (rotulo, valor) in linhas)
        {
            var linha = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            linha.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            linha.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var r = new TextBlock
            {
                Text = rotulo.ToUpperInvariant(),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(App.Warm),
            };
            linha.Children.Add(r);
            var v = new TextBlock
            {
                Text = valor,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(App.Ink),
            };
            Grid.SetColumn(v, 1);
            linha.Children.Add(v);
            _conteudo.Children.Add(linha);
        }

        _conteudo.Children.Add(new TextBlock
        {
            Text = "Você pode desinstalar quando quiser em Configurações ▸ Aplicativos. " +
                   "Notas, lembretes e a memória do Orbi ficam neste PC e não são apagados.",
            FontSize = 12.5,
            Margin = new Thickness(0, 12, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(App.Warm),
        });

        _principal.Content = "Instalar o Orbi";
        _secundario.Content = "Cancelar";
        _secundario.Visibility = Visibility.Visible;
    }

    private void OnPrincipal()
    {
        switch (_etapa)
        {
            case Etapa.BoasVindas:
                _ = InstalarAsync();
                break;
            case Etapa.Pronto:
                Abrir();
                break;
            case Etapa.Erro:
                MostrarBoasVindas();
                break;
        }
    }

    private void OnSecundario()
    {
        if (_etapa == Etapa.Pronto)
        {
            Close();
            return;
        }
        Close();
    }

    private async Task InstalarAsync()
    {
        _etapa = Etapa.Instalando;
        _barra.Visibility = Visibility.Visible;
        _barra.Value = 0;
        _principal.IsEnabled = false;
        _principal.Content = "Instalando…";
        _secundario.Visibility = Visibility.Collapsed;
        _conteudo.Children.Clear();

        // log ao vivo: mostra cada passo em vez de uma barra muda
        var log = new TextBlock
        {
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(App.Warm),
            LineHeight = 17,
        };
        _conteudo.Children.Add(log);
        ((App)Application.Current).LogBox = log;

        var progresso = new Progress<double>(valor =>
        {
            _barra.Value = valor;
            _status.Text = valor < 0.1 ? "Encerrando versões antigas…"
                : valor < 0.85 ? $"Copiando os arquivos… {valor * 100:0}%"
                : valor < 1 ? "Criando atalhos…"
                : "Pronto.";
        });

        try
        {
            await Installer.RunAsync(progresso, CancellationToken.None);
            MostrarPronto();
        }
        catch (Exception error)
        {
            _erro = error.Message;
            _etapa = Etapa.Erro;
            _status.Text = "A instalação não terminou.";
            log.Text += Environment.NewLine + "ERRO: " + error;
            _principal.IsEnabled = true;
            _principal.Content = "Tentar de novo";
            _secundario.Content = "Fechar";
            _secundario.Visibility = Visibility.Visible;
            _secundario.Click += (_, _) => Close();
        }
    }

    private void MostrarPronto()
    {
        _etapa = Etapa.Pronto;
        _barra.Value = 1;
        _status.Text = "Instalado. O Orbi já está no Menu Iniciar.";
        _principal.IsEnabled = true;
        _principal.Content = "Abrir o Orbi";
        _secundario.Content = "Fechar";
        _secundario.Visibility = Visibility.Visible;

        _conteudo.Children.Add(new TextBlock
        {
            Text = "Aperte Alt+Shift+O em qualquer programa.",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 16, 0, 0),
            Foreground = new SolidColorBrush(App.Ink),
        });

        _conteudo.Children.Add(new TextBlock
        {
            Text = "O instalador é leve: o app em si é baixado aqui, durante a instalação. " +
                   "Depois o Orbi abre como uma barra de comando, e o atalho do Menu Iniciar " +
                   "leva direto às ferramentas e aos ajustes.",
            FontSize = 12.5,
            Margin = new Thickness(0, 6, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(App.Warm),
        });
    }

    private static void Abrir()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Paths.Exe) { UseShellExecute = true, WorkingDirectory = Paths.Target });
        }
        catch (Exception error)
        {
            MessageBox.Show("Não consegui abrir o Orbi: " + error.Message, "Orbi");
        }
    }

    private static string Tamanho()
    {
        var instalador = Environment.ProcessPath is { } exe && File.Exists(exe)
            ? new FileInfo(exe).Length / 1024.0 / 1024.0
            : 0;
        var cache = Payload.Cached ? " · app já baixado neste PC" : "";
        return $"{instalador:0.0} MB · baixa o Orbi na hora{cache}";
    }

    /* ------------------------------ desinstalar ------------------------------ */
    private void Desinstalar()
    {
        var silencioso = _args.Contains("--silent");
        if (!silencioso)
        {
            var resposta = MessageBox.Show(
                "Remover o Orbi deste PC?\n\nNotas, lembretes e a memória em " + Paths.Data + " não são apagados.",
                "Desinstalar o Orbi", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (resposta != MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
                return;
            }
        }

        Installer.Uninstall();
        if (!silencioso)
        {
            MessageBox.Show("O Orbi foi removido.", "Desinstalar o Orbi");
        }
        Application.Current.Shutdown();
    }
}
