using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Orbi.Services;

namespace Orbi;

/// <summary>
/// Painel de IA: a chave, o modelo e como o Orbi pensa. Separado dos ajustes
/// do Windows e do perfil — é a única coisa que se faz aqui.
/// </summary>
public sealed class IaWindow : Window
{
    private readonly PasswordBox _chave = new()
    {
        FontSize = 13,
        Padding = new Thickness(10, 8, 10, 8),
        Background = Brushes.White,
        BorderBrush = Panel.Rule,
        BorderThickness = new Thickness(1),
    };

    private readonly ComboBox _modelo = new() { Width = 220, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _estado = Panel.Body("");
    private readonly TextBlock _status = Panel.Body("");

    public IaWindow()
    {
        var janela = Panel.Shell("Orbi — IA", 560, 560);
        Title = janela.Title;
        Width = janela.Width;
        Height = janela.Height;
        MinWidth = janela.MinWidth;
        MinHeight = janela.MinHeight;
        Background = janela.Background;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        UseLayoutRounding = true;
        FontFamily = janela.FontFamily;

        var raiz = new Grid();
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        raiz.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        raiz.Children.Add(Panel.Header("IA", "chave e modelo", () => App.Instance.ReturnBar(), this));

        var corpo = new StackPanel { Margin = new Thickness(20, 0, 20, 20) };

        _estado.Text = Estado();
        corpo.Children.Add(Panel.Card(new StackPanel
        {
            Children =
            {
                _estado,
                Panel.Divider(),
                Panel.Label("Chave da API"),
                _chave,
                Panel.Ghost("Pegar uma chave gratuita", AbrirPaginaDaChave),
            },
        }));

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        botoes.Children.Add(Panel.Primary("Salvar", Salvar));
        botoes.Children.Add(Panel.Ghost("Testar", Testar));
        botoes.Children.Add(Panel.Ghost("Remover", Remover));
        corpo.Children.Add(botoes);
        corpo.Children.Add(_status);

        corpo.Children.Add(Panel.Divider());

        foreach (var modelo in Gemini.Models)
        {
            _modelo.Items.Add(new ComboBoxItem { Content = modelo });
        }
        corpo.Children.Add(Panel.Card(new StackPanel
        {
            Children =
            {
                Panel.Label("Modelo"),
                _modelo,
                new TextBlock
                {
                    Text = "O Orbi usa esta chave para entender o pedido, montar o plano e executar as ferramentas.",
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Panel.Warm,
                    Margin = new Thickness(0, 10, 0, 0),
                },
            },
        }));

        var rolagem = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = corpo,
        };
        Grid.SetRow(rolagem, 1);
        raiz.Children.Add(rolagem);
        Content = raiz;

        Load();
    }

    private static string Estado() =>
        Gemini.HasKey
            ? "Chave configurada. O Orbi pensa e age."
            : "Nenhuma chave ainda. Sem ela o Orbi só executa as ferramentas.";

    private void Load()
    {
        _chave.Password = Store.LoadApiKey() ?? "";
        var indice = Gemini.Models.ToList().IndexOf(Store.Current.GeminiModel);
        _modelo.SelectedIndex = indice >= 0 ? indice : 0;
        _estado.Text = Estado();
    }

    private void Salvar()
    {
        var chave = _chave.Password.Trim();
        if (string.IsNullOrWhiteSpace(chave))
        {
            _status.Text = "Cole a chave antes de salvar.";
            return;
        }
        Store.SaveApiKey(chave);
        if ((_modelo.SelectedItem as ComboBoxItem)?.Content is string modelo)
        {
            Store.Current.GeminiModel = modelo;
            Store.Save();
            _ = Sync.PushPrefsAsync();
        }
        _estado.Text = Estado();
        _status.Text = "Chave salva, cifrada neste PC.";
        App.Instance.RefreshAfterKey();
        App.Instance.ReturnBar();
    }

    private async void Testar()
    {
        _status.Text = "Testando…";
        var chave = string.IsNullOrWhiteSpace(_chave.Password) ? Store.LoadApiKey() : _chave.Password.Trim();
        var (ok, mensagem) = await Gemini.TestAsync(chave ?? "");
        _status.Text = mensagem;
        _estado.Text = Estado();
        if (ok) App.Instance.RefreshAfterKey();
    }

    private void Remover()
    {
        Store.SaveApiKey("");
        _chave.Password = "";
        _estado.Text = Estado();
        _status.Text = "Chave removida.";
        App.Instance.RefreshAfterKey();
    }

    private static void AbrirPaginaDaChave()
    {
        try { Process.Start(new ProcessStartInfo(Gemini.KeyPage) { UseShellExecute = true }); }
        catch { /* sem navegador agora */ }
    }
}
