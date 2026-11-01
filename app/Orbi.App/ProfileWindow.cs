using System.Windows;
using System.Windows.Controls;
using Orbi.Services;

namespace Orbi;

/// <summary>
/// Perfil: quem você é, a conta e o que fica guardado. A IA tem o seu próprio
/// painel; aqui é só o que é seu.
/// </summary>
public sealed class ProfileWindow : Window
{
    private readonly TextBlock _nomeAtual = Panel.Body("", forte: true);
    private readonly TextBox _nome = Panel.Field();
    private readonly TextBlock _conta = Panel.Body("");
    private readonly TextBlock _estado = Panel.Body("");
    private readonly StackPanel _acoes = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _memoria = Panel.Body("");
    private readonly TextBlock _status = Panel.Body("");

    public ProfileWindow()
    {
        Width = 560;
        Height = 580;
        MinWidth = 440;
        MinHeight = 420;
        Panel.Install(this, "Orbi — Perfil");

        var raiz = new Grid();
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        raiz.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        raiz.Children.Add(Panel.Header("Perfil", "conta e memória", () => App.Instance.ReturnBar(), this));

        var corpo = new StackPanel { Margin = new Thickness(20, 0, 20, 20) };

        corpo.Children.Add(Panel.Card(new StackPanel
        {
            Children = { Panel.Label("Seu nome"), _nomeAtual, Panel.Divider(), _nome,
                         Panel.Primary("Salvar nome", SalvarNome) },
        }));

        corpo.Children.Add(Panel.Card(new StackPanel
        {
            Children = { Panel.Label("Conta"), _conta, _estado, Panel.Divider(), _acoes },
        }));

        corpo.Children.Add(Panel.Card(new StackPanel
        {
            Children =
            {
                Panel.Label("Memória"),
                _memoria,
                Panel.Divider(),
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
        _nomeAtual.Text = string.IsNullOrWhiteSpace(Store.Current.UserName) ? "Sem nome" : Store.Current.UserName;
        _nome.Text = Store.Current.UserName;

        var dentro = Firebase.SignedIn;
        _conta.Text = dentro ? Firebase.Email ?? "conta" : "Nenhuma conta";
        _estado.Text = dentro
            ? $"Memória e preferências acompanham você. {Store.LastSyncText}."
            : "Entre com a conta para levar a memória para outros PCs.";

        var registros = Memory.All.Count;
        _memoria.Text = registros == 0
            ? "Nada guardado ainda."
            : $"{registros} lembranças.";

        _acoes.Children.Clear();
        if (dentro)
        {
            _acoes.Children.Add(Panel.Primary("Sincronizar", Sincronizar));
            _acoes.Children.Add(Panel.Ghost("Sair", Sair));
        }
        else
        {
            _acoes.Children.Add(Panel.Primary("Entrar", () => { App.Instance.ShowLogin(); Refresh(); }));
            _acoes.Children.Add(Panel.Ghost("Criar conta", () => Browser.Open("https://orbi-app.web.app/conta")));
        }
    }

    private void SalvarNome()
    {
        var nome = _nome.Text.Trim();
        if (string.IsNullOrWhiteSpace(nome)) return;
        Store.Current.UserName = nome;
        Store.Save();
        _status.Text = "Nome salvo.";
        _ = Sync.PushFieldsAsync(new System.Text.Json.Nodes.JsonObject { ["name"] = nome });
        Refresh();
    }

    private async void Sincronizar()
    {
        _status.Text = "Sincronizando…";
        await Sync.PullAsync(true);
        await Sync.PushAsync();
        _status.Text = Sync.LastStatus;
        Refresh();
    }

    private void Sair()
    {
        Firebase.SignOut();
        Sync.Now();
        _status.Text = "Você saiu da conta.";
        Refresh();
    }

    private void Esquecer()
    {
        Memory.Forget();
        _status.Text = "Memória apagada.";
        Refresh();
    }
}
