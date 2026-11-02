using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Orbi.Services;

/// <summary>
/// Acesso à web sem depender de nada além da internet: pesquisa em um endpoint
/// público que devolve texto (DuckDuckGo Lite), leitura de página e extração de
/// título/parágrafos. É o que deixa o agente responder sobre o mundo, e não só
/// sobre o PC.
/// </summary>
public static class Web
{
    private static readonly HttpClient Cliente = Criar();

    private static HttpClient Criar()
    {
        var cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        cliente.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Orbi/1.1");
        return cliente;
    }

    /// <summary>Pesquisa e devolve (título do 1º resultado, resumo curto, achou?).</summary>
    public static async Task<(string Titulo, string Trecho, bool Ok)> PesquisarAsync(string consulta)
    {
        try
        {
            var url = "https://lite.duckduckgo.com/lite/?q=" + Uri.EscapeDataString(consulta);
            var html = await Cliente.GetStringAsync(url);
            var resultados = Resultados(html, 3);

            if (resultados.Count == 0)
            {
                // segundo caminho: o DuckDuckGo HTML, que responde outro layout
                html = await Cliente.GetStringAsync("https://html.duckduckgo.com/html/?q=" + Uri.EscapeDataString(consulta));
                resultados = Resultados(html, 3);
            }

            if (resultados.Count == 0) return ("sem resultados", "Não achei nada sobre isso.", false);

            var texto = string.Join(" · ", resultados.Select(r => $"{r.Titulo}: {r.Resumo}"));
            return (resultados[0].Titulo, texto.Length > 700 ? texto[..700] + "…" : texto, true);
        }
        catch (Exception error)
        {
            Log.Error("pesquisa web", error);
            return ("sem conexão", "Não consegui pesquisar agora (" + error.Message + ").", false);
        }
    }

    private sealed record Achado(string Titulo, string Trecho, string Resumo);

    private static List<Achado> Resultados(string html, int limite)
    {
        var lista = new List<Achado>();

        // duckduckgo lite
        var titulos = Regex.Matches(html, @"<a[^>]+class=""result-link""[^>]*href=""(?<url>[^""]+)""[^>]*>(?<t>.*?)</a>",
            RegexOptions.Singleline);
        var resumos = Regex.Matches(html, @"<td[^>]*class=""result-snippet""[^>]*>(?<s>.*?)</td>", RegexOptions.Singleline);

        for (var i = 0; i < Math.Min(limite, titulos.Count); i++)
        {
            var titulo = Limpar(titulos[i].Groups["t"].Value);
            var resumo = i < resumos.Count ? Limpar(resumos[i].Groups["s"].Value) : "";
            if (titulo.Length > 0) lista.Add(new Achado(titulo, titulos[i].Groups["url"].Value, resumo));
        }

        if (lista.Count > 0) return lista;

        // duckduckgo html
        var blocos = Regex.Matches(html, @"<a[^>]+class=""[^""]*result__a[^""]*""[^>]*href=""(?<url>[^""]+)""[^>]*>(?<t>.*?)</a>.*?<a[^>]+class=""[^""]*result__snippet[^""]*""[^>]*>(?<s>.*?)</a>",
            RegexOptions.Singleline);
        foreach (Match bloco in blocos.Take(limite))
        {
            var titulo = Limpar(bloco.Groups["t"].Value);
            if (titulo.Length > 0) lista.Add(new Achado(titulo, bloco.Groups["url"].Value, Limpar(bloco.Groups["s"].Value)));
        }
        return lista;
    }

    /// <summary>Abre uma página e devolve o texto que interessa (título + começo).</summary>
    public static async Task<string> LerAsync(string endereco)
    {
        try
        {
            var html = await Cliente.GetStringAsync(endereco);
            var titulo = Regex.Match(html, @"<title[^>]*>(?<t>.*?)</title>", RegexOptions.Singleline).Groups["t"].Value;
            var corpo = Regex.Replace(html, @"<(script|style)[^>]*>.*?</\1>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            corpo = Regex.Replace(corpo, @"<[^>]+>", " ");
            corpo = WebUtility.HtmlDecode(corpo);
            corpo = Regex.Replace(corpo, @"\s{2,}", " ").Trim();
            var inicio = corpo.Length > 1200 ? corpo[..1200] + "…" : corpo;
            return (titulo.Length > 0 ? Limpar(titulo) + "\n\n" : "") + inicio;
        }
        catch (Exception error)
        {
            return "Não consegui abrir a página: " + error.Message;
        }
    }

    private static string Limpar(string html)
    {
        var texto = Regex.Replace(html ?? "", @"<[^>]+>", " ");
        texto = WebUtility.HtmlDecode(texto);
        return Regex.Replace(texto, @"\s{2,}", " ").Trim();
    }
}
