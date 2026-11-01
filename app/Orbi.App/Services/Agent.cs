using System.Text.Json;
using System.Text.RegularExpressions;

namespace Orbi.Services;

/// <summary>Um passo executado pelo Orbi, mostrado na barra.</summary>
public sealed record AgentStep(string Title, string Detail, bool Ok = true);

/// <summary>
/// O cérebro em sequência: a IA pede um comando por vez (em JSON), o Orbi
/// executa, devolve o que mudou na tela e só então pede o próximo. Assim um
/// pedido de vários passos — abrir o navegador, ver a tela, clicar no campo,
/// digitar, apertar Enter — acontece sem adivinhar posições no escuro.
/// </summary>
public static class Agent
{
    private const int MaxSteps = 14;

    /// <summary>
    /// Entende pedidos que precisam de mais de uma ferramenta, em uma ordem
    /// específica. Devolve a resposta final e o que foi feito em cada passo.
    /// </summary>
    public static async Task<(string Answer, List<AgentStep> Steps)> RunAsync(
        string goal,
        IEnumerable<(string Role, string Text)>? history,
        string? memory,
        CancellationToken token = default)
    {
        var passos = new List<AgentStep>();
        if (!Gemini.Available) return ("Cole a sua chave do Gemini para eu pensar e agir.", passos);

        var roteiro = new List<string>();
        string? respostaFinal = null;

        for (var i = 0; i < MaxSteps; i++)
        {
            token.ThrowIfCancellationRequested();

            var decisao = await DecideAsync(goal, roteiro, i == 0 ? history : null, memory, token);
            if (decisao is null)
            {
                if (roteiro.Count == 0) return ("Não consegui pensar agora. Tente de novo em instantes.", passos);
                break;
            }

            if (decisao.Resposta is { Length: > 0 } pronta && decisao.Acoes.Count == 0)
            {
                respostaFinal = pronta;
                break;
            }

            if (decisao.Acoes.Count == 0) break;

            foreach (var acao in decisao.Acoes)
            {
                token.ThrowIfCancellationRequested();
                var (titulo, resultado, ok) = Execute(acao);
                passos.Add(new AgentStep(titulo, resultado, ok));
                roteiro.Add($"{i + 1}. {titulo} → {resultado}");
                if (!ok && acao.Kind is "ferramenta") goto encerrar;
            }

            if (decisao.Resposta is { Length: > 0 } parcial) respostaFinal = parcial;
            if (decisao.Fim) break;
        }

    encerrar:

        if (respostaFinal is null && roteiro.Count > 0)
        {
            respostaFinal = await ResumirAsync(goal, roteiro, memory, token);
        }

        var fala = string.IsNullOrWhiteSpace(respostaFinal)
            ? roteiro.Count == 0 ? "Não precisei mexer no PC." : "Feito."
            : respostaFinal!.Trim();

        if (roteiro.Count > 0) Memory.Remember(goal, "ação", fala);
        return (fala, passos);
    }

    /* ------------------------------ decisão ------------------------------ */

    private sealed record Acao(string Kind, string? Id, string? Texto, int? X, int? Y, string? Tecla, string? Alvo, string? Dado);

    private sealed record Decisao(string? Resposta, List<Acao> Acoes, bool Fim);

