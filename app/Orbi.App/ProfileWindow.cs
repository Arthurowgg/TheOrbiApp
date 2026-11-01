using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Orbi.Services;

namespace Orbi;

/// <summary>
/// Painel de perfil: quem você é, a conta do site, este PC e o que o Orbi
/// lembra. Nada de IA aqui — isso tem o seu próprio painel.
/// </summary>
public sealed class ProfileWindow : Window
{
    private readonly TextBlock _quem = Panel.Body("", forte: true);
    private readonly TextBlock _email = Panel.Body("");
    private readonly TextBlock _estado = Panel.Body("");
    private readonly StackPanel _acoes = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
    private readonly TextBox _nome = Panel.Field();
    private readonly TextBlock _memoria = Panel.Body("");
    private readonly TextBlock _status = Panel.Body("");

    public ProfileWindow()
    {
        var janela = Panel.Shell("Orbi — Perfil", 560, 580);
        Title = janela.Title;
        Width = janela.Width;
        Height = janela.Height;
        MinWidth = 440;
        MinHeight = 420;
        Background = janela.Background;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        UseLayoutRounding = true;
        FontFamily = janela.FontFamily;

        var raiz = new Grid();
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        raiz.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        raiz.Children.Add(Panel.Header("Perfil", "conta e este PC", () => App.Instance.ReturnBar(), this));

        var corpo = new StackPanel { Margin = new Thickness(20, 0, 20, 20) };

        corpo.Children.Add(Panel.Card(new StackPanel
        {
            Children = { _quem, _email, _estado, _acoes },
        }));

        var salvar = Panel.Primary("Salvar nome", SalvarNome);
        corpo.Children.Add(Panel.Card(new StackPanel
        {
            Children = { Panel.Label("Nome"), _nome, salvar },
        }));

        corpo.Children.Add(Panel.Card(new StackPanel
        {
            Children =
            {
                Panel.Label("Este PC"),
                Panel.Body(Environment.MachineName + " · " + Environment.UserName),
                Panel.Ghost("Trocar de PC", () => App.Instance.ShowSettings()),
            },
        }));

        corpo.Children.Add(Panel.Card(new StackPanel
        {
            Children =
            {
                Panel.Label("Memória"),
                _memoria,
                Panel.Ghost("Esquecer tudo", Esquecer),
            },
        }));

        corpo.Children.Add(_status);

        var rolagem = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = corpo,
        };
        Grid.SetRow(rolagem, 1);
        raiz.Children.Add(rolagem);
        Content = raiz;

        Refresh();
    }

    public void Refresh()
    {
        var nome = string.IsNullOrWhiteSpace(Store.Current.UserName) ? "Sem nome" : Store.Current.UserName;
        _quem.Text = nome;
        _email.Text = Firebase.Email ?? "";
        _email.Visibility = string.IsNullOrWhiteSpace(Firebase.Email) ? Visibility.Collapsed : Visibility.Visible;
        _estado.Text = Firebase.SignedIn
            ? "Conta conectada. O que você cria no site aparece aqui."
            : "Sem conta. O Orbi funciona assim mesmo — a conta é opcional.";
        _nome.Text = Store.Current.UserName;

        var lembrancas = Memory.All.Count;
        _memoria.Text = lembrancas == 0
            ? "Nada guardado ainda."
            : $"{lembrancas} registros neste PC.";

        _acoes.Children.Clear();
        if (Firebase.SignedIn)
        {
            _acoes.Children.Add(Panel.Primary("Sincronizar", Sincronizar));
            _acoes.Children.Add(Panel.Ghost("Sair da conta", Sair));
            _acoes.Children.Add(Panel.Ghost("Ver no site", () => Abrir("https://orbi-app.web.app/conta")));
        }
        else
        {
            _acoes.Children.Add(Panel.Primary("Entrar na conta", () =>
            {
                App.Instance.ShowLogin();
                Refresh();
            }));
            _acoes.Children.Add(Panel.Ghost("Criar conta", () => Abrir("https://orbi-app.web.app/criar")));
        }
    }

    private void SalvarNome(object? sender = null, RoutedEventArgs? e = null)
    {
        var nome = _nome.Text.Trim();
        if (string.IsNullOrWhiteSpace(nome)) return;
        Store.Current.UserName = nome;
        Store.Save();
        _status.Text = "Nome salvo.";
        Refresh();
    }

    private void Sincronizar(object? sender = null, RoutedEventArgs? e = null)
    {
        Sync.Now();
        _status.Text = "Sincronizando…";
        Refresh();
    }

    private void Sair(object? sender = null, RoutedEventArgs? e = null)
    {
        Firebase.SignOut();
        _status.Text = "Você saiu da conta.";
        Refresh();
    }

    private void Esquecer(object? sender = null, RoutedEventArgs? e = null)
    {
        Memory.Forget();
        _status.Text = "Memória apagada.";
        Refresh();
    }

    private static void Abrir(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* sem navegador agora */ }
    }
}
