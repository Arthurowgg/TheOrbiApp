using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace Orbi.Services;

/// <summary>
/// Entrar com o Google sem sair do Orbi.
///
/// O Google não deixa uma senha ser digitada dentro de um app — a janela dele
/// é obrigatória. Então o Orbi abre essa janela embutida (WebView2), espera o
/// retorno em 127.0.0.1 e troca o código por uma sessão da conta. Nada de
/// navegador externo, nada de página do site no meio.
///
/// Fluxo: authorization code + PKCE
///   1. o app abre accounts.google.com em uma janela própria;
///   2. o Google devolve um código para http://127.0.0.1:PORT/path;
///   3. o app troca o código por tokens em oauth2.googleapis.com;
///   4. o id_token vira uma sessão do Orbi (mesma conta de antes).
/// </summary>
public static class GoogleLogin
{
    /// <summary>
    /// Cliente OAuth do tipo "Web" do projeto. Fica aqui porque é público — o que
    /// protege o fluxo é o PKCE e o retorno só em 127.0.0.1.
    /// Pode ser trocado por variável de ambiente ORBI_GOOGLE_CLIENT_ID no build.
    /// </summary>
    public static string ClientId
    {
        get => !string.IsNullOrWhiteSpace(Store.Current.GoogleClientId)
            ? Store.Current.GoogleClientId.Trim()
            : Environment.GetEnvironmentVariable("ORBI_GOOGLE_CLIENT_ID") ?? "";
        set
        {
            Store.Current.GoogleClientId = value?.Trim() ?? "";
            Store.Save();
        }
    }

    /// <summary>O endereço parece um cliente OAuth do Google?</summary>
    public static bool IdValido(string texto) =>
        texto.Trim().EndsWith(".apps.googleusercontent.com", StringComparison.OrdinalIgnoreCase);

    /// <summary>Dá para usar o botão do Google? (cliente configurado + WebView2 presente)</summary>
    public static bool Disponivel => !string.IsNullOrWhiteSpace(ClientId) && WebView2Ok;

    private static bool? _webView2;

    private static bool WebView2Ok
    {
        get
        {
            if (_webView2 is { } conhecido) return conhecido;
            // A runtime do Edge WebView2 deixa uma chave por versão instalada.
            var chaves = new[]
            {
                @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
                @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
            };
            var achou = false;
            foreach (var chave in chaves)
            {
                using var registro = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(chave);
                if (registro?.GetValue("pv") is string versao && versao.Length > 3) { achou = true; break; }
            }
            _webView2 = achou;
            return achou;
        }
    }

    /// <summary>Endereços que o Google tem de ter autorizado no cliente OAuth.</summary>
    public const string RedirectModelo = "http://127.0.0.1:53682/orbi-google";

