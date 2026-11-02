using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Orbi.Services;

namespace Orbi;

/// <summary>
/// Conta: entrar, criar, Google, nome, foto, senha, e-mail, aparelhos e memória —
/// tudo aqui dentro, numa página só. Quando não há conta, a página é o próprio
/// formulário de entrada; quando há, é o retrato do que está guardado em você.
/// </summary>
public sealed class ProfileWindow : Window
{
    /* topo */
    private readonly Border _avatar = new();
    private readonly TextBlock _avatarLetra = new();
    private readonly TextBlock _titulo = Panel.Body("", forte: true);
    private readonly TextBlock _subtitulo = Panel.Body("");
    private readonly TextBlock _selo = new();

    /* fora da conta */
    private readonly StackPanel _entrar = new();
    private readonly TextBox _email = Panel.Field();
    private readonly PasswordBox _senha = new();
    private readonly TextBox _senhaVisivel = Panel.Field();
    private readonly TextBlock _aviso = Panel.Body("");
    private readonly Button _principal = new();
    private readonly Button _trocarModo = new();

    /* dentro da conta */
    private readonly StackPanel _dentro = new();
    private readonly TextBlock _nomeAtual = Panel.Body("", forte: true);
    private readonly TextBox _nome = Panel.Field();
    private readonly TextBlock _fotoEstado = Panel.Body("");
    private readonly TextBox _fotoUrl = Panel.Field();
    private readonly TextBlock _emailEstado = Panel.Body("");
    private readonly PasswordBox _senhaAtual = new();
    private readonly PasswordBox _senhaNova = new();
    private readonly StackPanel _aparelhos = new();
    private readonly TextBlock _memoria = Panel.Body("");
    private readonly TextBlock _sincronizacao = Panel.Body("");

    private bool _criando;
    private bool _ocupado;

    public ProfileWindow()
    {
        Width = 620;
        Height = 720;
        MinWidth = 520;
        MinHeight = 560;
        Panel.Install(this, "Orbi — Conta");

        var raiz = new Grid();
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        raiz.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        raiz.Children.Add(Panel.Header("Conta", "quem você é no Orbi", () => App.Instance.ReturnBar(), this));

        var corpo = new StackPanel { Margin = new Thickness(20, 0, 20, 20) };
        corpo.Children.Add(MontarTopo());
        corpo.Children.Add(MontarEntrar());
        corpo.Children.Add(MontarDentro());

        var rolagem = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = corpo };
        Grid.SetRow(rolagem, 1);
        raiz.Children.Add(rolagem);
        Content = raiz;