    private static async Task<Decisao?> DecideAsync(string goal, List<string> roteiro,
        IEnumerable<(string Role, string Text)>? history, string? memory, CancellationToken token)
    {
        var catalogo = Tools.ForAi(goal);
        var feito = roteiro.Count == 0
            ? "nada até agora (este é o primeiro passo)"
            : string.Join("\n", roteiro.TakeLast(6));

        var conversa = "";
        if (history is not null)
        {
            var linhas = history.TakeLast(6).Select(item => $"{item.Role}: {item.Text}").ToList();
            if (linhas.Count > 0) conversa = "Conversa recente:\n" + string.Join("\n", linhas) + "\n\n";
        }

        var pedido =
            conversa +
            "Pedido da pessoa: " + goal + "\n\n" +
            "O que já foi feito, em ordem:\n" + feito + "\n\n" +
            "Ferramentas disponíveis (use exatamente estes ids em \"ferramenta\"):\n" + catalogo + "\n\n" +
            "Ações de controle do PC:\n" +
            "  {\"acao\":\"ver\"}                      lê a tela, as janelas e o cursor\n" +
            "  {\"acao\":\"ferramenta\",\"id\":\"...\",\"dado\":\"...\"} executa uma ferramenta (dado = o que ela pede)\n" +
            "  {\"acao\":\"focar\",\"alvo\":\"chrome\"}   traz a janela para a frente\n" +
            "  {\"acao\":\"clicar\",\"x\":640,\"y\":400}  clique na posição da tela\n" +
            "  {\"acao\":\"digitar\",\"texto\":\"...\"}   digita no campo em foco\n" +
            "  {\"acao\":\"atalho\",\"tecla\":\"ctrl+t\"} atalho de teclado\n" +
            "  {\"acao\":\"pesquisar\",\"dado\":\"...\"} pesquisa na web e devolve os resultados\n" +
            "  {\"acao\":\"abrir\",\"dado\":\"https://...\"} abre o resultado no navegador\n\n" +
            "Responda APENAS com JSON:\n" +
            "{\"acoes\":[{\"acao\":\"ver\"}],\"fim\":false,\"resposta\":\"só quando terminar\"}\n\n" +
            "Regras: uma ação por passo quando o próximo depende do resultado (depois de \"ver\", " +
            "use as coordenadas e os títulos que voltaram); várias ações juntas só quando são independentes; " +
            "use \"fim\":true e escreva \"resposta\" quando o pedido estiver resolvido ou for só uma pergunta; " +
            "se não souber onde clicar, peça {\"acao\":\"ver\"} primeiro; " +
            "quando o pedido precisar de informação de fora (preço, notícia, clima, documentação), use " +
            "{\"acao\":\"pesquisar\"} e leia os resultados antes de responder; " +
            "sempre que uma ferramenta pedir um dado, preencha \"dado\"; nada de texto fora do JSON.";

        var bruto = await Gemini.CompleteAsync(pedido, SystemPrompt(memory), json: true, maxTokens: 700, token);
        if (string.IsNullOrWhiteSpace(bruto)) return null;

        try
        {
            using var documento = JsonDocument.Parse(Extrair(bruto!));
            var raiz = documento.RootElement;

            string? resposta = null;
            if (raiz.TryGetProperty("resposta", out var r) && r.ValueKind == JsonValueKind.String)
            {
                resposta = r.GetString();
            }

            var fim = raiz.TryGetProperty("fim", out var f) && f.ValueKind is JsonValueKind.True;

            var acoes = new List<Acao>();
            if (raiz.TryGetProperty("acoes", out var lista) && lista.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in lista.EnumerateArray())
                {
                    var acao = Ler(item);
                    if (acao is not null) acoes.Add(acao);
                }
            }

            // uma resposta pronta sem ações encerra o pedido
            if (!string.IsNullOrWhiteSpace(resposta) && acoes.Count == 0) fim = true;
            return new Decisao(resposta, acoes, fim);
        }
        catch (Exception error)
        {
            Log.Error("passo do agente", error);
            return null;
        }
    }

    private static async Task<string> ResumirAsync(string goal, List<string> roteiro, string? memory,
        CancellationToken token)
    {
        var resumo = string.Join("\n", roteiro);
        var fala = await Gemini.CompleteAsync(
            $"O que eu pedi: {goal}\nO que o app executou, em ordem:\n{resumo}\n\n" +
            "Responda em no máximo duas frases, em português do Brasil, contando o que foi feito. " +
            "Se algo falhou, diga só o essencial. Não use listas.",
            SystemPrompt(memory), json: false, maxTokens: 160, token);
        return string.IsNullOrWhiteSpace(fala) ? "Feito." : fala!.Trim();
    }

    private static Acao? Ler(JsonElement item)
    {
        string? Str(string nome) =>
            item.TryGetProperty(nome, out var valor) && valor.ValueKind == JsonValueKind.String
                ? valor.GetString() : null;

        int? Num(string nome) =>
            item.TryGetProperty(nome, out var valor) && valor.ValueKind == JsonValueKind.Number
                ? valor.GetInt32() : null;

        var acao = Str("acao")?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(acao)) return null;

        // às vezes o modelo manda {"acao":"ferramenta","id":"..."} e às vezes {"ferramenta":"volume-up"}
        var id = Str("id") ?? (acao == "ferramenta" ? null : Str("ferramenta"));
        if (id is null && item.TryGetProperty("ferramenta", out var f) && f.ValueKind == JsonValueKind.String)
        {
            id = f.GetString();
            acao = "ferramenta";
        }
        if (acao != "ferramenta" && acao != "ver" && acao != "clicar" && acao != "digitar" &&
            acao != "atalho" && acao != "focar" && acao != "pesquisar" && acao != "abrir")
        {
            // nomes de ferramentas vieram direto no campo "acao"
            id ??= acao;
            acao = "ferramenta";
        }

        return new Acao(acao!, id, Str("texto"), Num("x"), Num("y"), Str("tecla") ?? Str("atalho"),
            Str("alvo") ?? Str("janela"), Str("dado") ?? Str("arg") ?? Str("valor"));
    }

    private static string Extrair(string texto)
    {
        var inicio = texto.IndexOf('{');
        var fim = texto.LastIndexOf('}');
        if (inicio >= 0 && fim > inicio) return texto[inicio..(fim + 1)];
        return texto;
    }

    /* ------------------------------ execução ------------------------------ */

    private static (string Titulo, string Resultado, bool Ok) Execute(Acao acao)
    {
        switch (acao.Kind)
        {
            case "ver":
                return ("olhei a tela", Pc.DescribeScreen().Replace("\n", " · "), true);

            case "pesquisar":
            {
                var consulta = acao.Dado ?? acao.Texto ?? "";
                if (consulta.Length == 0) return ("pesquisar", "sem consulta", false);
                var (titulo, trecho, ok) = Web.PesquisarAsync(consulta).GetAwaiter().GetResult();
                return ($"pesquisei “{consulta}”", ok ? $"{titulo} — {trecho}" : trecho, ok);
            }

            case "abrir":
            {
                var link = acao.Dado ?? acao.Texto ?? "";
                if (link.Length == 0) return ("abrir", "sem endereço", false);
                return ("abri no navegador", Browser.Open(link), true);
            }

            case "clicar":
            {
                if (acao.X is not { } x || acao.Y is not { } y) return ("clicar", "sem posição", false);
                Pc.Click(x, y);
                return ($"cliquei em {x},{y}", "feito", true);
            }

            case "digitar":
            {
                if (string.IsNullOrEmpty(acao.Texto)) return ("digitar", "sem texto", false);
                Pc.Type(acao.Texto);
                var trecho = acao.Texto.Length > 40 ? acao.Texto[..40] + "…" : acao.Texto;
                return ($"digitei “{trecho}”", "feito", true);
            }

            case "atalho":
            {
                if (string.IsNullOrEmpty(acao.Tecla)) return ("atalho", "sem tecla", false);
                Pc.Hotkey(acao.Tecla);
                return ($"atalho {acao.Tecla}", "feito", true);
            }

            case "focar":
            {
                if (string.IsNullOrEmpty(acao.Alvo)) return ("focar", "sem janela", false);
                var ok = Pc.Focus(acao.Alvo);
                return ($"trouxe {acao.Alvo}", ok ? "feito" : "não achei essa janela", ok);
            }

            default:
            {
                var id = acao.Id ?? "";
                var ferramenta = Tools.Find(id) ?? Tools.Search(id).FirstOrDefault();
                if (ferramenta is null) return ("ferramenta " + id, "não existe no catálogo", false);
                if (ferramenta.NeedsArg && string.IsNullOrWhiteSpace(acao.Dado))
                {
                    return (ferramenta.Name, "faltou o dado: " + (ferramenta.Placeholder ?? "o complemento"), false);
                }
                var resultado = Tools.Run(ferramenta.Id, acao.Dado);
                return (ferramenta.Name, resultado, !resultado.StartsWith("Não consegui", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    /* ------------------------------ sistema ------------------------------ */

    private static string SystemPrompt(string? memory)
    {
        var nome = string.IsNullOrWhiteSpace(Store.Current.UserName) ? "amigo" : Store.Current.UserName.Split(' ')[0];
        var janela = Pc.ActiveWindowTitle();
        return "Você é o Orbi, o assistente que controla o Windows para " + nome + ". " +
               "Você fala português do Brasil, direto e sem enrolação. " +
               $"Agora: {DateTime.Now:dddd, dd/MM/yyyy HH:mm}. " +
               (janela.Length > 0 ? $"A janela em foco é “{janela}”. " : "") +
               "Você pode usar as ferramentas do app, clicar na tela, digitar, apertar atalhos e trocar de janela. " +
               "Memória do dia: " + (string.IsNullOrWhiteSpace(memory) ? "nada registrado ainda." : memory);
    }
}
