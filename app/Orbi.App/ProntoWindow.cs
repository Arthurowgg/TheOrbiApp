using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Orbi.Services;

namespace Orbi;

/// <summary>
/// Primeiro contato: uma janela montada só em código (sem XAML, sem tema, sem
/// rede) que prova que o Orbi está vivo e ensina o atalho.
///
/// Por que existe: o app é iniciado pelo Windows e, se a barra falhar em
/// silêncio, a pessoa vê "nada aparece". Esta janela aparece no primeiro
/// arranque e sempre que o modo de emergência é acionado.
/// </summary>
public static class ProntoWindow
{
    public static void Show(bool primeiraVez)
    {
        var fundo = new SolidColorBrush(Color.FromRgb(0xF2, 0xED, 0xE3));
        var tinta = new SolidColorBrush(Color.FromRgb(0x2B, 0x24, 0x1D));
        var suave = new SolidColorBrush(Color.FromRgb(0x7A, 0x6A, 0x5A));
        var destaque = new SolidColorBrush(Color.FromRgb(0xC7, 0x45, 0x2A));

        var janela = new Window
        {
            Title = primeiraVez ? "Orbi — bem-vindo" : "Orbi — modo de emergência",
            Width = 520,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = fundo,
            Topmost = true,
            ShowInTaskbar = true,
        };

        var painel = new StackPanel { Margin = new Thickness(28) };

        painel.Children.Add(new TextBlock
        {
            Text = primeiraVez ? "O Orbi está aberto." : "O Orbi abriu, mas a barra não.",
            FontSize = 26,
            FontWeight = FontWeights.SemiBold,
            Foreground = tinta,
            Margin = new Thickness(0, 0, 0, 8),
        });

        var atalho = string.IsNullOrEmpty(HotKey.Combination) ? "Alt+Shift+O" : HotKey.Combination;
        painel.Children.Add(new TextBlock
        {
            Text = primeiraVez
                ? $"Aperte {atalho} em qualquer lugar do Windows para a barra aparecer. " +
                  "Ela também fica na bandeja, perto do relógio."
                : $"Clique em \"Abrir a barra\" ou aperte {atalho}. O motivo da falha está no log abaixo.",
            FontSize = 14,
            Foreground = suave,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 20),
        });

        painel.Children.Add(new TextBlock
        {
            Text = "COISAS PARA TENTAR AGORA (funcionam sem internet e sem conta)",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = destaque,
            Margin = new Thickness(0, 0, 0, 8),
        });

        foreach (var exemplo in new[]
        {
            "abrir chrome            · abre qualquer programa instalado",
            "volume 30               · volume, brilho, energia, captura",
            "nota comprar leite      · notas rápidas",
            "lembrar 18:00 reunião   · aviso na hora marcada",
            "atalho gm → gmail.com   · atalho de texto",
            "status                  · CPU, memória, disco e bateria",
        })
        {
            painel.Children.Add(new TextBlock
            {
                Text = exemplo,
                FontFamily = new FontFamily("Consolas, Cascadia Mono"),
                FontSize = 12,
                Foreground = tinta,
                Margin = new Thickness(0, 0, 0, 4),
            });
        }

        var botoes = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 22, 0, 0),
        };

        botoes.Children.Add(Botao("Abrir a barra", true, () =>
        {
            try { App.Instance.ShowBar(); }
            catch (Exception erro) { Diagnostics.Report(erro, "Pronto → abrir barra", fatal: false); }
        }));

        botoes.Children.Add(Botao("Diagnóstico", false, () =>
        {
            try { App.Instance.ShowDiagnostics(); } catch { /* ignorar */ }
        }));

        botoes.Children.Add(Botao("Abrir a pasta dos logs", false, () =>
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", Store.Root) { UseShellExecute = true }); }
            catch { /* ignorar */ }
        }));

        painel.Children.Add(botoes);

        var log = ConsoleLog.Transcript;
        if (!string.IsNullOrWhiteSpace(log))
        {
            var caixa = new TextBox
            {
                Text = log,
                IsReadOnly = true,
                FontFamily = new FontFamily("Consolas, Cascadia Mono"),
                FontSize = 10,
                Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xD6, 0xC6)),
                MaxHeight = 150,
                Margin = new Thickness(0, 20, 0, 0),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            caixa.TextChanged += (_, _) => caixa.ScrollToEnd();
            painel.Children.Add(caixa);
        }

        janela.Content = new ScrollViewer
        {
            Content = painel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        janela.Show();
        janela.Activate();
    }

    private static Button Botao(string texto, bool principal, Action aoClicar)
    {
        var botao = new Button
        {
            Content = texto,
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(0, 0, 8, 0),
            FontSize = 13,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        if (principal)
        {
            botao.Background = new SolidColorBrush(Color.FromRgb(0xC7, 0x45, 0x2A));
            botao.Foreground = Brushes.White;
            botao.BorderThickness = new Thickness(0);
        }
        botao.Click += (_, _) => aoClicar();
        return botao;
    }
}