        Refresh();
    }

    /* --------------------------- para o autoteste --------------------------- */
    public bool FormularioVisivel => _entrar.Visibility == Visibility.Visible;
    public bool PainelDaContaVisivel => _painelDentro?.Visibility == Visibility.Visible;
    public bool GoogleDisponivel => GoogleLogin.Disponivel;

    /// <summary>Alterna a senha visível/oculta e confere se o texto acompanhou.</summary>
    public bool SenhaAcompanha()
    {
        _senha.Password = "orbi-teste";
        AlternarSenhaVisivel();
        var mostrou = _senhaVisivel.Visibility == Visibility.Visible && _senhaVisivel.Text == "orbi-teste";
        _senhaVisivel.Text = "orbi-trocado";
        AlternarSenhaVisivel();
        var voltou = _senha.Visibility == Visibility.Visible && _senha.Password == "orbi-trocado";
        _senha.Password = "";
        return mostrou && voltou;
    }

    /* ------------------------------ desenho ------------------------------ */

    private Border MontarTopo()
    {
        _avatarLetra.FontSize = 22;
        _avatarLetra.FontWeight = FontWeights.SemiBold;
        _avatarLetra.Foreground = Brushes.White;
        _avatarLetra.HorizontalAlignment = HorizontalAlignment.Center;
        _avatarLetra.VerticalAlignment = VerticalAlignment.Center;
        _avatar.Width = 56;
        _avatar.Height = 56;
        _avatar.CornerRadius = new CornerRadius(28);
        _avatar.Background = Panel.Signal;
        _avatar.Child = _avatarLetra;

        _selo.FontFamily = new FontFamily("Cascadia Mono, Consolas");
        _selo.FontSize = 10;
        _selo.Foreground = Panel.Signal;
        _selo.VerticalAlignment = VerticalAlignment.Center;

        var textos = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        textos.Children.Add(_titulo);
        textos.Children.Add(_subtitulo);
        textos.Children.Add(_selo);

        var linha = new StackPanel { Orientation = Orientation.Horizontal };
        linha.Children.Add(_avatar);
        linha.Children.Add(textos);

        var cartao = Panel.Card(linha);
        cartao.Margin = new Thickness(0, 0, 0, 12);
        return cartao;
    }

    private Border MontarEntrar()
    {
        _email.FontSize = 14;
        _senha.Style = (Style)FindResource("PasswordField");
        _senha.FontSize = 14;
        _senha.PasswordChanged += (_, _) =>
        {
            if (_senhaVisivel.Visibility == Visibility.Collapsed) _aviso.Text = "";
        };
        _senhaVisivel.FontSize = 14;
        _senhaVisivel.Visibility = Visibility.Collapsed;
        _senhaVisivel.TextChanged += (_, _) => { _senha.Password = _senhaVisivel.Text; };

        _aviso.Foreground = Panel.Signal;
        _aviso.TextWrapping = TextWrapping.Wrap;
        _aviso.Visibility = Visibility.Collapsed;
        _aviso.Margin = new Thickness(0, 10, 0, 0);

        var verSenha = Panel.Ghost("Mostrar", () => AlternarSenhaVisivel());
        verSenha.HorizontalAlignment = HorizontalAlignment.Right;

        var linhaSenha = new Grid();
        linhaSenha.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        linhaSenha.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        linhaSenha.Children.Add(_senha);
        Grid.SetColumn(verSenha, 1);
        verSenha.Margin = new Thickness(8, 0, 0, 0);
        linhaSenha.Children.Add(verSenha);
        linhaSenha.Children.Add(_senhaVisivel);

        _principal.IsEnabled = true;
        _principal.Style = (Style)FindResource("PrimaryButton");
        _principal.Padding = new Thickness(16, 11, 16, 11);
        _principal.Margin = new Thickness(0, 16, 0, 0);
        _principal.Click += (_, _) => _ = EnviarAsync();

        _trocarModo.Content = "Criar uma conta";
        _trocarModo.Style = (Style)FindResource("SecondaryButton");
        _trocarModo.Padding = new Thickness(16, 10, 16, 10);
        _trocarModo.Margin = new Thickness(0, 8, 0, 0);
        _trocarModo.Click += (_, _) => { _criando = !_criando; Aviso(""); Refresh(); };

        var google = BotaoGoogle();
        var esqueci = Panel.Ghost("Esqueci a senha", () => _ = EsquecerSenhaAsync());
        esqueci.HorizontalAlignment = HorizontalAlignment.Left;

        _entrar.Children.Add(Panel.Label("E-mail"));
        _entrar.Children.Add(_email);
        _entrar.Children.Add(Panel.Label("Senha"));
        _entrar.Children.Add(linhaSenha);
        _entrar.Children.Add(_aviso);
        _entrar.Children.Add(_principal);
        _entrar.Children.Add(_trocarModo);
        _entrar.Children.Add(Divisor());
        _entrar.Children.Add(google);
        _entrar.Children.Add(esqueci);
        _googleNota = Nota("Para liberar o Google, cole o identificador do cliente em " +
                           "Ajustes ▸ Entrar com o Google.");
        _googleNota.Visibility = Visibility.Collapsed;
        _entrar.Children.Add(_googleNota);
        _entrar.Children.Add(Nota("A conta guarda a sua memória e os seus ajustes. " +
                                  "Sem ela o Orbi continua funcionando, só neste PC."));

        var cartao = Panel.Card(_entrar);
        cartao.Margin = new Thickness(0, 0, 0, 12);
        return cartao;
    }

    /// <summary>
    /// Botão do Google com a marca dele (as quatro cores). Ele só aparece quando
    /// o login pelo app está realmente disponível — botão que erra é pior que ausência.
    /// </summary>
    private Button BotaoGoogle()
    {
        var icone = new TextBlock
        {
            Text = "G",
            FontFamily = new FontFamily("Segoe UI, Arial"),
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x42, 0x85, 0xF4)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        };
        var rotulo = new TextBlock
        {
            Text = "Continuar com o Google",
            FontSize = 13.5,
            Foreground = Panel.Ink,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var conteudo = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        conteudo.Children.Add(icone);
        conteudo.Children.Add(rotulo);

        var botao = new Button
        {
            Content = conteudo,
            Background = Brushes.Transparent,
            BorderBrush = Panel.Rule,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 10, 16, 10),
            Margin = new Thickness(0, 4, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            IsEnabled = GoogleLogin.Disponivel,
            ToolTip = GoogleLogin.Disponivel
                ? "Entra com a sua conta do Google, aqui dentro"
                : "Falta configurar o login do Google neste build",
        };
        botao.Click += (_, _) => _ = EntrarComGoogleAsync();
        if (!GoogleLogin.Disponivel) rotulo.Text = "Google indisponível neste PC";
        _google = botao;
        _googleRotulo = rotulo;
        return botao;
    }

    private Button? _google;
    private TextBlock? _googleRotulo;
    private TextBlock? _googleNota;

    private Border MontarDentro()
    {
        /* perfil */
        _fotoEstado.Foreground = Panel.Warm;
        var salvarPerfil = Panel.Primary("Salvar perfil", () => _ = SalvarPerfilAsync());

        _dentro.Children.Add(Panel.Label("Seu perfil"));
        _dentro.Children.Add(_nomeAtual);
        _dentro.Children.Add(Divisor());
        _dentro.Children.Add(Panel.Label("Nome"));
        _dentro.Children.Add(_nome);
        _dentro.Children.Add(Panel.Label("Foto (endereço da imagem)"));
        _dentro.Children.Add(_fotoUrl);
        _dentro.Children.Add(_fotoEstado);
        _dentro.Children.Add(salvarPerfil);
        var perfil = Panel.Card(_dentro);
        perfil.Margin = new Thickness(0, 0, 0, 12);

        /* e-mail e senha */
        var seguranca = new StackPanel();
        seguranca.Children.Add(Panel.Label("E-mail"));
        seguranca.Children.Add(_emailEstado);
        var confirmar = Panel.Ghost("Confirmar e-mail", () => _ = ConfirmarEmailAsync());
        confirmar.HorizontalAlignment = HorizontalAlignment.Left;
        seguranca.Children.Add(confirmar);
        seguranca.Children.Add(Divisor());
        seguranca.Children.Add(Panel.Label("Trocar a senha"));
        _senhaAtual.Style = (Style)FindResource("PasswordField");
        _senhaNova.Style = (Style)FindResource("PasswordField");
        _senhaAtual.Margin = new Thickness(0, 0, 0, 8);
        seguranca.Children.Add(_senhaAtual);
        seguranca.Children.Add(_senhaNova);
        var trocar = Panel.Primary("Trocar senha", () => _ = TrocarSenhaAsync());
        trocar.Margin = new Thickness(0, 12, 0, 0);
        seguranca.Children.Add(trocar);
        var cartaoSeguranca = Panel.Card(seguranca);
        cartaoSeguranca.Margin = new Thickness(0, 0, 0, 12);

        /* memória */
        var memoria = new StackPanel();
        memoria.Children.Add(Panel.Label("Memória"));
        memoria.Children.Add(_memoria);
        memoria.Children.Add(_sincronizacao);
        memoria.Children.Add(Divisor());
        var linhaBotoes = new StackPanel { Orientation = Orientation.Horizontal };
        var sincronizar = Panel.Primary("Sincronizar agora", () => _ = SincronizarAsync());
        var esquecer = Panel.Ghost("Esquecer tudo", Esquecer);
        esquecer.Margin = new Thickness(8, 0, 0, 0);
        linhaBotoes.Children.Add(sincronizar);
        linhaBotoes.Children.Add(esquecer);
        memoria.Children.Add(linhaBotoes);
        var cartaoMemoria = Panel.Card(memoria);
        cartaoMemoria.Margin = new Thickness(0, 0, 0, 12);

        /* aparelhos */
        var aparelhos = new StackPanel();
        aparelhos.Children.Add(Panel.Label("Aparelhos nesta conta"));
        aparelhos.Children.Add(_aparelhos);
        var cartaoAparelhos = Panel.Card(aparelhos);
        cartaoAparelhos.Margin = new Thickness(0, 0, 0, 12);

        /* sair e apagar */
        var rodape = new StackPanel { Orientation = Orientation.Horizontal };
        var sair = Panel.Ghost("Sair da conta", Sair);
        var apagar = Panel.Ghost("Apagar a conta", () => _ = ApagarContaAsync());
        apagar.Foreground = Panel.Signal;
        apagar.Margin = new Thickness(10, 0, 0, 0);
        rodape.Children.Add(sair);
        rodape.Children.Add(apagar);

        var tudo = new StackPanel();
        tudo.Children.Add(perfil);
        tudo.Children.Add(cartaoSeguranca);
        tudo.Children.Add(cartaoMemoria);
        tudo.Children.Add(cartaoAparelhos);
        tudo.Children.Add(rodape);
        _painelDentro = tudo;
        return new Border { Child = tudo, Background = Brushes.Transparent };
    }

    private StackPanel? _painelDentro;

    private static Border Divisor() => Panel.Divider();

    private static TextBlock Nota(string texto)
    {
        var nota = Panel.Body(texto);
        nota.Foreground = Panel.Warm;
        nota.FontSize = 11.5;
        nota.TextWrapping = TextWrapping.Wrap;
        nota.Margin = new Thickness(0, 10, 0, 0);
        return nota;
    }

    /* ------------------------------ estado ------------------------------ */

    public void Refresh()
    {
        var dentro = Firebase.SignedIn;
        var email = Firebase.Session?.Email ?? Store.Current.AccountEmail ?? "";

        _email.Text = string.IsNullOrWhiteSpace(_email.Text) ? email : _email.Text;
        _entrar.Visibility = dentro ? Visibility.Collapsed : Visibility.Visible;
        if (_painelDentro is not null)
        {
            _painelDentro.Visibility = dentro ? Visibility.Visible : Visibility.Collapsed;
        }

        // topo
        _avatarLetra.Text = Iniciais(dentro);
        var foto = Firebase.Session?.PhotoUrl ?? "";
        if (dentro && !string.IsNullOrWhiteSpace(foto)) AplicarFoto(foto);
        else _avatar.Background = Panel.Signal;

        _titulo.Text = dentro
            ? (Store.Current.UserName is { Length: > 0 } nome ? nome : Firebase.Session?.DisplayName ?? "Sua conta")
            : "Sem conta vinculada";
        _subtitulo.Text = dentro ? email : "Entre aqui dentro para a sua memória acompanhar você.";
        _selo.Text = dentro
            ? (Firebase.Session?.Provider == "google.com" ? "conta google" : "conta por e-mail")
            : "";

        if (!dentro)
        {
            _principal.Content = _criando ? "Criar conta" : "Entrar";
            _trocarModo.Content = _criando ? "Já tenho conta" : "Criar uma conta";
            _senha.Visibility = _criando || _senha.Visibility == Visibility.Visible ? Visibility.Visible : Visibility.Visible;
            if (_googleRotulo is not null)
            {
                _googleRotulo.Text = GoogleLogin.Disponivel ? "Continuar com o Google" : "Google indisponível neste PC";
            }
            if (_googleNota is not null)
            {
                _googleNota.Visibility = GoogleLogin.Disponivel ? Visibility.Collapsed : Visibility.Visible;
            }
            return;
        }

        _nomeAtual.Text = "Conectado como " + email;
        _nome.Text = string.IsNullOrWhiteSpace(Store.Current.UserName)
            ? Firebase.Session?.DisplayName ?? ""
            : Store.Current.UserName;
        _fotoUrl.Text = Firebase.Session?.PhotoUrl ?? "";
        _fotoEstado.Text = string.IsNullOrWhiteSpace(_fotoUrl.Text) ? "Sem foto — usamos as suas iniciais." : "";
        _emailEstado.Text = Firebase.Session?.EmailVerified == true
            ? email + " — confirmado"
            : email + " — ainda não confirmado";

        var registros = Memory.All.Count;
        _memoria.Text = registros == 0 ? "Nada guardado ainda." : $"{registros} lembranças guardadas.";
        _sincronizacao.Text = Sync.LastStatus;

        _ = CarregarAparelhosAsync();
    }

    private string Iniciais(bool dentro)
    {
        if (!dentro) return "?";
        var nome = Store.Current.UserName is { Length: > 0 } escolhido
            ? escolhido
            : Firebase.Session?.DisplayName ?? Firebase.Session?.Email ?? "";
        var partes = nome.Split(new[] { ' ', '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 0) return "O";
        var primeira = partes[0][..1];
        var segunda = partes.Length > 1 ? partes[^1][..1] : "";
        return (primeira + segunda).ToUpperInvariant();
    }

    private void AplicarFoto(string endereco)
    {
        try
        {
            var imagem = new BitmapImage();
            imagem.BeginInit();
            imagem.UriSource = new Uri(endereco, UriKind.Absolute);
            imagem.CacheOption = BitmapCacheOption.OnLoad;
            imagem.EndInit();
            _avatar.Background = new ImageBrush(imagem) { Stretch = Stretch.UniformToFill };
            _avatar.CornerRadius = new CornerRadius(28);
            _avatarLetra.Text = "";
        }
        catch
        {
            // endereço inválido: as iniciais continuam
            _avatar.Background = Panel.Signal;
        }
    }

    private async Task CarregarAparelhosAsync()
    {
        var lista = await Firebase.GetDevicesAsync();
        _aparelhos.Children.Clear();
        if (lista.Count == 0)
        {
            _aparelhos.Children.Add(Nota("Nenhum aparelho registrado ainda."));
            return;
        }
        foreach (var aparelho in lista)
        {
            var detalhe = string.IsNullOrWhiteSpace(aparelho.UltimoAcesso)
                ? aparelho.Nome
                : $"{aparelho.Nome} · {aparelho.UltimoAcesso}";
            if (aparelho.Atual) detalhe += " · este PC";
            var id = aparelho.Id;
            _aparelhos.Children.Add(Panel.Row(detalhe, null, () => _ = RemoverAparelhoAsync(id)));
        }
    }

    private void AlternarSenhaVisivel()
    {
        var mostrar = _senhaVisivel.Visibility == Visibility.Collapsed;
        if (mostrar)
        {
            _senhaVisivel.Text = _senha.Password;
            _senhaVisivel.Visibility = Visibility.Visible;
            _senha.Visibility = Visibility.Collapsed;
            _senhaVisivel.Focus();
        }
        else
        {
            _senha.Password = _senhaVisivel.Text;
            _senhaVisivel.Visibility = Visibility.Collapsed;
            _senha.Visibility = Visibility.Visible;
        }
    }

    private void Aviso(string texto, bool erro = true)
    {
        _aviso.Text = texto;
        _aviso.Foreground = erro ? Panel.Signal : Panel.Warm;
        _aviso.Visibility = string.IsNullOrWhiteSpace(texto) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Ocupado(bool ocupado, string? rotulo = null)
    {
        _ocupado = ocupado;
        _principal.IsEnabled = !ocupado;
        _trocarModo.IsEnabled = !ocupado;
        _email.IsEnabled = !ocupado;
        _senha.IsEnabled = !ocupado;
        _senhaVisivel.IsEnabled = !ocupado;
        if (_google is not null) _google.IsEnabled = !ocupado && GoogleLogin.Disponivel;
        if (rotulo is not null) _principal.Content = rotulo;
    }

    /* ------------------------------ ações: entrar ------------------------------ */

    private async Task EnviarAsync()
    {
        if (_ocupado) return;
        var senha = _senhaVisivel.Visibility == Visibility.Visible ? _senhaVisivel.Text : _senha.Password;
        Aviso("");
        Ocupado(true, _criando ? "Criando…" : "Entrando…");
        try
        {
            var (ok, mensagem) = _criando
                ? await Firebase.SignUpAsync(_email.Text, senha)
                : await Firebase.SignInAsync(_email.Text, senha);

            if (!ok)
            {
                Aviso(mensagem);
                return;
            }

            await DepoisDeEntrarAsync();
        }
        finally
        {
            Ocupado(false, _criando ? "Criar conta" : "Entrar");
        }
    }

    private async Task EntrarComGoogleAsync()
    {
        if (_ocupado) return;
        Aviso("");
        Ocupado(true, "Abrindo o Google…");
        try
        {
            var (ok, mensagem) = await GoogleLogin.EntrarAsync(this);
            if (!ok)
            {
                Aviso(mensagem);
                return;
            }
            await DepoisDeEntrarAsync();
        }
        finally
        {
            Ocupado(false, _criando ? "Criar conta" : "Entrar");
        }
    }

    private async Task DepoisDeEntrarAsync()
    {
        _senha.Password = "";
        _senhaVisivel.Text = "";
        _criando = false;
        await Sync.PullAsync(false);
        await Sync.PushAsync();
        await Firebase.RegisterDeviceAsync();
        App.Instance.RefreshAccount();

        // O nome que veio do Google entra no app sem a pessoa digitar de novo.
        if (string.IsNullOrWhiteSpace(Store.Current.UserName) &&
            Firebase.Session?.DisplayName is { Length: > 0 } nome)
        {
            Store.Current.UserName = nome;
            Store.Save();
        }
        Refresh();
    }

    private async Task EsquecerSenhaAsync()
    {
        var email = _email.Text.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            Aviso("Digite o e-mail primeiro — o link vai para ele.");
            return;
        }
        Ocupado(true, "Enviando…");
        try
        {
            var (ok, mensagem) = await Firebase.SendResetAsync(email);
            Aviso(mensagem, !ok);
        }
        finally
        {
            Ocupado(false, _criando ? "Criar conta" : "Entrar");
        }
    }

    /* ------------------------------ ações: conta ------------------------------ */

    private async Task SalvarPerfilAsync()
    {
        var (ok, mensagem) = await Firebase.SaveProfileAsync(_nome.Text, _fotoUrl.Text);
        if (ok)
        {
            Store.Current.UserName = _nome.Text.Trim();
            Store.Save();
            App.Instance.RefreshAccount();
        }
        _fotoEstado.Text = mensagem;
        _fotoEstado.Foreground = ok ? Panel.Warm : Panel.Signal;
        Refresh();
    }

    private async Task ConfirmarEmailAsync()
    {
        var (ok, mensagem) = await Firebase.SendVerificationAsync();
        _emailEstado.Text = mensagem;
        _emailEstado.Foreground = ok ? Panel.Warm : Panel.Signal;
    }

    private async Task TrocarSenhaAsync()
    {
        if (Firebase.Session?.Provider == "google.com")
        {
            _emailEstado.Text = "Esta conta entra pelo Google — a senha é gerenciada lá.";
            _emailEstado.Foreground = Panel.Warm;
            return;
        }
        var atual = _senhaAtual.Password;
        var nova = _senhaNova.Password;
        if (string.IsNullOrWhiteSpace(atual) || string.IsNullOrWhiteSpace(nova))
        {
            _emailEstado.Text = "Preencha a senha atual e a nova.";
            _emailEstado.Foreground = Panel.Signal;
            return;
        }
        var (ok, mensagem) = await Firebase.ChangePasswordAsync(atual, nova);
        _emailEstado.Text = mensagem;
        _emailEstado.Foreground = ok ? Panel.Warm : Panel.Signal;
        if (ok)
        {
            _senhaAtual.Password = "";
            _senhaNova.Password = "";
        }
    }

    private async Task RemoverAparelhoAsync(string id)
    {
        var (ok, mensagem) = await Firebase.ForgetDeviceAsync(id);
        _sincronizacao.Text = mensagem;
        _sincronizacao.Foreground = ok ? Panel.Warm : Panel.Signal;
        await CarregarAparelhosAsync();
    }

    private async Task SincronizarAsync()
    {
        _sincronizacao.Text = "Sincronizando…";
        await Sync.PullAsync(true);
        await Sync.PushAsync();
        await Firebase.RefreshProfileAsync();
        _sincronizacao.Text = Sync.LastStatus;
        Refresh();
    }

    private void Esquecer()
    {
        Memory.Forget();
        _sincronizacao.Text = "Memória apagada.";
        Refresh();
    }

    private void Sair()
    {
        Firebase.SignOut();
        Sync.Now();
        _senha.Password = "";
        _senhaVisivel.Text = "";
        App.Instance.RefreshAccount();
        Refresh();
    }

    private async Task ApagarContaAsync()
    {
        var resposta = MessageBox.Show(this,
            "Apagar a conta de vez? A memória e os ajustes guardados nela vão junto. " +
            "O Orbi continua funcionando neste PC.",
            "Apagar a conta", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (resposta != MessageBoxResult.Yes) return;

        var (ok, mensagem) = await Firebase.DeleteAccountAsync();
        _emailEstado.Text = mensagem;
        _emailEstado.Foreground = ok ? Panel.Warm : Panel.Signal;
        App.Instance.RefreshAccount();
        Refresh();
    }
}
