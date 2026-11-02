using System.Net.Http;
using System.Text.Json;

namespace Orbi.Services;

/// <summary>
/// Versão nova do Orbi.
///
/// Nada de servidor próprio: a release publicada é a única fonte. Quando existe
/// uma versão acima da que está instalada, o app avisa e leva a pessoa para o
/// instalador certo — sem atualizar nada pelas costas de ninguém.
/// </summary>
public static class Update
{
    private const string Repo = "Arthurowgg/TheOrbiApp";

    /// <summary>A versão instalada, do próprio assembly (vem de Directory.Build.props).</summary>
    public static string Instalada =>
        typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    private static readonly HttpClient Cliente = Criar();

    private static HttpClient Criar()
    {
        var cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        cliente.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Orbi/" + Instalada);
        return cliente;
    }

    public sealed record Disponivel(string Versao, string Notas, string InstaladorUrl, string Tag);

    /// <summary>
    /// Procura a release mais recente. Devolve null quando já está na última,
    /// quando está sem internet ou quando a resposta não faz sentido — nunca
    /// inventa uma versão nova.
    /// </summary>
    public static async Task<Disponivel?> ProcurarAsync(CancellationToken token = default)
    {
        try
        {
            var url = $"https://api.github.com/repos/{Repo}/releases/latest";
            using var pedido = new HttpRequestMessage(HttpMethod.Get, url);
            pedido.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
            using var resposta = await Cliente.SendAsync(pedido, token);
            if (!resposta.IsSuccessStatusCode) return null;

            using var documento = await JsonDocument.ParseAsync(
                await resposta.Content.ReadAsStreamAsync(token), cancellationToken: token);
            var raiz = documento.RootElement;

            var tag = raiz.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            var notas = raiz.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            var versao = tag.TrimStart('v', 'V');
            if (!EhVersao(versao)) return null;
            if (!MaisNova(versao, Instalada)) return null;

            var instalador = $"https://github.com/{Repo}/releases/download/{tag}/OrbiSetup-{versao}.exe";
            if (raiz.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var nome = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (!nome.StartsWith("OrbiSetup", StringComparison.OrdinalIgnoreCase)) continue;
                    if (asset.TryGetProperty("browser_download_url", out var d) &&
                        d.GetString() is { Length: > 0 } endereco)
                    {
                        instalador = endereco;
                    }
                    break;
                }
            }

            Log.Add($"atualização: {Instalada} → {versao}");
            return new Disponivel(versao, notas, instalador, tag);
        }
        catch (Exception error)
        {
            Log.Error("procurar atualização", error);
            return null;
        }
    }

    private static bool EhVersao(string texto) =>
        Version.TryParse(texto.Split('-')[0], out _);

    /// <summary>Só conta como nova se for maior de verdade (1.2.10 &gt; 1.2.9).</summary>
    public static bool MaisNova(string candidata, string atual)
    {
        if (!Version.TryParse(candidata.Split('-')[0], out var nova)) return false;
        if (!Version.TryParse(atual.Split('-')[0], out var hoje)) return false;
        return nova > hoje;
    }

    /// <summary>Abre o instalador da versão nova (o download é o do navegador).</summary>
    public static void Instalar(Disponivel nova)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(nova.InstaladorUrl) { UseShellExecute = true });
        }
        catch (Exception error)
        {
            Log.Error("abrir instalador", error);
        }
    }
}
