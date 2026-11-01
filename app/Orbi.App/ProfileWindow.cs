using System.Windows;
using System.Windows.Controls;
using Orbi.Services;

namespace Orbi;

/// <summary>
/// Painel de perfil: o seu nome neste PC, a máquina e o que o Orbi lembra.
/// Nada de conta, nada de sincronização — e nada de IA aqui, que tem o seu
/// próprio painel.
/// </summary>
public sealed class ProfileWindow : Window
{
    private readonly TextBlock _nomeAtual = Panel.Body("", forte: true);
    private readonly TextBox _nome = Panel.Field();
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

        raiz.Children.Add(Panel.Header("Perfil", "este PC", () => App.Instance.ReturnBar(), this));

        var corpo = new StackPanel { Margin = new Thickness(20, 0, 20, 20) };

        corpo.Children.Add(Panel.Card(new StackPanel
        {
            Children =
            {
                Panel.Label("Como o Orbi te chama"),
                _nomeAtual,
                Panel.Divider(),
                Panel.Body(Environment.MachineName + " · " + Environment.UserName),
            },
        }));

        corpo.Children.Add(Panel.Card(new StackPanel
        {
            Children = { Panel.Label("Nome"), _nome, Panel.Primary("Salvar nome", SalvarNome) },
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

        var lembrancas = Memory.All.Count;
        _memoria.Text = lembrancas == 0 ? "Nada guardado ainda." : $"{lembrancas} registros neste PC.";
    }

    private void SalvarNome()
    {
        var nome = _nome.Text.Trim();
        if (string.IsNullOrWhiteSpace(nome)) return;
        Store.Current.UserName = nome;
        Store.Save();
        _status.Text = "Nome salvo.";
        Refresh();
    }

    private void Esquecer()
    {
        Memory.Forget();
        _status.Text = "Memória apagada.";
        Refresh();
    }
}
