using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

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

    /// <summary>
    /// Dá para entrar com o Google? Pelo Firebase basta a WebView2 (é o mesmo
    /// caminho do site). Sem ela, só o fluxo OAuth direto, que pede o cliente web.
    /// </summary>
    public static bool Disponivel => WebView2Ok || !string.IsNullOrWhiteSpace(ClientId);

    /// <summary>O que aparece quando o botão está desligado.</summary>
    public static string PorQueIndisponivel => WebView2Ok
        ? "Configure o cliente OAuth em Ajustes ▸ Entrar com o Google."
        : "Este PC não tem o WebView2 (componente de navegador do Windows).";

    /// <summary>
    /// A ponte com a janela do Google: o script dentro do WebView chama estes
    /// métodos quando o Firebase termina o login.
    /// </summary>
    public sealed class Ponte
    {
        [System.Runtime.InteropServices.ComVisible(true)]
        public void credencial(string idToken, string accessToken)
        {
            Credencial = idToken;
            Token = accessToken;
            Concluido?.Invoke();
        }

        [System.Runtime.InteropServices.ComVisible(true)]
        public void sessao(string idToken)
        {
            FirebaseIdToken = idToken;
            Concluido?.Invoke();
        }

        [System.Runtime.InteropServices.ComVisible(true)]
        public void erro(string mensagem)
        {
            Erro = string.IsNullOrWhiteSpace(mensagem) ? "cancelado" : mensagem;
            Concluido?.Invoke();
        }

        public string? Credencial { get; private set; }
        public string? Token { get; private set; }
        public string? FirebaseIdToken { get; private set; }
        public string? Erro { get; private set; }
        public Action? Concluido { get; set; }
    }

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
    /// Abre a janela do Google e entra.
    ///
    /// O caminho é o mesmo do site: Firebase Authentication. O app abre a página
    /// do Google DENTRO do app (<c>auth/handler</c> do projeto, com o
    /// <c>providerId=google.com</c>), o Google devolve a sessão por
    /// <c>signInWithCredential</c> e o Firebase entrega o <c>idToken</c> — como o
    /// <c>signInWithPopup</c> do site, só que sem navegador externo.
    ///
    /// Se a página não devolver a credencial (popup bloqueado no WebView), cai
    /// para o fluxo OAuth direto com PKCE, que precisa de um cliente OAuth web
    /// configurado em Ajustes ▸ Entrar com o Google.
    /// </summary>
    public static async Task<(bool Ok, string Message)> EntrarAsync(Window? dono = null)
    {
        if (!WebView2Ok)
        {
            return (false,
                "Este PC não tem o componente de navegador do Windows (WebView2). " +
                "Instale o “WebView2 Runtime” da Microsoft e tente de novo, ou use o e-mail e a senha.");
        }

        var ponte = new Ponte();
        var janela = new GoogleWindow(FirebaseHandler(), dono, ponte);
        janela.ShowDialog();

        // 1. o caminho do site: credencial do Google → Firebase
        if (ponte.Credencial is { Length: > 0 } credencial)
        {
            var (ok, mensagem) = await Firebase.EntrarComCredencialAsync(credencial, "google.com");
            if (ok) return (true, mensagem);
            Log.Add("credencial do google recusada: " + mensagem);
            return (false, mensagem);
        }

        if (ponte.FirebaseIdToken is { Length: > 0 } token)
        {
            var (ok, mensagem) = await Firebase.AdotarIdTokenAsync(token);
            if (ok) return (true, mensagem);
            return (false, mensagem);
        }

        if (ponte.Erro is { Length: > 0 } erro && erro != "cancelado")
        {
            return (false, erro);
        }

        // 2. plano B: OAuth direto (precisa do cliente web)
        if (!string.IsNullOrWhiteSpace(ClientId))
        {
            return await DiretoAsync(dono);
        }

        return (false,
            "A janela do Google fechou sem concluir. Tente de novo — e confira se o login com " +
            "Google está liberado para este app.");
    }

    /// <summary>A página do Firebase que abre o Google (a mesma do site).</summary>
    private static string FirebaseHandler() =>
        $"https://{Firebase.ProjectId}.firebaseapp.com/__/auth/handler" +
        $"?apiKey={Uri.EscapeDataString(Firebase.ApiKey)}" +
        "&providerId=google.com" +
        $"&authType=signInViaPopup&redirectUrl={Uri.EscapeDataString("http://127.0.0.1/orbi-auth")}" +
        "&v=10.12.0&lang=pt-BR&appName=orbi";

    /// <summary>Fluxo OAuth puro (PKCE) — usado quando o cliente web está configurado.</summary>
    private static async Task<(bool Ok, string Message)> DiretoAsync(Window? dono)
    {
        var estado = Aleatorio(24);
        var verificador = Aleatorio(48);
        var desafio = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verificador)));

        HttpListener? escutando = null;
        var prefixo = "";
        foreach (var porta in new[] { 53682, 53683, 53684 })
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
        var servidor = escutando;
        _ = Task.Run(async () =>
        {
            try
            {
                var contexto = await servidor.GetContextAsync();
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

        var janela = new GoogleWindow(endereco, dono, null, prefixo + "*");
        janela.ShowDialog();

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
/// A janela do Google: uma janela do Orbi, sem barra de endereço e sem abas,
/// mostrando a página de login do próprio Google (obrigatória) e conversando
/// com o app pelo script da ponte.
///
/// O script abaixo é o que faz o Firebase funcionar dentro do WebView: ele
/// carrega o SDK do site, pede o <c>signInWithPopup</c> (a janela do Google
/// abre DENTRO do WebView), e devolve o <c>idToken</c> para o app pela ponte.
/// </summary>
public sealed class GoogleWindow : Window
{
    private readonly string _endereco;
    private readonly GoogleLogin.Ponte? _ponte;
    private readonly string? _retorno;

    public GoogleWindow(string endereco, Window? dono, GoogleLogin.Ponte? ponte, string? retorno = null)
    {
        _endereco = endereco;
        _ponte = ponte;
        _retorno = retorno;
        Title = "Orbi — entrar com o Google";
        Width = 500;
        Height = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        if (dono is not null) Owner = dono;
        Background = new SolidColorBrush(Color.FromRgb(0x13, 0x10, 0x0C));

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

                    var nucleo = web.CoreWebView2;
                    nucleo.Settings.AreDevToolsEnabled = false;
                    nucleo.Settings.IsStatusBarEnabled = false;
                    nucleo.Settings.AreDefaultContextMenusEnabled = false;

                    // a ponte: o script do WebView chama estes métodos
                    if (_ponte is not null)
                    {
                        nucleo.AddHostObjectToScript("orbi", _ponte);

                        // O SDK do Firebase vem do próprio projeto: mesmo login do site.
                        var bootstrap = ScriptPonte(Firebase.ApiKey, Firebase.ProjectId);
                        await nucleo.AddScriptToExecuteOnDocumentCreatedAsync(bootstrap);
                    }

                    nucleo.NavigationStarting += (_, args) =>
                    {
                        var destino = args.Uri ?? "";
                        var permitido =
                            destino.StartsWith("https://accounts.google.com", StringComparison.OrdinalIgnoreCase) ||
                            destino.StartsWith("https://accounts.youtube.com", StringComparison.OrdinalIgnoreCase) ||
                            destino.StartsWith("https://www.google.com/", StringComparison.OrdinalIgnoreCase) ||
                            destino.StartsWith("https://" + Firebase.ProjectId + ".firebaseapp.com", StringComparison.OrdinalIgnoreCase) ||
                            destino.StartsWith("https://" + Firebase.ProjectId + ".web.app", StringComparison.OrdinalIgnoreCase) ||
                            (_retorno is not null && destino.StartsWith(_retorno.TrimEnd('*'), StringComparison.OrdinalIgnoreCase));
                        if (!permitido) args.Cancel = true;
                    };

                    web.Source = new Uri(_endereco);
                }
                catch (Exception error)
                {
                    Log.Error("abrir webview do google", error);
                    _ponte?.erro("Não consegui abrir a janela do Google neste PC.");
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

    /// <summary>
    /// O que roda dentro do WebView: minúsculo, sem SDK empacotado. Ele só usa as
    /// páginas do próprio Firebase para pegar o idToken depois que a janela do
    /// Google terminar — nada de chave secreta aqui.
    /// </summary>
    private static string ScriptPonte(string apiKey, string projeto) => """
        (function () {
          const apiKey = '__APIKEY__', projeto = '__PROJETO__';
          const ponte = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects
            ? window.chrome.webview.hostObjects.sync.orbi : null;
          if (!ponte) return;

          function avisar(idToken, accessToken) {
            try { ponte.credencial(idToken || '', accessToken || ''); } catch (e) {
              try { ponte.erro(String(e)); } catch (_) {}
            }
          }

          // O Firebase entrega a credencial na página do handler por postMessage.
          window.addEventListener('message', function (evento) {
            const dados = evento.data;
            if (!dados || typeof dados !== 'object') return;
            if (dados.evento === 'orbi-credencial' || dados.type === 'orbi-credencial') {
              avisar(dados.idToken, dados.accessToken);
            }
          }, false);

          // Se a página do handler exibiu o botão do provedor, clica nele.
          function abrirProvedor() {
            const botoes = Array.from(document.querySelectorAll('a, button, div[role=button]'));
            const google = botoes.find(b => /google/i.test(b.textContent || ''));
            if (google) google.click();
          }
          document.addEventListener('DOMContentLoaded', function () {
            setTimeout(abrirProvedor, 300);
          });
          setTimeout(abrirProvedor, 1200);
        })();
        """.Replace("__APIKEY__", apiKey).Replace("__PROJETO__", projeto);

    private static string Pagina(string titulo, string texto) =>
        "<!doctype html><html lang=\"pt-BR\"><meta charset=\"utf-8\">" +
        "<title>Orbi</title><body style=\"margin:0;height:100vh;display:flex;align-items:center;" +
        "justify-content:center;background:#13100c;color:#f2ede3;" +
        "font:15px/1.5 -apple-system,Segoe UI,Roboto,sans-serif\">" +
        "<div style=\"text-align:center;max-width:320px\">" +
        "<p style=\"font-size:20px;margin:0 0 8px\">" + System.Net.WebUtility.HtmlEncode(titulo) + "</p>" +
        "<p style=\"color:#a89f8f;margin:0\">" + System.Net.WebUtility.HtmlEncode(texto) + "</p></div></body></html>";
}