    /// <summary>
    /// Abre a janela do Google e entra. Bloqueia a thread de quem chamou até
    /// fechar — chame de um handler assíncrono.
    /// </summary>
    public static async Task<(bool Ok, string Message)> EntrarAsync(Window? dono = null)
    {
        if (string.IsNullOrWhiteSpace(ClientId))
        {
            return (false,
                "O login com o Google ainda não está configurado neste build. Use o e-mail e a senha.");
        }
        if (!WebView2Ok)
        {
            return (false,
                "Este PC não tem o componente de navegador do Windows (WebView2). " +
                "Instale o “WebView2 Runtime” da Microsoft e tente de novo, ou use o e-mail e a senha.");
        }

        var estado = Aleatorio(24);
        var verificador = Aleatorio(48);
        var desafio = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verificador)));

        using var servidor = new HttpListener();
        // Porta fixa: o redirect precisa estar autorizado no Google. Se estiver
        // ocupada, o login cai para o caminho de código único (porta dinâmica).
        var portas = new[] { 53682, 53683, 53684 };
        HttpListener? escutando = null;
        var prefixo = "";
        foreach (var porta in portas)
        {
            try
            {
                var tentativa = new HttpListener();
                var url = $"http://127.0.0.1:{porta}/orbi-google/";
                tentativa.Prefixes.Add(url);
                tentativa.Start();
                escutando = tentativa;
                prefixo = url;
                break;
            }
            catch
            {
                // porta ocupada: tenta a próxima
            }
        }
        if (escutando is null)
        {
            return (false, "Não consegui abrir a porta local do login. Feche outros logins abertos e tente de novo.");
        }

        var saida = new TaskCompletionSource<(string? Codigo, string? Erro)>();
        _ = Task.Run(async () =>
        {
            try
            {
                var contexto = await escutando.GetContextAsync();
                var consulta = contexto.Request.QueryString;
                var codigo = consulta["code"];
                var erro = consulta["error"];
                var veio = consulta["state"];

                var html = codigo is not null && veio == estado
                    ? Pagina("Pronto", "Pode fechar esta janela — o Orbi continua daqui.")
                    : Pagina("Não deu", "O Google não liberou a entrada. Volte ao Orbi e tente de novo.");
                var bytes = Encoding.UTF8.GetBytes(html);
                contexto.Response.ContentType = "text/html; charset=utf-8";
                contexto.Response.ContentLength64 = bytes.Length;
                await contexto.Response.OutputStream.WriteAsync(bytes);
                contexto.Response.Close();

                if (veio != estado) saida.TrySetResult((null, "A resposta do Google não confere com este pedido."));
                else saida.TrySetResult((codigo, erro));
            }
            catch (Exception error)
            {
                saida.TrySetResult((null, error.Message));
            }
        });

        var endereco = "https://accounts.google.com/o/oauth2/v2/auth" +
                       "?client_id=" + Uri.EscapeDataString(ClientId) +
                       "&redirect_uri=" + Uri.EscapeDataString(prefixo) +
                       "&response_type=code" +
                       "&scope=" + Uri.EscapeDataString("openid email profile") +
                       "&code_challenge=" + desafio +
                       "&code_challenge_method=S256" +
                       "&state=" + estado +
                       "&prompt=select_account";

        var janela = new GoogleWindow(endereco, prefixo + "*", dono);
        janela.ShowDialog();

        // O código pode ter chegado antes da janela fechar; espera um instante curto.
        var pronto = await Task.WhenAny(saida.Task, Task.Delay(TimeSpan.FromSeconds(6)));
        if (pronto != saida.Task)
        {
            try { escutando.Stop(); } catch { }
            return (false, "O login foi fechado antes de terminar.");
        }

        var (code, falha) = await saida.Task;
        try { escutando.Stop(); } catch { }

        if (code is null)
        {
            return (false, string.IsNullOrWhiteSpace(falha) ? "O login com o Google foi cancelado." : falha!);
        }

        return await TrocarCodigoAsync(code, prefixo, verificador);
    }

    /// <summary>Troca o código pelos tokens e monta a sessão do Orbi.</summary>
    private static async Task<(bool, string)> TrocarCodigoAsync(string codigo, string redirect, string verificador)
    {
        try
        {
            using var cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
            var corpo = new Dictionary<string, string>
            {
                ["code"] = codigo,
                ["client_id"] = ClientId,
                ["redirect_uri"] = redirect,
                ["grant_type"] = "authorization_code",
                ["code_verifier"] = verificador,
            };
            using var resposta = await cliente.PostAsync(
                "https://oauth2.googleapis.com/token", new FormUrlEncodedContent(corpo));
            var json = await resposta.Content.ReadAsStringAsync();
            if (!resposta.IsSuccessStatusCode)
            {
                Log.Add("google login falhou: " + (int)resposta.StatusCode);
                return (false, "O Google recusou a entrada. Confira se este app está autorizado no projeto.");
            }

            using var documento = JsonDocument.Parse(json);
            var raiz = documento.RootElement;
            var idToken = raiz.TryGetProperty("id_token", out var id) ? id.GetString() ?? "" : "";
            var refresh = raiz.TryGetProperty("refresh_token", out var r) ? r.GetString() ?? "" : "";
            var expira = raiz.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var segundos) ? segundos : 3600;
            if (string.IsNullOrWhiteSpace(idToken)) return (false, "O Google não devolveu a identidade. Tente de novo.");

            var (uid, email, nome, foto) = LerIdentidade(idToken);
            if (string.IsNullOrWhiteSpace(email)) return (false, "O Google não informou o e-mail da conta.");

            Firebase.AdoptSession(new FirebaseSession
            {
                IdToken = idToken,
                RefreshToken = refresh,
                Uid = uid,
                Email = email,
                DisplayName = nome,
                PhotoUrl = foto,
                Provider = "google.com",
                EmailVerified = true,
                ExpiresAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expira,
            });
            return (true, "Entrou com o Google.");
        }
        catch (Exception error)
        {
            Log.Error("trocar código do google", error);
            return (false, "Não consegui concluir o login com o Google.");
        }
    }

    /// <summary>Lê sub/email/name/picture do id_token (JWT — a assinatura já veio por TLS).</summary>
    private static (string Uid, string Email, string Nome, string Foto) LerIdentidade(string jwt)
    {
        try
        {
            var partes = jwt.Split('.');
            if (partes.Length < 2) return ("", "", "", "");
            var json = Base64UrlDecode(partes[1]);
            using var documento = JsonDocument.Parse(json);
            var raiz = documento.RootElement;
            string Ler(string nome) => raiz.TryGetProperty(nome, out var valor) ? valor.GetString() ?? "" : "";
            return (Ler("sub"), Ler("email"), Ler("name"), Ler("picture"));
        }
        catch
        {
            return ("", "", "", "");
        }
    }

    private static string Aleatorio(int bytes)
    {
        var dados = RandomNumberGenerator.GetBytes(bytes);
        return Base64Url(dados);
    }

    private static string Base64Url(byte[] dados) =>
        Convert.ToBase64String(dados).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string texto)
    {
        var normal = texto.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(normal.PadRight(normal.Length + (4 - normal.Length % 4) % 4, '='));
    }

    private static string Pagina(string titulo, string texto) =>
        "<!doctype html><html lang=\"pt-BR\"><meta charset=\"utf-8\">" +
        "<title>Orbi</title><body style=\"margin:0;height:100vh;display:flex;align-items:center;" +
        "justify-content:center;background:#13100c;color:#f2ede3;" +
        "font:15px/1.5 -apple-system,Segoe UI,Roboto,sans-serif\">" +
        "<div style=\"text-align:center;max-width:320px\">" +
        "<p style=\"font-size:20px;margin:0 0 8px\">" + WebUtility.HtmlEncode(titulo) + "</p>" +
        "<p style=\"color:#a89f8f;margin:0\">" + WebUtility.HtmlEncode(texto) + "</p></div></body></html>";
}

