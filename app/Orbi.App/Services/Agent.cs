using System.Text.Json;
using System.Text.RegularExpressions;

namespace Orbi.Services;

/// <summary>Um passo executado pelo Orbi, mostrado na barra.</summary>
public sealed record AgentStep(string Title, string Detail, bool Ok = true);

/// <summary>
/// O cérebro em sequência: em vez de responder com um comando, o Orbi pede um
/// plano à IA, executa os passos na ordem (ferramentas, cliques, digitação,
/// atalhos, olhar a tela) e só então conta o que aconteceu.
/// </summary>
public static class Agent
{
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

        var plano = await PlanAsync(goal, history, memory, token);
        if (plano is null) return ("Não consegui pensar agora. Tente de novo em instantes.", passos);

        if (plano.Resposta is { Length: > 0 } direta && plano.Acoes.Count == 0)
        {
            return (direta, passos);
        }

        var resultados = new List<string>();
        foreach (var acao in plano.Acoes.Take(12))
        {
            token.ThrowIfCancellationRequested();
            var (titulo, resultado, ok) = Execute(acao);
            passos.Add(new AgentStep(titulo, resultado, ok));
            resultados.Add($"{titulo} → {resultado}");
            if (!ok && acao.Acao is "ferramenta") break;
        }

        // Uma segunda passada curta: transformar o que aconteceu em resposta falada.
        var resumo = string.Join("\n", resultados);
        var fala = await Gemini.CompleteAsync(
            $"O que eu pedi: {goal}\nO que o app executou, em ordem:\n{resumo}\n\n" +
            "Responda em no máximo duas frases, em português do Brasil, contando o que foi feito. " +
            "Se algo falhou, diga só o essencial. Não use listas.",
            SystemPrompt(memory), json: false, maxTokens: 160, token);
        var resposta = string.IsNullOrWhiteSpace(fala) ? plano.Resposta ?? "Feito." : fala!.Trim();
        Memory.Remember(goal, "ação", resposta);
        return (resposta, passos);
    }

    /* ------------------------------ plano ------------------------------ */

    private sealed record Acao(string Acao, string? Id, string? Texto, int? X, int? Y, string? Tecla, string? Alvo);

    private sealed record Plano(string? Resposta, List<Acao> Acoes);

    private static async Task<Plano?> PlanAsync(string goal, IEnumerable<(string Role, string Text)>? history,
        string? memory, CancellationToken token)
    {
        var catalogo = Tools.ForAi(Tools.All.Count);
        var pedido =
            "Pedido da pessoa: " + goal + "\n\n" +
            "Ferramentas disponíveis (use exatamente estes ids):\n" + catalogo + "\n\n" +
            "Ações de controle do PC que você pode pedir:\n" +
            "  {\"acao\":\"clicar\",\"x\":640,\"y\":400}\n" +
            "  {\"acao\":\"digitar\",\"texto\":\"...\"}\n" +
            "  {\"acao\":\"atalho\",\"tecla\":\"ctrl+t\"}\n" +
            "  {\"acao\":\"ver\"}            (lê a tela e as janelas abertas)\n" +
            "  {\"acao\":\"focar\",\"alvo\":\"chrome\"}\n\n" +
            "Responda APENAS com JSON válido neste formato:\n" +
            "{\"resposta\":\"o que responder se não precisar agir\",\"acoes\":[{\"acao\":\"ferramenta\",\"id\":\"volume-up\"}]}\n\n" +
            "Regras: use quantos passos o pedido precisar, na ordem certa; " +
            "para ver a tela use {\"acao\":\"ver\"} antes de clicar em algo que você não sabe onde está; " +
            "se o pedido for só uma pergunta, deixe acoes vazio e responda em resposta; " +
            "nada de texto fora do JSON.";

        var bruto = await Gemini.CompleteAsync(pedido, SystemPrompt(memory), json: true, maxTokens: 700, token);
        if (string.IsNullOrWhiteSpace(bruto)) return null;

        try
        {
            var json = Extrair(bruto!);
            using var documento = JsonDocument.Parse(json);
            var raiz = documento.RootElement;

            string? resposta = null;
            if (raiz.TryGetProperty("resposta", out var r) && r.ValueKind == JsonValueKind.String)
            {
                resposta = r.GetString();
            }

            var acoes = new List<Acao>();
            if (raiz.TryGetProperty("acoes", out var lista) && lista.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in lista.EnumerateArray())
                {
                    var acao = Ler(item);
                    if (acao is not null) acoes.Add(acao);
                }
            }
            return new Plano(resposta, acoes);
        }
        catch (Exception error)
        {
            Log.Error("plano", error);
            return null;
        }
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
            acao != "atalho" && acao != "focar")
        {
            // nomes de ferramentas vieram direto no campo "acao"
            id ??= acao;
            acao = "ferramenta";
        }

        return new Acao(acao!, id, Str("texto"), Num("x"), Num("y"), Str("tecla") ?? Str("atalho"), Str("alvo") ?? Str("janela"));
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
        switch (acao.Acao)
        {
            case "ver":
                return ("olhei a tela", Control.DescribeScreen().Replace("\n", " · "), true);

            case "clicar":
            {
                if (acao.X is not { } x || acao.Y is not { } y) return ("clicar", "sem posição", false);
                Control.Click(x, y);
                return ($"cliquei em {x},{y}", "feito", true);
            }

            case "digitar":
            {
                if (string.IsNullOrEmpty(acao.Texto)) return ("digitar", "sem texto", false);
                Control.Type(acao.Texto);
                var trecho = acao.Texto.Length > 40 ? acao.Texto[..40] + "…" : acao.Texto;
                return ($"digitei “{trecho}”", "feito", true);
            }

            case "atalho":
            {
                if (string.IsNullOrEmpty(acao.Tecla)) return ("atalho", "sem tecla", false);
                Control.Hotkey(acao.Tecla);
                return ($"atalho {acao.Tecla}", "feito", true);
            }

            case "focar":
            {
                if (string.IsNullOrEmpty(acao.Alvo)) return ("focar", "sem janela", false);
                var ok = Control.Focus(acao.Alvo);
                return ($"trouxe {acao.Alvo}", ok ? "feito" : "não achei essa janela", ok);
            }

            default:
            {
                var id = acao.Id ?? "";
                var ferramenta = Tools.Find(id) ?? Tools.Search(id).FirstOrDefault();
                if (ferramenta is null) return ("ferramenta " + id, "não existe no catálogo", false);
                var resultado = Tools.Run(ferramenta.Id);
                return (ferramenta.Name, resultado, !resultado.StartsWith("Não consegui", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    /* ------------------------------ sistema ------------------------------ */

    private static string SystemPrompt(string? memory)
    {
        var nome = string.IsNullOrWhiteSpace(Store.Current.UserName) ? "amigo" : Store.Current.UserName.Split(' ')[0];
        var janela = Control.ActiveWindowTitle();
        return "Você é o Orbi, o assistente que controla o Windows para " + nome + ". " +
               "Você fala português do Brasil, direto e sem enrolação. " +
               $"Agora: {DateTime.Now:dddd, dd/MM/yyyy HH:mm}. " +
               (janela.Length > 0 ? $"A janela em foco é “{janela}”. " : "") +
               "Você pode usar as ferramentas do app, clicar na tela, digitar, apertar atalhos e trocar de janela. " +
               "Memória do dia: " + (string.IsNullOrWhiteSpace(memory) ? "nada registrado ainda." : memory);
    }
}
