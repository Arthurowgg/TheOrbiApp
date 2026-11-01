using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Orbi;

/// <summary>
/// Peças comuns dos painéis do Orbi (IA, Ferramentas, Perfil): mesmo fundo,
/// mesmo título, mesmas linhas. Assim eles parecem parte do mesmo app, não
/// janelas avulsas do Windows.
/// </summary>
public static class Panel
{
    public static readonly Color PaperColor = Color.FromRgb(0xF2, 0xED, 0xE3);
    public static readonly Color InkColor = Color.FromRgb(0x14, 0x10, 0x0C);
    public static readonly Color WarmColor = Color.FromRgb(0x6A, 0x61, 0x52);
    public static readonly Color RuleColor = Color.FromRgb(0xD0, 0xC6, 0xB5);
    public static readonly Color SignalColor = Color.FromRgb(0xC7, 0x45, 0x2A);

    public static Brush Paper => new SolidColorBrush(PaperColor);
    public static Brush Ink => new SolidColorBrush(InkColor);
    public static Brush Warm => new SolidColorBrush(WarmColor);
    public static Brush Rule => new SolidColorBrush(RuleColor);
    public static Brush Signal => new SolidColorBrush(SignalColor);

    public static Window Shell(string titulo, double largura, double altura)
    {
        return new Window
        {
            Title = titulo,
            Width = largura,
            Height = altura,
            MinWidth = Math.Min(largura, 420),
            MinHeight = 340,
            Background = Paper,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            UseLayoutRounding = true,
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
        };
    }

    /// <summary>Aplica no painel o mesmo papel quente, fonte e cromo da barra.</summary>
    public static void Install(Window janela, string titulo)
    {
        janela.Title = titulo;
        janela.Background = Paper;
        janela.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        janela.UseLayoutRounding = true;
        janela.FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
    }

    /// <summary>Cabeçalho: marca, nome do painel e o atalho de volta para a barra.</summary>
    public static Grid Header(string titulo, string? nota, Action onMenu, Window janela)
    {
        var grade = new Grid { Margin = new Thickness(20, 16, 16, 14) };
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var marca = new Border
        {
            Width = 10, Height = 10, CornerRadius = new CornerRadius(5),
            Background = Signal, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
        };
        grade.Children.Add(marca);

        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textos.Children.Add(new TextBlock
        {
            Text = titulo,
            FontFamily = new FontFamily("Bahnschrift SemiBold, Segoe UI Semibold"),
            FontSize = 15,
            Foreground = Ink,
        });
        if (!string.IsNullOrWhiteSpace(nota))
        {
            textos.Children.Add(new TextBlock
            {
                Text = nota,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 10,
                Foreground = Warm,
            });
        }
        Grid.SetColumn(textos, 1);
        grade.Children.Add(textos);

        var menu = new Button
        {
            Content = "‹ barra",
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = Warm,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 11,
            Padding = new Thickness(10, 6, 10, 6),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
        };
        menu.Click += (_, _) =>
        {
            janela.Close();
            onMenu();
        };
        Grid.SetColumn(menu, 2);
        grade.Children.Add(menu);

        return grade;
    }

    /// <summary>Cartão branco com borda fina: o bloco de conteúdo dos painéis.</summary>
    public static Border Card(UIElement conteudo) => new()
    {
        Child = conteudo,
        Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xF2, 0xE9)),
        BorderBrush = Rule,
        BorderThickness = new Thickness(1),
        Padding = new Thickness(16, 14, 16, 14),
        Margin = new Thickness(0, 0, 0, 12),
    };

    public static Border Divider() => new()
    {
        Height = 1,
        Background = Rule,
        Margin = new Thickness(0, 12, 0, 12),
    };

    public static TextBlock Label(string texto) => new()
    {
        Text = texto.ToUpperInvariant(),
        FontFamily = new FontFamily("Cascadia Mono, Consolas"),
        FontSize = 10,
        Foreground = Warm,
        Margin = new Thickness(0, 0, 0, 6),
    };

    public static TextBlock Body(string texto, bool forte = false) => new()
    {
        Text = texto,
        FontSize = 13,
        TextWrapping = TextWrapping.Wrap,
        Foreground = forte ? Ink : new SolidColorBrush(Color.FromRgb(0x3D, 0x36, 0x2E)),
        FontWeight = forte ? FontWeights.SemiBold : FontWeights.Normal,
    };

    public static TextBox Field(string valor = "") => new()
    {
        Text = valor,
        FontSize = 13,
        Padding = new Thickness(10, 8, 10, 8),
        Background = Brushes.White,
        BorderBrush = Rule,
        BorderThickness = new Thickness(1),
        Foreground = Ink,
    };

    public static Button Primary(string texto, Action acao)
    {
        var botao = new Button
        {
            Content = texto,
            Padding = new Thickness(16, 9, 16, 9),
            Background = Ink,
            Foreground = Paper,
            BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        botao.Click += (_, _) => acao();
        return botao;
    }

    public static Button Ghost(string texto, Action acao)
    {
        var botao = new Button
        {
            Content = texto,
            Padding = new Thickness(12, 8, 12, 8),
            Background = Brushes.Transparent,
            Foreground = Warm,
            BorderThickness = new Thickness(0),
            FontSize = 12,
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        botao.Click += (_, _) => acao();
        return botao;
    }

    /// <summary>Uma linha de navegação/ação: título, detalhe e seta.</summary>
    public static Button Row(string titulo, string? detalhe, Action acao, bool forte = false, bool aviso = false)
    {
        var linha = new Grid();
        linha.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        linha.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textos.Children.Add(new TextBlock
        {
            Text = titulo,
            FontSize = 13.5,
            Foreground = aviso ? Signal : Ink,
            FontWeight = forte ? FontWeights.SemiBold : FontWeights.Normal,
        });
        if (!string.IsNullOrWhiteSpace(detalhe))
        {
            textos.Children.Add(new TextBlock
            {
                Text = detalhe,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 10,
                Foreground = Warm,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }
        linha.Children.Add(textos);

        var seta = new TextBlock
        {
            Text = "›",
            FontSize = 16,
            Foreground = Warm,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
        };
        Grid.SetColumn(seta, 1);
        linha.Children.Add(seta);

        var botao = new Button
        {
            Content = linha,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 9, 4, 9),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Cursor = Cursors.Hand,
        };
        botao.Click += (_, _) => acao();
        return botao;
    }
}
