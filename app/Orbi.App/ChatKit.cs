using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Orbi;

/// <summary>
/// As peças do chat do Orbi: ícones, balões, passos e animações.
///
/// Tudo é desenhado aqui (Path/Geometry) em vez de imagem: fica nítido em
/// qualquer escala, acompanha o tema e não pesa no instalador.
/// </summary>
public static class ChatKit
{
    public static readonly Color Papel = Color.FromRgb(0xF7, 0xF3, 0xEA);
    public static readonly Color PapelFundo = Color.FromRgb(0xEA, 0xE3, 0xD6);
    public static readonly Color Tinta = Color.FromRgb(0x16, 0x12, 0x0D);
    public static readonly Color Quente = Color.FromRgb(0x6B, 0x62, 0x53);
    public static readonly Color Sinal = Color.FromRgb(0xC7, 0x45, 0x2A);
    public static readonly Color Linha = Color.FromRgb(0xDD, 0xD4, 0xC4);

    public static Brush PapelBrush => new SolidColorBrush(Papel);
    public static Brush FundoBrush => new SolidColorBrush(PapelFundo);
    public static Brush TintaBrush => new SolidColorBrush(Tinta);
    public static Brush QuenteBrush => new SolidColorBrush(Quente);
    public static Brush SinalBrush => new SolidColorBrush(Sinal);
    public static Brush LinhaBrush => new SolidColorBrush(Linha);

    /* ------------------------------ ícones ------------------------------ */

    private static readonly Dictionary<string, string> Desenho = new()
    {
        ["spark"] = "M8,1 L9.6,6.4 L15,8 L9.6,9.6 L8,15 L6.4,9.6 L1,8 L6.4,6.4 Z",
        ["send"] = "M1,8 L15,1 L11,15 L7.6,9.6 Z",
        ["eye"] = "M1,8 Q8,2 15,8 Q8,14 1,8 M8,5.6 A2.4,2.4 0 1 1 7.99,5.6",
        ["camera"] = "M1,4.5 H4.2 L5.4,2.5 H10.6 L11.8,4.5 H15 V13.5 H1 Z M8,9.6 A2.4,2.4 0 1 0 8.01,9.6",
        ["cursor"] = "M3,1 L12,8 L7.6,8.6 L10,14 L8.2,14.8 L5.8,9.4 L3,12 Z",
        ["keyboard"] = "M1,4 H15 V12 H1 Z M3.5,6.5 H4.6 M6.2,6.5 H7.3 M8.9,6.5 H10 M11.6,6.5 H12.7 M3.5,9.5 H12.5",
        ["window"] = "M1,3 H15 V13 H1 Z M1,6 H15 M4,1.8 V4 M12,1.8 V4",
        ["search"] = "M6.8,1.6 A5.2,5.2 0 1 1 6.79,1.6 M10.6,10.6 L15,15",
        ["page"] = "M3,1 H10 L13,4 V15 H3 Z M10,1 V4 H13 M5,7 H11 M5,9.5 H11 M5,12 H9",
        ["globe"] = "M8,1 A7,7 0 1 1 7.99,1 M1,8 H15 M8,1 Q3,8 8,15 Q13,8 8,1",
        ["book"] = "M2,2 H7 Q8,2 8,3.4 Q8,2 9,2 H14 V13 H9 Q8,13 8,14.2 Q8,13 7,13 H2 Z M8,3.4 V14.2",
        ["grid"] = "M1,1 H6.4 V6.4 H1 Z M9.6,1 H15 V6.4 H9.6 Z M1,9.6 H6.4 V15 H1 Z M9.6,9.6 H15 V15 H9.6 Z",
        ["folder"] = "M1,3.4 H6 L7.4,5 H15 V13.6 H1 Z",
        ["wifi"] = "M1,6 Q8,0.4 15,6 M3.4,9 Q8,5 12.6,9 M6,12 A2,2 0 1 1 6.01,12",
        ["calc"] = "M3,1 H13 V15 H3 Z M5.2,4 H10.8 M5.2,7.6 H6.6 M9,7.6 H10.4 M5.2,10.6 H6.6 M9,10.6 H10.4",
        ["calendar"] = "M1.6,3.4 H14.4 V15 H1.6 Z M1.6,6.4 H14.4 M5,1.4 V4 M11,1.4 V4",
        ["text"] = "M2.4,2 H13.6 M8,2 V14 M5.4,14 H10.6",
        ["volume"] = "M2,6 H5 L9,2.6 V13.4 L5,10 H2 Z M11.4,5.6 Q13.4,8 11.4,10.4",
        ["sliders"] = "M2,4.4 H14 M2,8 H14 M2,11.6 H14 M5.4,2.6 V6.2 M10.6,6.2 V9.8 M7.6,9.8 V13.4",
        ["battery"] = "M1,4.6 H12.4 V11.4 H1 Z M13.4,7 V9 M3.4,6.6 V9.4",
        ["chip"] = "M4,4 H12 V12 H4 Z M6.4,1 V4 M9.6,1 V4 M6.4,12 V15 M9.6,12 V15 M1,6.4 H4 M1,9.6 H4 M12,6.4 H15 M12,9.6 H15",
        ["alert"] = "M8,1.6 L15,14 H1 Z M8,6 V9.6 M8,11.6 V12.4",
        ["check"] = "M2,8.4 L6,12.4 L14,3.6",
        ["question"] = "M8,1 A7,7 0 1 1 7.99,1 M6,6.2 Q6,4.2 8,4.2 Q10,4.2 10,6 Q10,7.6 8,8.4 V9.6 M8,11.8 V12.4",
        ["clock"] = "M8,1 A7,7 0 1 1 7.99,1 M8,4 V8 L11,9.6",
        ["stop"] = "M4,4 H12 V12 H4 Z",
        ["copy"] = "M5,5 H14 V14 H5 Z M2,2 H11 V5 M2,2 V11 H5",
        ["plus"] = "M8,2 V14 M2,8 H14",
        ["arrow"] = "M2,12 L12,4 M7,4 H12 V9",
        ["google"] = "M15,8 A7,7 0 1 1 8,1 M15,8 H8",
    };