/// <summary>
/// A janela do Google. É uma janela do Orbi — sem barra de endereço, sem abas —
/// mostrando só a página de login do próprio Google (obrigatória).
/// </summary>
public sealed class GoogleWindow : Window
{
    private readonly string _endereco;

    public GoogleWindow(string endereco, string retorno, Window? dono)
    {
        _endereco = endereco;
        Title = "Orbi — entrar com o Google";
        Width = 480;
        Height = 660;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        if (dono is not null) Owner = dono;
        Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x13, 0x10, 0x0C));

        try
        {
            var web = new Microsoft.Web.WebView2.Wpf.WebView2();
            Content = web;
            Loaded += async (_, _) =>
            {
                try
                {
                    var pasta = Path.Combine(Store.Root, "webview");
                    Directory.CreateDirectory(pasta);
                    var ambiente = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, pasta);
                    await web.EnsureCoreWebView2Async(ambiente);

                    // Só o Google e o retorno local: nada de navegação solta.
                    web.CoreWebView2.NavigationStarting += (_, args) =>
                    {
                        var destino = args.Uri ?? "";
                        var permitido = destino.StartsWith("https://accounts.google.com", StringComparison.OrdinalIgnoreCase)
                            || destino.StartsWith("https://accounts.youtube.com", StringComparison.OrdinalIgnoreCase)
                            || destino.StartsWith(retorno.TrimEnd('*'), StringComparison.OrdinalIgnoreCase)
                            || destino.StartsWith("https://www.google.com/", StringComparison.OrdinalIgnoreCase);
                        if (!permitido) args.Cancel = true;
                    };
                    web.Source = new Uri(_endereco);
                }
                catch (Exception error)
                {
                    Log.Error("abrir webview do google", error);
                    DialogResult = false;
                    Close();
                }
            };
        }
        catch (Exception error)
        {
            // Sem WebView2 instalado: fecha e deixa o aviso para o chamador.
            Log.Error("webview indisponível", error);
            Loaded += (_, _) => Close();
        }
    }
}