    public static Geometry? Geometria(string nome) =>
        Desenho.TryGetValue(nome, out var dados) ? Geometry.Parse(dados) : null;

    /// <summary>Um ícone de traço (16x16) na cor pedida.</summary>
    public static Path Icone(string nome, double tamanho = 16, Brush? cor = null, double espessura = 1.5)
    {
        var caminho = new Path
        {
            Data = Geometria(nome),
            Stroke = cor ?? TintaBrush,
            StrokeThickness = espessura,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Fill = Brushes.Transparent,
            Stretch = Stretch.Uniform,
            Width = tamanho,
            Height = tamanho,
            SnapsToDevicePixels = true,
        };
        return caminho;
    }

    /* ------------------------------ animações ------------------------------ */

    /// <summary>Entrada suave: sobe 8px e aparece.</summary>
    public static void Entrar(UIElement elemento, double atrasoMs = 0, double duracaoMs = 260)
    {
        elemento.Opacity = 0;
        var deslocamento = new TranslateTransform(0, 10);
        elemento.RenderTransform = deslocamento;

        var opacidade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(duracaoMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(atrasoMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        var subida = new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(duracaoMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(atrasoMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        elemento.BeginAnimation(UIElement.OpacityProperty, opacidade);
        deslocamento.BeginAnimation(TranslateTransform.YProperty, subida);
    }

    /// <summary>Pulso na cor do sinal — usado quando algo termina.</summary>
    public static void Pulsar(UIElement elemento, double escala = 1.06)
    {
        var grupo = new TransformGroup();
        var escalaTransform = new ScaleTransform(1, 1);
        grupo.Children.Add(escalaTransform);
        grupo.Children.Add(new TranslateTransform());
        elemento.RenderTransformOrigin = new Point(0.5, 0.5);

        var animacao = new DoubleAnimationUsingKeyFrames();
        animacao.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animacao.KeyFrames.Add(new EasingDoubleKeyFrame(escala, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(110))));
        animacao.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(280))));
        escalaTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animacao);
        escalaTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animacao);
    }

    /// <summary>Respiração infinita (pontinhos de “pensando”, brilho da marca).</summary>
    public static void Respirar(UIElement elemento, double minimo = 0.35, double maximo = 1, int ms = 900)
    {
        var animacao = new DoubleAnimation(minimo, maximo, TimeSpan.FromMilliseconds(ms))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        elemento.BeginAnimation(UIElement.OpacityProperty, animacao);
    }

    /// <summary>Gira (usado no ícone enquanto o agente trabalha).</summary>
    public static void Girar(UIElement elemento, int ms = 1400)
    {
        var rotacao = new RotateTransform(0);
        elemento.RenderTransformOrigin = new Point(0.5, 0.5);
        var grupo = elemento.RenderTransform as TransformGroup ?? new TransformGroup();
        if (elemento.RenderTransform is not TransformGroup) elemento.RenderTransform = grupo;
        grupo.Children.Add(rotacao);
        var animacao = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(ms))
        {
            RepeatBehavior = RepeatBehavior.Forever,
        };
        rotacao.BeginAnimation(RotateTransform.AngleProperty, animacao);
    }

    /// <summary>Três pontos que pulsam em sequência (indicador de digitação).</summary>
    public static FrameworkElement Pontos()
    {
        var painel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        for (var i = 0; i < 3; i++)
        {
            var ponto = new Ellipse
            {
                Width = 6,
                Height = 6,
                Margin = new Thickness(i == 0 ? 0 : 4, 0, 0, 0),
                Fill = new SolidColorBrush(Color.FromArgb(0x99, Sinal.R, Sinal.G, Sinal.B)),
            };
            var animacao = new DoubleAnimation(0.25, 1, TimeSpan.FromMilliseconds(600))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromMilliseconds(i * 160),
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            ponto.BeginAnimation(UIElement.OpacityProperty, animacao);
            painel.Children.Add(ponto);
        }
        return painel;
    }

    /* ------------------------------ balões ------------------------------ */

    /// <summary>Balão da pessoa: alinhado à direita, escuro.</summary>
    public static Border BalaoPessoa(string texto)
    {
        var bloco = new TextBlock
        {
            Text = texto,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13.5,
            LineHeight = 19,
            Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0xEF, 0xE5)),
        };
        return new Border
        {
            Child = bloco,
            Background = new SolidColorBrush(Tinta),
            CornerRadius = new CornerRadius(14, 14, 4, 14),
            Padding = new Thickness(14, 10, 14, 10),
            MaxWidth = 520,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(40, 6, 0, 6),
        };
    }

    /// <summary>Balão do Orbi: alinhado à esquerda, com o ícone da marca.</summary>
    public static Grid BalaoOrbi(FrameworkElement conteudo, bool comMarca = true)
    {
        var grade = new Grid { Margin = new Thickness(0, 6, 24, 6) };
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        if (comMarca)
        {
            var marca = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = new SolidColorBrush(Sinal),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 10, 0),
                Child = Icone("spark", 14, Brushes.White, 1.6),
            };
            grade.Children.Add(marca);
        }

        var caixa = new Border
        {
            Child = conteudo,
            Background = new SolidColorBrush(Papel),
            BorderBrush = new SolidColorBrush(Linha),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4, 14, 14, 14),
            Padding = new Thickness(14, 10, 14, 10),
        };
        Grid.SetColumn(caixa, 1);
        grade.Children.Add(caixa);
        return grade;
    }

    /// <summary>Cartão de passo: ícone, o que foi feito e o resultado.</summary>
    public static Border CartaoPasso(string icone, string titulo, string detalhe, bool ok)
    {
        var cor = ok ? QuenteBrush : SinalBrush;
        var linha = new Grid();
        linha.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        linha.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var iconeCaixa = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(6),
            Background = ok
                ? new SolidColorBrush(Color.FromArgb(0x14, Quente.R, Quente.G, Quente.B))
                : new SolidColorBrush(Color.FromArgb(0x1F, Sinal.R, Sinal.G, Sinal.B)),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 10, 0),
            Child = Icone(ok ? icone : "alert", 12, cor, 1.6),
        };
        linha.Children.Add(iconeCaixa);

        var textos = new StackPanel();
        textos.Children.Add(new TextBlock
        {
            Text = titulo,
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = cor,
            TextWrapping = TextWrapping.Wrap,
        });
        if (!string.IsNullOrWhiteSpace(detalhe))
        {
            textos.Children.Add(new TextBlock
            {
                Text = detalhe,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 10.5,
                Foreground = QuenteBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 2, 0, 0),
            });
        }
        Grid.SetColumn(textos, 1);
        linha.Children.Add(textos);

        return new Border
        {
            Child = linha,
            Background = new SolidColorBrush(Papel),
            BorderBrush = new SolidColorBrush(Linha),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(36, 3, 24, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
    }
}
